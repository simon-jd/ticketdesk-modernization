using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TicketDesk.Identity;

/// <summary>
/// Authorization policies mapped to the legacy <c>[TdAuthorize(Roles = ...)]</c> role lists
/// (see docs/migration/routes.csv, column <c>auth</c>).
/// </summary>
public static class TicketDeskPolicies
{
    /// <summary>Ticket center, tickets, files, search: <c>TdInternalUsers|TdHelpDeskUsers|TdAdministrators</c>.</summary>
    public const string InternalUser = "TicketDesk.InternalUser";

    /// <summary>Help-desk staff: <c>TdHelpDeskUsers|TdAdministrators</c>.</summary>
    public const string HelpDesk = "TicketDesk.HelpDesk";

    /// <summary>Administration pages: <c>TdAdministrators</c>.</summary>
    public const string Administrator = "TicketDesk.Administrator";

    public static AuthorizationBuilder AddTicketDeskPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(InternalUser, p => p.RequireAuthenticatedUser()
            .RequireRole(TicketDeskRoles.InternalUsers, TicketDeskRoles.HelpDeskUsers, TicketDeskRoles.Administrators))
        .AddPolicy(HelpDesk, p => p.RequireAuthenticatedUser()
            .RequireRole(TicketDeskRoles.HelpDeskUsers, TicketDeskRoles.Administrators))
        .AddPolicy(Administrator, p => p.RequireAuthenticatedUser()
            .RequireRole(TicketDeskRoles.Administrators));

    /// <summary>Legacy <c>TicketDeskUserManager.IsTdHelpDeskUser</c>: help desk or admin.</summary>
    public static bool IsHelpDeskOrAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(TicketDeskRoles.HelpDeskUsers) || user.IsInRole(TicketDeskRoles.Administrators);

    public static string? GetUserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
