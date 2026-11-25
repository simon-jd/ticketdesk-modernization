namespace TicketDesk.Data.Entities;

/// <summary>Row of <c>dbo.ApplicationSettings</c> (one row, <c>TicketDesk</c>). The JSON columns were EF6 complex types.</summary>
public class ApplicationSetting
{
    public const string TicketDeskApplicationName = "TicketDesk";
    public string ApplicationName { get; set; } = TicketDeskApplicationName;
    public string? PermissionsSettingsJson { get; set; }
    public string? SelectListSettingsJson { get; set; }
    public string? SecuritySettingsJson { get; set; }
    public string? ClientSettingsJson { get; set; }
}
