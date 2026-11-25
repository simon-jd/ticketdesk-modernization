using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketDesk.Data;

namespace TicketDesk.Settings;

/// <summary>One configuration field of a delivery provider (legacy *DeliveryProviderConfiguration classes).</summary>
public sealed record ProviderField(string Name, ProviderFieldKind Kind, bool Required = false, bool Secret = false);

public enum ProviderFieldKind { Text, Number, Email, Password, Boolean }

/// <summary>
/// The two delivery providers of the legacy app (TicketDesk.PushNotifications.Delivery), identified by the assembly-qualified
/// type name the legacy settings JSON and URLs use. Secret fields (S6) never go to the database from the new app.
/// </summary>
public sealed record DeliveryProvider(string Key, string TypeName, IReadOnlyList<ProviderField> Fields, IReadOnlyDictionary<string, JsonNode?> Defaults)
{
    public string AssemblyQualifiedName => $"{TypeName}, TicketDesk.PushNotifications, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

    /// <summary>Short type name (the legacy view used it for the checkbox ids: SmtpDeliveryProvider_IsEnabled).</summary>
    public string ShortName => TypeName[(TypeName.LastIndexOf('.') + 1)..];

    public static readonly DeliveryProvider SendGrid = new("SendGrid", "TicketDesk.PushNotifications.Delivery.SendGridDeliveryProvider",
    [
        new("ApiKey", ProviderFieldKind.Password, Required: true, Secret: true),
        new("FromAddress", ProviderFieldKind.Email, Required: true),
        new("FromDisplayName", ProviderFieldKind.Text, Required: true),
        new("EnableClickTracking", ProviderFieldKind.Boolean),
        new("EnableOpenTracking", ProviderFieldKind.Boolean),
        new("EnableGravatar", ProviderFieldKind.Boolean),
        new("SendToSink", ProviderFieldKind.Boolean),
    ], new Dictionary<string, JsonNode?> { ["FromDisplayName"] = "TicketDesk", ["EnableClickTracking"] = false, ["EnableOpenTracking"] = false, ["EnableGravatar"] = false, ["SendToSink"] = false });

    public static readonly DeliveryProvider Smtp = new("Smtp", "TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider",
    [
        new("SmtpServer", ProviderFieldKind.Text, Required: true),
        new("SmtpPort", ProviderFieldKind.Number),
        new("EnableSsl", ProviderFieldKind.Boolean),
        new("SmtpUserName", ProviderFieldKind.Text),
        new("SmtpPassword", ProviderFieldKind.Password, Secret: true),
        new("SmtpFromAddress", ProviderFieldKind.Email, Required: true),
        new("SmtpFromDisplayName", ProviderFieldKind.Text, Required: true),
    ], new Dictionary<string, JsonNode?> { ["SmtpServer"] = "localhost", ["SmtpPort"] = 25, ["EnableSsl"] = false, ["SmtpFromDisplayName"] = "TicketDesk" });

    /// <summary>In the order the legacy provider list showed them (reflection over the loaded assembly: SendGrid, then SMTP).</summary>
    public static readonly IReadOnlyList<DeliveryProvider> All = [SendGrid, Smtp];

    public static DeliveryProvider? Find(string? assemblyQualifiedName) =>
        All.FirstOrDefault(p => string.Equals(p.AssemblyQualifiedName, assemblyQualifiedName, StringComparison.Ordinal)
                                || (assemblyQualifiedName != null && assemblyQualifiedName.Split(',')[0].Trim() == p.TypeName));
}

/// <summary>
/// S6: provider secrets come from configuration (user secrets or environment locally, Key Vault on Azure through the
/// configuration provider / CSI-mounted environment), keyed <c>PushNotifications:Secrets:&lt;provider&gt;:&lt;field&gt;</c>,
/// e.g. <c>PushNotifications__Secrets__Smtp__SmtpPassword</c>. The new app neither shows nor stores them.
/// </summary>
public sealed class PushNotificationSecretsOptions
{
    public const string SectionName = "PushNotifications:Secrets";
    public Dictionary<string, Dictionary<string, string>> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string Key(DeliveryProvider p, ProviderField f) => $"{SectionName}:{p.Key}:{f.Name}";
}

/// <summary>
/// <c>notifications.ApplicationPushNotificationSettings.PushNotificationSettingsJson</c> (row <c>TicketDesk</c>) as a JSON
/// tree: the new app changes only the fields its forms post and keeps everything else as the legacy app wrote it.
/// </summary>
public sealed class PushNotificationSettingsDocument(JsonObject root)
{
    public JsonObject Root { get; } = root;

