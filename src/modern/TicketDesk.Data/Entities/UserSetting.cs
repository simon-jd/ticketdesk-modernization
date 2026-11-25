namespace TicketDesk.Data.Entities;

/// <summary>Row of <c>dbo.UserSettings</c>. <see cref="ListSettingsJson"/> was an EF6 complex type serialized to JSON.</summary>
public class UserSetting
{
    public string UserId { get; set; } = "";
    public string? ListSettingsJson { get; set; }
    public int? SelectedProjectId { get; set; }
}
