using System.ComponentModel;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Settings;
using TicketDesk.Tickets.Writes;
using TicketDesk.Users;

namespace TicketDesk.Tickets;

/// <summary>Body of <c>POST /api/tickets/{id}/activities</c>: one workflow activity, as the legacy activity forms posted it.</summary>
public sealed class TicketActivityRequest
{
    [Description("One of the activities in the ticket's `activities` list (GET /api/tickets/{id}).")]
    public string? Activity { get; set; }
    [Description("Markdown. Required for AddComment, SupplyMoreInfo, RequestMoreInfo, Resolve, ReOpen, GiveUp and ForceClose.")]
    public string? Comment { get; set; }
    [Description("Assign, ReAssign, Pass: the help-desk user to assign (required).")]
    public string? AssignedTo { get; set; }
    [Description("Assign, ReAssign, Pass, TakeOver: a new priority (optional).")]
    public string? Priority { get; set; }
    [Description("ReOpen: assign the reopened ticket to the caller.")]
    public bool AssignToMe { get; set; }
    [Description("SupplyMoreInfo: set the ticket back to Active.")]
    public bool Reactivate { get; set; }
}

/// <summary>
/// The workflow activities the API offers: all of the legacy activity forms except EditTicketInfo and ModifyAttachments (they
/// edit fields and files; they stay on the server page). Validity per ticket and user is the legacy rule set
/// (<see cref="TicketActivityRules"/>); <see cref="TicketWriter"/> writes the same event, subscribers and notifications.
/// </summary>
public static class TicketActivitiesApi
{
    public static readonly IReadOnlyList<TicketActivity> Offered =
    [
        TicketActivity.AddComment, TicketActivity.SupplyMoreInfo, TicketActivity.Resolve, TicketActivity.RequestMoreInfo, TicketActivity.CancelMoreInfo,
        TicketActivity.TakeOver, TicketActivity.Assign, TicketActivity.ReAssign, TicketActivity.Pass, TicketActivity.GiveUp,
        TicketActivity.Close, TicketActivity.ForceClose, TicketActivity.ReOpen,
    ];

    /// <summary>The offered activities valid for this user on this ticket, in <see cref="Offered"/> order.</summary>
    public static IReadOnlyList<string> For(Ticket ticket, TicketActor actor)
    {
        var valid = TicketActivityRules.Valid(ticket, actor);
        return Offered.Where(a => (valid & a) == a).Select(a => a.ToString()).ToList();
    }

    public static TicketActivityInput? TryCreate(TicketActivityRequest r, TicketDeskSettings settings, IReadOnlyList<UserRef> helpDeskUsers,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (!Enum.TryParse<TicketActivity>(r.Activity, ignoreCase: false, out var activity) || !Offered.Contains(activity) || int.TryParse(r.Activity, out _))
        {
            errors["activity"] = [$"Unknown activity. Allowed: {string.Join(", ", Offered)}."];
            return null;
        }
        var comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment;
        if (TicketActivityRules.IsCommentRequired(activity) && comment == null) errors["comment"] = ["A comment is required for this activity."];
        var assignment = activity is TicketActivity.Assign or TicketActivity.ReAssign or TicketActivity.Pass;
        var assigned = string.IsNullOrWhiteSpace(r.AssignedTo) ? null : r.AssignedTo.Trim();
        if (assignment && assigned == null) errors["assignedTo"] = ["Choose the help-desk user to assign."];
        else if (assignment && !helpDeskUsers.Any(u => string.Equals(u.Id, assigned, StringComparison.OrdinalIgnoreCase)))
            errors["assignedTo"] = ["Tickets can only be assigned to help-desk users."];
        var priority = string.IsNullOrWhiteSpace(r.Priority) ? null : r.Priority.Trim();
        if (priority != null && !(assignment || activity == TicketActivity.TakeOver)) errors["priority"] = ["This activity does not set a priority."];
        else if (priority != null && !settings.PriorityList.Contains(priority)) errors["priority"] = ["Unknown priority."];
        if (errors.Count > 0) return null;
        return new TicketActivityInput(activity)
        {
            Comment = comment,
            AssignedTo = assignment ? assigned : null,
            Priority = priority,
            AssignToMe = activity == TicketActivity.ReOpen && r.AssignToMe,
            Reactivate = activity == TicketActivity.SupplyMoreInfo && r.Reactivate,
        };
    }
}
