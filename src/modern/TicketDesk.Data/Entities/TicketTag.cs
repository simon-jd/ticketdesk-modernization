namespace TicketDesk.Data.Entities;

public class TicketTag
{
    public int TicketTagId { get; set; }
    public string TagName { get; set; } = "";
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
}
