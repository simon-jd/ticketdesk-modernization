using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Identity;

/// <summary>What the sign-in page tells the user after Entra ID sent them back without a sign-in.</summary>
public enum EntraSignInFailureKind
{
    /// <summary>Any remote failure: an error response, a lost correlation cookie, an invalid state or token.</summary>
    Failed,
    /// <summary><c>access_denied</c> or a consent error: an administrator may need to approve the app in the tenant.</summary>
    ConsentRequired,
}

/// <summary>
/// Remote failures of the Entra ID (OpenID Connect) scheme, e.g. <c>access_denied</c> / AADSTS65004 at the callback when
/// the tenant requires admin approval and the user cancels (docs/evidence/identity-20261110/step8-entra-user-check).
/// Without a handler the <c>AuthenticationFailureException</c> reached the exception handler and the user saw the error
/// page. Now: one warning with the OIDC <c>error</c> and the AADSTS code (never tokens, codes, state or cookies), a one-time
/// cookie holding only the failure kind, and a redirect to the sign-in page that keeps a local ReturnUrl only.
/// </summary>
public static partial class EntraSignInFailure
{
    public const string CookieName = "TicketDesk.EntraSignIn";
    public const string LogCategory = "TicketDesk.Identity.EntraSignIn";

    // 65001 consent not granted, 65004 user declined consent, 90094 admin approval required, 90095 admin consent
    // workflow (request approval), 900941 admin consent required
    private static readonly HashSet<string> ConsentCodes = ["AADSTS65001", "AADSTS65004", "AADSTS90094", "AADSTS90095", "AADSTS900941"];

    [GeneratedRegex(@"\bAADSTS\d{4,7}\b")]
    private static partial Regex AadstsRx();

    public static Task HandleAsync(RemoteFailureContext ctx)
    {
        var (error, description) = ProtocolError(ctx.Failure);
        var code = description != null && AadstsRx().Match(description) is { Success: true } m ? m.Value : null;
        var kind = error is "access_denied" or "consent_required" || (code != null && ConsentCodes.Contains(code))
            ? EntraSignInFailureKind.ConsentRequired
            : EntraSignInFailureKind.Failed;
        ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LogCategory).LogWarning(
            "Entra ID sign-in did not complete: error {OidcError}, {AadstsCode} ({FailureType})",
            error ?? "none", code ?? "no AADSTS code", ctx.Failure?.GetType().Name ?? "none");

        var http = ctx.HttpContext;
        http.Response.Cookies.Append(CookieName, kind.ToString(), FailureCookie(http, TimeSpan.FromMinutes(5)));
        var returnUrl = LocalReturnUrl(ctx.Properties?.RedirectUri);
        http.Response.Redirect(SignInPath(http) + (returnUrl == null ? "" : "?ReturnUrl=" + Uri.EscapeDataString(returnUrl)));
        ctx.HandleResponse();
        return Task.CompletedTask;
    }

    /// <summary>Reads and deletes the one-time cookie. Null without it: the sign-in page is then unchanged.</summary>
    public static EntraSignInFailureKind? Take(HttpContext http)
    {
        if (!http.Request.Cookies.TryGetValue(CookieName, out var value)) return null;
        http.Response.Cookies.Delete(CookieName, FailureCookie(http, null));
        return Enum.TryParse<EntraSignInFailureKind>(value, out var kind) && Enum.IsDefined(kind) ? kind : null;
    }

    /// <summary>The OIDC <c>error</c> (only if it is a plain token) and <c>error_description</c> of an error response.</summary>
    private static (string? Error, string? Description) ProtocolError(Exception? failure)
    {
        for (var e = failure; e != null; e = e.InnerException)
        {
            if (e.Data["error"] is not string error) continue;
            var plain = error.Length is > 0 and <= 64 && error.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
            return (plain ? error : "unrecognized", e.Data["error_description"] as string);
        }
        return (null, null);
    }

    /// <summary>The ReturnUrl of the challenge's callback (UserController.EntraChallenge), when it is a local path.</summary>
    private static string? LocalReturnUrl(string? redirectUri)
    {
        var q = redirectUri?.IndexOf('?') ?? -1;
        if (q < 0) return null;
        var url = QueryHelpers.ParseQuery(redirectUri![q..]).TryGetValue("ReturnUrl", out var v) ? v.FirstOrDefault() : null;
        // UrlHelper.IsLocalUrl for "/path": not "//host" or "/\host", no control characters
        return url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) && !url.Any(char.IsControl) ? url : null;
    }

    private static string SignInPath(HttpContext http) => http.Request.PathBase.Add(IdentityServiceCollectionExtensions.SignInPath).ToString();

    private static CookieOptions FailureCookie(HttpContext http, TimeSpan? maxAge) => new()
    {
        Path = SignInPath(http),
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        MaxAge = maxAge,
    };
}
