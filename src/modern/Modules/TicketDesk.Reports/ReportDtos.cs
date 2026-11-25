using System.ComponentModel;
using TicketDesk.Data.Entities;
using TicketDesk.Users;

namespace TicketDesk.Reports;

/// <summary>Age buckets of the open-ticket report, by whole hours since <c>CreatedDate</c> (<c>DATEDIFF(hour, ...)</c>).</summary>
public static class AgeBuckets
{
    public static readonly IReadOnlyList<string> Labels = ["<1d", "1-7d", "7-30d", "30-90d", ">90d"];

    /// <summary>Exclusive upper bounds in hours of buckets 0-3; bucket 4 is everything older.</summary>
    public static readonly IReadOnlyList<int> UpperHours = [24, 168, 720, 2160];

    public static int Of(long hours) => hours < 24 ? 0 : hours < 168 ? 1 : hours < 720 ? 2 : hours < 2160 ? 3 : 4;
}

/// <summary>Assignment-changing activities (legacy <c>TicketActionManager</c>): TakeOver, Assign, ReAssign, Pass. ReOpen
/// assigns only when the person re-opening takes the ticket (<c>assignToMe</c>), i.e. when the event's author is the assignee.</summary>
public static class AssignmentActivities
{
    public const int Mask = (int)(TicketActivity.TakeOver | TicketActivity.Assign | TicketActivity.ReAssign | TicketActivity.Pass);
    public const int ReOpen = (int)TicketActivity.ReOpen;
}

public sealed class OpenTicketsQuery
{
    [Description("Reference time for ticket age (default: now).")]
    public DateTimeOffset? AsOf { get; set; }

    [Description("Project id filter.")]
    public int? ProjectId { get; set; }
}

public sealed class AssigneeWorkloadQuery
{
    [Description("Reference time for 'hours since assigned' and the 30-day closed window (default: now).")]
    public DateTimeOffset? AsOf { get; set; }

    [Description("Project id filter.")]
    public int? ProjectId { get; set; }

    [Description("Number of assignees returned, by open tickets descending (1-500, default 50).")]
    public int? Top { get; set; }
}

public sealed class TimeToCloseQuery
{
    [Description("Start of the closing-date window, inclusive (default: 'to' minus 90 days).")]
    public DateTimeOffset? From { get; set; }

    [Description("End of the closing-date window, exclusive (default: now).")]
    public DateTimeOffset? To { get; set; }

    [Description("Project id filter.")]
    public int? ProjectId { get; set; }
}

public sealed record OpenTicketsRow(int ProjectId, string ProjectName, TicketStatus Status, int AgeBucket, string AgeBucketLabel, int Tickets);

public sealed record OpenTicketsReport(
    DateTimeOffset AsOf,
    int? ProjectId,
    IReadOnlyList<string> AgeBuckets,
    int TotalOpen,
    [property: Description("One row per project, status and age bucket that has tickets; ordered by project name, status, bucket.")]
    IReadOnlyList<OpenTicketsRow> Rows);

public sealed record AssigneeWorkloadRow(
    UserRef Assignee,
    int Active,
    int MoreInfo,
    int Resolved,
    int Open,
    DateTimeOffset OldestOpenCreatedDate,
    [property: Description("Mean whole hours since the ticket was assigned to its current assignee, over the open tickets.")]
    double AvgHoursSinceAssigned,
    int MaxHoursSinceAssigned,
    [property: Description("Tickets assigned to this person that were closed in the 30 days before asOf.")]
    int ClosedLast30Days);

public sealed record AssigneeWorkloadReport(
    DateTimeOffset AsOf,
    int? ProjectId,
    int UnassignedOpen,
    [property: Description("Number of people with at least one open assigned ticket (rows are the top N of them).")]
    int Assignees,
    IReadOnlyList<AssigneeWorkloadRow> Rows);

public sealed record TimeToCloseRow(int? ProjectId, string? ProjectName, int Closed, double AvgHours, double P50Hours, double P90Hours, double MaxHours);

public sealed record TimeToCloseReport(
    DateTimeOffset From,
    DateTimeOffset To,
    int? ProjectId,
    [property: Description("All projects together; null when no ticket was closed in the window.")]
    TimeToCloseRow? Overall,
    [property: Description("Per project, ordered by project name. Hours = DATEDIFF(second, created, closed) / 3600; p50/p90 = PERCENTILE_CONT.")]
    IReadOnlyList<TimeToCloseRow> Projects);

/// <summary>Validated report parameters (defaults applied).</summary>
public sealed record OpenTicketsRequest(DateTimeOffset AsOf, int? ProjectId);

public sealed record AssigneeWorkloadRequest(DateTimeOffset AsOf, int? ProjectId, int Top)
{
    public const int DefaultTop = 50;
    public const int MaxTop = 500;
}

public sealed record TimeToCloseRequest(DateTimeOffset From, DateTimeOffset To, int? ProjectId)
{
    public const int DefaultDays = 90;
    public const int MaxDays = 3660;
}

public static class ReportRequests
{
    public static OpenTicketsRequest Create(OpenTicketsQuery q, DateTimeOffset now) => new(q.AsOf ?? now, q.ProjectId);

    public static AssigneeWorkloadRequest? TryCreate(AssigneeWorkloadQuery q, DateTimeOffset now, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var top = q.Top ?? AssigneeWorkloadRequest.DefaultTop;
        if (top is < 1 or > AssigneeWorkloadRequest.MaxTop) errors["top"] = [$"Must be between 1 and {AssigneeWorkloadRequest.MaxTop}."];
        return errors.Count > 0 ? null : new AssigneeWorkloadRequest(q.AsOf ?? now, q.ProjectId, top);
    }

    public static TimeToCloseRequest? TryCreate(TimeToCloseQuery q, DateTimeOffset now, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var to = q.To ?? now;
        var from = q.From ?? to.AddDays(-TimeToCloseRequest.DefaultDays);
        if (from >= to) errors["from"] = ["Must be earlier than 'to'."];
        else if ((to - from).TotalDays > TimeToCloseRequest.MaxDays) errors["from"] = [$"The window may span at most {TimeToCloseRequest.MaxDays} days."];
        return errors.Count > 0 ? null : new TimeToCloseRequest(from, to, q.ProjectId);
    }
}

internal static class Percentiles
{
    /// <summary>T-SQL <c>PERCENTILE_CONT</c>: linear interpolation between the closest ranks of the sorted values.</summary>
    public static double Cont(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return double.NaN;
        var rank = p * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }
}
