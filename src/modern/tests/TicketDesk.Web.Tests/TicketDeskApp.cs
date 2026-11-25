using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>
/// The real TicketDesk.Web host with test settings. <paramref name="kestrel"/> runs it on real sockets (so Kestrel limits
/// apply, as in production); <paramref name="testAuth"/> authenticates requests by an <c>X-Test-User</c> header holding an
/// IdentityUsers.Id, with the principal built by the app's own claims factory (roles from the database).
/// </summary>
public sealed class TicketDeskApp(IDictionary<string, string?> settings, bool kestrel = false, bool testAuth = false) : WebApplicationFactory<Program>
{
    public const string NoDatabase = "Server=127.0.0.1,1;Database=none;User Id=x;Password=y;TrustServerCertificate=True;Connect Timeout=2";

    private Uri? _address;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestSignInEndpoint>();
            if (testAuth)
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = TestAuthHandler.SchemeName;
                    o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    o.DefaultForbidScheme = TestAuthHandler.SchemeName;
                });
            }
        });
    }

    /// <summary>Starts the host on Kestrel (when requested) and returns a client that neither follows redirects nor stores cookies.</summary>
    public HttpClient RawClient()
    {
        if (!kestrel) return CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        if (_address == null)
        {
            UseKestrel(0);
            StartServer();
            var addr = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            _address = new Uri(addr.TrimEnd('/') + "/");
        }
        return new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = _address, Timeout = TimeSpan.FromMinutes(2) };
    }

    public static Dictionary<string, string?> Settings(string? connectionString = null) => new()
    {
        ["ConnectionStrings:TicketDesk"] = connectionString ?? NoDatabase,
        // the shipped appsettings.json keeps the Data Protection key ring in the database; tests that need that say so (SignInTests)
        ["DataProtection:KeyRing"] = "Default",
        // no registry file: every page is its server page unless a test says otherwise (the shipped migration-registry.json,
        // with the Angular pages, is not loaded)
        ["Migration:RegistryFile"] = "",
    };
}

internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";
    /// <summary>"display name|role,role|user id": a principal without a database row (layout tests).</summary>
    public const string PrincipalHeader = "X-Test-Principal";

    public static string Principal(string displayName, string userId, params string[] roles) => $"{displayName}|{string.Join(',', roles)}|{userId}";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(PrincipalHeader, out var p))
        {
            var parts = p.ToString().Split('|');
            var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, parts[2]),
                new(System.Security.Claims.ClaimTypes.Name, parts[2]),
                new(System.Security.Claims.ClaimTypes.GivenName, parts[0]),
            };
            claims.AddRange(parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r)));
            return AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, SchemeName)), SchemeName));
        }
        if (!Request.Headers.TryGetValue(Header, out var id)) return AuthenticateResult.NoResult();
        var user = await Context.RequestServices.GetRequiredService<UserManager<TicketDeskUser>>().FindByIdAsync(id.ToString());
        if (user == null) return AuthenticateResult.Fail("unknown test user");
        var principal = await Context.RequestServices.GetRequiredService<IUserClaimsPrincipalFactory<TicketDeskUser>>().CreateAsync(user);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

/// <summary>
/// Test-only <c>POST /test/sign-in?u=...</c>: password sign-in through the app's SignInManager with the scratch users' password,
/// so a real Identity cookie can be exercised without the sign-in page's form.
/// </summary>
internal sealed class TestSignInEndpoint : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // not app.Map(): Map moves the path into PathBase and the cookie would be scoped to /test/sign-in
        app.Use(async (ctx, nextMiddleware) =>
        {
            if (!HttpMethods.IsPost(ctx.Request.Method) || ctx.Request.Path != "/test/sign-in")
            {
                await nextMiddleware(ctx);
                return;
            }
            var signIn = ctx.RequestServices.GetRequiredService<SignInManager<TicketDeskUser>>();
            var result = await signIn.PasswordSignInAsync(ctx.Request.Query["u"].ToString(), ScratchDatabase.UserPassword, false, true);
            ctx.Response.StatusCode = result.Succeeded ? StatusCodes.Status204NoContent : StatusCodes.Status401Unauthorized;
        });
        next(app);
    };
}
