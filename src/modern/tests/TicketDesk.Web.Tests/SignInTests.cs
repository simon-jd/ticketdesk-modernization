using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>A browser for the tests: keeps cookies by name (one host), sends them back, and reads tokens from pages.</summary>
internal sealed class TestBrowser(HttpClient client)
{
    public Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);
    public List<string> LastSetCookies { get; private set; } = [];

    public async Task<(HttpResponseMessage Response, string Body)> SendAsync(HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>>? form = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, url);
        if (form != null) request.Content = new FormUrlEncodedContent(form);
        if (Cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}")));
        var response = await client.SendAsync(request, ct);
        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var sc) ? sc.ToList() : [];
        foreach (var c in LastSetCookies)
        {
            var nv = c.Split(';')[0].Split('=', 2);
            if (nv[1].Length == 0 || c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)) Cookies.Remove(nv[0]);
            else Cookies[nv[0]] = nv[1];
        }
        return (response, await response.Content.ReadAsStringAsync(ct));
    }

    public Task<(HttpResponseMessage Response, string Body)> GetAsync(string url, CancellationToken ct = default) => SendAsync(HttpMethod.Get, url, null, ct);

    /// <summary>GET a form page and POST the fields with its antiforgery token.</summary>
    public async Task<(HttpResponseMessage Response, string Body)> SubmitAsync(string pageUrl, string postUrl, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default)
    {
        var (_, page) = await GetAsync(pageUrl, ct);
        var token = IdentityWaveTests.TokenRx().Match(page).Groups[1].Value;
        return await SendAsync(HttpMethod.Post, postUrl, fields.Append(new("__RequestVerificationToken", token)), ct);
    }
}

/// <summary>
/// Sign-in after the legacy app is retired: the app's own Identity cookie (no longer the legacy OWIN name and attributes of the
/// shared-cookie mode), a ticket protected with the key ring in the database (readable by a second instance = another pod),
/// cookies from before the switch not accepted (one more sign-in), Identity V3 hashes with V2 hashes rehashed at sign-in,
/// lockout, the e-mail fallback and register (S4).
/// </summary>
public sealed class SignInTests(BaselineDatabase db) : IClassFixture<BaselineDatabase>
{
    /// <summary>ASP.NET Core Identity's application cookie (the default name since the shared-cookie mode is gone).</summary>
    public const string IdentityCookie = ".AspNetCore.Identity.Application";

    /// <summary>The legacy OWIN cookie, which the shared-cookie mode also used until Round 4.</summary>
    public const string LegacyCookie = ".AspNet.ApplicationCookie";

    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string User = TicketDesk.Replay.TicketDeskJourneys.InternalUser;

    private TicketDeskApp App(params (string Key, string Value)[] extra)
    {
        var s = TicketDeskApp.Settings(db.ConnectionString);
        s["DataProtection:KeyRing"] = "Database"; // as shipped (appsettings.json)
        foreach (var (k, v) in extra) s[k] = v;
        return new TicketDeskApp(s);
    }

    private static TestBrowser Browser(TicketDeskApp app) =>
        new(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost/") }));

    private static KeyValuePair<string, string>[] Credentials(string user, string password) =>
        [new("UserNameOrEmail", user), new("Password", password), new("RememberMe", "false")];

    private string Password => SqlTestEnvironment.SeedUserPassword ?? "";

    private void RequireData()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        if (SqlTestEnvironment.SeedUserPassword == null) Assert.Skip("set TD_SEED_USER_PASSWORD (the seeded users' password)");
    }

    private Task<string> HashOfAsync(string userNameOrEmail) => Sql.ScalarAsync<string>(db.ConnectionString!,
        "SELECT PasswordHash FROM dbo.IdentityUsers WHERE UserName = @u OR Email = @u", ("@u", userNameOrEmail));

    /// <summary>Identity V3: 0x01 | PRF (UInt32, 2 = HMAC-SHA512) | iterations (UInt32) | salt length (UInt32) | salt | subkey, big-endian.</summary>
    private static (int Prf, int Iterations, int Length) V3(string hash)
    {
        var b = Convert.FromBase64String(hash);
        Assert.Equal(1, b[0]);
        static int U32(byte[] x, int at) => (x[at] << 24) | (x[at + 1] << 16) | (x[at + 2] << 8) | x[at + 3];
        return (U32(b, 1), U32(b, 5), b.Length);
    }

