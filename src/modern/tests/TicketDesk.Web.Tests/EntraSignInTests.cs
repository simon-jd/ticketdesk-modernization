using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketDesk.TestIdp;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>The local test identity provider on a real socket (the app's OIDC handler fetches its metadata and redeems codes).</summary>
public sealed class TestIdpHost : IAsyncLifetime
{
    public const string Tenant = "7d1a0e7c-3c1e-4f6a-9a51-2f0b6c1d0001";
    public const string ClientId = "ticketdesk-lab-web-test";
    public const string ClientSecret = "test-client-secret";

    public static readonly TestAccount NewHelpDesk = new("a1111111-0000-0000-0000-000000000001", "entra.helpdesk@lab.test", "Entra Helpdesk", "entra.helpdesk@lab.test", ["TdHelpDeskUsers"]);
    public static readonly TestAccount NoRoles = new("a1111111-0000-0000-0000-000000000002", "entra.noroles@lab.test", "Entra Noroles", "entra.noroles@lab.test");
    /// <summary>Same e-mail as a seeded legacy account: the first Entra sign-in links to that row.</summary>
    public static readonly TestAccount Existing = new("a1111111-0000-0000-0000-000000000003", TicketDesk.Replay.TicketDeskJourneys.InternalUser, "Ali Chen (Entra)", TicketDesk.Replay.TicketDeskJourneys.InternalUser);

    private WebApplication? _app;
    public TestIdentityProvider Idp { get; } = new(Tenant, ClientId, ClientSecret, [NewHelpDesk, NoRoles, Existing]);
    public string Authority { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _app = builder.Build();
        Idp.Map(_app);
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Authority = address.TrimEnd('/') + "/" + Tenant + "/v2.0";
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null) await _app.DisposeAsync();
    }
}

