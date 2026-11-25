using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using TicketDesk.Database;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>An empty installation for <see cref="FirstAdministratorWebTests"/> (its own: the test creates the administrator).</summary>
public sealed class FirstAdminInstallDatabase : EmptyInstallDatabase;

/// <summary>
/// The operator's path on an empty installation (first-run setup retired, X1): td-db create-admin, then the administrator signs
/// in on the app's sign-in page and reaches the administration pages.
/// </summary>
public sealed class FirstAdministratorWebTests(FirstAdminInstallDatabase db) : IClassFixture<FirstAdminInstallDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_administrator_from_td_db_signs_in_on_the_sign_in_page_and_administers()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        const string email = "operator.admin@example.test", password = "Web-Secret-2026";
        var (o, e) = (new StringWriter(), new StringWriter());
        var code = await Cli.RunAsync(["create-admin", "--email", email, "--display-name", "Operator Admin", "--password-stdin"], o, e,
            n => n == "TD_DB_CONN" ? db.ConnectionString : null, new StringReader(password + "\n"));
        Assert.True(code == 0, e.ToString());

        var settings = TicketDeskApp.Settings(db.ConnectionString);
        await using var app = new TicketDeskApp(settings);
        var browser = new TestBrowser(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost/") }));
        var (signIn, _) = await browser.SubmitAsync("/user/sign-in", "/user/sign-in",
            [new("UserNameOrEmail", email), new("Password", password), new("RememberMe", "false")], Ct);
        Assert.Equal(HttpStatusCode.Found, signIn.StatusCode);
        Assert.True(browser.Cookies.ContainsKey(SignInTests.IdentityCookie));
        var (users, page) = await browser.GetAsync("/admin/users", Ct);
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        Assert.Contains("Operator Admin", page);
        // a second administrator comes from the app now, not from td-db
        var again = await Cli.RunAsync(["create-admin", "--email", "other@example.test", "--display-name", "Other", "--password-stdin"], TextWriter.Null, TextWriter.Null,
            n => n == "TD_DB_CONN" ? db.ConnectionString : null, new StringReader(password));
        Assert.Equal(1, again);
    }
}
