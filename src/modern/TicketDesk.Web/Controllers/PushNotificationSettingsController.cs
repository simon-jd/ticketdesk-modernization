using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Settings;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using PushStrings = TicketDesk.Localization.PushNotifications.Strings;
using ValidationStrings = TicketDesk.Localization.Validation;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Wave 5: legacy <c>PushNotificationSettingsController</c>: the settings page (GET form of wave 2) and its POST, the provider
/// list partial, and the provider configuration form. S6: provider secrets (SMTP password, SendGrid API key) are neither
/// shown, accepted from the form nor stored in the database; they come from configuration (user secrets / environment
/// locally, Key Vault on Azure): <c>PushNotifications:Secrets:&lt;Smtp|SendGrid&gt;:&lt;field&gt;</c>.
/// </summary>
/// <remarks>
/// The legacy app unloaded its AppDomain after a save so its in-process delivery re-read the settings; the new app reads them
/// per request and cannot restart the legacy app, whose delivery picks the change up at its next app-pool start.
/// </remarks>
[Authorize(Policy = TicketDeskPolicies.Administrator)]
[Route("admin/push-notification-settings")]
public sealed class PushNotificationSettingsController(IPushNotificationSettingsStore store, IApplicationSettingsStore appSettings) : LegacyPageController
{
    public sealed record Page(PushNotificationSettingsDocument Settings, string SiteRootUrl, string CurrentRootUrl, bool Saved);

    public sealed record ProviderPage(DeliveryProvider Provider, bool IsEnabled, JsonObject Configuration, IReadOnlyDictionary<string, bool> SecretConfigured);

    /// <summary>Int fields of the settings form (name → label), validated like the legacy int properties.</summary>
    public static readonly (string Name, string Label)[] IntFields =
    [
        ("DeliveryIntervalMinutes", PushStrings.DeliveryAttemptInterval), ("RetryAttempts", PushStrings.NumberOfRetryAttempts),
        ("RetryIntervalMinutes", PushStrings.InitialRetryInterval), ("BroadcastSettings.BroadcastMode", PushStrings.BroadcastMode),
        ("AntiNoiseSettings.InitialConsolidationDelayMinutes", PushStrings.InitialConsolidationDelay),
        ("AntiNoiseSettings.MaxConsolidationDelayMinutes", PushStrings.MaximumConsolidationDelay),
    ];

    public static readonly string[] BoolFields =
    [
        "IsEnabled", "IsBackgroundQueueEnabled", "BroadcastSettings.IsBroadcastEnabled", "AntiNoiseSettings.ExcludeSubscriberEvents",
        "AntiNoiseSettings.IsConsolidationEnabled",
    ];

    private string CurrentRootUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');

    private async Task<string> SiteRootUrl()
    {
        var root = (await appSettings.GetAsync(HttpContext.RequestAborted)).ClientSettings.DefaultSiteRootUrl;
        return string.IsNullOrEmpty(root) ? CurrentRootUrl() : root;
    }

    [HttpGet("")]
    [HttpGet("index")]
    [MigratedRoute("admin.push-settings.get", LegacyTokenPage = true)]
    public async Task<IActionResult> Index() =>
        View("Index", new Page(await store.GetAsync(HttpContext.RequestAborted), await SiteRootUrl(), CurrentRootUrl(), false));

