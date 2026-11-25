using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Reports;
using TicketDesk.SqlPerf;
using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>
/// The report procedures give the first-pass LINQ reports' answers (doubles to a relative 1e-9), including the age
/// bucket edges with mixed time-zone offsets (the legacy app stores server-local offsets, the new app UTC).
/// </summary>
public sealed class ReportProcedureTests(ScratchDatabase db) : IClassFixture<ScratchDatabase>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset BaseDate = new(2026, 10, 31, 17, 0, 0, TimeSpan.Zero);
    private AppServices? _app;

    public ValueTask InitializeAsync()
    {
        if (db.Available) _app = new AppServices(db.ConnectionString!);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null) await _app.DisposeAsync();
    }

    private static void AssertSame<T>(T expected, T actual)
    {
        var diff = Verify.Diff(JsonSerializer.SerializeToNode(expected), JsonSerializer.SerializeToNode(actual), "$");
        Assert.True(diff == null, diff);
    }

    private Task<(T Linq, T Proc)> Both<T>(Func<LinqReportQueries, Task<T>> linq, Func<ReportQueries, Task<T>> proc) =>
        _app!.Run(async sp => (await linq(sp.GetRequiredService<LinqReportQueries>()), await proc(sp.GetRequiredService<ReportQueries>())));

    private static OpenTicketsReport ByKey(OpenTicketsReport r) => r with { Rows = r.Rows.OrderBy(x => x.ProjectId).ThenBy(x => x.Status).ThenBy(x => x.AgeBucket).ToList() };
    private static TimeToCloseReport ByKey(TimeToCloseReport r) => r with { Projects = r.Projects.OrderBy(x => x.ProjectId).ToList() };

    [Fact]
    public async Task Open_ticket_aging_equals_LINQ_including_bucket_edges_with_mixed_offsets()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        // tickets exactly at and next to the hour boundaries that separate the buckets, in three offsets
        var asOf = new DateTimeOffset(2026, 11, 5, 9, 41, 17, TimeSpan.FromHours(-8));
        var hourStartUtc = new DateTimeOffset(asOf.UtcDateTime.Year, asOf.UtcDateTime.Month, asOf.UtcDateTime.Day, asOf.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);
        var staff = db.UserInRole("TdHelpDeskUsers").Id;
        foreach (var hours in new[] { 23, 24, 167, 168, 719, 720, 2159, 2160 })
            foreach (var offset in new[] { TimeSpan.Zero, TimeSpan.FromHours(-8), TimeSpan.FromHours(5.5) })
                foreach (var delta in new[] { TimeSpan.Zero, TimeSpan.FromTicks(-1) })
                {
                    var created = hourStartUtc.AddHours(-(hours - 1)).Add(delta).ToOffset(offset);
                    await SqlHelpers.ExecAsync(db.ConnectionString!, """
                        INSERT dbo.Tickets (ProjectId, TicketType, Category, Title, Details, IsHtml, CreatedBy, CreatedDate, Owner, TicketStatus,
                            CurrentStatusDate, CurrentStatusSetBy, LastUpdateBy, LastUpdateDate, AffectsCustomer)
                        VALUES (@p, N'Question', N'Hardware', N'bucket edge', N'', 0, @u, @c, @u, 0, @c, @u, @u, @c, 0)
                        """, ("@p", db.Generator!.ProjectIds[0]), ("@u", staff), ("@c", created));
                }
        foreach (var at in new[] { asOf, BaseDate, BaseDate.AddDays(-45) })
            foreach (var project in new int?[] { null, db.Generator!.ProjectIds[0], db.Generator.ProjectIds[2] })
            {
                var r = new OpenTicketsRequest(at, project);
                var (a, b) = await Both(l => l.OpenTicketsAsync(r, Ct), p => p.OpenTicketsAsync(r, Ct));
                AssertSame(ByKey(a), ByKey(b));
                Assert.True(b.TotalOpen > 0);
            }
    }

    [Fact]
    public async Task Assignee_workload_equals_LINQ()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        foreach (var at in new[] { BaseDate, BaseDate.AddDays(-60) })
            foreach (var project in new int?[] { null, db.Generator!.ProjectIds[1] })
                foreach (var top in new[] { 1, 5, 500 })
                {
                    var r = new AssigneeWorkloadRequest(at, project, top);
                    var (a, b) = await Both(l => l.AssigneeWorkloadAsync(r, Ct), p => p.AssigneeWorkloadAsync(r, Ct));
                    AssertSame(a, b);
                    Assert.NotEmpty(b.Rows);
                }
    }

    [Fact]
    public async Task Time_to_close_equals_LINQ_and_an_empty_window_has_no_overall_row()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        foreach (var (from, to) in new[] { (BaseDate.AddDays(-90), BaseDate), (BaseDate.AddDays(-730), BaseDate), (BaseDate.AddDays(-7), BaseDate) })
            foreach (var project in new int?[] { null, db.Generator!.ProjectIds[0] })
            {
                var r = new TimeToCloseRequest(from, to, project);
                var (a, b) = await Both(l => l.TimeToCloseAsync(r, Ct), p => p.TimeToCloseAsync(r, Ct));
                AssertSame(ByKey(a), ByKey(b));
            }
        var empty = new TimeToCloseRequest(new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1999, 2, 1, 0, 0, 0, TimeSpan.Zero), null);
        var (x, y) = await Both(l => l.TimeToCloseAsync(empty, Ct), p => p.TimeToCloseAsync(empty, Ct));
        AssertSame(x, y);
        Assert.Null(y.Overall);
        Assert.Empty(y.Projects);
    }

    [Theory]
    [InlineData("EXEC dbo.usp_Report_AssigneeWorkload @AsOf = '2026-10-31', @Top = 0", 50020)]
    [InlineData("EXEC dbo.usp_Report_TimeToClose @From = '2026-10-31', @To = '2026-10-01'", 50030)]
    public async Task Report_procedures_validate_their_parameters(string sql, int error)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var e = await Assert.ThrowsAsync<SqlException>(() => SqlHelpers.ExecAsync(db.ConnectionString!, sql));
        Assert.Equal(error, e.Number);
    }
}
