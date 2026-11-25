using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace TicketDesk.Identity;

/// <summary>
/// The legacy role hierarchy (<c>TicketDeskUserManager.IsTd*</c>): an administrator is also help desk, help desk is also an
/// internal user. The legacy schema stores only the direct role (the hierarchy was code), so a principal built from the
/// database (or from an Entra ID app role) carries one role claim. This adds the implied role claims (idempotent) for every
/// scheme, so <c>IsInRole</c> answers as the legacy checks did.
/// </summary>
public sealed class LegacyRoleHierarchy : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity) return Task.FromResult(principal);
        var implied = new List<string>();
        if (principal.IsInRole(TicketDeskRoles.Administrators)) implied.AddRange([TicketDeskRoles.HelpDeskUsers, TicketDeskRoles.InternalUsers]);
        else if (principal.IsInRole(TicketDeskRoles.HelpDeskUsers)) implied.Add(TicketDeskRoles.InternalUsers);
        var missing = implied.Where(r => !principal.IsInRole(r)).ToList();
        if (missing.Count == 0) return Task.FromResult(principal);
        // a new principal: transformations may run more than once per request and must not mutate a cached one
        // (ClaimsPrincipal.Clone shares the identity objects, so clone each identity)
        var clone = new ClaimsPrincipal(principal.Identities.Select(i => i.Clone()));
        var target = (ClaimsIdentity)clone.Identity!;
        foreach (var role in missing) target.AddClaim(new Claim(target.RoleClaimType, role));
        return Task.FromResult(clone);
    }
}
