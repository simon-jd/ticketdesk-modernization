namespace TicketDesk.Data.Entities;

public class Project
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string? ProjectDescription { get; set; }
    public byte[] Version { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
