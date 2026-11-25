using System.Globalization;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Attachments;
using TicketDesk.Data;
using TicketDesk.Identity;
using TicketDesk.Reports;
using TicketDesk.Search;
using TicketDesk.Tickets;
using TicketDesk.Users;

namespace TicketDesk.SqlPerf;

/// <summary>
/// The slow-path candidates: one scenario per API call (ticket-center lists and filters, ticket detail, the three
/// reports), run through the app's own query classes so the captured SQL is exactly what the app sends.
/// Set "naive": the Round 1 EF Core LINQ list (<see cref="TicketQueries.ListWithLinqAsync"/>, which was ListAsync until
/// this round) and the first-pass LINQ reports (<see cref="LinqReportQueries"/>). Set "tuned": what the API runs now
/// (<see cref="TicketQueries.ListAsync"/> and <see cref="ReportQueries"/>, over the stored procedures). Detail is the
/// same EF Core code in both sets. Set "search" (round 3): the full-text search statement (td-db V0011) for the golden
/// queries and the harder shapes, through <see cref="ITicketSearch"/>.
/// </summary>
public static class Scenarios
{
    public static readonly DateTimeOffset DefaultAsOf = new(2026, 10, 31, 17, 0, 0, TimeSpan.Zero);

    private sealed record Def(string Name, string Description, Func<IServiceProvider, Values, Task> Run);

    public sealed record Values(string Staff, string HeavyOwner, string LightOwner, int BigProject, int SmallProject, string Tag, int DetailTicket, DateTimeOffset AsOf)
    {
        public Dictionary<string, string> ToDictionary() => new()
        {
            ["staff (help desk user with the most open assigned tickets)"] = Staff,
            ["heavyOwner (owner with the most tickets)"] = HeavyOwner,
            ["lightOwner (first owner, by id, with 3-10 open tickets)"] = LightOwner,
            ["bigProject (most tickets)"] = BigProject.ToString(CultureInfo.InvariantCulture),
            ["smallProject (fewest tickets)"] = SmallProject.ToString(CultureInfo.InvariantCulture),
            ["tag (most used)"] = Tag,
            ["detailTicket (most events)"] = DetailTicket.ToString(CultureInfo.InvariantCulture),
            ["asOf"] = AsOf.ToString("O", CultureInfo.InvariantCulture),
        };
    }

