using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Attachments;
using TicketDesk.Data;
using TicketDesk.Reports;
using TicketDesk.Tickets;
using TicketDesk.Users;

namespace TicketDesk.SqlPerf;

/// <summary>
/// Same answers at scale: runs every list scenario and report through the naive LINQ code and through the tuned
/// procedures against one database and compares the serialized results (doubles to a relative 1e-9).
/// </summary>
public static class Verify
{
    public static async Task<int> RunAsync(string conn, DateTimeOffset asOf, TextWriter output)
    {
        var values = await Scenarios.PickValuesAsync(conn, asOf);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TicketDesk"] = conn,
            ["Attachments:RootPath"] = Path.Combine(Path.GetTempPath(), "td-sqlperf-no-attachments"),
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment, PerfHostEnvironment>();
        services.AddDbContext<TicketDeskDbContext>(o => o.UseTicketDeskSqlServer(conn));
        services.AddAuthorizationCore();
        services.AddUsersModule().AddAttachmentsModule(config).AddTicketsModule().AddReportsModule();
        services.AddScoped<LinqReportQueries>();
        await using var sp = services.BuildServiceProvider();

        var listQueries = new (string Name, string Query, string User)[]
        {
            ("default", "", values.Staff), ("opentickets", "list=opentickets", values.Staff), ("opentickets p500", "list=opentickets&page=500", values.Staff),
            ("unassigned", "list=unassigned", values.Staff), ("assignedToMe", "list=assignedToMe", values.Staff),
            ("mytickets heavy", "list=mytickets", values.HeavyOwner), ("mytickets light", "list=mytickets", values.LightOwner),
            ("historytickets", "list=historytickets", values.Staff), ("project big", $"projectId={values.BigProject}", values.Staff),
            ("project small", $"projectId={values.SmallProject}", values.Staff), ("status+title", "status=Active&status=MoreInfo&sort=title", values.Staff),
            ("tag", $"tag={values.Tag}", values.Staff), ("tag+sort", $"tag={values.Tag}&sort=-priority,createdDate&page=3&pageSize=50", values.Staff),
            ("owner+project", $"owner={values.HeavyOwner}&projectId={values.BigProject}&sort=dueDate", values.Staff),
            ("assignee+status", $"assignedTo={values.Staff}&status=Resolved&sort=-currentStatusDate", values.Staff),
        };
        var failures = 0;
        foreach (var (name, query, user) in listQueries)
        {
            await using var scope = sp.CreateAsyncScope();
            var q = scope.ServiceProvider.GetRequiredService<TicketQueries>();
            var request = TicketListRequest.TryCreate(Parse(query), out _)!;
            var linq = await q.ListWithLinqAsync(request, user, CancellationToken.None);
            var proc = await q.ListAsync(request, user, CancellationToken.None);
            failures += Report(output, $"list {name}", linq, proc, $"total {proc.TotalCount:N0}, {proc.Items.Count} rows");
        }

        {
            await using var scope = sp.CreateAsyncScope();
            var linq = scope.ServiceProvider.GetRequiredService<LinqReportQueries>();
            var proc = scope.ServiceProvider.GetRequiredService<ReportQueries>();
            var open = new OpenTicketsRequest(values.AsOf, null);
            var a = await linq.OpenTicketsAsync(open, CancellationToken.None);
            var b = await proc.OpenTicketsAsync(open, CancellationToken.None);
            failures += Report(output, "report open-tickets", Sorted(a), Sorted(b), $"{b.Rows.Count} rows, {b.TotalOpen:N0} open");
            var openP = new OpenTicketsRequest(values.AsOf, values.SmallProject);
            failures += Report(output, "report open-tickets (project)", Sorted(await linq.OpenTicketsAsync(openP, CancellationToken.None)), Sorted(await proc.OpenTicketsAsync(openP, CancellationToken.None)), "");
            var work = new AssigneeWorkloadRequest(values.AsOf, null, 50);
            var w = await proc.AssigneeWorkloadAsync(work, CancellationToken.None);
            failures += Report(output, "report assignee-workload", await linq.AssigneeWorkloadAsync(work, CancellationToken.None), w, $"{w.Rows.Count} rows of {w.Assignees:N0} assignees, {w.UnassignedOpen:N0} unassigned");
            var workP = new AssigneeWorkloadRequest(values.AsOf, values.BigProject, 500);
            failures += Report(output, "report assignee-workload (project, top 500)", await linq.AssigneeWorkloadAsync(workP, CancellationToken.None), await proc.AssigneeWorkloadAsync(workP, CancellationToken.None), "");
            var ttc = new TimeToCloseRequest(values.AsOf.AddDays(-90), values.AsOf, null);
            var t = await proc.TimeToCloseAsync(ttc, CancellationToken.None);
            failures += Report(output, "report time-to-close", SortedT(await linq.TimeToCloseAsync(ttc, CancellationToken.None)), SortedT(t), $"{t.Overall?.Closed:N0} closed, {t.Projects.Count} projects");
            var ttcP = new TimeToCloseRequest(values.AsOf.AddDays(-365), values.AsOf, values.SmallProject);
            failures += Report(output, "report time-to-close (project, 365 days)", SortedT(await linq.TimeToCloseAsync(ttcP, CancellationToken.None)), SortedT(await proc.TimeToCloseAsync(ttcP, CancellationToken.None)), "");
        }
        output.WriteLine(failures == 0 ? "ALL EQUAL" : $"{failures} DIFFERENCE(S)");
        return failures == 0 ? 0 : 1;
    }

