using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Users;

namespace TicketDesk.Reports;

/// <summary>The reports over their stored procedures (dbo.usp_Report_*; docs/sql/tuning.md). Same results as <see cref="LinqReportQueries"/>.</summary>
public sealed class ReportQueries(TicketDeskDbContext db)
{
    public async Task<OpenTicketsReport> OpenTicketsAsync(OpenTicketsRequest r, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<OpenTicketsProcRow>(
                "EXEC dbo.usp_Report_OpenTicketsAging @AsOf = @AsOf, @ProjectId = @ProjectId",
                AsOf(r.AsOf), ProjectId(r.ProjectId))
            .ToListAsync(ct);
        var result = rows.Select(x => new OpenTicketsRow(x.ProjectId, x.ProjectName, (TicketStatus)x.TicketStatus, x.AgeBucket, AgeBuckets.Labels[x.AgeBucket], x.Tickets)).ToList();
        return new OpenTicketsReport(r.AsOf, r.ProjectId, AgeBuckets.Labels, result.Sum(x => x.Tickets), result);
    }

    public async Task<AssigneeWorkloadReport> AssigneeWorkloadAsync(AssigneeWorkloadRequest r, CancellationToken ct)
    {
        var unassigned = new SqlParameter("@UnassignedOpen", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var rows = await db.Database.SqlQueryRaw<WorkloadProcRow>(
                "EXEC dbo.usp_Report_AssigneeWorkload @AsOf = @AsOf, @ProjectId = @ProjectId, @Top = @Top, @UnassignedOpen = @UnassignedOpen OUTPUT",
                AsOf(r.AsOf), ProjectId(r.ProjectId), new SqlParameter("@Top", SqlDbType.Int) { Value = r.Top }, unassigned)
            .ToListAsync(ct);
        var result = rows.Select(x => new AssigneeWorkloadRow(new UserRef(x.AssignedTo, x.AssigneeDisplayName), x.Active, x.MoreInfo, x.Resolved, x.Open,
            x.OldestOpenCreatedDate, x.AvgHoursSinceAssigned, x.MaxHoursSinceAssigned, x.ClosedLast30Days)).ToList();
        return new AssigneeWorkloadReport(r.AsOf, r.ProjectId, unassigned.Value is int u ? u : 0, rows.FirstOrDefault()?.Assignees ?? 0, result);
    }

    public async Task<TimeToCloseReport> TimeToCloseAsync(TimeToCloseRequest r, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<TimeToCloseProcRow>(
                "EXEC dbo.usp_Report_TimeToClose @From = @From, @To = @To, @ProjectId = @ProjectId",
                new SqlParameter("@From", SqlDbType.DateTimeOffset) { Value = r.From }, new SqlParameter("@To", SqlDbType.DateTimeOffset) { Value = r.To },
                ProjectId(r.ProjectId))
            .ToListAsync(ct);
        static TimeToCloseRow Row(TimeToCloseProcRow x) => new(x.ProjectId, x.ProjectName, x.Closed, x.AvgHours, x.P50Hours, x.P90Hours, x.MaxHours);
        var overall = rows.Where(x => x.ProjectId == null).Select(Row).FirstOrDefault();
        return new TimeToCloseReport(r.From, r.To, r.ProjectId, overall, rows.Where(x => x.ProjectId != null).Select(Row).ToList());
    }

    private static SqlParameter AsOf(DateTimeOffset v) => new("@AsOf", SqlDbType.DateTimeOffset) { Value = v };
    private static SqlParameter ProjectId(int? v) => new("@ProjectId", SqlDbType.Int) { Value = (object?)v ?? DBNull.Value };

    internal sealed class OpenTicketsProcRow
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public int TicketStatus { get; set; }
        public int AgeBucket { get; set; }
        public int Tickets { get; set; }
    }

    internal sealed class WorkloadProcRow
    {
        public string AssignedTo { get; set; } = "";
        public string? AssigneeDisplayName { get; set; }
        public int Active { get; set; }
        public int MoreInfo { get; set; }
        public int Resolved { get; set; }
        public int Open { get; set; }
        public DateTimeOffset OldestOpenCreatedDate { get; set; }
        public double AvgHoursSinceAssigned { get; set; }
        public int MaxHoursSinceAssigned { get; set; }
        public int ClosedLast30Days { get; set; }
        public int Assignees { get; set; }
    }

    internal sealed class TimeToCloseProcRow
    {
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int Closed { get; set; }
        public double AvgHours { get; set; }
        public double P50Hours { get; set; }
        public double P90Hours { get; set; }
        public double MaxHours { get; set; }
    }
}