    public static async Task<Values> PickValuesAsync(string conn, DateTimeOffset asOf)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync();
        async Task<object> Scalar(string sql)
        {
            await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 600 };
            return (await cmd.ExecuteScalarAsync())!;
        }
        return new Values(
            (string)await Scalar("""
                SELECT TOP 1 t.AssignedTo FROM dbo.Tickets t
                JOIN dbo.IdentityUserRoles ur ON ur.UserId = t.AssignedTo JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = 'TdHelpDeskUsers'
                WHERE t.TicketStatus <> 3 GROUP BY t.AssignedTo ORDER BY COUNT(*) DESC, t.AssignedTo
                """),
            (string)await Scalar("SELECT TOP 1 Owner FROM dbo.Tickets GROUP BY Owner ORDER BY COUNT(*) DESC, Owner"),
            (string)await Scalar("SELECT TOP 1 Owner FROM dbo.Tickets WHERE TicketStatus <> 3 GROUP BY Owner HAVING COUNT(*) BETWEEN 3 AND 10 ORDER BY Owner"),
            (int)await Scalar("SELECT TOP 1 ProjectId FROM dbo.Tickets GROUP BY ProjectId ORDER BY COUNT(*) DESC, ProjectId"),
            (int)await Scalar("SELECT TOP 1 ProjectId FROM dbo.Tickets GROUP BY ProjectId ORDER BY COUNT(*), ProjectId"),
            (string)await Scalar("SELECT TOP 1 TagName FROM dbo.TicketTags GROUP BY TagName ORDER BY COUNT(*) DESC, TagName"),
            (int)await Scalar("SELECT TOP 1 TicketId FROM dbo.TicketEvents GROUP BY TicketId ORDER BY COUNT(*) DESC, TicketId"),
            asOf);
    }

    private static TicketListRequest List(string query)
    {
        var parsed = System.Web.HttpUtility.ParseQueryString(query);
        int? Int(string k) => parsed[k] is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : null;
        var q = new TicketListQuery
        {
            Page = Int("page"), PageSize = Int("pageSize"), List = parsed["list"], Sort = parsed["sort"], Status = parsed.GetValues("status"),
            ProjectId = Int("projectId"), Owner = parsed["owner"], AssignedTo = parsed["assignedTo"], Tag = parsed["tag"],
        };
        return TicketListRequest.TryCreate(q, out var errors) ?? throw new InvalidOperationException($"bad scenario query '{query}': {string.Join("; ", errors.Keys)}");
    }

    private static IEnumerable<Def> SearchDefinitions()
    {
        Def Search(string name, string description, Func<Values, string> term, Func<Values, int>? project = null, bool helpDesk = true) =>
            new(name, $"GET /search?term={description}", (sp, v) => sp.GetRequiredService<ITicketSearch>().SearchAsync(
                new SearchRequest(term(v), project?.Invoke(v) ?? 0, new SearchViewer(helpDesk ? v.Staff : v.HeavyOwner, helpDesk)), CancellationToken.None));
        yield return Search("search-printer", "printer (golden query; 66,582 titles at L)", _ => "printer");
        yield return Search("search-network", "network (golden path query)", _ => "network");
        yield return Search("search-team", "team (most frequent title word, 167,045 titles at L)", _ => "team");
        yield return Search("search-two-words", "printer network (either word)", _ => "printer network");
        yield return Search("search-required", "+printer +remote (both words, any field)", _ => "+printer +remote");
        yield return Search("search-excluded", "printer -remote", _ => "printer -remote");
        yield return Search("search-phrase", "\"remote staff\"", _ => "\"remote staff\"");
        yield return Search("search-prefix", "print*", _ => "print*");
        yield return Search("search-tag-field", "tags:printer", _ => "tags:printer");
        yield return Search("search-ticket-id", "<detail ticket id>", v => v.DetailTicket.ToString(CultureInfo.InvariantCulture));
        yield return Search("search-project", "printer, selected project = biggest", _ => "printer", v => v.BigProject);
        yield return Search("search-internal-user", "printer as an internal user (S12 filter; owner with the most tickets)", _ => "printer", helpDesk: false);
        yield return Search("search-no-hits", "zzqqxxnohits (golden query)", _ => "zzqqxxnohits");
    }

    private static IEnumerable<Def> Definitions(string set)
    {
        if (set == "search")
        {
            foreach (var d in SearchDefinitions()) yield return d;
            yield break;
        }
        Def ListCall(string name, string description, Func<Values, string> query, Func<Values, string> user) =>
            new(name, $"GET /api/tickets?{description}", async (sp, v) =>
            {
                var queries = sp.GetRequiredService<TicketQueries>();
                _ = set == "naive"
                    ? await queries.ListWithLinqAsync(List(query(v)), user(v), CancellationToken.None)
                    : await queries.ListAsync(List(query(v)), user(v), CancellationToken.None);
            });

        yield return ListCall("list-default", "(no list; default sort -lastUpdateDate)", _ => "", v => v.Staff);
        yield return ListCall("list-open-p1", "list=opentickets (sort status,targetDate,dueDate,-lastUpdateDate)", _ => "list=opentickets", v => v.Staff);
        yield return ListCall("list-open-p500", "list=opentickets&page=500 (deep page)", _ => "list=opentickets&page=500", v => v.Staff);
        yield return ListCall("list-unassigned", "list=unassigned (help desk)", _ => "list=unassigned", v => v.Staff);
        yield return ListCall("list-assignedToMe", "list=assignedToMe (busiest help desk user)", _ => "list=assignedToMe", v => v.Staff);
        yield return ListCall("list-mytickets-heavy", "list=mytickets (owner with the most tickets)", _ => "list=mytickets", v => v.HeavyOwner);
        yield return ListCall("list-mytickets-light", "list=mytickets (owner with 3-10 open tickets)", _ => "list=mytickets", v => v.LightOwner);
        yield return ListCall("list-history", "list=historytickets (closed, sort status,-lastUpdateDate)", _ => "list=historytickets", v => v.Staff);
        yield return ListCall("list-project-big", "projectId=<biggest project> (default sort)", v => $"projectId={v.BigProject}", v => v.Staff);
        yield return ListCall("list-project-small", "projectId=<smallest project> (default sort)", v => $"projectId={v.SmallProject}", v => v.Staff);
        yield return ListCall("list-status-sort-title", "status=Active&status=MoreInfo&sort=title", _ => "status=Active&status=MoreInfo&sort=title", v => v.Staff);
        yield return ListCall("list-tag", "tag=<most used tag> (default sort)", v => $"tag={Uri.EscapeDataString(v.Tag)}", v => v.Staff);
        yield return new Def("detail", "GET /api/tickets/{id} (ticket with the most events)", async (sp, v) =>
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, v.Staff), new Claim(ClaimTypes.Role, TicketDeskRoles.HelpDeskUsers)], "perf"));
            _ = await sp.GetRequiredService<TicketQueries>().GetAsync(v.DetailTicket, user, CancellationToken.None)
                ?? throw new InvalidOperationException("detail ticket not found");
        });

        if (set == "naive")
        {
            yield return new Def("report-open-aging", "GET /api/reports/open-tickets (first-pass LINQ)", (sp, v) =>
                sp.GetRequiredService<LinqReportQueries>().OpenTicketsAsync(new OpenTicketsRequest(v.AsOf, null), CancellationToken.None));
            yield return new Def("report-workload", "GET /api/reports/assignee-workload?top=50 (first-pass LINQ, assigned-since from TicketEvents)", (sp, v) =>
                sp.GetRequiredService<LinqReportQueries>().AssigneeWorkloadAsync(new AssigneeWorkloadRequest(v.AsOf, null, 50), CancellationToken.None));
            yield return new Def("report-time-to-close", "GET /api/reports/time-to-close (last 90 days; first-pass LINQ, percentiles in .NET)", (sp, v) =>
                sp.GetRequiredService<LinqReportQueries>().TimeToCloseAsync(new TimeToCloseRequest(v.AsOf.AddDays(-90), v.AsOf, null), CancellationToken.None));
        }
        else
        {
            yield return new Def("report-open-aging", "GET /api/reports/open-tickets (usp_Report_OpenTicketsAging)", (sp, v) =>
                sp.GetRequiredService<ReportQueries>().OpenTicketsAsync(new OpenTicketsRequest(v.AsOf, null), CancellationToken.None));
            yield return new Def("report-workload", "GET /api/reports/assignee-workload?top=50 (usp_Report_AssigneeWorkload, Tickets.AssignedDate)", (sp, v) =>
                sp.GetRequiredService<ReportQueries>().AssigneeWorkloadAsync(new AssigneeWorkloadRequest(v.AsOf, null, 50), CancellationToken.None));
            yield return new Def("report-time-to-close", "GET /api/reports/time-to-close (last 90 days; usp_Report_TimeToClose)", (sp, v) =>
                sp.GetRequiredService<ReportQueries>().TimeToCloseAsync(new TimeToCloseRequest(v.AsOf.AddDays(-90), v.AsOf, null), CancellationToken.None));
        }
    }

    public static async Task<CaseFile> CaptureAsync(string conn, string set, DateTimeOffset asOf, string source)
    {
        var values = await PickValuesAsync(conn, asOf);
        var recorder = new CommandRecorder();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TicketDesk"] = conn,
            ["Attachments:RootPath"] = Path.Combine(Path.GetTempPath(), "td-sqlperf-no-attachments"),
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment, PerfHostEnvironment>();
        services.AddDbContext<TicketDeskDbContext>(o => o.UseTicketDeskSqlServer(conn).AddInterceptors(recorder));
        services.AddAuthorizationCore();
        services.AddUsersModule().AddAttachmentsModule(config).AddTicketsModule().AddSearchModule();
        services.AddScoped<LinqReportQueries>();
        services.AddReportsModule();
        await using var sp = services.BuildServiceProvider(validateScopes: true);
        if (set == "search")
        {
            // the stoplist is read once per process: read it before recording, so each scenario holds only its search statement
            await using var warm = sp.CreateAsyncScope();
            await warm.ServiceProvider.GetRequiredService<FullTextStopwords>().GetAsync(warm.ServiceProvider.GetRequiredService<TicketDeskDbContext>(), CancellationToken.None);
        }

        var file = new CaseFile
        {
            Set = set, Database = new SqlConnectionStringBuilder(conn).InitialCatalog, CapturedAt = DateTimeOffset.UtcNow.ToString("O"),
            Source = source, Values = values.ToDictionary(),
        };
        foreach (var def in Definitions(set))
        {
            var scenario = new Scenario { Name = def.Name, Description = def.Description };
            await using var scope = sp.CreateAsyncScope();
            recorder.Current = scenario;
            try { await def.Run(scope.ServiceProvider, values); }
            finally { recorder.Current = null; }
            file.Scenarios.Add(scenario);
            Console.Error.WriteLine($"captured {set}/{def.Name}: {scenario.Commands.Count} command(s)");
        }
        return file;
    }
}

internal sealed class PerfHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string ApplicationName { get; set; } = "td-sqlperf";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Perf";
}
