namespace TicketDesk.Data.Entities;

/// <summary>Row of <c>dbo.Tickets</c>. User columns hold <c>IdentityUsers.Id</c> strings without foreign keys.</summary>
public class Ticket
{
    public int TicketId { get; set; }
    public int ProjectId { get; set; }
    public string TicketType { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public bool IsHtml { get; set; }
    public string? TagList { get; set; }
    public string CreatedBy { get; set; } = "";
    /// <summary>Database default <c>SYSDATETIMEOFFSET()</c>; never written by EF (EF6 <c>DatabaseGenerated(Computed)</c>).</summary>
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public DateTimeOffset? ResolutionDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public decimal? EstimatedDuration { get; set; }
    public decimal? ActualDuration { get; set; }
    public string Owner { get; set; } = "";
    public string? AssignedTo { get; set; }
    /// <summary>
    /// When the ticket got its current assignee (expand migration V0001/V0002, not in the legacy schema). While the legacy
    /// app writes, trigger <c>TR_Tickets_AssignedDate</c> sets it for statements that change AssignedTo without it.
    /// </summary>
    public DateTimeOffset? AssignedDate { get; set; }
    public TicketStatus TicketStatus { get; set; }
    public DateTimeOffset CurrentStatusDate { get; set; }
    public string CurrentStatusSetBy { get; set; } = "";
    public string LastUpdateBy { get; set; } = "";
    public DateTimeOffset LastUpdateDate { get; set; }
    public string? Priority { get; set; }
    public bool AffectsCustomer { get; set; }
    public byte[] Version { get; set; } = [];

    public Project? Project { get; set; }
    public ICollection<TicketEvent> TicketEvents { get; set; } = new List<TicketEvent>();
    public ICollection<TicketTag> TicketTags { get; set; } = new List<TicketTag>();
    public ICollection<TicketSubscriber> TicketSubscribers { get; set; } = new List<TicketSubscriber>();
}
