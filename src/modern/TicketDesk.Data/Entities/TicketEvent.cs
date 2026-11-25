namespace TicketDesk.Data.Entities;

/// <summary>Row of <c>dbo.TicketEvents</c>: key (TicketId, EventId) where EventId is an identity column.</summary>
public class TicketEvent
{
    public int TicketId { get; set; }
    public int EventId { get; set; }
    public string? EventDescription { get; set; }
    public string? Comment { get; set; }
    public bool IsHtml { get; set; }
    public string EventBy { get; set; } = "";
    /// <summary>Database default <c>SYSDATETIMEOFFSET()</c>; never written by EF (EF6 <c>DatabaseGenerated(Computed)</c>).</summary>
    public DateTimeOffset EventDate { get; set; }
    public TicketActivity ForActivity { get; set; }
    public byte[] Version { get; set; } = [];

    public Ticket? Ticket { get; set; }
    public ICollection<TicketEventNotification> TicketEventNotifications { get; set; } = new List<TicketEventNotification>();
}
