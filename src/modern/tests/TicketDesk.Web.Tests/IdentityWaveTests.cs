using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TicketDesk.Replay;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>
/// Wave 5 pages (identity and admin forms) compared with the golden legacy recording, on a copy of the baseline data. The
/// signed-in user comes from the test scheme; the admin row of the copy gets the recording's placeholders as e-mail and
/// name so the profile page compares equal. Allowed differences: the antiforgery ones (S3: no-cache headers, the new app's
/// antiforgery cookie, the token in forms that had none) and the secret-store note on the provider form (S6).
/// </summary>
public sealed partial class IdentityWaveTests(BaselineDatabase db) : IClassFixture<BaselineDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static readonly string[] Wave5Routes =
    [
        "user.sign-in.get", "user.sign-in.post", "user.register.get", "user.register.post", "user.sign-out",
        "account.change-password.get", "account.change-password.post", "account.edit-profile.get", "account.edit-profile.post",
        "admin.user-new.get", "admin.user-new.post", "admin.user-edit.get", "admin.user-edit.post", "admin.user-delete",
        "admin.project-new.get", "admin.project-new.post", "admin.project-edit.get", "admin.project-edit.post", "admin.project-delete",
        "admin.application-settings.get", "admin.application-settings.post", "admin.push-settings.get", "admin.push-settings.post",
        "admin.push-providers", "admin.push-provider.get", "admin.push-provider.post",
    ];

    private const string AdminRoles = "TdAdministrators,TdHelpDeskUsers,TdInternalUsers";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    internal static partial Regex TokenRx();

    private async Task<string> AdminIdAsync()
    {
        var id = await Sql.ScalarAsync<string>(db.ConnectionString!,
            "SELECT TOP (1) u.Id FROM dbo.IdentityUsers u JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE r.Name = N'TdAdministrators' ORDER BY u.Id");
        // the recording shows the lab admin's e-mail and name as placeholders
        await Sql.ScalarAsync<int>(db.ConnectionString!,
            "UPDATE dbo.IdentityUsers SET Email = N'{secret:TD_ADMIN_EMAIL}', DisplayName = N'{secret:TD_ADMIN_NAME}' WHERE Id = @id; SELECT @@ROWCOUNT", ("@id", id));
        return id;
    }

    internal static bool Allowed(DiffEntry d) =>
        Golden.IsAntiforgeryDifference(d) || (d.Actual ?? "").Contains("secret store", StringComparison.Ordinal);

    private TicketDeskApp App() => new(WaveParityTests.Migrated(db.ConnectionString, [.. WaveParityTests.Wave01Routes, .. WaveParityTests.Wave2PageRoutes, .. Wave5Routes]), testAuth: true);

    private static HttpClient Client(TicketDeskApp app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost/") });

    [Theory]
    [InlineData("anonymous#006.0", false)]
    [InlineData("anonymous#007.0", false)]
    [InlineData("sign-in#004.0", true)]
    [InlineData("sign-in#006.0", true)]
    [InlineData("admin-settings#002.0", true)]
    [InlineData("admin-settings#005.0", true)]
    [InlineData("admin-settings#006.0", true)]
    [InlineData("admin-settings#008.0", true)]
    [InlineData("admin-settings#009.0", true)]
    [InlineData("admin-settings#010.0", true)]
    [InlineData("admin-settings#011.0", true)]
    [InlineData("admin-changes#006.0", true)]
    public async Task Form_pages_match_the_golden_recording(string key, bool admin)
    {
        var golden = Golden.Get(key);
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        var adminId = await AdminIdAsync();
        await using var app = App();
        using var client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, golden.Request.Path);
        if (admin) request.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal("{secret:TD_ADMIN_NAME}", adminId, AdminRoles.Split(',')));
        if (golden.Request.Headers.ContainsKey("X-Requested-With")) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request, Ct);
        Assert.StartsWith("modern;", response.Headers.GetValues("X-TicketDesk-Backend").Single());
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct)).Where(d => !Allowed(d)).ToList();
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    /// <summary>Posts of the recording that re-render the form (validation errors), with the golden form fields and a fresh token.</summary>
    [Theory]
    [InlineData("sign-in#001.0", "sign-in#000.0", false)]
    [InlineData("sign-in#005.0", "sign-in#004.0", true)]
    [InlineData("admin-changes#003.0", "admin-changes#002.0", true)]
    [InlineData("admin-changes#007.0", "admin-changes#006.0", true)]
    public async Task Form_posts_that_re_render_match_the_golden_recording(string key, string formKey, bool admin)
    {
        var golden = Golden.Get(key);
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        var adminId = await AdminIdAsync();
        await using var app = App();
        using var client = Client(app);
        var principal = TestAuthHandler.Principal("{secret:TD_ADMIN_NAME}", adminId, AdminRoles.Split(','));
        using var get = new HttpRequestMessage(HttpMethod.Get, Golden.Get(formKey).Request.Path);
        if (admin) get.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
        using var page = await client.SendAsync(get, Ct);
        var token = TokenRx().Match(await page.Content.ReadAsStringAsync(Ct)).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        var form = golden.Request.Form!.Select(f => new KeyValuePair<string, string>(f.Key,
            f.Key == "__RequestVerificationToken" ? token : f.Value == "***" ? (f.Key == "SmtpPassword" ? "" : "not-the-password") : f.Value)).ToList();
        if (!form.Any(f => f.Key == "__RequestVerificationToken")) form.Add(new("__RequestVerificationToken", token));
        using var post = new HttpRequestMessage(HttpMethod.Post, golden.Request.Path) { Content = new FormUrlEncodedContent(form) };
        post.Headers.Add("Cookie", cookie);
        if (admin) post.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
        using var response = await client.SendAsync(post, Ct);
        var diffs = Golden.Compare(golden, await Golden.RecordAsync(golden, response, Ct)).Where(d => !Allowed(d)).ToList();
        Assert.True(diffs.Count == 0, Golden.Describe(diffs));
    }

    [Fact]
    public async Task Posts_without_the_antiforgery_token_are_refused()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        var adminId = await AdminIdAsync();
        await using var app = App();
        using var client = Client(app);
        foreach (var path in new[] { "/account/change-password", "/account/edit-profile", "/admin/new", "/admin/project/new", "/admin/application-settings", "/user/sign-in" })
        {
            using var post = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent([new("x", "y")]) };
            post.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal("Admin", adminId, AdminRoles.Split(',')));
            using var response = await client.SendAsync(post, Ct);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{path}: {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Admin_pages_refuse_non_admins_like_the_legacy_app()
    {
        if (db.ConnectionString == null) Assert.Skip(SqlTestEnvironment.SkipReason);
        await using var app = App();
        using var client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/application-settings");
        request.Headers.Add(TestAuthHandler.PrincipalHeader, TestAuthHandler.Principal("Ali Chen", "x", "TdInternalUsers"));
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/access-denied", response.Headers.Location!.OriginalString);
    }
}