    // ordering of report rows by project name differs between SQL collation and .NET; compare by key
    private static OpenTicketsReport Sorted(OpenTicketsReport r) => r with { Rows = r.Rows.OrderBy(x => x.ProjectId).ThenBy(x => x.Status).ThenBy(x => x.AgeBucket).ToList() };
    private static TimeToCloseReport SortedT(TimeToCloseReport r) => r with { Projects = r.Projects.OrderBy(x => x.ProjectId).ToList() };

    private static TicketListQuery Parse(string query)
    {
        var p = System.Web.HttpUtility.ParseQueryString(query);
        int? Int(string k) => p[k] is { } v ? int.Parse(v) : null;
        return new TicketListQuery
        {
            Page = Int("page"), PageSize = Int("pageSize"), List = p["list"], Sort = p["sort"], Status = p.GetValues("status"),
            ProjectId = Int("projectId"), Owner = p["owner"], AssignedTo = p["assignedTo"], Tag = p["tag"],
        };
    }

    private static int Report<T>(TextWriter output, string name, T expected, T actual, string detail)
    {
        var a = JsonSerializer.SerializeToNode(expected)!;
        var b = JsonSerializer.SerializeToNode(actual)!;
        var diff = Diff(a, b, "$");
        output.WriteLine($"{(diff == null ? "equal    " : "DIFFERENT")} {name}{(detail.Length > 0 ? " (" + detail + ")" : "")}{(diff == null ? "" : ": " + diff)}");
        return diff == null ? 0 : 1;
    }

    public static string? Diff(JsonNode? a, JsonNode? b, string path)
    {
        switch (a, b)
        {
            case (null, null): return null;
            case (JsonObject oa, JsonObject ob):
                foreach (var key in oa.Select(p => p.Key).Union(ob.Select(p => p.Key)))
                    if (Diff(oa[key], ob[key], $"{path}.{key}") is { } d) return d;
                return null;
            case (JsonArray xa, JsonArray xb):
                if (xa.Count != xb.Count) return $"{path}: {xa.Count} vs {xb.Count} elements";
                for (var i = 0; i < xa.Count; i++)
                    if (Diff(xa[i], xb[i], $"{path}[{i}]") is { } d) return d;
                return null;
            case (JsonValue va, JsonValue vb):
                if (va.TryGetValue<double>(out var da) && vb.TryGetValue<double>(out var db))
                    return Math.Abs(da - db) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(da), Math.Abs(db))) ? null : $"{path}: {da} vs {db}";
                return va.ToJsonString() == vb.ToJsonString() ? null : $"{path}: {va.ToJsonString()} vs {vb.ToJsonString()}";
            default:
                return $"{path}: {a?.ToJsonString() ?? "null"} vs {b?.ToJsonString() ?? "null"}";
        }
    }
}
