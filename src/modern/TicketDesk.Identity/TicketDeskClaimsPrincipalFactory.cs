using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity;

/// <summary>
/// Roles as role claims (base class) plus the display name as <c>GivenName</c>, like the legacy <c>GenerateUserIdentityAsync</c>.
/// (Until Round 4 it also added ASP.NET Identity 2's identity-provider claim, which only MVC 5's antiforgery on the legacy
/// pages needed for the shared sign-in cookie.)
/// </summary>
public sealed class TicketDeskClaimsPrincipalFactory(
    UserManager<TicketDeskUser> userManager,
    RoleManager<TicketDeskRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<TicketDeskUser, TicketDeskRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TicketDeskUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        return identity;
    }
}
