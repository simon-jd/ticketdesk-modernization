using TicketDesk.Data.Entities;

namespace TicketDesk.Tickets;

/// <summary>
/// The five default ticket-center lists of the legacy app (<c>UserTicketListSetting.GetDefaultListSettings</c>): their
/// filters and default sorts. The legacy app stored them per user and let users change filters and sorts; here a list is
/// a fixed starting point that explicit query parameters narrow further.
/// </summary>
public static class TicketLists
{
    public sealed record ListDefinition(string Name, Func<IQueryable<Ticket>, string, IQueryable<Ticket>> Filter, IReadOnlyList<SortKey> DefaultSort);

    private static readonly SortKey TargetAsc = new("targetDate", false);
    private static readonly SortKey DueAsc = new("dueDate", false);
    private static readonly SortKey UpdatedDesc = new("lastUpdateDate", true);
    private static readonly SortKey StatusAsc = new("status", false);

    public static readonly IReadOnlyList<ListDefinition> All =
    [
        // legacy filter: AssignedTo = null through Entity SQL; see docs/migration/ef6-to-efcore.md (null comparison)
        new("unassigned", (q, _) => q.Where(t => t.TicketStatus != TicketStatus.Closed && t.AssignedTo == null), [TargetAsc, DueAsc, UpdatedDesc]),
        new("assignedToMe", (q, me) => q.Where(t => t.TicketStatus != TicketStatus.Closed && t.AssignedTo == me), [StatusAsc, TargetAsc, DueAsc, UpdatedDesc]),
        new("mytickets", (q, me) => q.Where(t => t.TicketStatus != TicketStatus.Closed && t.Owner == me), [TargetAsc, DueAsc, UpdatedDesc]),
        new("opentickets", (q, _) => q.Where(t => t.TicketStatus != TicketStatus.Closed), [StatusAsc, TargetAsc, DueAsc, UpdatedDesc]),
        new("historytickets", (q, _) => q.Where(t => t.TicketStatus == TicketStatus.Closed), [StatusAsc, UpdatedDesc]),
    ];

    public static readonly IReadOnlyList<SortKey> DefaultSort = [UpdatedDesc];

    /// <summary>
    /// The lists a user has in the ticket center, in the legacy menu order (<c>UserTicketListSetting.GetDefaultListSettings</c>):
    /// help desk and admins all five, other internal users mytickets, opentickets and historytickets.
    /// </summary>
    public static IReadOnlyList<ListDefinition> ForUser(bool isHelpDeskOrAdmin) =>
        isHelpDeskOrAdmin ? All : [.. All.Where(l => l.Name is not ("unassigned" or "assignedToMe"))];

    /// <summary>Legacy default list: unassigned for help desk and admins, mytickets for everybody else.</summary>
    public static string DefaultFor(bool isHelpDeskOrAdmin) => isHelpDeskOrAdmin ? "unassigned" : "mytickets";

    public static ListDefinition? Find(string? name) =>
        All.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
}
