using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TicketDesk.Identity;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>/api/reports over the report procedures: help desk only, validated parameters, totals that match the data.</summary>
public sealed class ReportsApiTests(ApiFixture f) : IClassFixture<ApiFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private ScratchDatabase Db => f.Db;
    private string Staff => Db.UserInRole(TicketDeskRoles.HelpDeskUsers).Id;
    private string Internal => Db.UserInRole(TicketDeskRoles.InternalUsers).Id;
    private const string AsOf = "2026-10-31T17:00:00Z";

    private async Task<JsonElement> GetJson(string url)
    {
        using var response = await f.As(Staff).GetAsync(url, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Theory]
    [InlineData("/api/reports/open-tickets")]
    [InlineData("/api/reports/assignee-workload")]
    [InlineData("/api/reports/time-to-close")]
    public async Task Reports_are_for_help_desk_only(string url)
    {
        using var forbidden = await f.As(Internal).GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var anonymous = await f.As(null).GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Open_tickets_report_counts_every_open_ticket_once()
    {
        var report = await GetJson($"/api/reports/open-tickets?asOf={Uri.EscapeDataString(AsOf)}");
        var open = await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus <> 3");
        Assert.Equal(open, report.GetProperty("totalOpen").GetInt32());
        Assert.Equal(open, report.GetProperty("rows").EnumerateArray().Sum(r => r.GetProperty("tickets").GetInt32()));
        Assert.Equal(["<1d", "1-7d", "7-30d", "30-90d", ">90d"], report.GetProperty("ageBuckets").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain(report.GetProperty("rows").EnumerateArray(), r => r.GetProperty("status").GetString() == "Closed");
    }

    [Fact]
    public async Task Workload_report_matches_open_assigned_tickets_and_honours_top()
    {
        var report = await GetJson($"/api/reports/assignee-workload?asOf={Uri.EscapeDataString(AsOf)}&top=3");
        var rows = report.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo IS NULL"), report.GetProperty("unassignedOpen").GetInt32());
        Assert.Equal(await Db.ScalarAsync<int>("SELECT COUNT(DISTINCT AssignedTo) FROM dbo.Tickets WHERE TicketStatus <> 3"), report.GetProperty("assignees").GetInt32());
        var top = rows[0];
        var id = top.GetProperty("assignee").GetProperty("id").GetString();
        Assert.Equal(await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo = @a", ("@a", id)), top.GetProperty("open").GetInt32());
        Assert.False(string.IsNullOrEmpty(top.GetProperty("assignee").GetProperty("displayName").GetString()));
        Assert.True(rows.Select(r => r.GetProperty("open").GetInt32()).SequenceEqual(rows.Select(r => r.GetProperty("open").GetInt32()).OrderDescending()));
    }

    [Fact]
    public async Task Time_to_close_report_covers_the_closed_tickets_of_the_window()
    {
        var report = await GetJson($"/api/reports/time-to-close?from={Uri.EscapeDataString("2025-10-31T17:00:00Z")}&to={Uri.EscapeDataString(AsOf)}");
        var closed = await Db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketStatus = 3 AND CurrentStatusDate >= '2025-10-31T17:00:00Z' AND CurrentStatusDate < '2026-10-31T17:00:00Z'");
        Assert.True(closed > 0);
        Assert.Equal(closed, report.GetProperty("overall").GetProperty("closed").GetInt32());
        Assert.Equal(closed, report.GetProperty("projects").EnumerateArray().Sum(p => p.GetProperty("closed").GetInt32()));
        var o = report.GetProperty("overall");
        Assert.True(o.GetProperty("p50Hours").GetDouble() <= o.GetProperty("p90Hours").GetDouble());
        Assert.True(o.GetProperty("p90Hours").GetDouble() <= o.GetProperty("maxHours").GetDouble());
    }

    [Theory]
    [InlineData("/api/reports/assignee-workload?top=0", "top")]
    [InlineData("/api/reports/assignee-workload?top=501", "top")]
    [InlineData("/api/reports/time-to-close?from=2026-10-01T00:00:00Z&to=2026-09-01T00:00:00Z", "from")]
    [InlineData("/api/reports/time-to-close?from=2000-01-01T00:00:00Z&to=2026-01-01T00:00:00Z", "from")]
    public async Task Invalid_report_parameters_are_400_with_the_parameter_named(string url, string field)
    {
        using var response = await f.As(Staff).GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }
}
