using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Testing;
using TicketDesk.Web.Migration;
using TicketDesk.Web.Spa;

namespace TicketDesk.Web.Tests;

/// <summary>
/// Its own scratch database (the API tests here create tickets), a fake front-end build and a registry file per app: the
/// Angular pages behind the registry, their switch-back to the server pages, and the API the pages use.
/// </summary>
public sealed class AngularFixture : ScratchDatabase
{
    public const string MainJs = "main-ABCDEFGH.js";
    public string FrontEnd { get; } = Directory.CreateTempSubdirectory("td-frontend-").FullName;
    public string EmptyFrontEnd { get; } = Directory.CreateTempSubdirectory("td-frontend-none-").FullName;

    public static readonly (string, string)[] AngularRegistry =
    [
        ("tickets.index", "Angular"), ("tickets.page", "New"), ("tickets.sort", "New"), ("tickets.filter", "New"),
        ("ticket.view", "Angular"), ("ticket.subscription", "New"), ("ticket.new.get", "Angular"), ("ticket.new.post", "New"),
    ];

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        File.WriteAllText(Path.Combine(FrontEnd, "index.html"),
            "<!doctype html><html lang=\"en\"><head><base href=\"/app/\"><title>TicketDesk</title></head>" +
            $"<body><td-root></td-root><script src=\"{MainJs}\" type=\"module\"></script></body></html>");
        File.WriteAllText(Path.Combine(FrontEnd, MainJs), string.Concat(Enumerable.Repeat("console.log('TicketDesk front end');\n", 200)));
    }

    public override async ValueTask DisposeAsync()
    {
        Directory.Delete(FrontEnd, true);
        Directory.Delete(EmptyFrontEnd, true);
        await base.DisposeAsync();
    }
}

public sealed class AngularPagesTests(AngularFixture f) : IClassFixture<AngularFixture>, IDisposable
{
    private readonly string _registry = Path.Combine(Path.GetTempPath(), $"td-registry-{Guid.NewGuid():N}.json");
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private string Internal => f.UserInRole(TicketDeskRoles.InternalUsers).Id;
    private string Staff => f.UserInRole(TicketDeskRoles.HelpDeskUsers).Id;

    public void Dispose() => File.Delete(_registry);

    private void WriteRegistry(params (string Id, string Mode)[] routes)
    {
        var json = JsonSerializer.Serialize(new { Migration = new { Routes = routes.ToDictionary(r => r.Id, r => r.Mode) } });
        File.WriteAllText(_registry + ".tmp", json);
        File.Move(_registry + ".tmp", _registry, overwrite: true);
    }

    private TicketDeskApp App(bool frontEndBuilt = true)
    {
        Assert.SkipUnless(f.Available, SqlTestEnvironment.SkipReason);
        var s = TicketDeskApp.Settings(f.ConnectionString);
        s["Migration:RegistryFile"] = _registry;
        s[AngularShell.RootKey] = frontEndBuilt ? f.FrontEnd : f.EmptyFrontEnd;
        return new TicketDeskApp(s, kestrel: true, testAuth: true);
    }

