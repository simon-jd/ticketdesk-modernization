using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;

namespace TicketDesk.Tickets.Settings;

/// <summary>
/// The parts of the legacy <c>ApplicationSettings</c> row the ticket pages use (EF6 complex types serialized as JSON with
/// Newtonsoft; missing values fall back to the legacy constructors' defaults).
/// </summary>
public sealed record TicketDeskSettings(
    IReadOnlyList<string> CategoryList,
    IReadOnlyList<string> PriorityList,
    IReadOnlyList<string> TicketTypesList,
    bool AllowInternalUsersToEditPriority,
    bool AllowInternalUsersToEditTags,
    bool AllowInternalUsersToSetAssigned,
    bool AllowInternalUsersToSetOwner,
    string DefaultTextEditorType)
{
    /// <summary>Legacy <c>ClientSettings.GetDefaultTextEditorType() == "summernote"</c>: new text is HTML.</summary>
    public bool IsEditorDefaultHtml => DefaultTextEditorType == "summernote";

    public static async Task<TicketDeskSettings> LoadAsync(TicketDeskDbContext db, CancellationToken ct)
    {
        var row = await db.ApplicationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.ApplicationName == ApplicationSetting.TicketDeskApplicationName, ct);
        return Parse(row);
    }

    public static TicketDeskSettings Parse(ApplicationSetting? row)
    {
        var lists = Read(row?.SelectListSettingsJson);
        var perms = Read(row?.PermissionsSettingsJson);
        var client = Read(row?.ClientSettingsJson);
        string editor = "summernote";
        if (client is { } c && c.TryGetProperty("Settings", out var settings) && settings.ValueKind == JsonValueKind.Object &&
            settings.TryGetProperty("DefaultTextEditorType", out var e) && e.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(e.GetString()))
            editor = e.GetString()!;
        return new TicketDeskSettings(
            List(lists, "CategoryList", ["Hardware", "Software", "Network"]),
            List(lists, "PriorityList", ["High", "Medium", "Low"]),
            List(lists, "TicketTypesList", ["Question", "Problem", "Request"]),
            Bool(perms, "AllowInternalUsersToEditPriority", false),
            Bool(perms, "AllowInternalUsersToEditTags", true),
            Bool(perms, "AllowInternalUsersToSetAssigned", false),
            Bool(perms, "AllowInternalUsersToSetOwner", false),
            editor);
    }

    private static JsonElement? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<string> List(JsonElement? e, string name, string[] fallback) =>
        e is { } o && o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
            : fallback;

    private static bool Bool(JsonElement? e, string name, bool fallback) =>
        e is { } o && o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? b.GetBoolean()
            : fallback;
}
