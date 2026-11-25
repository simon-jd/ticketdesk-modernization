using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Settings;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;
using ModelStrings = TicketDesk.Localization.Models.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Wave 5: legacy <c>UserController</c> — sign-in, sign-out, register — and the Entra ID sign-in. The new app issues its own
/// Identity cookie (until Round 4, with the shared-cookie mode, one the legacy app could read too). A password sign-in rehashes
/// a legacy Identity V2 hash to V3 (see <c>AddTicketDeskIdentity</c>).
/// </summary>
[Route("user")]
public sealed class UserController(
    SignInManager<TicketDeskUser> signIn,
    UserManager<TicketDeskUser> users,
    IApplicationSettingsStore settings,
    IUserAccounts accounts,
    IOptionsSnapshot<SiteOptions> site,
    IOptionsSnapshot<TicketDeskAuthenticationOptions> auth,
    ILogger<UserController> logger) : LegacyPageController
{
    public static LegacyField UserNameOrEmailField => new("UserNameOrEmail", ModelStrings.UserNameOrEmail) { Required = true };
    public static LegacyField PasswordField => new("Password", ModelStrings.Password) { Required = true };
    public static LegacyField RememberMeField => new("RememberMe", ModelStrings.RememberMe) { Required = true };

    /// <summary>UserRegisterViewModel (also the admin "new user" form).</summary>
    public static LegacyField[] RegisterFields =>
    [
        new("UserName", ModelStrings.UserName) { Required = true, MaxLength = 100 },
        new("Email", ModelStrings.Email) { Required = true, Email = true },
        new("DisplayName", ModelStrings.DisplayName) { Required = true, MaxLength = 100 },
        new("Password", ModelStrings.Password) { Required = true, MaxLength = 100, MinLength = 6 },
        new("ConfirmPassword", ModelStrings.ConfirmPassword) { EqualTo = ("Password", ModelStrings.ConfirmationDoNotMatch) },
    ];

    public sealed record SignInPage(string? ReturnUrl, string? UserNameOrEmail, bool RememberMe, bool DemoMode, bool RegisterEnabled, string? EntraDisplayName);

    private SignInPage Page(string? returnUrl, string? userName = null, bool rememberMe = false) =>
        new(returnUrl, userName, rememberMe, site.Value.DemoModeEnabled, site.Value.RegisterEnabled,
            auth.Value.EntraId.Enabled ? auth.Value.EntraId.DisplayName : null);

    [HttpGet("sign-in")]
    [MigratedRoute("user.sign-in.get", LegacyTokenPage = true)]
    public IActionResult SignIn([FromQuery(Name = "ReturnUrl")] string? returnUrl)
    {
        // back from a remote failure at the Entra ID callback (EntraSignInFailure); without its one-time cookie the page is the legacy one
        if (EntraSignInFailure.Take(HttpContext) is { } failure)
            ModelState.AddModelError("", failure == EntraSignInFailureKind.ConsentRequired ? EntraConsentRequired : EntraSignInIncomplete);
        return View("SignIn", Page(returnUrl));
    }

    // new-app text: the Entra ID option has no legacy resource strings
    public const string EntraSignInIncomplete = "Entra ID sign-in did not complete.";
    public const string EntraConsentRequired = EntraSignInIncomplete + " An administrator may need to approve the app for your organization first.";

    [HttpPost("sign-in")]
    [MigratedRoute("user.sign-in.post", LegacyTokenPage = true)]
    public async Task<IActionResult> SignInPost([FromQuery(Name = "ReturnUrl")] string? returnUrl)
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var userName = form["UserNameOrEmail"].FirstOrDefault();
        var password = form["Password"].FirstOrDefault() ?? "";
        var rememberMe = form["RememberMe"].Contains("true");
        // the legacy POST re-rendered the form without the return URL (ViewBag.ReturnUrl is only set by the GET)
        if (!LegacyForm.Validate(ModelState, form, UserNameOrEmailField, PasswordField)) return View("SignIn", Page(null, userName, rememberMe));

        var result = await signIn.PasswordSignInAsync(userName!, password, rememberMe, lockoutOnFailure: true);
        if (!result.Succeeded && !result.IsLockedOut && userName!.Contains('@'))
        {
            var byEmail = await users.FindByEmailAsync(userName);
            if (byEmail != null) result = await signIn.PasswordSignInAsync(byEmail.UserName, password, rememberMe, lockoutOnFailure: true);
        }
        if (result.Succeeded)
        {
            // OWIN's sign-in response headers (ASP.NET Core sends "no-cache,no-store"; same effect for a redirect)
            Response.Headers.CacheControl = "no-cache";
            return RedirectToLocal(returnUrl);
        }
        if (result.IsLockedOut) return View("Lockout");
        if (!string.IsNullOrWhiteSpace((await settings.GetAsync(HttpContext.RequestAborted)).SecuritySettings.DefaultLogonDomain))
            logger.LogWarning("DefaultLogonDomain is set, but LDAP sign-in (System.DirectoryServices) is not available in the new app; Entra ID replaces it");
        ModelState.AddModelError("", ControllerStrings.InvalidLoginAttempt);
        return View("SignIn", Page(null, userName, rememberMe));
    }

    /// <summary>
    /// S3 (Round 5): sign-out is a POST with the antiforgery token (the user menu's item is a form, on the server pages and in
    /// the Angular front end); the legacy app signed out on any verb.
    /// </summary>
    [Authorize]
    [HttpPost("sign-out")]
    [MigratedRoute("user.sign-out")]
    public async Task<IActionResult> SignOutPost()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        if (Request.Cookies.ContainsKey(".AspNetCore." + IdentityConstants.ExternalScheme)) await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        Response.Headers.CacheControl = "s-maxage=0, private";
        return new LegacyRedirectResult("/");
    }

    /// <summary>
    /// S3 (Round 5): a GET of the old link (a bookmark, a tab opened before the change) signs nobody out: it shows a page with
    /// the Log off button, which posts. Unlike the other state-changing URLs (405) this one gets a page, because a person who
    /// follows it means to sign out and should not be left signed in with an error.
    /// </summary>
    [Authorize]
    [HttpGet("sign-out")]
    [MigratedRoute("user.sign-out")]
    public IActionResult SignOutGet() => View("SignOut");

    [HttpGet("register")]
    [MigratedRoute("user.register.get", LegacyTokenPage = true)]
    public IActionResult Register() => site.Value.RegisterEnabled ? View("Register") : new LegacyRedirectResult("/");

    /// <summary>S4: refused (same redirect as the page) while <c>TicketDesk:RegisterEnabled</c> is false.</summary>
    [HttpPost("register")]
    [MigratedRoute("user.register.post", LegacyTokenPage = true)]
    public async Task<IActionResult> RegisterPost()
    {
        if (!site.Value.RegisterEnabled) return new LegacyRedirectResult("/");
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        if (!LegacyForm.Validate(ModelState, form, RegisterFields)) return View("Register");
        var (user, errors) = await CreateUserAsync(users, settings, accounts, form, HttpContext.RequestAborted);
        if (user == null)
        {
            foreach (var e in errors) ModelState.AddModelError("", e);
            return View("Register");
        }
        await signIn.SignInAsync(user, isPersistent: false);
        return new LegacyRedirectResult("/");
    }

    /// <summary>Legacy UserController.Register / UserAdministrationController.New: the user, the default roles, the e-mail destination.</summary>
    internal static async Task<(TicketDeskUser? User, IReadOnlyList<string> Errors)> CreateUserAsync(UserManager<TicketDeskUser> users,
        IApplicationSettingsStore settings, IUserAccounts accounts, IFormCollection form, CancellationToken ct)
    {
        var user = new TicketDeskUser
        {
            UserName = form["UserName"].FirstOrDefault() ?? "",
            Email = form["Email"].FirstOrDefault(),
            DisplayName = form["DisplayName"].FirstOrDefault() ?? "",
            LockoutEnabled = true,
        };
        var result = await users.CreateAsync(user, form["Password"].FirstOrDefault() ?? "");
        if (!result.Succeeded) return (null, result.Errors.Select(e => e.Description).ToList());
        var roles = (await settings.GetAsync(ct)).SecuritySettings.DefaultNewUserRoles;
        await users.AddToRolesAsync(user, roles.Where(r => TicketDeskRoles.All.Contains(r)));
        await accounts.AddEmailDestinationAsync(user.Id, user.Email, user.DisplayName, ct);
        return (user, []);
    }

    // ---- Entra ID (OpenID Connect), a second sign-in option; the button is shown only when Authentication:EntraId:Enabled

    [HttpPost("sign-in/entra")]
    public IActionResult EntraChallenge([FromQuery(Name = "ReturnUrl")] string? returnUrl)
    {
        if (!auth.Value.EntraId.Enabled) return NotFound();
        var callback = "/user/sign-in/entra/callback" + (Url.IsLocalUrl(returnUrl) ? "?ReturnUrl=" + Uri.EscapeDataString(returnUrl!) : "");
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, EntraIdOptions.Scheme);
    }

    [HttpGet("sign-in/entra/callback")]
    public async Task<IActionResult> EntraCallback([FromServices] EntraAccountLinker linker, [FromQuery(Name = "ReturnUrl")] string? returnUrl)
    {
        var external = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!external.Succeeded || external.Principal == null) return new LegacyRedirectResult("/user/sign-in");
        var defaults = (await settings.GetAsync(HttpContext.RequestAborted)).SecuritySettings.DefaultNewUserRoles;
        var link = await linker.LinkOrProvisionAsync(external.Principal, defaults);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (link.Outcome == EntraLinkOutcome.Refused || link.User == null)
        {
            logger.LogWarning("Entra ID sign-in refused: {Reason}", link.Reason);
            if (link.Reason == "locked out") return View("Lockout");
            ModelState.AddModelError("", ControllerStrings.InvalidLoginAttempt);
            return View("SignIn", Page(null));
        }
        await signIn.SignInAsync(link.User, isPersistent: false, authenticationMethod: EntraIdOptions.Scheme);
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        new LegacyRedirectResult(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
}
