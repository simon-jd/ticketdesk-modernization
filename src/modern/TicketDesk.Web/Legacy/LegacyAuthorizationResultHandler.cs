using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Legacy;

/// <summary>
/// For migrated pages, a signed-in user without the role gets what the legacy <c>TdAuthorizeAttribute</c> sent: a 302 to
/// <c>/access-denied</c> (no ReturnUrl) with the ASP.NET "Object moved" body, whichever scheme authenticated the user
/// (Identity cookie or remote authentication). Anonymous users are challenged as usual (302 to <c>/user/sign-in</c>).
/// </summary>
public sealed class LegacyAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var migrated = context.GetEndpoint()?.Metadata.GetMetadata<MigratedRouteAttribute>() != null;
        if (migrated && authorizeResult.Succeeded) context.Items[MigrationRegistration.LegacyAuthorizedItem] = true;
        if (migrated && authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
            return new LegacyRedirectResult("/access-denied").ExecuteAsync(context);
        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
