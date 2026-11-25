using System.Net;
using System.Text.RegularExpressions;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>A scratch database of its own for <see cref="StateChangingPostTests"/>: they change list settings and project selections.</summary>
public sealed class StatePostDatabase : ScratchDatabase;

/// <summary>
/// S3, Round 5: the five URLs that still changed state by GET (the culture switch, the ticket list's sort, the reset of the
/// list settings, the project switch, sign-out) are POSTs with the antiforgery token. Each: a GET of the old URL changes
/// nothing (405 with <c>Allow: POST</c>; sign-out: a page with the Log off button), a POST without the token is refused (400)
/// and changes nothing, a POST with the token of the page the user is on works. The server pages offer forms instead of the
/// links.
/// </summary>
public sealed partial class StateChangingPostTests(StatePostDatabase db) : IClassFixture<StatePostDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The four that answer a GET with 405 (sign-out has its own tests).</summary>
    public static TheoryData<string, string> PostOnlyRoutes => new()
    {
        { "/language?name=pt-BR", "home.language" },
        { "/tickets/sortList/opentickets/1?ColumnName=Title", "tickets.sort" },
        { "/tickets/reset-user-lists", "tickets.reset-lists" },
        { "/navigation/change-project/2", "navigation.change-project" },
    };

    [GeneratedRegex("<a [^>]*data-ajax-method=\"POST\"[^>]*href=\"/tickets/sortList/opentickets/1\\?ColumnName=Title\"")]
    private static partial Regex SortHeaderRx();

    private TicketDeskApp App(bool testAuth = true)
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        return new TicketDeskApp(TicketDeskApp.Settings(db.ConnectionString), testAuth: testAuth);
    }

    /// <summary>A browser signed in as <paramref name="userId"/> through the test scheme (the header goes with every request).</summary>
    private static TestBrowser Browser(TicketDeskApp app, string userId)
    {
        var client = app.RawClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId);
        return new TestBrowser(client);
    }

    private async Task<(string? Lists, int? Project)> SettingsAsync(string userId)
    {
        var rows = await db.RowsAsync("SELECT ListSettingsJson, SelectedProjectId FROM dbo.UserSettings WHERE UserId = @u", ("@u", userId));
        return rows.Count == 0 ? (null, null) : ((string?)rows[0][0], rows[0][1] as int?);
    }

    private async Task<int> LastProjectIdAsync() => await db.ScalarAsync<int>("SELECT MAX(ProjectId) FROM dbo.Projects");

    private static async Task<HttpResponseMessage> AjaxPostAsync(TestBrowser browser, TicketDeskApp app, string userId, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent([]) };
        request.Headers.Add(TestAuthHandler.Header, userId);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        request.Headers.Add("Cookie", string.Join("; ", browser.Cookies.Select(c => $"{c.Key}={c.Value}")));
        return await app.RawClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(PostOnlyRoutes))]
    public async Task A_get_or_head_of_the_old_url_answers_405_and_changes_nothing(string path, string route)
    {
        await using var app = App();
        var user = db.UserInRole("TdHelpDeskUsers", 0).Id;
        var before = await SettingsAsync(user);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Head })
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(TestAuthHandler.Header, user);
            using var r = await app.RawClient().SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, r.StatusCode);
            Assert.Equal(["POST"], r.Content.Headers.Allow);
            Assert.Equal($"modern; route={route}", r.Headers.GetValues("X-TicketDesk-Backend").Single());
            Assert.False(r.Headers.Contains("Set-Cookie")); // no culture cookie, no token
            Assert.Empty(await r.Content.ReadAsByteArrayAsync(Ct));
        }
        Assert.Equal(before, await SettingsAsync(user));
    }

    [Theory]
    [MemberData(nameof(PostOnlyRoutes))]
    public async Task A_post_without_the_token_is_refused_and_changes_nothing(string path, string route)
    {
        await using var app = App();
        var user = db.UserInRole("TdHelpDeskUsers", 0).Id;
        var browser = Browser(app, user);
        await browser.GetAsync("/", Ct); // holds the antiforgery cookie like a browser that has seen a page; only the token is missing
        Assert.True(browser.Cookies.ContainsKey(".TicketDesk.Antiforgery"));
        var before = await SettingsAsync(user);
        var (form, _) = await browser.SendAsync(HttpMethod.Post, path, [], Ct);
        Assert.Equal(HttpStatusCode.BadRequest, form.StatusCode);
        Assert.Equal($"modern; route={route}", form.Headers.GetValues("X-TicketDesk-Backend").Single());
        Assert.DoesNotContain(browser.LastSetCookies, c => c.StartsWith("_culture=", StringComparison.Ordinal));
        using var ajax = await AjaxPostAsync(browser, app, user, path, token: null);
        Assert.Equal(HttpStatusCode.BadRequest, ajax.StatusCode);
        using var foreign = await AjaxPostAsync(browser, app, user, path, token: "CfDJ8-not-a-token");
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(before, await SettingsAsync(user));
    }

    [Fact]
    public async Task The_language_menu_posts_the_culture_with_the_pages_token()
    {
        await using var app = App();
        var browser = new TestBrowser(app.RawClient()); // anonymous, like the golden journey
        var (_, home) = await browser.GetAsync("/", Ct);
        var (r, _) = await browser.SendAsync(HttpMethod.Post, "/language?name=pt-BR", [new("__RequestVerificationToken", WaveParityTests.MenuForm(home, "/language?name=pt-BR"))], Ct);
        Assert.Equal(HttpStatusCode.Found, r.StatusCode);
        Assert.Equal("/", r.Headers.Location!.OriginalString);
        Assert.Equal("pt-BR", browser.Cookies["_culture"]);
        var (_, again) = await browser.GetAsync("/", Ct);
        Assert.Contains("window.currentCulture = 'pt-BR';", again);
    }

    [Fact]
    public async Task The_sort_headers_post_and_the_reset_dialog_posts_with_the_pages_token()
    {
        await using var app = App();
        var user = db.UserInRole("TdHelpDeskUsers", 1).Id;
        var browser = Browser(app, user);
        var (_, list) = await browser.GetAsync("/tickets/opentickets", Ct);
        Assert.Matches(SortHeaderRx(), list);
        Assert.Contains("S3 RequestVerificationToken", list); // the script that sends the token as a header on the page's AJAX posts
        var token = IdentityWaveTests.TokenRx().Match(list).Groups[1].Value;
        Assert.NotEmpty(token);

        using (var sorted = await AjaxPostAsync(browser, app, user, "/tickets/sortList/opentickets/1?ColumnName=Title", token))
        {
            Assert.Equal(HttpStatusCode.OK, sorted.StatusCode);
            Assert.Contains("<div id=\"ticketList\"", await sorted.Content.ReadAsStringAsync(Ct));
        }
        var afterSort = await SettingsAsync(user);
        Assert.Contains("\"ColumnName\":\"Title\"", afterSort.Lists);

        Assert.Contains("<form action=\"/tickets/reset-user-lists\" method=\"post\"", list);
        var (reset, _) = await browser.SendAsync(HttpMethod.Post, "/tickets/reset-user-lists", [new("__RequestVerificationToken", token)], Ct);
        Assert.Equal(HttpStatusCode.Found, reset.StatusCode);
        Assert.Equal("/tickets", reset.Headers.Location!.OriginalString);
        var afterReset = await SettingsAsync(user);
        Assert.DoesNotContain("\"ColumnName\":\"Title\"", afterReset.Lists);
    }

    [Fact]
    public async Task The_project_menu_posts_the_selection_with_the_pages_token()
    {
        await using var app = App();
        var user = db.UserInRole("TdHelpDeskUsers", 2).Id;
        var project = await LastProjectIdAsync();
        var browser = Browser(app, user);
        var (_, home) = await browser.GetAsync("/", Ct);
        var (r, _) = await browser.SendAsync(HttpMethod.Post, $"/navigation/change-project/{project}",
            [new("__RequestVerificationToken", WaveParityTests.MenuForm(home, $"/navigation/change-project/{project}"))], Ct);
        Assert.Equal(HttpStatusCode.Found, r.StatusCode);
        Assert.Equal(project, (await SettingsAsync(user)).Project);
        var (back, _) = await browser.SendAsync(HttpMethod.Post, "/navigation/change-project/0",
            [new("__RequestVerificationToken", WaveParityTests.MenuForm(home, "/navigation/change-project/0"))], Ct);
        Assert.Equal(HttpStatusCode.Found, back.StatusCode);
        Assert.Equal(0, (await SettingsAsync(user)).Project);
    }

    [Fact]
    public async Task Server_pages_offer_the_state_changes_as_forms_not_links()
    {
        await using var app = App();
        var browser = Browser(app, db.UserInRole("TdHelpDeskUsers", 0).Id);
        var project = await LastProjectIdAsync();
        foreach (var path in new[] { "/", "/about", "/tickets/opentickets", "/account/manage" })
        {
            var (r, page) = await browser.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            foreach (var action in new[] { "/user/sign-out", "/language?name=en-US", "/language?name=pt-BR", "/language?name=de-DE", "/navigation/change-project/0", $"/navigation/change-project/{project}" })
                WaveParityTests.MenuForm(page, action);
            foreach (var link in new[] { "href=\"/user/sign-out", "href=\"/language", "href=\"/navigation/change-project", "href=\"/tickets/reset-user-lists" })
                Assert.DoesNotContain(link, page);
        }
    }

    /// <summary>
    /// td-replay sends a recorded GET as a post only where a page of the journey offers that URL as one (PostedLinks): on the
    /// legacy app's own pages (the recording) that is never the case, so a replay against the legacy app is unchanged.
    /// </summary>
    [Fact]
    public void Against_the_legacy_pages_no_recorded_get_becomes_a_post()
    {
        var routes = TicketDesk.Replay.RouteTable.FromCsv(Path.Combine(Golden.RepoRoot!, "docs", "migration", "routes.csv"));
        var checkedSteps = 0;
        foreach (var journey in Golden.Exchanges().GroupBy(e => e.Journey))
        {
            var pages = new List<(Uri Uri, string Body)>();
            foreach (var e in journey)
            {
                var uri = new Uri(new Uri(e.Target), e.Request.Path.TrimStart('/'));
                if (e.Hop == 0 && e.Request.Method == "GET" && e.Step is { Kind: TicketDesk.Replay.StepKinds.Request, Form.Count: 0 } step)
                {
                    checkedSteps++;
                    Assert.Null(TicketDesk.Replay.PostedLinks.Find(pages, uri, step.Ajax, routes));
                }
                if (e.Response?.Body is { } body && (e.Response.ContentType ?? "").StartsWith("text/html", StringComparison.Ordinal)) pages.Add((uri, body));
            }
        }
        Assert.True(checkedSteps >= 100, $"{checkedSteps} GET steps checked");
    }

    [Fact]
    public async Task Sign_out_by_get_shows_the_button_and_signs_nobody_out_the_post_needs_the_token()
    {
        await using var app = App(testAuth: false);
        var browser = new TestBrowser(app.RawClient());
        var user = db.UserInRole("TdInternalUsers", 3);
        var (signedIn, _) = await browser.SendAsync(HttpMethod.Post, "/test/sign-in?u=" + Uri.EscapeDataString(user.UserName), [], Ct);
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        Assert.True(browser.Cookies.ContainsKey(SignInTests.IdentityCookie));

        var (get, page) = await browser.GetAsync("/user/sign-out", Ct);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("modern; route=user.sign-out", get.Headers.GetValues("X-TicketDesk-Backend").Single());
        Assert.Contains("<form action=\"/user/sign-out\" method=\"post\" id=\"signOutForm\">", page);
        Assert.DoesNotContain(browser.LastSetCookies, c => c.StartsWith(SignInTests.IdentityCookie, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/account/manage", Ct)).Response.StatusCode);

        var (refused, _) = await browser.SendAsync(HttpMethod.Post, "/user/sign-out", [], Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/account/manage", Ct)).Response.StatusCode);

        var token = IdentityWaveTests.TokenRx().Match(page).Groups[1].Value;
        var (signedOut, _) = await browser.SendAsync(HttpMethod.Post, "/user/sign-out", [new("__RequestVerificationToken", token)], Ct);
        Assert.Equal(HttpStatusCode.Found, signedOut.StatusCode);
        Assert.Equal("/", signedOut.Headers.Location!.OriginalString);
        Assert.False(browser.Cookies.ContainsKey(SignInTests.IdentityCookie));
        var (after, _) = await browser.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.Found, after.StatusCode);
        Assert.StartsWith("/user/sign-in", after.Headers.Location!.PathAndQuery);
    }
}
