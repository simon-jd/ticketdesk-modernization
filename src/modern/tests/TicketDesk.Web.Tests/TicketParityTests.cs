using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Replay;

namespace TicketDesk.Web.Tests;

/// <summary>
/// The ticket page group (wave 2) and the ticket writes (wave 4): the golden ticket journeys run by td-replay's own runner
/// against the new app in process (test principal instead of the legacy sign-in, a copy of the baseline data, a local
/// attachment folder), every exchange of a migrated route compared with the recorded legacy answer. The journeys create
/// tickets 10001-10008 in the recorded order, so ticket ids, events and subscribers line up with the recording.
/// </summary>
public sealed class TicketParityTests(BaselineDatabase db) : IClassFixture<BaselineDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static readonly string[] TicketPageRoutes = ["ticket.view", "ticket.details", "ticket.events", "activity.buttons", "ticket.subscription", "ticket.files",
        "file.get", "file.attachments-info"];

    internal static readonly string[] Wave4Routes =
    [
        "ticket.new.get", "ticket.new.post", "activity.load", "activity.add-comment", "activity.assign", "activity.cancel-more-info", "activity.close",
        "activity.edit-ticket-info", "activity.force-close", "activity.give-up", "activity.modify-attachments", "activity.pass", "activity.reassign",
        "activity.request-more-info", "activity.reopen", "activity.resolve", "activity.supply-more-info", "activity.take-over", "file.upload", "file.delete",
    ];

    private static readonly string[] Journeys = ["ticket-create-view", "ticket-edit", "ticket-comment", "ticket-assign", "ticket-close", "ticket-workflow", "attachments", "internal-user"];

    /// <summary>
    /// Intended differences (expected-differences.md): S3 (the new app's antiforgery cookie, no-cache headers and, on pages
    /// whose scripts post without a form, the token field plus the script that sends it as a header; since Round 5 the sort
    /// headers that post and the layout's post forms, <see cref="Golden.IsMenuPostDifference"/>), L1 and L2.
    /// </summary>
    internal static bool IsExpected(ExchangeMismatch m, DiffEntry d) =>
        Golden.IsAntiforgeryDifference(d)
        || Golden.IsAntiforgeryScript(d)
        || Golden.IsSortHeaderPost(d) // S3 (Round 5): the ticket list's sort headers post
        || m.Key == "attachments#006.0" // L1: legacy 500, new app lists the pending file
        || m.Key is "internal-user#004.0" or "internal-user#004.1"; // L2: legacy 500, new app 302 to /access-denied

    [Fact]
    public async Task Golden_ticket_journeys_match_on_the_new_app()
    {
        if (db.ConnectionString == null) Assert.Skip("needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        var store = Directory.CreateTempSubdirectory("td-attachments-");
        try
        {
            var settings = WaveParityTests.Migrated(db.ConnectionString, [.. WaveParityTests.Wave01Routes, .. TicketCenterParityTests.Wave23Routes, .. TicketPageRoutes, .. Wave4Routes]);
            settings["Attachments:Store"] = "Local";
            settings["Attachments:RootPath"] = store.FullName;
            await using var app = new TicketDeskApp(settings, testAuth: true);
            string adminId, aliId, adminName;
            using (var scope = app.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
                adminId = Golden.Get("ticket-create-view#003.0").Request.Form!.Single(f => f.Key == "Owner").Value;
                aliId = await ctx.Users.Where(u => u.DisplayName == "Ali Chen").Select(u => u.Id).FirstAsync(Ct);
                // the recording redacted the lab admin's display name ({secret:TD_ADMIN_NAME}); the runner does the same here
                adminName = await ctx.Users.Where(u => u.Id == adminId).Select(u => u.DisplayName).FirstAsync(Ct);
                Assert.Equal(10000, await ctx.Tickets.MaxAsync(t => t.TicketId, Ct)); // the recording's tickets start at 10001
            }
            var routes = RouteTable.FromCsv(Path.Combine(Golden.RepoRoot!, "docs", "migration", "routes.csv"));
            var secrets = new SecretStore();
            foreach (var name in new[] { "TD_ADMIN_USER", "TD_ADMIN_PASSWORD", "TD_SEED_USER_PASSWORD" }) secrets.Set(name, "unused-" + name);
            secrets.Set("TD_ADMIN_NAME", adminName);
            var candidate = new List<RecordedExchange>();
            foreach (var journey in TicketDeskJourneys.Select(string.Join(",", Journeys)))
            {
                var principal = journey.Name == "internal-user"
                    ? TestAuthHandler.Principal("Ali Chen", aliId, "TdInternalUsers")
                    : TestAuthHandler.Principal(adminName, adminId, "TdAdministrators", "TdHelpDeskUsers", "TdInternalUsers");
                var runner = new JourneyRunner(new RunnerOptions
                {
                    Target = new Uri("http://localhost/"), RunId = "new-app-test", Secrets = secrets, Routes = routes,
                    HandlerFactory = () => new SignedInHandler(app.Server.CreateHandler(), principal, journey.Name),
                });
                candidate.AddRange(await runner.RunJourneyAsync(journey, Ct));
            }

            var golden = Journeys.SelectMany(j => GoldenOf(j)).ToList();
            var report = new ExchangeComparer().Compare(golden, candidate);
            var migrated = new HashSet<string>([.. TicketPageRoutes, .. Wave4Routes, .. WaveParityTests.Wave01Routes, .. TicketCenterParityTests.Wave23Routes]);
            var byKey = candidate.ToDictionary(c => c.MatchKey);
            var failures = new List<string>();
            int compared = 0, explained = 0;
            static string RouteOf(RecordedExchange? c) =>
                (c?.Response?.Headers.GetValueOrDefault("X-TicketDesk-Backend")?.FirstOrDefault() ?? "") is var b && b.StartsWith("modern; route=", StringComparison.Ordinal)
                    ? b["modern; route=".Length..].Split(';')[0] : "";
            foreach (var g in golden)
            {
                var c = byKey.GetValueOrDefault(g.MatchKey);
                // only the steps of the ticket routes (all hops of a step whose first request is one of them): sign-in and sign-out
                // run through the test principal here, which outlives a sign-out; the golden replay against the running app covers them
                var hop0 = byKey.GetValueOrDefault(g.MatchKey[..g.MatchKey.LastIndexOf('.')] + ".0");
                if (!migrated.Contains(RouteOf(hop0)) || RouteOf(c) == "") continue;
                compared++;
                var m = report.Mismatches.FirstOrDefault(x => x.Key == g.MatchKey);
                if (m == null) continue;
                var left = m.Diffs.Where(d => !IsExpected(m, d) && !Golden.IsMenuPostDifference(d, c?.Response?.Body)).ToList();
                if (left.Count == 0) { explained++; continue; }
                failures.Add($"{m.Key} {m.Method} {m.Path}:\n{Golden.Describe(left)}");
            }
            var errors = candidate.Where(c => c.Error != null).Select(c => $"{c.MatchKey}: {c.Error}").ToList();
            Assert.True(failures.Count == 0 && errors.Count == 0,
                $"compared {compared}, explained {explained}, failed {failures.Count}\n{string.Join("\n", errors)}\n\n{string.Join("\n\n", failures)}");
            Assert.True(compared >= 100, $"only {compared} exchanges were answered by the new app");
            TestContext.Current.SendDiagnosticMessage($"ticket journeys: {compared} exchanges of migrated routes compared, {explained} explained (S3/L1/L2), 0 unexplained");
        }
        finally
        {
            store.Delete(recursive: true);
        }
    }

    private static IEnumerable<RecordedExchange> GoldenOf(string journey)
    {
        for (var step = 0; ; step++)
        {
            RecordedExchange? hop0;
            try { hop0 = Golden.Get($"{journey}#{step:000}.0"); } catch (KeyNotFoundException) { yield break; }
            yield return hop0;
            for (var hop = 1; ; hop++)
            {
                RecordedExchange? h;
                try { h = Golden.Get($"{journey}#{step:000}.{hop}"); } catch (KeyNotFoundException) { break; }
                yield return h;
            }
        }
    }

    /// <summary>
    /// The user of the journey (test scheme header) and a stand-in for the legacy sign-in, which is not migrated: the sign-in
    /// page is the recorded one (its form is read by the next step) and the post redirects to the home page like the legacy app.
    /// </summary>
    private sealed class SignedInHandler(HttpMessageHandler inner, string principal, string journey) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.Equals("/user/sign-in", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Method == HttpMethod.Get)
                {
                    var page = Golden.Get($"{journey}#000.0").Response!.Body!;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page, Encoding.UTF8, "text/html") });
                }
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("/", UriKind.Relative);
                return Task.FromResult(redirect);
            }
            request.Headers.Remove(TestAuthHandler.PrincipalHeader);
            request.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
            return base.SendAsync(request, ct);
        }
    }
}