    [HttpPost("")]
    [HttpPost("index")]
    [MigratedRoute("admin.push-settings.post", LegacyTokenPage = true)]
    public async Task<IActionResult> IndexPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var doc = await store.GetAsync(HttpContext.RequestAborted);
        var ints = new Dictionary<string, int>();
        foreach (var (name, label) in IntFields)
        {
            var raw = form[name].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw)) ModelState.AddModelError(name, string.Format(CultureInfo.CurrentCulture, ValidationStrings.FieldRequired, label));
            else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out var v)) ints[name] = v;
            else ModelState.AddModelError(name, string.Format(CultureInfo.CurrentCulture, "The value '{0}' is not valid for {1}.", raw, label));
        }
        var saved = false;
        if (ModelState.IsValid)
        {
            foreach (var b in BoolFields) doc.Set(b, form[b].Contains("true"));
            foreach (var (name, value) in ints) doc.Set(name, value);
            doc.Set("BroadcastSettings.SendToCustomEmailDisplayName", Nullable(form["BroadcastSettings.SendToCustomEmailDisplayName"].FirstOrDefault()));
            doc.Set("BroadcastSettings.SendToCustomEmailAddress", Nullable(form["BroadcastSettings.SendToCustomEmailAddress"].FirstOrDefault()));
            await store.SaveAsync(doc, HttpContext.RequestAborted);
            var s = await appSettings.GetAsync(HttpContext.RequestAborted);
            s.ClientSettings.Settings["DefaultSiteRootUrl"] = (form["siteRootUrl"].FirstOrDefault() ?? "").TrimEnd('/');
            await appSettings.SaveAsync(s, HttpContext.RequestAborted);
            saved = true;
        }
        return View("Index", new Page(doc, await SiteRootUrl(), CurrentRootUrl(), saved));
    }

    private static string? Nullable(string? s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>The providers panel (legacy child action, also reachable by URL). Rendered inside the settings page too.</summary>
    [HttpGet("list-providers")]
    [MigratedRoute("admin.push-providers")]
    public async Task<IActionResult> ListProviders() => PartialView("_ListDeliveryProviderSettings", await store.GetAsync(HttpContext.RequestAborted));

    private ProviderPage ProviderPageFor(DeliveryProvider p, bool enabled, JsonObject config) =>
        new(p, enabled, config, p.Fields.Where(f => f.Secret).ToDictionary(f => f.Name, f => store.GetSecret(p, f) != null));

    [HttpGet("configure-provider")]
    [MigratedRoute("admin.push-provider.get")]
    public async Task<IActionResult> ConfigureProvider(string? providerAssemblyQualifiedName)
    {
        var provider = DeliveryProvider.Find(providerAssemblyQualifiedName);
        if (provider == null) return new LegacyRedirectResult("/admin/push-notification-settings");
        var doc = await store.GetAsync(HttpContext.RequestAborted);
        return View("ConfigureDeliveryProvider", ProviderPageFor(provider, doc.ProviderEnabled(provider), doc.ProviderConfiguration(provider)));
    }

    [HttpPost("configure-provider")]
    [MigratedRoute("admin.push-provider.post")]
    public async Task<IActionResult> ConfigureProviderPost()
    {
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var provider = DeliveryProvider.Find(form["ProviderAssemblyQualifiedName"].FirstOrDefault());
        if (provider == null) return new LegacyRedirectResult("/admin/push-notification-settings");
        var enabled = form["IsEnabled"].Contains("true");
        var config = new JsonObject();
        foreach (var f in provider.Fields)
        {
            var label = ProviderLabel(f);
            var raw = form[f.Name].FirstOrDefault();
            if (f.Secret)
            {
                // S6: never from the form; required secrets must exist in configuration
                if (!string.IsNullOrEmpty(raw))
                    ModelState.AddModelError(f.Name, $"{label}: not stored in the database; set {PushNotificationSecretsOptions.Key(provider, f)} in the secret store.");
                else if (f.Required && store.GetSecret(provider, f) == null)
                    ModelState.AddModelError(f.Name, string.Format(CultureInfo.CurrentCulture, ValidationStrings.FieldRequired, label));
                config[f.Name] = null;
                continue;
            }
            switch (f.Kind)
            {
                case ProviderFieldKind.Boolean:
                    config[f.Name] = form[f.Name].Contains("true");
                    break;
                case ProviderFieldKind.Number:
                    if (string.IsNullOrWhiteSpace(raw)) config[f.Name] = null;
                    else if (int.TryParse(raw, out var n)) config[f.Name] = n;
                    else ModelState.AddModelError(f.Name, string.Format(CultureInfo.CurrentCulture, "The value '{0}' is not valid for {1}.", raw, label));
                    break;
                default:
                    if (f.Required && string.IsNullOrWhiteSpace(raw))
                        ModelState.AddModelError(f.Name, string.Format(CultureInfo.CurrentCulture, ValidationStrings.FieldRequired, label));
                    config[f.Name] = string.IsNullOrEmpty(raw) ? null : raw;
                    break;
            }
        }
        if (!ModelState.IsValid) return View("ConfigureDeliveryProvider", ProviderPageFor(provider, enabled, config));
        var doc = await store.GetAsync(HttpContext.RequestAborted);
        doc.SetProvider(provider, enabled, config);
        await store.SaveAsync(doc, HttpContext.RequestAborted);
        return new LegacyRedirectResult("/admin/push-notification-settings");
    }

    public static string ProviderTitle(DeliveryProvider p) => p == DeliveryProvider.Smtp ? PushStrings.SMTPProvider : PushStrings.SendGridProvider;

    public static string ProviderLabel(ProviderField f) => f.Name switch
    {
        "SmtpServer" => PushStrings.SMTPServerName,
        "SmtpPort" => PushStrings.SMTPPort,
        "EnableSsl" => PushStrings.EnableSSL,
        "SmtpUserName" => PushStrings.SMTPUserName,
        "SmtpPassword" => PushStrings.SMTPPassword,
        "SmtpFromAddress" => PushStrings.SMTPFromAddress,
        "SmtpFromDisplayName" => PushStrings.SMTPFromDisplayName,
        "ApiKey" => PushStrings.ApiKey,
        "FromAddress" => PushStrings.FromAddress,
        "FromDisplayName" => PushStrings.FromDisplayName,
        "EnableClickTracking" => PushStrings.ClickTracking,
        "EnableOpenTracking" => PushStrings.OpenTracking,
        "EnableGravatar" => PushStrings.Gravatar,
        "SendToSink" => PushStrings.SendToSink,
        _ => f.Name,
    };

    public static string? ProviderPrompt(ProviderField f) => f.Name switch
    {
        "EnableClickTracking" => PushStrings.ClickTracking_Prompt,
        "EnableOpenTracking" => PushStrings.OpenTracking_Prompt,
        "EnableGravatar" => PushStrings.Gravatar_Prompt,
        "SendToSink" => PushStrings.SendToSink_Prompt,
        _ => null,
    };

    public static string? ProviderDescription(ProviderField f) => f.Name switch
    {
        "SmtpUserName" => PushStrings.SMTPUserName_Description,
        "SmtpPassword" => PushStrings.SMTPPassword_Description,
        "SmtpFromAddress" => PushStrings.SMTPFromAddress_Description,
        "SmtpFromDisplayName" => PushStrings.SMTPFromDisplayName_Description,
        "ApiKey" => PushStrings.ApiKey_Description,
        "FromAddress" => PushStrings.FromAddress_Description,
        "FromDisplayName" => PushStrings.FromDisplayName_Description,
        "EnableClickTracking" => PushStrings.ClickTracking_Description,
        "EnableOpenTracking" => PushStrings.OpenTracking_Description,
        "EnableGravatar" => PushStrings.Gravatar_Description,
        "SendToSink" => PushStrings.SendToSink_Description,
        _ => null,
    };
}
