using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TicketDesk.Testing;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Tests;

/// <summary>
/// A scratch database of its own for <see cref="RetiredRoutesTests"/>: "runs nothing" is checked on whole tables (the count of
/// IdentityUsers), and on the assembly's shared database another class's fixture adds a user while classes run in parallel
/// (ApiFixture's pending user: X1 failed 5 of 10 full runs with "Expected 24, Actual 25", Round 5 follow-ups).
/// </summary>
public sealed class RetiredRoutesDatabase : ScratchDatabase;

/// <summary>
/// The 9 routes of wave <c>retire</c> (first-run setup x6 incl. the child actions, demo data create/remove, the e-mail test page)
/// were dropped with the legacy app: the new app answers their URLs with 410 Gone and a pointer (X1, X2) or 404 (S2), runs
/// nothing, and reports each one as retired with its replacement.
/// </summary>
public sealed class RetiredRoutesTests(RetiredRoutesDatabase db) : IClassFixture<RetiredRoutesDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private TicketDeskApp App()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        return new TicketDeskApp(TicketDeskApp.Settings(db.ConnectionString), testAuth: true);
    }

    private static string Backend(HttpResponseMessage r) => r.Headers.GetValues(MigrationRegistration.BackendHeader).Single();

    [Theory]
    [InlineData("GET", "/first-run-setup", "setup.index")]
    [InlineData("GET", "/first-run-setup/index", "setup.index")]
    [InlineData("POST", "/first-run-setup", "setup.index")]
    [InlineData("GET", "/first-run-setup/legacy-migration", "setup.legacy-migration")]
    [InlineData("GET", "/first-run-setup/upgrade-database", "setup.upgrade-database")]
    [InlineData("GET", "/first-run-setup/check-upgrade-progress", "setup.check-upgrade-progress")]
    [InlineData("POST", "/first-run-setup/create-database", "setup.create-database")]
    public async Task First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1(string method, string path, string route)
    {
        await using var app = App();
        using var client = app.RawClient();
        var users = await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.IdentityUsers");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        // the legacy wizard's create-database form: without a token (none needed, nothing happens) and with a user to (re)create
        if (method == "POST")
            request.Content = new FormUrlEncodedContent([new("email", "evil@example.test"), new("password", "Pa55word!"), new("displayName", "Evil")]);
        using var r = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
        Assert.Equal($"modern; route={route}", Backend(r));
        var html = await r.Content.ReadAsStringAsync(Ct);
        Assert.Contains("<h2>First-run setup</h2>", html);
        Assert.Contains("td-db migrate", html);
        Assert.Contains("td-db create-admin", html); // the wizard's administrator step (Round 5)
        Assert.Contains("id=\"mainNav\"", html); // the legacy layout, like every other page
        Assert.Equal(users, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.IdentityUsers"));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.IdentityUsers WHERE Email = N'evil@example.test'"));
    }

    [Fact]
    public async Task Demo_data_actions_answer_410_to_administrators_and_keep_the_legacy_refusals_X2()
    {
        await using var app = App();
        using var client = app.RawClient();
        var admin = db.UserInRole("TdAdministrators").Id;
        var tickets = await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets");
        foreach (var (path, route) in new[] { ("/admin/data-management/create-demo-data", "admin.demo.create"), ("/admin/data-management/remove-demo-data", "admin.demo.remove") })
        {
            using var asAdmin = new HttpRequestMessage(HttpMethod.Get, path);
            asAdmin.Headers.Add(TestAuthHandler.Header, admin);
            using (var r = await client.SendAsync(asAdmin, Ct))
            {
                Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
                Assert.Equal($"modern; route={route}", Backend(r));
                var html = await r.Content.ReadAsStringAsync(Ct);
                Assert.Contains("td-seed", html);
                Assert.Contains("href=\"/admin/data-management/demo\"", html);
            }
            using var asStaff = new HttpRequestMessage(HttpMethod.Get, path);
            asStaff.Headers.Add(TestAuthHandler.Header, db.UserInRole("TdHelpDeskUsers").Id);
            using (var r = await client.SendAsync(asStaff, Ct))
            {
                Assert.Equal(HttpStatusCode.Found, r.StatusCode);
                Assert.Equal("/access-denied", r.Headers.Location!.OriginalString);
            }
        }
        Assert.Equal(tickets, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets")); // nothing created, nothing removed
    }

    [Fact]
    public async Task The_email_test_page_is_gone_S2()
    {
        await using var app = App();
        using var client = app.RawClient();
        var id = await db.ScalarAsync<int>("SELECT TOP 1 TicketId FROM dbo.Tickets ORDER BY TicketId");
        using var r = await client.GetAsync($"/emailtest/index/{id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("modern; route=emailtest.index", Backend(r));
        Assert.Empty(await r.Content.ReadAsByteArrayAsync(Ct));
        using var other = await client.GetAsync("/emailtest", Ct);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    /// <summary>The five golden exchanges of retired routes (anonymous journey) get the documented answers instead of the legacy ones.</summary>
    [Theory]
    [InlineData("anonymous#009.0", 302, 410)]
    [InlineData("anonymous#010.0", 302, 410)]
    [InlineData("anonymous#011.0", 302, 410)]
    [InlineData("anonymous#012.0", 302, 410)]
    [InlineData("anonymous#013.0", 200, 404)]
    public async Task Golden_exchanges_of_retired_routes_get_the_expected_difference(string key, int legacy, int now)
    {
        var golden = Golden.Get(key);
        Assert.Equal(legacy, golden.Response!.Status);
        await using var app = App();
        using var client = app.RawClient();
        using var r = await client.SendAsync(new HttpRequestMessage(new HttpMethod(golden.Request.Method), golden.Request.Path), Ct);
        Assert.Equal(now, (int)r.StatusCode);
    }

    [Fact]
    public async Task Status_reports_the_nine_retired_rows_each_with_its_replacement()
    {
        await using var app = App();
        using var client = app.RawClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/migration/routes", Ct);
        Assert.Equal(9, status.GetProperty("retired").GetInt32());
        var retired = status.GetProperty("routes").EnumerateArray().Where(r => r.GetProperty("retired").GetBoolean()).ToList();
        Assert.Equal(["admin.demo.create", "admin.demo.remove", "emailtest.index", "setup.check-upgrade-progress", "setup.child-actions",
                      "setup.create-database", "setup.index", "setup.legacy-migration", "setup.upgrade-database"],
            retired.Select(r => r.GetProperty("id").GetString()).Order(StringComparer.Ordinal));
        Assert.All(retired, r => Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("note").GetString())));
        Assert.Contains("td-db create-admin", retired.Single(r => r.GetProperty("id").GetString() == "setup.create-database").GetProperty("note").GetString());
        // every routable retired row has an answer of its own (the child actions have no URL)
        Assert.All(retired.Where(r => r.GetProperty("routable").GetBoolean()), r => Assert.True(r.GetProperty("implemented").GetBoolean(), r.GetProperty("id").GetString()));
    }
}
