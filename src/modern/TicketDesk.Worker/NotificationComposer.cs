using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Notifications;
using TicketDesk.Settings;
using TicketDesk.Tickets.Pages;

namespace TicketDesk.Worker;

/// <summary>SMTP sender of the push-notification settings (provider "SmtpDeliveryProvider"); the password is a configured secret (S6).</summary>
public sealed record MailSender(string FromAddress, string FromDisplayName, string SmtpServer, int SmtpPort, bool EnableSsl, string? UserName, string? Password);

/// <summary>One e-mail to one destination (<c>notifications.PushNotificationDestinations</c> row).</summary>
public sealed record OutgoingMail(int DestinationId, string SubscriberId, string Address, string? DisplayName, RenderedEmail Content);

/// <summary>The mails of one batch, or why there are none.</summary>
public sealed record Composition(string? SkipReason, bool TicketMissing, MailSender? Sender, IReadOnlyList<OutgoingMail> Mails)
{
    public static Composition Skip(string reason) => new(reason, false, null, []);
}

/// <summary>
/// Who gets mail for one batch, with the legacy rules (Startup.PushNotifications / TdPushNotificationContext):
/// nothing while push notifications are off (<c>IsEnabled</c>, the lab default); a subscriber's own event is skipped when
/// anti-noise <c>ExcludeSubscriberEvents</c> is on (its notification row is IsRead); each subscriber with an enabled
/// settings row gets one mail per e-mail destination; a new ticket also goes to the "new ticket broadcast" destinations when
/// broadcast is on (those rows are maintained by the legacy app at its start: help desk + admins, or a custom address).
/// Not kept: consolidation (the legacy app delayed and merged a subscriber's mails for 2–10 minutes; here each write is one
/// mail per recipient) and SendGrid (the worker sends through the SMTP provider only).
/// </summary>
public sealed class NotificationComposer(TicketDeskDbContext db, IPushNotificationSettingsStore pushSettings, IApplicationSettingsStore appSettings,
    TicketUsers users, TicketEmailRenderer renderer)
{
    public const string BroadcastSubscriber = "new ticket broadcast";

    public async Task<Composition> ComposeAsync(TicketNotificationMessage m, CancellationToken ct)
    {
        var push = await pushSettings.GetAsync(ct);
        if (!push.Bool("IsEnabled")) return Composition.Skip("push notifications are disabled (admin/push-notification-settings)");
        var smtp = DeliveryProvider.Smtp;
        if (!push.ProviderEnabled(smtp)) return Composition.Skip("the SMTP delivery provider is not enabled (the worker sends through SMTP only)");
        var cfg = push.ProviderConfiguration(smtp);
        var sender = new MailSender(
            cfg["SmtpFromAddress"]?.GetValue<string>() ?? "", cfg["SmtpFromDisplayName"]?.GetValue<string>() ?? "TicketDesk",
            cfg["SmtpServer"]?.GetValue<string>() ?? "localhost", cfg["SmtpPort"]?.GetValue<int>() ?? 25, cfg["EnableSsl"]?.GetValue<bool>() ?? false,
            cfg["SmtpUserName"]?.GetValue<string>(), pushSettings.GetSecret(smtp, smtp.Fields.Single(f => f.Name == "SmtpPassword")));

        var ticket = await db.Tickets.AsNoTracking().Include(t => t.Project).Include(t => t.TicketEvents).AsSplitQuery()
            .FirstOrDefaultAsync(t => t.TicketId == m.TicketId, ct);
        if (ticket == null) return new Composition($"ticket {m.TicketId} does not exist", true, sender, []);

        var eventIds = m.EventIds.ToArray();
        var notes = await db.TicketEventNotifications.AsNoTracking().Where(n => n.TicketId == m.TicketId && eventIds.Contains(n.EventId)).ToListAsync(ct);
        var excludeOwn = push.Bool("AntiNoiseSettings.ExcludeSubscriberEvents");
        var subscribers = notes.Where(n => !(excludeOwn && n.IsRead)).Select(n => n.SubscriberId).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        var broadcast = m.IsNewTicket && push.Bool("BroadcastSettings.IsBroadcastEnabled");
        var wanted = broadcast ? subscribers.Append(BroadcastSubscriber).ToArray() : subscribers.ToArray();
        var settings = await db.Set<SubscriberPushNotificationSetting>().AsNoTracking().Include(s => s.PushNotificationDestinations)
            .Where(s => wanted.Contains(s.SubscriberId)).ToDictionaryAsync(s => s.SubscriberId, StringComparer.OrdinalIgnoreCase, ct);

        var names = await users.NamesAsync(new[] { ticket.Owner, ticket.AssignedTo, ticket.LastUpdateBy }.Concat(ticket.TicketEvents.Select(e => e.EventBy)), ct);
        var site = (await appSettings.GetAsync(ct)).ClientSettings.DefaultSiteRootUrl;
        var multiProject = await db.Projects.CountAsync(ct) > 1;
        RenderedEmail? normal = null, forNew = null;
        RenderedEmail Content(bool isNew) => isNew
            ? forNew ??= renderer.Render(new TicketEmailModel(ticket, names, site, multiProject, true))
            : normal ??= renderer.Render(new TicketEmailModel(ticket, names, site, multiProject, false));

        var mails = new List<OutgoingMail>();
        void AddDestinations(string subscriber, bool isNew)
        {
            if (!settings.TryGetValue(subscriber, out var s) || !s.IsEnabled) return; // legacy default settings: no destinations
            foreach (var d in s.PushNotificationDestinations.Where(d => d.DestinationType == "email" && !string.IsNullOrWhiteSpace(d.DestinationAddress)).OrderBy(d => d.DestinationId))
                mails.Add(new OutgoingMail(d.DestinationId, subscriber, d.DestinationAddress!, d.SubscriberName, Content(isNew)));
        }
        foreach (var s in subscribers) AddDestinations(s, false);
        if (broadcast) AddDestinations(BroadcastSubscriber, true);
        if (m.PendingDestinationIds is { } pending) mails = mails.Where(x => pending.Contains(x.DestinationId)).ToList();
        return new Composition(mails.Count == 0 ? "no recipient with an e-mail destination" : null, false, sender, mails);
    }
}
