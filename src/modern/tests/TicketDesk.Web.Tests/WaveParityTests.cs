using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>
/// Waves 0, 1 and the culture switch (wave 3): the new app's answers compared with the golden legacy recording, exchange by
/// exchange, with td-replay's comparer. Every request goes to the new app directly (the legacy URL is a closed port), so a
/// match proves the migrated endpoint itself reproduces the legacy page.
/// </summary>
public sealed class WaveParityTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static readonly string[] Wave01Routes = ["bundles.styles", "bundles.scripts", "home.root", "home.index", "home.about", "home.access-denied", "home.language"];

    internal static readonly string[] Wave2PageRoutes = ["account.manage", "admin.users", "admin.users-page", "admin.demo", "admin.projects"];

    internal static Dictionary<string, string?> Migrated(string? connectionString = null, params string[] routes)
    {
        var s = TicketDeskApp.Settings(connectionString);
        foreach (var id in routes) s[$"Migration:Routes:{id}"] = "New";
        return s;
    }

    [Theory]
    [InlineData("anonymous#000.0", "/", null)]
    [InlineData("anonymous#001.0", "/index", null)]
    [InlineData("anonymous#002.0", "/about", null)]
    [InlineData("anonymous#008.0", "/access-denied", null)]
    [InlineData("anonymous#017.0", "/", "pt-BR")]
    [InlineData("anonymous#014.0", "/content/styles", null)]
    [InlineData("anonymous#015.0", "/bundles/common", null)]
    // anonymous requests to signed-in routes: the challenge (302 to the sign-in page) with the legacy headers
    [InlineData("anonymous#003.0", "/tickets", null)]
    [InlineData("anonymous#005.0", "/admin/users", null)]
    [InlineData("sign-in#009.0", "/tickets", null)]
    public async Task Anonymous_pages_match_the_golden_recording(string key, string path, string? culture)
    {
        var golden = Golden.Get(key);
        Assert.Equal(path, golden.Request.Path);
        await using var app = new TicketDeskApp(Migrated(routes: [.. Wave01Routes, "tickets.index", "tickets.page", "tickets.sort", "tickets.filter", "admin.users"]));
        using var client = app.RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (culture != null) request.Headers.Add("Cookie", $"_culture={culture}");
        using var response = await client.SendAsync(request, Ct);
        Assert.StartsWith("modern;", response.Headers.GetValues("X-TicketDesk-Backend").Single());
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct));
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    /// <summary>
    /// Signed-in pages need the project menu (database) and the user's name and roles. The principal comes from the test
    /// scheme here; in the gateway it comes from the legacy app (remote authentication). The admin's name is a secret, so the
    /// recording holds the placeholder, which the test uses as the name.
    /// </summary>
    [Theory]
    [InlineData("sign-in#002.1", "/", "{secret:TD_ADMIN_NAME}", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("internal-user#001.1", "/", "Ali Chen", "TdInternalUsers")]
    [InlineData("internal-user#005.1", "/access-denied", "Ali Chen", "TdInternalUsers")]
    [InlineData("register-disabled#003.0", "/", "Replay Register", "TdPendingUsers")]
    [InlineData("register-disabled#004.1", "/access-denied", "Replay Register", "TdPendingUsers")]
    [InlineData("sign-in#003.0", "/account/manage", "{secret:TD_ADMIN_NAME}", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("admin-settings#003.0", "/admin/users", "{secret:TD_ADMIN_NAME}", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("admin-settings#004.0", "/admin/users/page/1", "{secret:TD_ADMIN_NAME}", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    [InlineData("admin-settings#013.0", "/admin/data-management/demo", "{secret:TD_ADMIN_NAME}", "TdAdministrators,TdHelpDeskUsers,TdInternalUsers")]
    public async Task Signed_in_pages_match_the_golden_recording(string key, string path, string displayName, string roles)
    {
        var golden = Golden.Get(key);
        Assert.Equal(path, golden.Request.Path);
        var cs = await SqlTestEnvironment.SeededDatabaseAsync();
        if (cs == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        // the recording's user list and role names are the lab's baseline data (synthetic seeds name the roles differently)
        if (!BaselineDatabase.IsBaselineName(SqlTestEnvironment.SeededDatabaseName))
            Assert.Skip("needs TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data) or a copy of it (TicketDesk_*)");
        await using var app = new TicketDeskApp(Migrated(cs, [.. Wave01Routes, .. Wave2PageRoutes]), testAuth: true);
        string userId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
            userId = await db.Users.Where(u => u.DisplayName == displayName).Select(u => u.Id).FirstOrDefaultAsync(Ct) ?? "no-such-user";
        }
        using var client = app.RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal(displayName, userId, roles.Split(',')));
        if (golden.Request.Headers.ContainsKey("X-Requested-With")) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct));
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    /// <summary>
    /// The project list counts tickets, and the recording was made after the journeys had created some, so the counts are
    /// not compared; the rest of the page is.
    /// </summary>
    [Fact]
    public async Task Admin_project_list_matches_the_golden_recording_except_the_ticket_counts()
    {
        var golden = Golden.Get("admin-settings#007.0");
        var cs = await SqlTestEnvironment.SeededDatabaseAsync();
        if (cs == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = new TicketDeskApp(Migrated(cs, [.. Wave01Routes, .. Wave2PageRoutes]), testAuth: true);
        using var client = app.RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/projects");
        request.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal("{secret:TD_ADMIN_NAME}", "no-such-user", "TdAdministrators", "TdHelpDeskUsers", "TdInternalUsers"));
        using var response = await client.SendAsync(request, Ct);
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct));
        diffs.RemoveAll(d => d.Kind == "text" && System.Text.RegularExpressions.Regex.IsMatch(d.Path, @"/table\[1\]/tbody\[1\]/tr\[\d+\]/td\[3\]/text\(\)\[1\]$"));
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    /// <summary>
    /// The culture switch of the anonymous journey: the recording's GETs were clicks on the language menu's links; the menu
    /// items post now (S3, Round 5), so the click is the form's post with the token of the page it is on.
    /// </summary>
    [Theory]
    [InlineData("anonymous#016.0", "/language?name=pt-BR", null)]
    [InlineData("anonymous#018.0", "/language?name=en-US", "pt-BR")]
    public async Task The_culture_switch_posted_from_the_menu_matches_the_golden_recording(string key, string path, string? culture)
    {
        var golden = Golden.Get(key);
        Assert.Equal(path, golden.Request.Path);
        await using var app = new TicketDeskApp(Migrated(routes: Wave01Routes));
        var browser = new TestBrowser(app.RawClient());
        if (culture != null) browser.Cookies["_culture"] = culture;
        var (_, home) = await browser.GetAsync("/", Ct);
        var token = MenuForm(home, path);
        var (response, _) = await browser.SendAsync(HttpMethod.Post, path, [new("__RequestVerificationToken", token)], Ct);
        Assert.StartsWith("modern;", response.Headers.GetValues("X-TicketDesk-Backend").Single());
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct));
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    /// <summary>The token of the layout's one-button form that posts to <paramref name="action"/> (fails when there is none).</summary>
    internal static string MenuForm(string html, string action)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html,
            $"<form action=\"{System.Text.RegularExpressions.Regex.Escape(System.Net.WebUtility.HtmlEncode(action))}\" class=\"td-menu-post\" method=\"post\"><input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\" /><button type=\"submit\">");
        Assert.True(m.Success, $"no menu form posting to {action}");
        return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    [Fact]
    public async Task Language_switch_returns_to_the_referring_page()
    {
        await using var app = new TicketDeskApp(Migrated(routes: Wave01Routes));
        var browser = new TestBrowser(app.RawClient());
        var (_, about) = await browser.GetAsync("/about", Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/language?name=de-DE")
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", MenuForm(about, "/language?name=de-DE"))]),
        };
        request.Headers.Referrer = new Uri("https://gateway.example.test/about?x=1&y=2");
        request.Headers.Add("Cookie", string.Join("; ", browser.Cookies.Select(c => $"{c.Key}={c.Value}")));
        using var response = await app.RawClient().SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://gateway.example.test/about?x=1&y=2", response.Headers.Location!.OriginalString);
        Assert.Contains("href=\"https://gateway.example.test/about?x=1&amp;y=2\"", await response.Content.ReadAsStringAsync(Ct));
        Assert.StartsWith("_culture=de-DE;", response.Headers.GetValues("Set-Cookie").Single());
    }

    [Theory]
    [InlineData("de-DE", "Deutsch", "/bundles/common_locale_de-DE\"")]   // no such bundle in the legacy app: plain URL, no hash
    [InlineData("pt-PT", "pt-BR", "/bundles/common_locale_pt-BR?v=")]   // same language → the implemented culture
    [InlineData("xx-YY", "en-US", "/bundles/common_locale_en-US?v=")]   // invalid → the default
    public async Task Culture_cookie_follows_the_legacy_rules(string cookie, string expected, string localeBundle)
    {
        await using var app = new TicketDeskApp(Migrated(routes: Wave01Routes));
        using var client = app.RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.Add("Cookie", $"_culture={cookie}");
        var html = await (await client.SendAsync(request, Ct)).Content.ReadAsStringAsync(Ct);
        Assert.Contains(expected, html);
        Assert.Contains(localeBundle, html);
    }

    [Fact]
    public async Task Home_redirects_permanently_to_the_ticket_center_when_disabled()
    {
        var settings = Migrated(routes: Wave01Routes);
        settings["TicketDesk:HomeEnabled"] = "false";
        await using var app = new TicketDeskApp(settings);
        using var client = app.RawClient();
        using var response = await client.GetAsync("/", Ct);
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/tickets", response.Headers.Location!.OriginalString);
    }
}
