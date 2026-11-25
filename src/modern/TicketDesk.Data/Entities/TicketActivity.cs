namespace TicketDesk.Data.Entities;

/// <summary>Flags stored as int in <c>TicketEvents.ForActivity</c> (values from the legacy enum).</summary>
[Flags]
public enum TicketActivity
{
    None = 0,
    AddComment = 1,
    SupplyMoreInfo = 2,
    CancelMoreInfo = 4,
    RequestMoreInfo = 8,
    TakeOver = 16,
    Resolve = 32,
    Assign = 64,
    ReAssign = 128,
    Pass = 256,
    Close = 512,
    ReOpen = 1024,
    GiveUp = 2048,
    ForceClose = 4096,
    ModifyAttachments = 8192,
    EditTicketInfo = 16384,
    Create = 32768,
    CreateOnBehalfOf = 65536,
}
