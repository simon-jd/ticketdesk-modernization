using Microsoft.AspNetCore.Authorization;
using TicketDesk.Identity;

namespace TicketDesk.Tickets;

/// <summary>
/// Per-ticket attachment access (fixes S8). Legacy: any internal user could list and download any ticket's files.
/// New rule: help desk and admins see all; other users only tickets they own, created, are assigned to or subscribe to.
/// </summary>
public sealed class TicketAttachmentsRequirement : IAuthorizationRequirement
{
    public static readonly TicketAttachmentsRequirement Instance = new();
}

public sealed record TicketAccessResource(string Owner, string? AssignedTo, string CreatedBy, IReadOnlyCollection<string> SubscriberIds);

public sealed class TicketAttachmentsHandler : AuthorizationHandler<TicketAttachmentsRequirement, TicketAccessResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TicketAttachmentsRequirement requirement, TicketAccessResource resource)
    {
        var user = context.User;
        if (user.IsHelpDeskOrAdmin())
        {
            context.Succeed(requirement);
        }
        else if (user.IsInRole(TicketDeskRoles.InternalUsers) && user.GetUserId() is { } id &&
                 (Same(id, resource.Owner) || Same(id, resource.AssignedTo) || Same(id, resource.CreatedBy) ||
                  resource.SubscriberIds.Any(s => Same(id, s))))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }

    private static bool Same(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
