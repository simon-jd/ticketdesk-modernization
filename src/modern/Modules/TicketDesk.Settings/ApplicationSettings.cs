using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;

namespace TicketDesk.Settings;

/// <summary>Legacy <c>ApplicationPermissionsSetting</c> (defaults from its constructor).</summary>
public sealed class PermissionsSettings
{
    public bool AllowInternalUsersToEditPriority { get; set; }
    public bool AllowInternalUsersToEditTags { get; set; } = true;
    public bool AllowInternalUsersToSetAssigned { get; set; }
    public bool AllowInternalUsersToSetOwner { get; set; }
}

/// <summary>Legacy <c>ApplicationSelectListSetting</c>.</summary>
public sealed class SelectListSettings
{
    public List<string> CategoryList { get; set; } = ["Software", "Hardware", "Network"];
    public List<string> PriorityList { get; set; } = ["High", "Medium", "Low"];
    public List<string> TicketTypesList { get; set; } = ["Problem", "Question", "Request"];
}

/// <summary>Legacy <c>ApplicationSecuritySetting</c>: TdPendingUsers is always among the default roles.</summary>
public sealed class SecuritySettings
{
    private List<string> _roles = ["TdPendingUsers"];

    public List<string> DefaultNewUserRoles
    {
        get => _roles;
        set
        {
            var v = value ?? [];
            if (!v.Contains("TdPendingUsers")) v.Add("TdPendingUsers");
            _roles = v;
        }
    }

    public string? DefaultLogonDomain { get; set; }
}

/// <summary>Legacy <c>ClientSetting</c>: a string dictionary (DefaultTextEditorType, DefaultSiteRootUrl).</summary>
public sealed class ClientSettings
{
    public Dictionary<string, string> Settings { get; set; } = new();

    public string DefaultTextEditorType => Settings.GetValueOrDefault("DefaultTextEditorType") ?? "summernote";
    public string DefaultSiteRootUrl => Settings.GetValueOrDefault("DefaultSiteRootUrl") ?? "";
}

/// <summary>The <c>dbo.ApplicationSettings</c> row <c>TicketDesk</c> with its four JSON columns, as the legacy domain model read it.</summary>
public sealed class TicketDeskSettings
{
    public PermissionsSettings Permissions { get; set; } = new();
    public SelectListSettings SelectLists { get; set; } = new();
    public SecuritySettings SecuritySettings { get; set; } = new();
    public ClientSettings ClientSettings { get; set; } = new();
}

public interface IApplicationSettingsStore
{
    Task<TicketDeskSettings> GetAsync(CancellationToken ct);

    /// <summary>Writes all four JSON columns in the legacy (Newtonsoft default) format; creates the row when missing.</summary>
    Task SaveAsync(TicketDeskSettings settings, CancellationToken ct);
}

internal sealed class ApplicationSettingsStore(TicketDeskDbContext db) : IApplicationSettingsStore
{
    // Newtonsoft's defaults as the legacy Serialized properties used them: PascalCase names, nulls written
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    internal static T Read<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try { return JsonSerializer.Deserialize<T>(json, Json) ?? new T(); }
        catch (JsonException) { return new T(); }
    }

    public async Task<TicketDeskSettings> GetAsync(CancellationToken ct)
    {
        var row = await db.ApplicationSettings.AsNoTracking().SingleOrDefaultAsync(s => s.ApplicationName == ApplicationSetting.TicketDeskApplicationName, ct);
        return new TicketDeskSettings
        {
            Permissions = Read<PermissionsSettings>(row?.PermissionsSettingsJson),
            SelectLists = Read<SelectListSettings>(row?.SelectListSettingsJson),
            SecuritySettings = Read<SecuritySettings>(row?.SecuritySettingsJson),
            ClientSettings = Read<ClientSettings>(row?.ClientSettingsJson),
        };
    }

    public async Task SaveAsync(TicketDeskSettings settings, CancellationToken ct)
    {
        var row = await db.ApplicationSettings.SingleOrDefaultAsync(s => s.ApplicationName == ApplicationSetting.TicketDeskApplicationName, ct);
        if (row == null) db.ApplicationSettings.Add(row = new ApplicationSetting());
        row.PermissionsSettingsJson = JsonSerializer.Serialize(settings.Permissions);
        row.SelectListSettingsJson = JsonSerializer.Serialize(settings.SelectLists);
        row.SecuritySettingsJson = JsonSerializer.Serialize(new { settings.SecuritySettings.DefaultNewUserRoles, settings.SecuritySettings.DefaultLogonDomain });
        row.ClientSettingsJson = JsonSerializer.Serialize(new { settings.ClientSettings.Settings });
        await db.SaveChangesAsync(ct);
    }
}
