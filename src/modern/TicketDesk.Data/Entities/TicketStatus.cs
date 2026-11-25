namespace TicketDesk.Data.Entities;

/// <summary>Stored as int in <c>Tickets.TicketStatus</c> (values from the legacy enum).</summary>
public enum TicketStatus
{
    Active = 0,
    MoreInfo = 1,
    Resolved = 2,
    Closed = 3,
}
