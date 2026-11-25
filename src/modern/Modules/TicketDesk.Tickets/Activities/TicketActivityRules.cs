// Port of Ticket.GetAvailableActivites and TdDomainSecurityProviderBase.GetValidTicketActivities (TicketDesk 2.5, Ms-PL;
// attribution: Stephen Redd, https://github.com/stephenredd).
using System.Security.Claims;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;

namespace TicketDesk.Tickets.Activities;

/// <summary>Who acts on a ticket: the user id and the legacy role checks (admin ⊃ help desk ⊃ internal user).</summary>
public sealed record TicketActor(string UserId, bool IsAdministrator, bool IsHelpDesk, bool IsInternal)
{
    public static TicketActor From(ClaimsPrincipal user)
    {
        var admin = user.IsInRole(TicketDeskRoles.Administrators);
        var helpDesk = admin || user.IsInRole(TicketDeskRoles.HelpDeskUsers);
        return new TicketActor(user.GetUserId() ?? "", admin, helpDesk, helpDesk || user.IsInRole(TicketDeskRoles.InternalUsers));
    }
}

/// <summary>Which activities a user may perform on a ticket, as the legacy domain decided it.</summary>
public static class TicketActivityRules
{
    /// <summary>Activities whose form marks the comment as required (legacy <c>[CommentRequired]</c>; not enforced on the server, as before).</summary>
    public static bool IsCommentRequired(TicketActivity activity) => activity is TicketActivity.AddComment or TicketActivity.SupplyMoreInfo
        or TicketActivity.RequestMoreInfo or TicketActivity.Resolve or TicketActivity.ReOpen or TicketActivity.GiveUp or TicketActivity.ForceClose;

    private const TicketActivity InternalUser = TicketActivity.ModifyAttachments | TicketActivity.EditTicketInfo | TicketActivity.SupplyMoreInfo |
        TicketActivity.ReOpen | TicketActivity.Create | TicketActivity.AddComment | TicketActivity.Close | TicketActivity.ForceClose;

    private const TicketActivity HelpDeskUser = InternalUser | TicketActivity.RequestMoreInfo | TicketActivity.CancelMoreInfo |
        TicketActivity.CreateOnBehalfOf | TicketActivity.TakeOver | TicketActivity.Assign | TicketActivity.ReAssign | TicketActivity.Pass |
        TicketActivity.GiveUp | TicketActivity.Resolve;

    public static bool IsOpen(TicketStatus status) => status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>Legacy <c>Ticket.GetAvailableActivites(userId)</c>: the workflow state machine, before role permissions.</summary>
    public static TicketActivity Available(Ticket t, string? userId)
    {
        var isOwnedByMe = t.Owner == userId;
        var isMoreInfo = t.TicketStatus == TicketStatus.MoreInfo;
        var isAssignedToMe = !string.IsNullOrEmpty(t.AssignedTo) && t.AssignedTo == userId;
        var isResolved = t.TicketStatus == TicketStatus.Resolved;
        var isOpen = IsOpen(t.TicketStatus);
        var isAssigned = !string.IsNullOrEmpty(t.AssignedTo);
        var a = TicketActivity.None;
        if (t.TicketId == 0) a |= TicketActivity.Create | TicketActivity.CreateOnBehalfOf;
        if (isOpen)
        {
            a |= TicketActivity.ModifyAttachments;
            if (isOwnedByMe || isAssignedToMe) a |= TicketActivity.EditTicketInfo;
            if (isMoreInfo)
            {
                a |= TicketActivity.SupplyMoreInfo;
                if (isAssignedToMe) a |= TicketActivity.CancelMoreInfo;
            }
            else
            {
                a |= TicketActivity.AddComment;
                if (isAssignedToMe) a |= TicketActivity.Resolve | TicketActivity.RequestMoreInfo;
            }
        }
        else
        {
            a |= TicketActivity.ReOpen;
        }
        if (isResolved && isOwnedByMe) a |= TicketActivity.Close;
        if (isOpen || isResolved)
        {
            if (isAssigned)
            {
                if (!isAssignedToMe) a |= TicketActivity.ReAssign;
            }
            else
            {
                a |= TicketActivity.Assign;
            }
            if ((isAssignedToMe || isOwnedByMe) && !(isResolved && isOwnedByMe)) a |= TicketActivity.ForceClose;
            a |= isAssignedToMe ? TicketActivity.Pass | TicketActivity.GiveUp : TicketActivity.TakeOver;
        }
        return a;
    }

    /// <summary>Legacy <c>GetValidTicketActivities</c>: the state machine masked by the role's permissions (admins also act as the owner).</summary>
    public static TicketActivity Valid(Ticket t, TicketActor actor)
    {
        if (!actor.IsInternal) return TicketActivity.None; // legacy threw a SecurityException ("not authorized to read ticket data")
        var valid = Available(t, actor.UserId);
        var allowed = actor.IsHelpDesk ? HelpDeskUser : InternalUser; // admin = internal | help desk = help desk
        if (actor.IsAdministrator) valid |= Available(t, t.Owner);
        return valid & allowed;
    }

    public static bool IsValid(Ticket t, TicketActor actor, TicketActivity activity) => (Valid(t, actor) & activity) == activity;

    /// <summary>Legacy <c>EnumExtensions.GetFlags</c>: the set flags in ascending value order, without None.</summary>
    public static IEnumerable<TicketActivity> Flags(TicketActivity value) =>
        Enum.GetValues<TicketActivity>().Where(f => f != TicketActivity.None && value.HasFlag(f));
}
