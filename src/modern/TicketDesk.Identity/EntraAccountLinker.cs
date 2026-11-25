using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketDesk.Data.Entities;

namespace TicketDesk.Identity;

public enum EntraLinkOutcome { Existing, Linked, Provisioned, Refused }

public sealed record EntraLinkResult(EntraLinkOutcome Outcome, TicketDeskUser? User, string? Reason = null, IReadOnlyList<string>? Roles = null);

/// <summary>
/// First and later Entra ID sign-ins: finds the <c>IdentityUsers</c> row for the Entra account (login provider
/// <see cref="LoginProvider"/>, key = the account's object id <c>oid</c>), or links an existing row with the same e-mail /
/// user name, or provisions a new row (no password) — so the legacy pages, which read the user from that table, work for
/// the account too. App roles in the token (values = legacy role names) set the user's legacy roles; without app roles a
/// new account gets the configured default roles (TdPendingUsers) and an existing one keeps its roles.
/// </summary>
/// <remarks>
/// Linking by e-mail is safe here because the app registration is single-tenant (AzureADMyOrg): only accounts of the
/// configured tenant get a token, and the tenant controls their addresses. A token from another tenant is refused.
/// </remarks>
public sealed class EntraAccountLinker(UserManager<TicketDeskUser> users, IOptions<TicketDeskAuthenticationOptions> options, ILogger<EntraAccountLinker> logger)
{
    public const string LoginProvider = "EntraId";

    public async Task<EntraLinkResult> LinkOrProvisionAsync(ClaimsPrincipal external, IReadOnlyCollection<string> defaultRoles)
    {
        var oid = external.FindFirstValue("oid") ?? external.FindFirstValue("sub");
        if (string.IsNullOrEmpty(oid)) return new(EntraLinkOutcome.Refused, null, "the token has no oid/sub claim");
        var tenant = options.Value.EntraId.TenantId;
        var tid = external.FindFirstValue("tid");
        if (!string.IsNullOrEmpty(tenant) && !string.Equals(tid, tenant, StringComparison.OrdinalIgnoreCase))
            return new(EntraLinkOutcome.Refused, null, "the account belongs to another tenant");

        var email = external.FindFirstValue("email");
        var upn = external.FindFirstValue("preferred_username");
        var name = external.FindFirstValue("name");
        var outcome = EntraLinkOutcome.Existing;
        var user = await users.FindByLoginAsync(LoginProvider, oid);
        if (user == null)
        {
            user = (email != null ? await users.FindByEmailAsync(email) : null)
                   ?? (upn != null ? await users.FindByNameAsync(upn) ?? await users.FindByEmailAsync(upn) : null);
            if (user != null)
            {
                outcome = EntraLinkOutcome.Linked;
            }
            else
            {
                user = new TicketDeskUser
                {
                    UserName = upn ?? email ?? oid,
                    Email = email ?? upn,
                    DisplayName = string.IsNullOrWhiteSpace(name) ? upn ?? email ?? oid : name,
                    LockoutEnabled = true,
                    SecurityStamp = Guid.NewGuid().ToString(),
                };
                var created = await users.CreateAsync(user);
                if (!created.Succeeded) return new(EntraLinkOutcome.Refused, null, string.Join(" ", created.Errors.Select(e => e.Description)));
                outcome = EntraLinkOutcome.Provisioned;
            }
            var added = await users.AddLoginAsync(user, new UserLoginInfo(LoginProvider, oid, LoginProvider));
            if (!added.Succeeded) return new(EntraLinkOutcome.Refused, null, string.Join(" ", added.Errors.Select(e => e.Description)));
        }

        var appRoles = DirectRoles(external.FindAll("roles").Select(c => c.Value).Where(r => TicketDeskRoles.All.Contains(r)).Distinct().ToList());
        var current = await users.GetRolesAsync(user);
        IEnumerable<string> wanted = appRoles.Count > 0 ? appRoles
            : outcome == EntraLinkOutcome.Provisioned ? defaultRoles.Where(r => TicketDeskRoles.All.Contains(r)) : current;
        var wantedSet = wanted.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = wantedSet.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var toRemove = current.Where(r => TicketDeskRoles.All.Contains(r, StringComparer.OrdinalIgnoreCase) && !wantedSet.Contains(r)).ToList();
        if (toAdd.Count > 0) await users.AddToRolesAsync(user, toAdd);
        if (toRemove.Count > 0) await users.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count + toRemove.Count > 0 && outcome != EntraLinkOutcome.Provisioned)
            await users.UpdateSecurityStampAsync(user); // cookies issued with the old roles are re-validated

        if (await users.IsLockedOutAsync(user)) return new(EntraLinkOutcome.Refused, user, "locked out");
        logger.LogInformation("Entra ID sign-in: {Outcome} user {UserId} (roles {Roles})", outcome, user.Id, string.Join(",", wantedSet));
        return new(outcome, user, null, wantedSet.Order().ToList());
    }

    /// <summary>
    /// Only the roles the hierarchy does not already imply (the legacy app stores the direct role; admin ⊃ help desk ⊃
    /// internal). The external principal has been through <see cref="LegacyRoleHierarchy"/> too, which adds implied ones.
    /// </summary>
    internal static List<string> DirectRoles(List<string> roles)
    {
        if (roles.Contains(TicketDeskRoles.Administrators)) roles.RemoveAll(r => r is TicketDeskRoles.HelpDeskUsers or TicketDeskRoles.InternalUsers);
        else if (roles.Contains(TicketDeskRoles.HelpDeskUsers)) roles.Remove(TicketDeskRoles.InternalUsers);
        return roles;
    }
}