    public static PushNotificationSettingsDocument Default() => new(new JsonObject
    {
        ["DeliveryProviderSettings"] = new JsonArray(),
        ["IsEnabled"] = false,
        ["IsBackgroundQueueEnabled"] = true,
        ["DeliveryIntervalMinutes"] = 2,
        ["RetryAttempts"] = 5,
        ["RetryIntervalMinutes"] = 2,
        ["AntiNoiseSettings"] = new JsonObject { ["IsConsolidationEnabled"] = true, ["InitialConsolidationDelayMinutes"] = 2, ["MaxConsolidationDelayMinutes"] = 10, ["ExcludeSubscriberEvents"] = true },
        ["BroadcastSettings"] = new JsonObject { ["BroadcastMode"] = 0, ["IsBroadcastEnabled"] = true, ["SendToCustomEmailAddress"] = null, ["SendToCustomEmailDisplayName"] = null },
    });

    private static JsonNode? Walk(JsonObject o, string path)
    {
        JsonNode? n = o;
        foreach (var part in path.Split('.')) n = (n as JsonObject)?[part];
        return n;
    }

    public bool Bool(string path) => Walk(Root, path) is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    public int Int(string path) => Walk(Root, path) is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;
    public string? Text(string path) => Walk(Root, path) is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public void Set(string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var o = Root;
        foreach (var part in parts[..^1]) o = o[part] as JsonObject ?? (JsonObject)(o[part] = new JsonObject());
        o[parts[^1]] = value;
    }

    private JsonArray Providers => Root["DeliveryProviderSettings"] as JsonArray ?? (JsonArray)(Root["DeliveryProviderSettings"] = new JsonArray());

    /// <summary>The provider's entry (ProviderAssemblyQualifiedName, IsEnabled, ProviderConfigurationData), or null.</summary>
    public JsonObject? Provider(DeliveryProvider p) =>
        Providers.OfType<JsonObject>().FirstOrDefault(o => DeliveryProvider.Find(o["ProviderAssemblyQualifiedName"]?.GetValue<string>()) == p);

    public bool ProviderEnabled(DeliveryProvider p) => Provider(p)?["IsEnabled"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>Configuration values: the stored ones, else the provider's defaults (legacy CreateDefaultDeliveryProviderInstance).</summary>
    public JsonObject ProviderConfiguration(DeliveryProvider p)
    {
        var stored = Provider(p)?["ProviderConfigurationData"] as JsonObject;
        var config = new JsonObject();
        foreach (var f in p.Fields)
            config[f.Name] = stored != null && stored.TryGetPropertyValue(f.Name, out var v) ? v?.DeepClone() : p.Defaults.GetValueOrDefault(f.Name)?.DeepClone();
        return config;
    }

    /// <summary>Stores the provider entry; secret fields are written as null (S6: never in the database).</summary>
    public void SetProvider(DeliveryProvider p, bool enabled, JsonObject configuration)
    {
        foreach (var f in p.Fields.Where(f => f.Secret)) configuration[f.Name] = null;
        var entry = Provider(p);
        if (entry == null)
        {
            entry = new JsonObject { ["ProviderAssemblyQualifiedName"] = p.AssemblyQualifiedName };
            Providers.Add(entry);
        }
        entry["IsEnabled"] = enabled;
        entry["ProviderConfigurationData"] = configuration;
    }

    public override string ToString() => Root.ToJsonString();
}

public interface IPushNotificationSettingsStore
{
    Task<PushNotificationSettingsDocument> GetAsync(CancellationToken ct);
    Task SaveAsync(PushNotificationSettingsDocument settings, CancellationToken ct);

    /// <summary>The secret for a provider field from configuration (S6), or null when not configured.</summary>
    string? GetSecret(DeliveryProvider provider, ProviderField field);
}

internal sealed class PushNotificationSettingsStore(TicketDeskDbContext db, IOptionsMonitor<PushNotificationSecretsOptions> secrets) : IPushNotificationSettingsStore
{
    public async Task<PushNotificationSettingsDocument> GetAsync(CancellationToken ct)
    {
        var json = await db.Database.SqlQuery<string?>(
            $"SELECT PushNotificationSettingsJson AS Value FROM notifications.ApplicationPushNotificationSettings WHERE ApplicationName = N'TicketDesk'").FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return PushNotificationSettingsDocument.Default();
        try { return new PushNotificationSettingsDocument(JsonNode.Parse(json) as JsonObject ?? throw new JsonException()); }
        catch (JsonException) { return PushNotificationSettingsDocument.Default(); }
    }

    public async Task SaveAsync(PushNotificationSettingsDocument settings, CancellationToken ct)
    {
        var json = settings.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF EXISTS (SELECT 1 FROM notifications.ApplicationPushNotificationSettings WHERE ApplicationName = N'TicketDesk')
                UPDATE notifications.ApplicationPushNotificationSettings SET PushNotificationSettingsJson = {json} WHERE ApplicationName = N'TicketDesk';
            ELSE
                INSERT notifications.ApplicationPushNotificationSettings (ApplicationName, PushNotificationSettingsJson) VALUES (N'TicketDesk', {json});
            """, ct);
    }

    public string? GetSecret(DeliveryProvider provider, ProviderField field) =>
        secrets.CurrentValue.Providers.TryGetValue(provider.Key, out var fields) && fields.TryGetValue(field.Name, out var v) && !string.IsNullOrEmpty(v) ? v : null;
}
