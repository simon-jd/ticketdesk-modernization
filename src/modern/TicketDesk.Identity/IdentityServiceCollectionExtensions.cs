using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>The sign-in page (legacy URL).</summary>
    public const string SignInPath = "/user/sign-in";

    /// <summary>
    /// ASP.NET Core Identity over the legacy tables with the legacy user-manager rules, Identity V3 password hashes (legacy V2
    /// hashes are rehashed at sign-in), cookie authentication, legacy-role policies and the Entra ID seam.
    /// </summary>
    public static AuthenticationBuilder AddTicketDeskIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TicketDeskAuthenticationOptions.SectionName);
        services.AddOptions<TicketDeskAuthenticationOptions>().Bind(section);
        services.AddTicketDeskDataProtection(configuration);

        // Identity V3 hashes (PBKDF2-HMAC-SHA512, 100,000 iterations) since the legacy app is retired (plan/DECISIONS.md: V2
        // until retirement, so the legacy app could verify them). A V2 hash (legacy or td-seed: PBKDF2-HMAC-SHA1, 1,000
        // iterations) still verifies; the hasher then answers SuccessRehashNeeded and UserManager.CheckPasswordAsync, which
        // every password sign-in goes through, stores the password again as V3 (and renews the security stamp).
        services.Configure<PasswordHasherOptions>(o => o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3);

        services.AddIdentityCore<TicketDeskUser>(ApplyLegacyUserManagerRules)
            .AddRoles<TicketDeskRole>()
            .AddUserStore<TicketDeskUserStore>()
            .AddRoleStore<TicketDeskRoleStore>()
            .AddClaimsPrincipalFactory<TicketDeskClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // legacy SecurityStampValidator interval
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(30));

        var auth = services.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });
        auth.AddIdentityCookies();
        services.ConfigureApplicationCookie(ConfigureApplicationCookie);

        var entra = section.GetSection("EntraId").Get<EntraIdOptions>() ?? new EntraIdOptions();
        if (entra.Enabled) AddEntraId(auth, entra);

        services.AddAuthorizationBuilder().AddTicketDeskPolicies();
        services.AddTransient<IClaimsTransformation, LegacyRoleHierarchy>();
        services.AddScoped<EntraAccountLinker>();
        return auth;
    }

    /// <summary>Mirrors <c>TicketDeskUserManager</c> (TicketDesk.Web.Identity) so both apps accept the same input.</summary>
    public static void ApplyLegacyUserManagerRules(IdentityOptions o)
    {
        o.Password.RequiredLength = 5;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredUniqueChars = 1;
        o.User.RequireUniqueEmail = true;
        o.User.AllowedUserNameCharacters = string.Empty; // AllowOnlyAlphanumericUserNames = false
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        o.SignIn.RequireConfirmedAccount = false;
        o.SignIn.RequireConfirmedEmail = false;
    }

    internal static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api");

    /// <summary>
    /// The app's own Identity cookie (<c>.AspNetCore.Identity.Application</c>: HttpOnly, SameSite=Lax, Secure on HTTPS). Until
    /// Round 4 the shared-cookie mode gave it the legacy OWIN name and attributes so the legacy app could read it; cookies of
    /// that name are no longer read (users signed in at the switch sign in once more).
    /// </summary>
    private static void ConfigureApplicationCookie(CookieAuthenticationOptions o)
    {
        // The sign-in, sign-out and access-denied pages have the legacy URLs (wave 5: served by the new app).
        o.LoginPath = SignInPath;
        o.LogoutPath = "/user/sign-out";
        o.AccessDeniedPath = "/access-denied";
        o.ReturnUrlParameter = "ReturnUrl";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Events.OnRedirectToLogin = ctx => RedirectOrStatus(ctx, StatusCodes.Status401Unauthorized);
        o.Events.OnRedirectToAccessDenied = ctx => RedirectOrStatus(ctx, StatusCodes.Status403Forbidden);
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> ctx, int apiStatus)
    {
        if (IsApiRequest(ctx.Request)) ctx.Response.StatusCode = apiStatus;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }

    private static void AddEntraId(AuthenticationBuilder auth, EntraIdOptions entra)
    {
        if (string.IsNullOrWhiteSpace(entra.Authority) || string.IsNullOrWhiteSpace(entra.ClientId))
            throw new InvalidOperationException("Authentication:EntraId:Enabled is true but Authority or ClientId is missing.");
        auth.AddOpenIdConnect(EntraIdOptions.Scheme, entra.DisplayName, o =>
        {
            o.Authority = entra.Authority;
            o.ClientId = entra.ClientId;
            o.ClientSecret = string.IsNullOrEmpty(entra.ClientSecret) ? null : entra.ClientSecret;
            o.CallbackPath = entra.CallbackPath;
            o.RequireHttpsMetadata = entra.RequireHttpsMetadata;
            o.ResponseType = "code";
            o.UsePkce = true;
            o.SignInScheme = IdentityConstants.ExternalScheme;
            o.MapInboundClaims = false;
            o.SaveTokens = false;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.Scope.Add("profile");
            o.Scope.Add("email");
            // Entra app roles named like the legacy roles (TdAdministrators, ...) arrive in the "roles" claim; the callback
            // (UserController.EntraCallback) links or provisions the IdentityUsers row and maps them (EntraSignIn).
            o.TokenValidationParameters.RoleClaimType = "roles";
            o.TokenValidationParameters.NameClaimType = "preferred_username";
            // an error response (access_denied when consent is declined or needs an administrator) or any other remote
            // failure goes back to the sign-in page with a message instead of the error page
            o.Events.OnRemoteFailure = EntraSignInFailure.HandleAsync;
        });
    }
}
