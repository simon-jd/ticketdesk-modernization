using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Notifications;

/// <summary>What a ticket write produced (legacy: the static TdDomainContext events TicketsCreated/TicketsChanged/NotificationsCreated).</summary>
public sealed record TicketChange(int TicketId, bool IsNew, IReadOnlyList<int> EventIds);

/// <summary>
/// Called by the write path (TicketWriter) after a ticket write is committed. Search needs nothing from it any more (SQL
/// full-text indexes the tables by itself, td-db V0011); notification delivery gets one Service Bus message per write.
/// </summary>
public interface ITicketChangeNotifier
{
    Task TicketsChangedAsync(IReadOnlyList<TicketChange> changes, CancellationToken ct);
}

/// <summary>
/// The queue message: one notification batch = the event notifications one ticket write created
/// (<c>dbo.TicketEventNotifications</c> rows of those events). The worker renders and sends from the database, so the
/// message carries ids, not mail content (the legacy queue held BinaryFormatter blobs, risk 5). A retry copy carries the
/// destinations that are still to be sent (<see cref="PendingDestinationIds"/>), so a partial failure never re-sends to the
/// recipients that already got the mail.
/// </summary>
public sealed record TicketNotificationMessage(int TicketId, bool IsNewTicket, IReadOnlyList<int> EventIds, DateTimeOffset CreatedUtc)
{
    public const string ContentType = "application/vnd.ticketdesk.ticket-notification+json";

    /// <summary>Delivery attempts made before this copy (0 for the message the web app published).</summary>
    public int Attempt { get; init; }

    /// <summary>Null: every recipient of the batch. Otherwise the <c>notifications.PushNotificationDestinations</c> ids still to send.</summary>
    public IReadOnlyList<int>? PendingDestinationIds { get; init; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public BinaryData ToBody() => BinaryData.FromObjectAsJson(this, Json);

    /// <summary>Null when the body is not a valid message (a poison message: the worker dead-letters it).</summary>
    public static TicketNotificationMessage? TryParse(BinaryData body)
    {
        try
        {
            var m = body.ToObjectFromJson<TicketNotificationMessage>(Json);
            return m is { TicketId: > 0, EventIds.Count: > 0 } ? m : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ServiceBusMessage ToServiceBusMessage() => new(ToBody())
    {
        ContentType = ContentType,
        MessageId = $"ticket-{TicketId}-events-{string.Join('-', EventIds)}-attempt-{Attempt}",
        Subject = IsNewTicket ? "new-ticket" : "ticket-event",
        ApplicationProperties = { ["ticketId"] = TicketId, ["attempt"] = Attempt },
    };
}

/// <summary>Section <c>Notifications:ServiceBus</c>. Without a connection string or namespace nothing is published.</summary>
public sealed class ServiceBusNotificationOptions
{
    public const string SectionName = "Notifications:ServiceBus";

    /// <summary>Lab: the emulator (<c>ops/dev/servicebus-emulator.sh conn</c>). Takes precedence over <see cref="FullyQualifiedNamespace"/>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure Service Bus without a key (AKS round): <c>&lt;namespace&gt;.servicebus.windows.net</c>, signed in with
    /// <see cref="DefaultAzureCredential"/> = the pod's workload identity (Azure Service Bus Data Sender for the web app, Data
    /// Receiver + Sender for the worker, which schedules its retry copies). The namespace has local (SAS) auth disabled.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    public string QueueName { get; set; } = "ticket-notifications";

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(FullyQualifiedNamespace);

    public ServiceBusClient CreateClient() => !string.IsNullOrWhiteSpace(ConnectionString)
        ? new ServiceBusClient(ConnectionString)
        : new ServiceBusClient(FullyQualifiedNamespace!.Trim(), new DefaultAzureCredential());
}

internal sealed class NoOpTicketChangeNotifier : ITicketChangeNotifier
{
    public Task TicketsChangedAsync(IReadOnlyList<TicketChange> changes, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Publishes one message per ticket write after the write committed. Basic on purpose (plan: no outbox): if Service Bus is
/// unreachable the error is logged with the ticket and events and the request still succeeds (the write is done); those
/// notifications are not sent. The legacy app lost them the same way when its background work item failed.
/// </summary>
internal sealed class ServiceBusTicketChangeNotifier(ServiceBusSender sender, TimeProvider clock, ILogger<ServiceBusTicketChangeNotifier> log) : ITicketChangeNotifier
{
    public async Task TicketsChangedAsync(IReadOnlyList<TicketChange> changes, CancellationToken ct)
    {
        foreach (var c in changes.Where(c => c.EventIds.Count > 0))
        {
            var message = new TicketNotificationMessage(c.TicketId, c.IsNew, c.EventIds, clock.GetUtcNow());
            try
            {
                await sender.SendMessageAsync(message.ToServiceBusMessage(), ct);
            }
            catch (Exception e) when (e is ServiceBusException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
            {
                log.LogError(e, "Notification batch for ticket {TicketId} (events {EventIds}) not published; its e-mails will not be sent", c.TicketId, string.Join(",", c.EventIds));
            }
        }
    }
}

public static class NotificationsModule
{
    /// <summary>Without configuration: the no-op notifier (tests, replays, and any host where notifications are off).</summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) =>
        services.AddSingleton<ITicketChangeNotifier, NoOpTicketChangeNotifier>();

    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ServiceBusNotificationOptions.SectionName).Get<ServiceBusNotificationOptions>() ?? new ServiceBusNotificationOptions();
        if (!options.Enabled) return services.AddNotificationsModule();
        services.AddSingleton(_ => options.CreateClient());
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(options.QueueName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITicketChangeNotifier, ServiceBusTicketChangeNotifier>();
        return services;
    }
}