/// <summary>
/// Entra ID sign-in (second sign-in option) against the local test identity provider: the code flow with PKCE end to end,
/// first sign-in provisions or links an IdentityUsers row (so the legacy pages work for the account), app roles map to the
/// legacy roles, the issued cookie is the app's Identity cookie.
/// </summary>
public sealed partial class EntraSignInTests(BaselineDatabase db, TestIdpHost idp) : IClassFixture<BaselineDatabase>, IClassFixture<TestIdpHost>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [GeneratedRegex("<form method=\"post\" action=\"([^\"]+)\"><input type=\"hidden\" name=\"code\" value=\"([^\"]+)\"><input type=\"hidden\" name=\"state\" value=\"([^\"]+)\">")]
    private static partial Regex FormPostRx();

    [GeneratedRegex("<form action=\"/user/sign-in/entra[^\"]*\"[^>]*id=\"entraSignIn\">\\s*<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex EntraFormRx();

    private TicketDeskApp App(bool enabled = true, string? tenant = TestIdpHost.Tenant)
    {
        var s = WaveParityTests.Migrated(db.ConnectionString, [.. WaveParityTests.Wave01Routes, .. WaveParityTests.Wave2PageRoutes, .. IdentityWaveTests.Wave5Routes]);
        s["Authentication:EntraId:Enabled"] = enabled ? "true" : "false";
        s["Authentication:EntraId:Authority"] = idp.Authority;
        s["Authentication:EntraId:ClientId"] = TestIdpHost.ClientId;
        s["Authentication:EntraId:ClientSecret"] = TestIdpHost.ClientSecret;
        s["Authentication:EntraId:TenantId"] = tenant;
        s["Authentication:EntraId:RequireHttpsMetadata"] = "false";
        return new TicketDeskApp(s);
    }

    /// <summary>The browser's part up to the provider: sign-in page → Entra ID button → challenge (the correlation cookie is set).</summary>
    private async Task<(TestBrowser Browser, Uri Authorize)> ChallengeAsync(WebApplicationFactory<Program> app, string? returnUrl)
    {
        var browser = new TestBrowser(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost/") }));
        var (_, page) = await browser.GetAsync("/user/sign-in" + (returnUrl != null ? "?ReturnUrl=" + Uri.EscapeDataString(returnUrl) : ""), Ct);
        var token = EntraFormRx().Match(page).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "the sign-in page shows the Entra ID option");
        var (challenge, _) = await browser.SendAsync(HttpMethod.Post, "/user/sign-in/entra" + (returnUrl != null ? "?ReturnUrl=" + Uri.EscapeDataString(returnUrl) : ""),
            [new("__RequestVerificationToken", token)], Ct);
        Assert.Equal(HttpStatusCode.Found, challenge.StatusCode);
        var authorize = challenge.Headers.Location!;
        Assert.StartsWith(idp.Authority + "/authorize?", authorize.AbsoluteUri);
        Assert.Contains("code_challenge_method=S256", authorize.Query);
        return (browser, authorize);
    }

    /// <summary>The browser's part of the flow: sign-in page → challenge → IdP (login_hint picks the account) → form_post → callback.</summary>
    private async Task<(TestBrowser Browser, HttpResponseMessage Final)> SignInAsync(WebApplicationFactory<Program> app, TestAccount account, string? returnUrl = null)
    {
        var (browser, authorize) = await ChallengeAsync(app, returnUrl);
        using var idpClient = new HttpClient();
        var formPost = await idpClient.GetStringAsync(authorize.AbsoluteUri + "&login_hint=" + Uri.EscapeDataString(account.PreferredUsername), Ct);
        var m = FormPostRx().Match(formPost);
        Assert.True(m.Success, "the provider answers with a form_post to the app");
        var callbackUrl = new Uri(WebUtility.HtmlDecode(m.Groups[1].Value));
        var (signedIn, _) = await browser.SendAsync(HttpMethod.Post, callbackUrl.PathAndQuery,
            [new("code", m.Groups[2].Value), new("state", WebUtility.HtmlDecode(m.Groups[3].Value))], Ct);
        Assert.Equal(HttpStatusCode.Found, signedIn.StatusCode);
        var (final, _) = await browser.GetAsync(signedIn.Headers.Location!.OriginalString, Ct);
        return (browser, final);
    }

    private async Task<List<string>> RolesAsync(string email) =>
        (await Sql.RowsAsync(db.ConnectionString!,
            "SELECT r.Name FROM dbo.IdentityUsers u JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE u.Email = @e ORDER BY r.Name", ("@e", email)))
        .Select(r => (string)r[0]!).ToList();

    [Fact]
    public async Task First_sign_in_provisions_a_user_with_the_app_role_and_the_shared_cookie()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = App();
        var (browser, final) = await SignInAsync(app, TestIdpHost.NewHelpDesk, "/tickets");
        Assert.Equal(HttpStatusCode.Found, final.StatusCode);
        Assert.Equal("/tickets", final.Headers.Location!.OriginalString);
        Assert.True(browser.Cookies.ContainsKey(SignInTests.IdentityCookie), "the sign-in cookie");
        Assert.False(browser.Cookies.ContainsKey(".AspNetCore.Identity.External"), "the external cookie is removed after the sign-in");
        Assert.Equal(["TdHelpDeskUsers"], await RolesAsync(TestIdpHost.NewHelpDesk.Email!));
        Assert.Equal(1, await Sql.ScalarAsync<int>(db.ConnectionString!,
            "SELECT COUNT(*) FROM dbo.IdentityUserLogins l JOIN dbo.IdentityUsers u ON u.Id = l.UserId WHERE l.LoginProvider = N'EntraId' AND l.ProviderKey = @k AND u.UserName = @n AND u.PasswordHash IS NULL",
            ("@k", TestIdpHost.NewHelpDesk.Oid), ("@n", TestIdpHost.NewHelpDesk.PreferredUsername)));
        // help desk may open help-desk pages; the cookie carries the user's display name like any other sign-in
        var (manage, body) = await browser.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);
        Assert.Contains("Entra Helpdesk", body);
        var (admin, _) = await browser.GetAsync("/admin/users", Ct);
        Assert.Equal("/access-denied", admin.Headers.Location!.OriginalString);

        // second sign-in: the same row (found by the login), no new user
        await SignInAsync(app, TestIdpHost.NewHelpDesk);
        Assert.Equal(1, await Sql.ScalarAsync<int>(db.ConnectionString!, "SELECT COUNT(*) FROM dbo.IdentityUsers WHERE Email = @e", ("@e", TestIdpHost.NewHelpDesk.Email!)));
    }

    [Fact]
    public async Task Without_app_roles_a_new_account_gets_the_default_roles()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = App();
        await SignInAsync(app, TestIdpHost.NoRoles);
        Assert.Equal(["TdPendingUsers"], await RolesAsync(TestIdpHost.NoRoles.Email!));
    }

    [Fact]
    public async Task First_sign_in_links_an_existing_legacy_account_by_email_and_keeps_its_roles()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        var before = await RolesAsync(TestIdpHost.Existing.Email!);
        var id = await Sql.ScalarAsync<string>(db.ConnectionString!, "SELECT Id FROM dbo.IdentityUsers WHERE Email = @e", ("@e", TestIdpHost.Existing.Email!));
        await using var app = App();
        var (browser, _) = await SignInAsync(app, TestIdpHost.Existing);
        Assert.Equal(before, await RolesAsync(TestIdpHost.Existing.Email!));
        Assert.Equal(id, await Sql.ScalarAsync<string>(db.ConnectionString!,
            "SELECT UserId FROM dbo.IdentityUserLogins WHERE LoginProvider = N'EntraId' AND ProviderKey = @k", ("@k", TestIdpHost.Existing.Oid)));
        var (_, body) = await browser.GetAsync("/account/manage", Ct);
        Assert.Contains("Ali Chen", body); // the legacy row's display name, not the token's
    }

    [Fact]
    public async Task A_token_for_another_tenant_is_refused()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = App(tenant: "00000000-0000-0000-0000-0000000000ff");
        var (browser, final) = await SignInAsync(app, TestIdpHost.NoRoles);
        Assert.Equal(HttpStatusCode.OK, final.StatusCode); // the callback shows the sign-in page again
        Assert.False(browser.Cookies.ContainsKey(SignInTests.IdentityCookie));
    }

    /// <summary>
    /// What Entra ID form-posts to the callback when the user cancels the consent prompt (step 8, 2026-11-10:
    /// docs/evidence/identity-20261110/step8-entra-user-check). The state is the app's own, so the handler gets as far as the error.
    /// </summary>
    private async Task<(TestBrowser Browser, HttpResponseMessage Callback, string State)> RemoteErrorAsync(WebApplicationFactory<Program> app,
        string error, string description, string? returnUrl)
    {
        var (browser, authorize) = await ChallengeAsync(app, returnUrl);
        var state = System.Web.HttpUtility.ParseQueryString(authorize.Query)["state"]!;
        var (callback, _) = await browser.SendAsync(HttpMethod.Post, "/signin-oidc",
            [new("error", error), new("error_description", description), new("state", state)], Ct);
        return (browser, callback, state);
    }

    private const string ConsentDeclined = "AADSTS65004: User declined to consent to access the app. Trace ID: 0d3e5c2a-0000-4000-8000-000000000001 Correlation ID: 6b0c1f9e-0000-4000-8000-000000000002 Timestamp: 2026-11-10 15:04:05Z";

    [GeneratedRegex("<div class=\"validation-summary-errors text-danger\"><ul>(.*?)</ul></div>", RegexOptions.Singleline)]
    private static partial Regex SummaryRx();

    [Theory]
    [InlineData("/tickets", "/user/sign-in?ReturnUrl=%2Ftickets")]
    [InlineData(null, "/user/sign-in")]
    [InlineData("https://elsewhere.example/tickets", "/user/sign-in")]
    public async Task A_declined_consent_at_the_callback_returns_to_the_sign_in_page_not_the_error_page(string? returnUrl, string expected)
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        var logs = new CapturedLogs();
        await using var root = App();
        await using var app = root.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        var (browser, callback, state) = await RemoteErrorAsync(app, "access_denied", ConsentDeclined, returnUrl);
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal(expected, callback.Headers.Location!.OriginalString);
        Assert.False(browser.Cookies.ContainsKey(SignInTests.IdentityCookie));

        // one warning with the OIDC error and the AADSTS code; never the state, cookies or the provider's trace ids
        var warning = Assert.Single(logs.Entries, e => e.Category.StartsWith("TicketDesk.", StringComparison.Ordinal) && e.Message.Contains("access_denied", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("AADSTS65004", warning.Message);
        Assert.DoesNotContain(state, warning.Message);
        Assert.DoesNotContain("Trace ID", warning.Message);
        Assert.All(browser.Cookies.Values.Where(v => v.Length >= 16), v => Assert.DoesNotContain(v, warning.Message));
    }

    [Theory]
    [InlineData("access_denied", ConsentDeclined, true)]
    [InlineData("server_error", "<b id=\"td-raw\">AADSTS50000: There was an error issuing a token.</b>", false)]
    public async Task The_sign_in_page_then_says_entra_sign_in_did_not_complete(string error, string description, bool adminApproval)
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = App();
        var (browser, callback, _) = await RemoteErrorAsync(app, error, description, "/tickets");
        var (page, html) = await browser.GetAsync(callback.Headers.Location!.OriginalString, Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var summary = SummaryRx().Match(html);
        Assert.True(summary.Success, "the message is in the validation summary");
        Assert.Contains("Entra ID sign-in did not complete.", summary.Groups[1].Value);
        Assert.Equal(adminApproval, summary.Groups[1].Value.Contains("administrator may need to approve", StringComparison.Ordinal));
        Assert.DoesNotContain("td-raw", html); // the provider's text never reaches the page
        Assert.DoesNotContain("AADSTS", html);
        Assert.Contains("action=\"/user/sign-in?ReturnUrl=%2Ftickets\"", html); // the return URL survives for the next attempt

        // shown once: the next visit is the plain page again
        var (_, again) = await browser.GetAsync("/user/sign-in", Ct);
        Assert.DoesNotContain("validation-summary-errors", again);
    }

    [Fact]
    public async Task The_option_is_hidden_when_entra_is_off_so_the_page_stays_the_legacy_one()
    {
        await using var app = App(enabled: false);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var html = await client.GetStringAsync("/user/sign-in", Ct);
        Assert.DoesNotContain("entraSignIn", html);
        using var post = await client.PostAsync("/user/sign-in/entra", new FormUrlEncodedContent([]), Ct);
        Assert.NotEqual(HttpStatusCode.Found, post.StatusCode);
    }
}

/// <summary>Everything the app logs, for assertions on what a log line holds.</summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    public System.Collections.Concurrent.ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose() { }

    private sealed class Logger(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Entries.Enqueue((category, logLevel, formatter(state, exception) + (exception == null ? "" : " " + exception)));
    }
}
