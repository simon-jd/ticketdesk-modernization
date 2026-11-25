namespace TicketDesk.Data.Entities;

public class TicketEventNotification
{
    public int TicketId { get; set; }
    public int EventId { get; set; }
    public string SubscriberId { get; set; } = "";
    public bool IsNew { get; set; }
    public bool IsRead { get; set; }
    public byte[] Version { get; set; } = [];

    public TicketEvent? TicketEvent { get; set; }
    public TicketSubscriber? TicketSubscriber { get; set; }
}
