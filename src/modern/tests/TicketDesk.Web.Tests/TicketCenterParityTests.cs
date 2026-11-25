using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Replay;

namespace TicketDesk.Web.Tests;

/// <summary>
/// Waves 2 and 3 of the ticket center replayed against the new app on a copy of the baseline data, step by step as the golden
/// <c>ticket-list</c> and <c>internal-user</c> journeys did (same order, so list settings and project selection evolve the
/// same way), each answer compared with the recorded legacy answer by td-replay's comparer. The user comes from the test
/// scheme (in the gateway: remote authentication).
/// </summary>
public sealed partial class TicketCenterParityTests(BaselineDatabase db) : IClassFixture<BaselineDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static readonly string[] Wave23Routes =
    [
        "tickets.index", "tickets.page", "tickets.filter", "tickets.sort", "tickets.reset-lists", "autocomplete.tag-list",
        "navigation.change-project", "ticket.list-redirect",
    ];

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRx();

    private sealed class Journey(HttpClient client, string principal, CancellationToken ct)
    {
        public List<string> Failures { get; } = [];
        public int Explained { get; private set; }
        public string LastBody { get; private set; } = "";

        /// <summary>The token of the latest page that rendered one (the list partials do not).</summary>
        public string LastToken { get; private set; } = "";

        public int TieRows { get; private set; }

        /// <param name="sortTies">The list is sorted by a column with ties (title): the legacy order within ties is not
        /// reproducible (S5), so table rows are checked by their sort keys instead of compared one by one.</param>
        public async Task Step(string key, HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? form = null, bool ajax = false,
            bool sortTies = false)
        {
            var golden = Golden.Get(key);
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
            if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            if (form != null) request.Content = new FormUrlEncodedContent(form);
            // the page script sends the token as a header on AJAX posts without one in the form (_AntiforgeryAjax)
            if (ajax && method == HttpMethod.Post && form == null) request.Headers.Add("RequestVerificationToken", LastToken);
            using var response = await client.SendAsync(request, ct);
            var recorded = await Golden.RecordAsync(golden, response, ct);
            LastBody = recorded.Response!.Body ?? "";
            if (Token() is { Length: > 0 } token) LastToken = token;
            if (!response.Headers.TryGetValues("X-TicketDesk-Backend", out var backend) || !backend.Single().StartsWith("modern;", StringComparison.Ordinal))
                Failures.Add($"{key} {path}: not answered by the new app");
            var diffs = Golden.Compare(golden, recorded);
            if (sortTies && !SameColumn(golden.Response!.Body!, recorded.Response!.Body!, 2))
                Failures.Add($"{key}: the title sequence differs from the legacy page");
            Explained += diffs.RemoveAll(Golden.IsAntiforgeryDifference); // S3: forms that post to the new app carry its token
            Explained += diffs.RemoveAll(d => Golden.IsAntiforgeryScript(d) || Golden.IsSortHeaderPost(d)); // S3 (Round 5): the sort headers post
            if (sortTies) TieRows += diffs.RemoveAll(Golden.IsTicketRow);
            if (diffs.Count > 0) Failures.Add($"{key} {method} {path}:\n{Golden.Describe(diffs)}");
        }

        /// <summary>The text of one table column, row by row (sort keys).</summary>
        private static bool SameColumn(string a, string b, int td)
        {
            static List<string> Column(string html, int td) =>
                new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).QuerySelectorAll("tbody.ticketItems tr")
                    .Select(tr => string.Join(" ", tr.QuerySelectorAll("td").First().ChildNodes.Where(n => n.NodeType == AngleSharp.Dom.NodeType.Text).Select(n => n.TextContent.Trim())).Trim()).ToList();
            return Column(a, td).SequenceEqual(Column(b, td));
        }

        public string Token() => TokenRx().Match(LastBody) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : "";
    }

    private async Task<(TicketDeskApp App, string AdminId, string AliId)> StartAsync()
    {
        if (db.ConnectionString == null) Assert.Skip("needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        var app = new TicketDeskApp(WaveParityTests.Migrated(db.ConnectionString, [.. WaveParityTests.Wave01Routes, .. Wave23Routes]), testAuth: true);
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        // the lab admin (its name and e-mail are secrets): the owner the create-ticket step of the recording submitted
        var adminId = Golden.Get("ticket-create-view#003.0").Request.Form!.Single(f => f.Key == "Owner").Value;
        Assert.True(await ctx.UserRoles.AnyAsync(ur => ur.UserId == adminId && ctx.Roles.Any(r => r.Id == ur.RoleId && r.Name == "TdAdministrators"), Ct));
        var aliId = await ctx.Users.Where(u => u.DisplayName == "Ali Chen").Select(u => u.Id).FirstAsync(Ct);
        return (app, adminId, aliId);
    }

    private static HttpClient Client(TicketDeskApp app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [Fact]
    public async Task Ticket_list_journey_matches_the_golden_recording()
    {
        var (app, adminId, _) = await StartAsync();
        await using var _ = app;
        using var client = Client(app);
        var adminPrincipal = TestAuthHandler.Principal("{secret:TD_ADMIN_NAME}", adminId, "TdAdministrators", "TdHelpDeskUsers", "TdInternalUsers");
        var j = new Journey(client, adminPrincipal, Ct);

        await j.Step("ticket-list#002.0", HttpMethod.Get, "/tickets");
        await j.Step("ticket-list#003.0", HttpMethod.Get, "/ticket");
        await j.Step("ticket-list#003.1", HttpMethod.Get, "/tickets");
        await j.Step("ticket-list#004.0", HttpMethod.Get, "/tickets/mytickets");
        await j.Step("ticket-list#005.0", HttpMethod.Get, "/tickets/unassigned");
        await j.Step("ticket-list#006.0", HttpMethod.Get, "/tickets/assignedToMe");
        await j.Step("ticket-list#007.0", HttpMethod.Get, "/tickets/historytickets");
        await j.Step("ticket-list#008.0", HttpMethod.Get, "/tickets/opentickets");
        await j.Step("ticket-list#009.0", HttpMethod.Get, "/tickets/pageList/opentickets/2", ajax: true);
        // the journey submits the filter form of the previous answer; the new app's form carries its antiforgery token
        await j.Step("ticket-list#010.0", HttpMethod.Post, "/tickets/filterList",
            [new("Owner", "anyone"), new("AssignedTo", "anyone"), new("PageSize", "50"), new("__RequestVerificationToken", j.Token())], ajax: true);
        // S3 (Round 5): the column headers, the reset dialog and the project menu post with the page's token (GETs in the recording)
        await j.Step("ticket-list#011.0", sortTies: true, method: HttpMethod.Post, path: "/tickets/sortList/opentickets?columnName=Title", ajax: true);
        await j.Step("ticket-list#012.0", sortTies: true, method: HttpMethod.Post, path: "/tickets/sortList/opentickets?columnName=Title", ajax: true);
        await j.Step("ticket-list#013.0", sortTies: true, method: HttpMethod.Post, path: "/tickets/sortList/opentickets?columnName=Priority&isMultiSort=true", ajax: true);
        await j.Step("ticket-list#014.0", HttpMethod.Post, "/tickets/reset-user-lists", [new("__RequestVerificationToken", j.LastToken)]);
        await j.Step("ticket-list#014.1", HttpMethod.Get, "/tickets");
        // D1: the round-1 golden has plan order; the new app (and the legacy app on the shared schema) sort by name. Compared as a set.
        var tags = Golden.Get("ticket-list#015.0").Response!.Body!;
        using (var r = await Send(client, adminPrincipal, "/auto-complete/tag-list?term=net"))
        {
            static string[] Sorted(string json) => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>[]>(json)!.Select(t => t["id"] + "|" + t["text"]).Order().ToArray();
            Assert.Equal(Sorted(tags), Sorted(await r.Content.ReadAsStringAsync(Ct)));
        }
        await j.Step("ticket-list#016.0", HttpMethod.Post, "/navigation/change-project/2", [new("__RequestVerificationToken", j.LastToken)]);
        await j.Step("ticket-list#016.1", HttpMethod.Get, "/");
        await j.Step("ticket-list#017.0", HttpMethod.Get, "/tickets/opentickets");
        await j.Step("ticket-list#018.0", HttpMethod.Post, "/navigation/change-project/0", [new("__RequestVerificationToken", j.LastToken)]);
        await j.Step("ticket-list#018.1", HttpMethod.Get, "/");

        Assert.True(j.Failures.Count == 0, string.Join("\n\n", j.Failures));
        Assert.True(j.Explained > 0); // the list pages carry the antiforgery field (S3)
    }

    private async Task<HttpResponseMessage> Send(HttpClient client, string principal, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Internal_user_lists_match_the_golden_recording()
    {
        var (app, _, aliId) = await StartAsync();
        await using var _ = app;
        using var client = Client(app);
        var j = new Journey(client, TestAuthHandler.Principal("Ali Chen", aliId, "TdInternalUsers"), Ct);
        await j.Step("internal-user#002.0", HttpMethod.Get, "/tickets");
        await j.Step("internal-user#003.0", HttpMethod.Get, "/tickets/mytickets");
        Assert.True(j.Failures.Count == 0, string.Join("\n\n", j.Failures));
    }
}