    private static HttpRequestMessage Get(string path, string? user, params (string, string)[] headers)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, path);
        if (user != null) r.Headers.Add(TestAuthHandler.Header, user);
        foreach (var (k, v) in headers) r.Headers.Add(k, v);
        return r;
    }

    private static string? Backend(HttpResponseMessage r) =>
        r.Headers.TryGetValues(MigrationRegistration.BackendHeader, out var v) ? v.Single() : null;

    private async Task<HttpResponseMessage> WaitFor(HttpClient client, Func<HttpRequestMessage> request, Func<HttpResponseMessage, bool> done)
    {
        for (var i = 0; i < 100; i++)
        {
            var response = await client.SendAsync(request(), Ct);
            if (done(response)) return response;
            response.Dispose();
            await Task.Delay(100, Ct);
        }
        return await client.SendAsync(request(), Ct);
    }

    private async Task<int> SomeTicketAsync() => await f.ScalarAsync<int>("SELECT TOP 1 TicketId FROM dbo.Tickets ORDER BY TicketId");

    [Fact]
    public async Task Pages_on_Angular_answer_with_the_shell_and_the_server_page_only_when_asked_for()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var id = await SomeTicketAsync();
        foreach (var (path, route) in new[] { ("/tickets", "tickets.index"), ("/tickets/opentickets/2", "tickets.index"), ($"/ticket/{id}", "ticket.view"), ("/ticket/new", "ticket.new.get") })
        {
            using var r = await client.SendAsync(Get(path, Internal), Ct);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal($"modern; route={route}; ui=angular", Backend(r));
            var html = await r.Content.ReadAsStringAsync(Ct);
            var nonce = Regex.Match(html, "<td-root ngCspNonce=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(nonce);
            Assert.Contains($"style-src 'self' 'nonce-{nonce}'", r.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("frame-ancestors 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("text/html; charset=utf-8", r.Content.Headers.ContentType!.ToString());
            Assert.True(r.Headers.CacheControl!.NoStore);
            Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
            var cookies = r.Headers.GetValues("Set-Cookie").ToList();
            Assert.Contains(cookies, c => c.StartsWith(AngularShell.XsrfCookie + "=", StringComparison.Ordinal) && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) && !c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(cookies, c => c.StartsWith(".TicketDesk.Antiforgery=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

            using var server = await client.SendAsync(Get(path, Internal, (MigrationRegistry.UiHeader, MigrationRegistry.ServerUiValue)), Ct);
            Assert.Equal(HttpStatusCode.OK, server.StatusCode);
            Assert.Equal($"modern; route={route}", Backend(server));
            Assert.Contains("id=\"mainNav\"", await server.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task Switching_back_gives_the_server_page_without_a_restart()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        using (var r = await client.SendAsync(Get("/tickets", Internal), Ct)) Assert.Equal("modern; route=tickets.index; ui=angular", Backend(r));

        WriteRegistry([.. AngularFixture.AngularRegistry.Select(x => (x.Item1, "New"))]);
        using (var r = await WaitFor(client, () => Get("/tickets", Internal), x => Backend(x) == "modern; route=tickets.index"))
        {
            Assert.Equal("modern; route=tickets.index", Backend(r));
            Assert.Contains("id=\"mainNav\"", await r.Content.ReadAsStringAsync(Ct));
        }
        using (var s = await client.SendAsync(Get("/api/session", Internal), Ct))
            Assert.Empty((await s.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("angularPages").EnumerateArray());

        WriteRegistry(AngularFixture.AngularRegistry);
        using (var r = await WaitFor(client, () => Get("/tickets", Internal), x => Backend(x)?.EndsWith("ui=angular", StringComparison.Ordinal) == true))
            Assert.Equal("modern; route=tickets.index; ui=angular", Backend(r));
    }

    [Fact]
    public async Task The_shell_keeps_the_server_pages_redirects_and_refusals()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        // L2: a list the user does not have; a missing ticket goes back to the ticket center, as on the server page
        foreach (var (path, user, location) in new[] { ("/tickets/unassigned", Internal, "/access-denied"), ("/tickets/favourites", Staff, "/access-denied"), ("/ticket/999999", Internal, "/tickets") })
        {
            using var r = await client.SendAsync(Get(path, user), Ct);
            Assert.Equal(HttpStatusCode.Found, r.StatusCode);
            Assert.Equal(location, r.Headers.Location!.OriginalString);
        }
        using (var r = await client.SendAsync(Get("/tickets/unassigned", Staff), Ct)) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using (var anonymous = await client.SendAsync(Get("/ticket/new", null), Ct)) Assert.NotEqual(HttpStatusCode.OK, anonymous.StatusCode);
    }

    [Fact]
    public async Task Pending_and_anonymous_users_get_no_shell()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = new TicketDeskApp(CookieSettings(), kestrel: true);
        using var client = app.RawClient();
        using var anonymous = await client.GetAsync("/tickets", Ct);
        Assert.Equal(HttpStatusCode.Found, anonymous.StatusCode);
        Assert.StartsWith("/user/sign-in", anonymous.Headers.Location!.PathAndQuery);
        Assert.Equal("modern; route=tickets.index; ui=angular", Backend(anonymous));
    }

    private Dictionary<string, string?> CookieSettings()
    {
        Assert.SkipUnless(f.Available, SqlTestEnvironment.SkipReason);
        var s = TicketDeskApp.Settings(f.ConnectionString);
        s["Migration:RegistryFile"] = _registry;
        s[AngularShell.RootKey] = f.FrontEnd;
        return s;
    }

    [Fact]
    public async Task Without_a_built_front_end_the_server_pages_answer()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App(frontEndBuilt: false);
        using var client = app.RawClient();
        using var r = await client.SendAsync(Get("/tickets", Internal), Ct);
        Assert.Equal("modern; route=tickets.index", Backend(r));
        var status = app.Services.GetRequiredService<MigrationRegistry>().Status();
        Assert.Equal(0, status.AngularPages);
        Assert.Contains("not built", status.Routes.Single(x => x.Id == "tickets.index").Note);
    }

    [Fact]
    public async Task Assets_are_served_by_the_new_app_with_long_caching_and_compression()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        using (var r = await client.GetAsync($"/app/{AngularFixture.MainJs}", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("text/javascript", r.Content.Headers.ContentType!.MediaType);
            Assert.Equal("public, max-age=31536000, immutable", r.Headers.CacheControl!.ToString());
        }
        using (var r = await client.SendAsync(Get($"/app/{AngularFixture.MainJs}", null, ("Accept-Encoding", "br")), Ct))
        {
            Assert.Equal("br", r.Content.Headers.ContentEncoding.Single());
            Assert.True(r.Content.Headers.ContentLength is null or < 2000);
        }
        // a missing asset is a 404
        using (var r = await client.GetAsync("/app/chunk-ZZZZZZZZ.js", Ct)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using (var r = await client.SendAsync(Get("/tickets", Internal, ("Accept-Encoding", "br")), Ct)) Assert.Empty(r.Content.Headers.ContentEncoding); // pages are not compressed
    }

    [Fact]
    public async Task Session_lists_the_users_lists_and_the_pages_on_Angular()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        using var mine = await client.SendAsync(Get("/api/session", Internal), Ct);
        var s = await mine.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(Internal, s.GetProperty("userId").GetString());
        Assert.Equal(["mytickets", "opentickets", "historytickets"], s.GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.Equal("mytickets", s.GetProperty("defaultList").GetString());
        Assert.False(s.GetProperty("isHelpDesk").GetBoolean());
        Assert.Equal(["tickets.index", "ticket.view", "ticket.new.get"], s.GetProperty("angularPages").EnumerateArray().Select(p => p.GetString()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", s.GetProperty("today").GetString()!);

        using var staff = await client.SendAsync(Get("/api/session", Staff), Ct);
        var st = await staff.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(["unassigned", "assignedToMe", "mytickets", "opentickets", "historytickets"], st.GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.Equal("unassigned", st.GetProperty("defaultList").GetString());

        var status = await (await client.GetAsync("/api/migration/routes", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(3, status.GetProperty("angularPages").GetInt32());
        Assert.Equal("Angular", status.GetProperty("routes").EnumerateArray().Single(r => r.GetProperty("id").GetString() == "ticket.view").GetProperty("mode").GetString());
    }

    // ------------------------------------------------------------------ the API the pages use

    /// <summary>The antiforgery cookies and header value a browser gets from the shell (what the Angular HttpClient sends back).</summary>
    private static async Task<(string Cookie, string Token)> TokensAsync(HttpClient client, string user)
    {
        using var shell = await client.SendAsync(Get("/ticket/new", user), TestContext.Current.CancellationToken);
        var set = shell.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var token = Uri.UnescapeDataString(set.Single(c => c.StartsWith(AngularShell.XsrfCookie + "=", StringComparison.Ordinal))[(AngularShell.XsrfCookie.Length + 1)..]);
        return (string.Join("; ", set), token);
    }

    private static HttpRequestMessage Post(string user, object body, (string Cookie, string Token)? tokens)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, "/api/tickets") { Content = JsonContent.Create(body) };
        r.Headers.Add(TestAuthHandler.Header, user);
        if (tokens is { } t)
        {
            r.Headers.Add("Cookie", t.Cookie);
            r.Headers.Add("RequestVerificationToken", t.Token);
        }
        return r;
    }

    private static object NewTicket(string title, object? extra = null)
    {
        var d = new Dictionary<string, object?> { ["title"] = title, ["details"] = "The **printer** on floor 3 jams.", ["ticketType"] = "Problem", ["category"] = "Hardware" };
        if (extra != null) foreach (var p in extra.GetType().GetProperties()) d[p.Name] = p.GetValue(extra);
        return d;
    }

    private async Task<int> ProjectAsync() => await f.ScalarAsync<int>("SELECT MIN(ProjectId) FROM dbo.Projects");

    [Fact]
    public async Task Creating_a_ticket_needs_the_antiforgery_header()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var before = await f.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets");
        var tokens = await TokensAsync(client, Internal);
        using (var none = await client.SendAsync(Post(Internal, NewTicket("no token", new { projectId = await ProjectAsync() }), null), Ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
            Assert.Contains("antiforgery", (await none.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString());
        }
        using (var wrong = await client.SendAsync(Post(Internal, NewTicket("wrong token", new { projectId = await ProjectAsync() }), (tokens.Cookie, tokens.Token[..^4] + "AAAA")), Ct))
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        // a token issued to another user does not work either
        var other = await TokensAsync(client, Staff);
        using (var stolen = await client.SendAsync(Post(Internal, NewTicket("someone else's token", new { projectId = await ProjectAsync() }), other), Ct))
            Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);
        Assert.Equal(before, await f.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets"));
    }

    [Fact]
    public async Task A_ticket_created_through_the_api_is_in_the_database_and_in_my_list()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var tokens = await TokensAsync(client, Internal);
        var title = $"API ticket {Guid.NewGuid():N}";
        using var created = await client.SendAsync(Post(Internal, NewTicket(title, new { projectId = await ProjectAsync(), tagList = "printer, floor-3", dueDate = "2026-12-24", affectsCustomer = true }), tokens), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ticketId").GetInt32();
        Assert.Equal($"/api/tickets/{id}", created.Headers.Location!.OriginalString);

        var row = (await f.RowsAsync("SELECT Owner, CreatedBy, TicketStatus, IsHtml, TagList, AffectsCustomer, AssignedTo FROM dbo.Tickets WHERE TicketId = @id", ("@id", id))).Single();
        Assert.Equal(new object?[] { Internal, Internal, 0, false, "printer,floor-3", true, null }, row);
        Assert.Equal(1, await f.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TicketEvents WHERE TicketId = @id AND EventDescription LIKE 'created the ticket%'", ("@id", id)));
        Assert.Equal(2, await f.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TicketTags WHERE TicketId = @id", ("@id", id)));

        using var get = await client.SendAsync(Get($"/api/tickets/{id}", Internal), Ct);
        var t = await get.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(title, t.GetProperty("title").GetString());
        Assert.Contains("<strong>printer</strong>", t.GetProperty("detailsHtml").GetString());
        Assert.True(t.GetProperty("isSubscribed").GetBoolean());
        Assert.Equal("2026-12-24", t.GetProperty("dueDate").GetString()![..10]);
        Assert.Equal("Create", t.GetProperty("events")[0].GetProperty("forActivity").GetString());

        using var list = await client.SendAsync(Get("/api/tickets?list=mytickets&sort=-createdDate", Internal), Ct);
        Assert.Equal(id, (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items")[0].GetProperty("ticketId").GetInt32());
    }

    [Fact]
    public async Task Validation_errors_name_the_fields()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var tokens = await TokensAsync(client, Internal);
        var body = new Dictionary<string, object?> { ["projectId"] = 999999, ["title"] = " ", ["details"] = "", ["ticketType"] = "Complaint", ["tagList"] = "ok, ," };
        using var r = await client.SendAsync(Post(Internal, body, tokens), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errors = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.Equal(["category", "details", "projectId", "tagList", "ticketType", "title"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        // a quoted number is not a number (strict JSON numbers)
        using var quoted = await client.SendAsync(Post(Internal, new Dictionary<string, object?> { ["projectId"] = "1", ["title"] = "x" }, tokens), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, quoted.StatusCode);
    }

    [Fact]
    public async Task Internal_users_may_not_set_owner_assignee_or_priority_but_help_desk_may()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var project = await ProjectAsync();

        using (var o = await client.SendAsync(Get("/api/tickets/options", Internal), Ct))
        {
            var p = (await o.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("permissions");
            Assert.Equal((false, false, false, true), (p.GetProperty("canSetOwner").GetBoolean(), p.GetProperty("canSetAssignedTo").GetBoolean(), p.GetProperty("canSetPriority").GetBoolean(), p.GetProperty("canSetTags").GetBoolean()));
        }
        var tokens = await TokensAsync(client, Internal);
        using (var r = await client.SendAsync(Post(Internal, NewTicket("for someone else", new { projectId = project, owner = Staff, assignedTo = Staff, priority = "High" }), tokens), Ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            var errors = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
            Assert.Equal(["assignedTo", "owner", "priority"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        }

        using (var o = await client.SendAsync(Get("/api/tickets/options", Staff), Ct))
        {
            var options = await o.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.True(options.GetProperty("permissions").GetProperty("canSetOwner").GetBoolean());
            Assert.Contains(options.GetProperty("internalUsers").EnumerateArray(), u => u.GetProperty("id").GetString() == Internal);
            Assert.Contains(options.GetProperty("helpDeskUsers").EnumerateArray(), u => u.GetProperty("id").GetString() == Staff);
            Assert.DoesNotContain(options.GetProperty("helpDeskUsers").EnumerateArray(), u => u.GetProperty("id").GetString() == Internal);
            Assert.NotEmpty(options.GetProperty("projects").EnumerateArray());
        }
        var staffTokens = await TokensAsync(client, Staff);
        using var ok = await client.SendAsync(Post(Staff, NewTicket("on behalf", new { projectId = project, owner = Internal, assignedTo = Staff, priority = "High" }), staffTokens), Ct);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var id = (await ok.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ticketId").GetInt32();
        Assert.Equal(new object?[] { Internal, Staff, "High" }, (await f.RowsAsync("SELECT Owner, AssignedTo, Priority FROM dbo.Tickets WHERE TicketId = @id", ("@id", id))).Single());
        Assert.Equal((int)TicketActivity.CreateOnBehalfOf, await f.ScalarAsync<int>("SELECT ForActivity FROM dbo.TicketEvents WHERE TicketId = @id", ("@id", id)));
        Assert.NotNull(await f.ScalarAsync<DateTimeOffset?>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));
    }

    private static HttpRequestMessage Activity(string user, int ticketId, object body, (string Cookie, string Token)? tokens)
    {
        var r = Post(user, body, tokens);
        r.RequestUri = new Uri($"/api/tickets/{ticketId}/activities", UriKind.Relative);
        return r;
    }

    private async Task<JsonElement> TicketAsync(HttpClient client, string user, int id)
    {
        using var r = await client.SendAsync(Get($"/api/tickets/{id}", user), Ct);
        return await r.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static string[] Activities(JsonElement t) => t.GetProperty("activities").EnumerateArray().Select(a => a.GetString()!).ToArray();

    [Fact]
    public async Task Workflow_activities_through_the_api_follow_the_legacy_rules()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var staff = await TokensAsync(client, Staff);
        var mine = await TokensAsync(client, Internal);
        using var created = await client.SendAsync(Post(Internal, NewTicket("activities", new { projectId = await ProjectAsync() }), mine), Ct);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ticketId").GetInt32();

        // the owner (internal user) may comment and force-close; help desk may also take over / assign
        Assert.Equal(["AddComment", "ForceClose"], Activities(await TicketAsync(client, Internal, id)));
        Assert.Equal(["AddComment", "TakeOver", "Assign"], Activities(await TicketAsync(client, Staff, id)));

        using (var noToken = await client.SendAsync(Activity(Staff, id, new { activity = "AddComment", comment = "x" }, null), Ct))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using (var bad = await client.SendAsync(Activity(Staff, id, new { activity = "AddComment" }, staff), Ct))
            Assert.Equal(["comment"], (await bad.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        using (var bad = await client.SendAsync(Activity(Staff, id, new { activity = "EditTicketInfo", comment = "x" }, staff), Ct))
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // stays on the server page
        using (var bad = await client.SendAsync(Activity(Staff, id, new { activity = "Assign", assignedTo = Internal }, staff), Ct))
            Assert.True((await bad.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("assignedTo", out _));
        using (var refused = await client.SendAsync(Activity(Internal, id, new { activity = "Resolve", comment = "done" }, mine), Ct))
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode); // not the internal user's activity

        using (var ok = await client.SendAsync(Activity(Staff, id, new { activity = "AddComment", comment = "Looking at **it**." }, staff), Ct))
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        var t = await TicketAsync(client, Internal, id);
        Assert.Equal("AddComment", t.GetProperty("events")[0].GetProperty("forActivity").GetString());
        Assert.Contains("<strong>it</strong>", t.GetProperty("events")[0].GetProperty("commentHtml").GetString());

        // assigning to oneself is a take-over (legacy ChangeAssignment), with the priority named in the event
        using (var ok = await client.SendAsync(Activity(Staff, id, new { activity = "Assign", assignedTo = Staff, priority = "High" }, staff), Ct))
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        t = await TicketAsync(client, Staff, id);
        Assert.Equal(Staff, t.GetProperty("assignedTo").GetProperty("id").GetString());
        Assert.Equal("TakeOver", t.GetProperty("events")[0].GetProperty("forActivity").GetString());
        Assert.Equal("High", t.GetProperty("priority").GetString());
        Assert.Contains("Resolve", Activities(t));

        using (var ok = await client.SendAsync(Activity(Staff, id, new { activity = "Resolve", comment = "Replaced the roller." }, staff), Ct))
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Contains("Close", Activities(await TicketAsync(client, Internal, id)));
        using (var ok = await client.SendAsync(Activity(Internal, id, new { activity = "Close", comment = "Thanks" }, mine), Ct))
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        t = await TicketAsync(client, Internal, id);
        Assert.Equal("Closed", t.GetProperty("status").GetString());
        Assert.Equal(["ReOpen"], Activities(t));
        Assert.Equal(5, await f.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TicketEvents WHERE TicketId = @id", ("@id", id)));
        using (var missing = await client.SendAsync(Activity(Staff, 999999, new { activity = "AddComment", comment = "x" }, staff), Ct))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Ticket_text_is_rendered_and_sanitized_for_the_page()
    {
        WriteRegistry(AngularFixture.AngularRegistry);
        await using var app = App();
        using var client = app.RawClient();
        var tokens = await TokensAsync(client, Staff);
        using var created = await client.SendAsync(Post(Staff, new Dictionary<string, object?>
        {
            ["projectId"] = await ProjectAsync(), ["title"] = "html", ["isHtml"] = true, ["ticketType"] = "Problem", ["category"] = "Software",
            ["details"] = "<p onclick=\"alert(1)\">hi<script>alert(2)</script></p>",
        }, tokens), Ct);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ticketId").GetInt32();
        using var get = await client.SendAsync(Get($"/api/tickets/{id}", Staff), Ct);
        var t = await get.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("<p>hi</p>", t.GetProperty("detailsHtml").GetString());
        Assert.All(t.GetProperty("events").EnumerateArray(), e => Assert.True(e.TryGetProperty("commentHtml", out _)));
    }
}
