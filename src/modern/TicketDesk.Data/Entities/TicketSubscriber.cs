namespace TicketDesk.Data.Entities;

public class TicketSubscriber
{
    public int TicketId { get; set; }
    public string SubscriberId { get; set; } = "";
    public Ticket? Ticket { get; set; }
}
