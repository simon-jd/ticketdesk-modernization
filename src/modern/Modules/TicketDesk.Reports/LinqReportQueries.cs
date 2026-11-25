using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Users;

namespace TicketDesk.Reports;

/// <summary>
/// First-pass EF Core LINQ port of the three reports: the straightforward version a team writes first. It is not what
/// the API runs (that is <see cref="ReportQueries"/>, over stored procedures); it stays as the executable specification
/// that the procedure results are compared with in the tests, and as the "naive port" side of docs/sql/tuning.md.
/// The workload report derives "assigned since" from TicketEvents, as it had to before Tickets.AssignedDate existed.
/// </summary>
internal sealed class LinqReportQueries(TicketDeskDbContext db, IUserDirectory users)
{
    public async Task<OpenTicketsReport> OpenTicketsAsync(OpenTicketsRequest r, CancellationToken ct)
    {
        var q = db.Tickets.AsNoTracking().Where(t => t.TicketStatus != TicketStatus.Closed);
        if (r.ProjectId is { } projectId) q = q.Where(t => t.ProjectId == projectId);
        var asOf = r.AsOf;
        var groups = await q
            .Select(t => new { t.ProjectId, t.TicketStatus, Hours = EF.Functions.DateDiffHour(t.CreatedDate, asOf) })
            .GroupBy(x => new
            {
                x.ProjectId,
                x.TicketStatus,
                Bucket = x.Hours < 24 ? 0 : x.Hours < 168 ? 1 : x.Hours < 720 ? 2 : x.Hours < 2160 ? 3 : 4,
            })
            .Select(g => new { g.Key.ProjectId, g.Key.TicketStatus, g.Key.Bucket, Count = g.Count() })
            .ToListAsync(ct);
        var names = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.ProjectId, p => p.ProjectName, ct);
        var rows = groups
            .Select(g => new OpenTicketsRow(g.ProjectId, names.GetValueOrDefault(g.ProjectId, ""), g.TicketStatus, g.Bucket, AgeBuckets.Labels[g.Bucket], g.Count))
            .OrderBy(x => x.ProjectName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ProjectId).ThenBy(x => x.Status).ThenBy(x => x.AgeBucket)
            .ToList();
        return new OpenTicketsReport(r.AsOf, r.ProjectId, AgeBuckets.Labels, rows.Sum(x => x.Tickets), rows);
    }

    public async Task<AssigneeWorkloadReport> AssigneeWorkloadAsync(AssigneeWorkloadRequest r, CancellationToken ct)
    {
        var open = db.Tickets.AsNoTracking().Where(t => t.TicketStatus != TicketStatus.Closed);
        if (r.ProjectId is { } projectId) open = open.Where(t => t.ProjectId == projectId);
        var asOf = r.AsOf;
        var unassigned = await open.CountAsync(t => t.AssignedTo == null, ct);

        var perTicket = open.Where(t => t.AssignedTo != null).Select(t => new
        {
            AssignedTo = t.AssignedTo!,
            t.TicketStatus,
            t.CreatedDate,
            Since = t.TicketEvents
                .Where(e => ((int)e.ForActivity & AssignmentActivities.Mask) != 0 ||
                            (((int)e.ForActivity & AssignmentActivities.ReOpen) != 0 && e.EventBy == t.AssignedTo))
                .Max(e => (DateTimeOffset?)e.EventDate) ?? t.CreatedDate,
        });
        var grouped = await perTicket
            .GroupBy(x => x.AssignedTo)
            .Select(g => new
            {
                AssignedTo = g.Key,
                Active = g.Count(x => x.TicketStatus == TicketStatus.Active),
                MoreInfo = g.Count(x => x.TicketStatus == TicketStatus.MoreInfo),
                Resolved = g.Count(x => x.TicketStatus == TicketStatus.Resolved),
                Open = g.Count(),
                Oldest = g.Min(x => x.CreatedDate),
                AvgHours = g.Average(x => (double)EF.Functions.DateDiffHour(x.Since, asOf)),
                MaxHours = g.Max(x => EF.Functions.DateDiffHour(x.Since, asOf)),
            })
            .OrderByDescending(x => x.Open).ThenBy(x => x.AssignedTo)
            .Take(r.Top)
            .ToListAsync(ct);
        var assignees = await perTicket.Select(x => x.AssignedTo).Distinct().CountAsync(ct);

        var ids = grouped.Select(x => x.AssignedTo).ToArray();
        var windowStart = asOf.AddDays(-30);
        var closedQuery = db.Tickets.AsNoTracking().Where(t => t.TicketStatus == TicketStatus.Closed &&
            t.CurrentStatusDate >= windowStart && t.CurrentStatusDate < asOf && ids.Contains(t.AssignedTo!));
        if (r.ProjectId is { } p2) closedQuery = closedQuery.Where(t => t.ProjectId == p2);
        var closed = await closedQuery.GroupBy(t => t.AssignedTo!).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);

        var names = await users.GetDisplayNamesAsync(ids, ct);
        var rows = grouped.Select(x => new AssigneeWorkloadRow(names.ToUserRef(x.AssignedTo), x.Active, x.MoreInfo, x.Resolved, x.Open,
            x.Oldest, x.AvgHours, x.MaxHours, closed.GetValueOrDefault(x.AssignedTo))).ToList();
        return new AssigneeWorkloadReport(r.AsOf, r.ProjectId, unassigned, assignees, rows);
    }

    public async Task<TimeToCloseReport> TimeToCloseAsync(TimeToCloseRequest r, CancellationToken ct)
    {
        var q = db.Tickets.AsNoTracking().Where(t => t.TicketStatus == TicketStatus.Closed && t.CurrentStatusDate >= r.From && t.CurrentStatusDate < r.To);
        if (r.ProjectId is { } projectId) q = q.Where(t => t.ProjectId == projectId);
        // EF Core cannot translate percentiles: fetch one duration per closed ticket and aggregate in memory
        var durations = await q.Select(t => new { t.ProjectId, Seconds = EF.Functions.DateDiffSecond(t.CreatedDate, t.CurrentStatusDate) }).ToListAsync(ct);
        var names = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.ProjectId, p => p.ProjectName, ct);

        static TimeToCloseRow Row(int? projectId, string? name, IEnumerable<int> seconds)
        {
            var hours = seconds.Select(s => s / 3600.0).Order().ToList();
            return new TimeToCloseRow(projectId, name, hours.Count, hours.Average(), Percentiles.Cont(hours, 0.5), Percentiles.Cont(hours, 0.9), hours[^1]);
        }

        var projects = durations.GroupBy(d => d.ProjectId)
            .Select(g => Row(g.Key, names.GetValueOrDefault(g.Key), g.Select(x => x.Seconds)))
            .OrderBy(x => x.ProjectName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ProjectId)
            .ToList();
        var overall = durations.Count == 0 ? null : Row(null, null, durations.Select(x => x.Seconds));
        return new TimeToCloseReport(r.From, r.To, r.ProjectId, overall, projects);
    }
}