    [Fact]
    public async Task Sign_in_issues_the_apps_own_cookie_that_a_second_instance_reads_from_the_key_ring_in_the_database()
    {
        RequireData();
        await using var podA = App();
        await using var podB = App();
        var a = Browser(podA);
        var (signIn, _) = await a.SubmitAsync("/user/sign-in?ReturnUrl=%2Ftickets", "/user/sign-in?ReturnUrl=%2Ftickets", Credentials(User, Password), Ct);
        Assert.Equal(HttpStatusCode.Found, signIn.StatusCode);
        Assert.Equal("/tickets", signIn.Headers.Location!.OriginalString);
        var cookie = Assert.Single(a.LastSetCookies, c => c.StartsWith(IdentityCookie + "=", StringComparison.Ordinal));
        // ASP.NET Core's defaults: path, secure (HTTPS), SameSite=Lax, HttpOnly; a session cookie (RememberMe off)
        Assert.Equal("path=/; secure; samesite=lax; httponly", string.Join("; ", cookie.Split("; ").Skip(1)).ToLowerInvariant());
        Assert.DoesNotContain(a.LastSetCookies, c => c.StartsWith(LegacyCookie + "=", StringComparison.Ordinal));
        Assert.Equal("no-cache", signIn.Headers.CacheControl!.ToString());

        var b = Browser(podB);
        b.Cookies[IdentityCookie] = a.Cookies[IdentityCookie];
        var (manage, body) = await b.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);
        Assert.Contains("Ali Chen", body);
        Assert.True(await Sql.ScalarAsync<int>(db.ConnectionString!, "SELECT COUNT(*) FROM dbo.DataProtectionKeys") >= 1);
        Assert.Contains("<key ", await Sql.ScalarAsync<string>(db.ConnectionString!, "SELECT TOP (1) Xml FROM dbo.DataProtectionKeys ORDER BY Id"));
    }

    /// <summary>
    /// The switch: a browser signed in before it holds the shared cookie (same ticket format and key ring, legacy name). The app
    /// reads only its own cookie name now, so that browser is anonymous and signs in once more.
    /// </summary>
    [Fact]
    public async Task A_sign_in_cookie_from_before_the_switch_is_not_read_so_the_user_signs_in_once_more()
    {
        RequireData();
        await using var app = App();
        var signedIn = Browser(app);
        await signedIn.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(User, Password), Ct);
        var before = Browser(app);
        before.Cookies[LegacyCookie] = signedIn.Cookies[IdentityCookie]; // a valid ticket under the pre-switch name
        var (manage, _) = await before.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.Found, manage.StatusCode);
        Assert.StartsWith("/user/sign-in", manage.Headers.Location!.PathAndQuery);
        var (signIn, _) = await before.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(User, Password), Ct);
        Assert.Equal(HttpStatusCode.Found, signIn.StatusCode);
        var (again, body) = await before.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("Ali Chen", body);
    }

    [Fact]
    public async Task Sign_out_deletes_the_cookie_and_ends_the_session()
    {
        RequireData();
        await using var app = App();
        var browser = Browser(app);
        await browser.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(User, Password), Ct);
        Assert.True(browser.Cookies.ContainsKey(IdentityCookie));
        // S3 (Round 5): the user menu's Log off item posts with the page's token
        var (signOut, _) = await browser.SubmitAsync("/account/manage", "/user/sign-out", [], Ct);
        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        Assert.Equal("/", signOut.Headers.Location!.OriginalString);
        var deleted = Assert.Single(browser.LastSetCookies);
        Assert.StartsWith(IdentityCookie + "=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/", deleted);
        var (after, _) = await browser.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.Found, after.StatusCode);
        Assert.StartsWith("/user/sign-in", after.Headers.Location!.PathAndQuery);
    }

    /// <summary>
    /// A seeded user with a legacy Identity V2 hash (td-seed writes the legacy format, like the migrated production rows) signs
    /// in; the stored hash is then V3 (PBKDF2-HMAC-SHA512, 100,000 iterations) and the next sign-in verifies that without another
    /// rehash. A wrong password changes nothing.
    /// </summary>
    [Fact]
    public async Task A_v2_hash_still_signs_in_and_is_rehashed_to_v3_which_then_signs_in()
    {
        RequireData();
        // a seeded internal user no other test signs in with, still on its V2 hash
        var user = await Sql.ScalarAsync<string>(db.ConnectionString!, """
            SELECT TOP (1) u.UserName FROM dbo.IdentityUsers u
            JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = N'TdInternalUsers'
            WHERE u.UserName LIKE N'%@seed.ticketdesk.test' AND u.UserName <> @me AND u.LockoutEndDateUtc IS NULL
            ORDER BY u.UserName DESC
            """, ("@me", User));
        var v2 = await HashOfAsync(user);
        Assert.Equal(0, Convert.FromBase64String(v2)[0]);
        Assert.True(TicketDesk.Seed.IdentityV2PasswordHasher.Verify(v2, Password));
        var stamp = await Sql.ScalarAsync<string>(db.ConnectionString!, "SELECT SecurityStamp FROM dbo.IdentityUsers WHERE UserName = @u", ("@u", user));

        await using var app = App();
        var (wrong, _) = await Browser(app).SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(user, Password + "x"), Ct);
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode); // the form again, with the error
        Assert.Equal(v2, await HashOfAsync(user));

        var first = Browser(app);
        var (ok, _) = await first.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(user, Password), Ct);
        Assert.Equal(HttpStatusCode.Found, ok.StatusCode);
        var v3 = await HashOfAsync(user);
        Assert.NotEqual(v2, v3);
        Assert.Equal((2, 100_000, 61), V3(v3));
        Assert.False(TicketDesk.Seed.IdentityV2PasswordHasher.Verify(v3, Password)); // the legacy app could not verify it any more
        Assert.NotEqual(stamp, await Sql.ScalarAsync<string>(db.ConnectionString!, "SELECT SecurityStamp FROM dbo.IdentityUsers WHERE UserName = @u", ("@u", user)));
        Assert.Equal(0, await Sql.ScalarAsync<int>(db.ConnectionString!, "SELECT AccessFailedCount FROM dbo.IdentityUsers WHERE UserName = @u", ("@u", user)));
        var (page, _) = await first.GetAsync("/account/manage", Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode); // the cookie issued with the rehash is valid

        var (second, _) = await Browser(app).SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(user, Password), Ct);
        Assert.Equal(HttpStatusCode.Found, second.StatusCode);
        Assert.Equal(v3, await HashOfAsync(user)); // V3 at the configured strength: no rehash
    }

    [Fact]
    public async Task Sign_in_by_email_and_by_user_name_and_lockout_after_five_failures()
    {
        RequireData();
        var victim = "quinn.lopez." + Guid.NewGuid().ToString("N")[..6] + "@seed.ticketdesk.test";
        await using var app = App(("TicketDesk:RegisterEnabled", "true"));
        // a fresh account (register is on for this test): user name differs from the e-mail
        var reg = Browser(app);
        var (created, _) = await reg.SubmitAsync("/user/register", "/user/register",
            [new("UserName", "quinn" + victim[12..18]), new("Email", victim), new("DisplayName", "Quinn Lopez"), new("Password", Password), new("ConfirmPassword", Password)], Ct);
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);
        var byEmail = Browser(app);
        var (ok, _) = await byEmail.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(victim, Password), Ct);
        Assert.Equal(HttpStatusCode.Found, ok.StatusCode);
        var attacker = Browser(app);
        string last = "";
        for (var i = 0; i < 5; i++) (_, last) = await attacker.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(victim, "wrong-" + i), Ct);
        Assert.Contains("Locked out", last, StringComparison.OrdinalIgnoreCase);
        var (locked, lockedBody) = await attacker.SubmitAsync("/user/sign-in", "/user/sign-in", Credentials(victim, Password), Ct);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("Locked out", lockedBody, StringComparison.OrdinalIgnoreCase);
        // the lockout is stored in the legacy column (5 minutes)
        var end = await Sql.ScalarAsync<DateTime>(db.ConnectionString!, "SELECT LockoutEndDateUtc FROM dbo.IdentityUsers WHERE Email = @e", ("@e", victim));
        Assert.InRange(end, DateTime.UtcNow.AddMinutes(4), DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public async Task Register_is_refused_while_disabled_S4()
    {
        RequireData();
        await using var app = App();
        var browser = Browser(app);
        var (page, _) = await browser.GetAsync("/user/register", Ct);
        Assert.Equal(HttpStatusCode.Found, page.StatusCode);
        var email = "s4." + Guid.NewGuid().ToString("N")[..8] + "@example.test";
        var (post, _) = await browser.SubmitAsync("/user/sign-in", "/user/register",
            [new("UserName", email), new("Email", email), new("DisplayName", "S4"), new("Password", Password), new("ConfirmPassword", Password)], Ct);
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);
        Assert.Equal("/", post.Headers.Location!.OriginalString);
        Assert.DoesNotContain(browser.LastSetCookies, c => c.StartsWith(IdentityCookie + "=", StringComparison.Ordinal));
        Assert.Equal(0, await Sql.ScalarAsync<int>(db.ConnectionString!, "SELECT COUNT(*) FROM dbo.IdentityUsers WHERE Email = @e", ("@e", email)));
    }

    [Fact]
    public async Task Register_when_enabled_creates_a_pending_user_with_an_email_destination_and_a_v3_hash()
    {
        RequireData();
        await using var app = App(("TicketDesk:RegisterEnabled", "true"));
        var browser = Browser(app);
        var email = "reg." + Guid.NewGuid().ToString("N")[..8] + "@example.test";
        var (post, _) = await browser.SubmitAsync("/user/register", "/user/register",
            [new("UserName", email), new("Email", email), new("DisplayName", "Reg User"), new("Password", Password), new("ConfirmPassword", Password)], Ct);
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);
        Assert.True(browser.Cookies.ContainsKey(IdentityCookie));
        var roles = await Sql.RowsAsync(db.ConnectionString!,
            "SELECT r.Name FROM dbo.IdentityUsers u JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE u.Email = @e", ("@e", email));
        Assert.Equal("TdPendingUsers", Assert.Single(roles)[0]);
        Assert.Equal(1, await Sql.ScalarAsync<int>(db.ConnectionString!,
            "SELECT COUNT(*) FROM notifications.PushNotificationDestinations d JOIN dbo.IdentityUsers u ON u.Id = d.SubscriberId WHERE u.Email = @e AND d.DestinationType = N'email' AND d.DestinationAddress = @e", ("@e", email)));
        Assert.Equal((2, 100_000, 61), V3(await HashOfAsync(email)));
    }

    [Fact]
    public async Task Change_password_writes_a_v3_hash_and_edit_profile_saves_L5()
    {
        RequireData();
        await using var app = App(("TicketDesk:RegisterEnabled", "true"));
        var browser = Browser(app);
        var email = "pw." + Guid.NewGuid().ToString("N")[..8] + "@example.test";
        await browser.SubmitAsync("/user/register", "/user/register",
            [new("UserName", email), new("Email", email), new("DisplayName", "Pw User"), new("Password", Password), new("ConfirmPassword", Password)], Ct);
        var registered = await HashOfAsync(email);
        var (changed, _) = await browser.SubmitAsync("/account/change-password", "/account/change-password",
            [new("OldPassword", Password), new("NewPassword", "Changed-Pass-2"), new("ConfirmPassword", "Changed-Pass-2")], Ct);
        Assert.Equal(HttpStatusCode.Found, changed.StatusCode);
        Assert.Equal("/account/manage?Message=ChangePasswordSuccess", changed.Headers.Location!.OriginalString);
        var hash = await HashOfAsync(email);
        Assert.NotEqual(registered, hash);
        Assert.Equal((2, 100_000, 61), V3(hash));
        var (mismatch, body) = await browser.SubmitAsync("/account/change-password", "/account/change-password",
            [new("OldPassword", "Changed-Pass-2"), new("NewPassword", "abcdefg"), new("ConfirmPassword", "abcdefx")], Ct);
        Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        Assert.Contains("The new password and confirmation password do not match.", body);

        var newEmail = "pw2." + email;
        var (saved, _) = await browser.SubmitAsync("/account/edit-profile", "/account/edit-profile", [new("Email", newEmail), new("DisplayName", "Pw User Renamed")], Ct);
        Assert.Equal(HttpStatusCode.Found, saved.StatusCode);
        Assert.Equal("/account/manage?Message=ProfileSaveSuccess", saved.Headers.Location!.OriginalString);
        var (manage, manageBody) = await browser.GetAsync("/account/manage?Message=ProfileSaveSuccess", Ct);
        Assert.Contains("Pw User Renamed", manageBody); // the cookie was refreshed with the new display name
        Assert.Contains("Your profile has been updated.", manageBody);
        Assert.Equal(1, await Sql.ScalarAsync<int>(db.ConnectionString!,
            "SELECT COUNT(*) FROM notifications.PushNotificationDestinations WHERE DestinationAddress = @e AND SubscriberName = N'Pw User Renamed'", ("@e", newEmail)));
    }
}
