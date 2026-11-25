using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using TicketDesk.Notifications;

namespace TicketDesk.Worker;

/// <summary>
/// Delivers ticket notifications from the queue (legacy: InProcessPushNotificationScheduler + PushNotificationDeliveryManager
/// in the web app, or the PushNotifications WebJob on Azure). Per message: compose from the database, send each mail; then
/// <list type="bullet">
/// <item>all sent (or nothing to send): complete;</item>
/// <item>some failed: schedule a copy for the failed destinations only, after BaseDelay × 2^attempt, and complete; after
/// <see cref="WorkerOptions.MaxAttempts"/> attempts dead-letter it ("delivery-failed");</item>
/// <item>not a ticket-notification message, or the ticket does not exist: dead-letter at once ("invalid-message",
/// "unknown-ticket") — retrying cannot help a poison message.</item>
/// </list>
/// Delivery is at least once: a crash between sending and completing redelivers the message (lock expiry; the queue's
/// MaxDeliveryCount dead-letters a message that keeps crashing the worker).
/// </summary>
public sealed class NotificationWorker(ServiceBusClient client, IOptions<ServiceBusNotificationOptions> bus, IOptions<WorkerOptions> options,
    IServiceScopeFactory scopes, IMailTransport transport, TimeProvider clock, ILogger<NotificationWorker> log) : BackgroundService
{
    private ServiceBusProcessor? _processor;
    private ServiceBusSender? _sender;

    /// <summary>The queue processor runs (readiness of the worker pod, <see cref="WorkerHealthCheck"/>).</summary>
    public bool IsProcessing => _processor?.IsProcessing == true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _sender = client.CreateSender(bus.Value.QueueName);
        _processor = client.CreateProcessor(bus.Value.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false, MaxConcurrentCalls = 1, PrefetchCount = 0, MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
        });
        _processor.ProcessMessageAsync += HandleAsync;
        _processor.ProcessErrorAsync += e =>
        {
            log.LogWarning(e.Exception, "Service Bus error ({Source}, {Entity})", e.ErrorSource, e.EntityPath);
            return Task.CompletedTask;
        };
        await _processor.StartProcessingAsync(stoppingToken);
        log.LogInformation("Delivering ticket notifications from queue {Queue}", bus.Value.QueueName);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null) await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    internal async Task HandleAsync(ProcessMessageEventArgs args)
    {
        var ct = args.CancellationToken;
        var message = TicketNotificationMessage.TryParse(args.Message.Body);
        if (message == null)
        {
            log.LogWarning("Dead-lettering message {MessageId}: not a ticket-notification message", args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "invalid-message", "the body is not a ticket-notification message", ct);
            return;
        }
        Composition composition;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            composition = await scope.ServiceProvider.GetRequiredService<NotificationComposer>().ComposeAsync(message, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await RetryOrDeadLetterAsync(args, message, message.PendingDestinationIds, $"composing failed: {e.GetType().Name}: {e.Message}", ct);
            return;
        }
        if (composition.TicketMissing)
        {
            log.LogWarning("Dead-lettering batch for ticket {TicketId}: {Reason}", message.TicketId, composition.SkipReason);
            await args.DeadLetterMessageAsync(args.Message, "unknown-ticket", composition.SkipReason, ct);
            return;
        }
        if (composition.Mails.Count == 0)
        {
            log.LogInformation("Ticket {TicketId} events {Events}: nothing sent ({Reason})", message.TicketId, string.Join(",", message.EventIds), composition.SkipReason);
            await args.CompleteMessageAsync(args.Message, ct);
            return;
        }
        var failed = new List<int>();
        string? lastError = null;
        foreach (var mail in composition.Mails)
        {
            try
            {
                await transport.SendAsync(composition.Sender!, mail, message, ct);
                log.LogInformation("Sent ticket {TicketId} events {Events} to destination {DestinationId} (attempt {Attempt})",
                    message.TicketId, string.Join(",", message.EventIds), mail.DestinationId, message.Attempt + 1);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed.Add(mail.DestinationId);
                lastError = $"{e.GetType().Name}: {e.Message}";
                log.LogWarning("Sending ticket {TicketId} to destination {DestinationId} failed (attempt {Attempt}): {Error}", message.TicketId, mail.DestinationId, message.Attempt + 1, lastError);
            }
        }
        if (failed.Count == 0) await args.CompleteMessageAsync(args.Message, ct);
        else await RetryOrDeadLetterAsync(args, message, failed, $"{failed.Count} of {composition.Mails.Count} destination(s) failed: {lastError}", ct);
    }

    private async Task RetryOrDeadLetterAsync(ProcessMessageEventArgs args, TicketNotificationMessage message, IReadOnlyList<int>? pending, string error, CancellationToken ct)
    {
        var attempts = message.Attempt + 1;
        if (attempts >= options.Value.MaxAttempts)
        {
            log.LogError("Dead-lettering batch for ticket {TicketId} after {Attempts} attempts: {Error}", message.TicketId, attempts, error);
            await args.DeadLetterMessageAsync(args.Message, new Dictionary<string, object>
            {
                ["pendingDestinationIds"] = string.Join(",", pending ?? []),
                ["attempts"] = attempts,
            }, "delivery-failed", Trim(error), ct);
            return;
        }
        var delay = options.Value.BaseDelay * Math.Pow(2, message.Attempt);
        var retry = message with { Attempt = attempts, PendingDestinationIds = pending };
        await _sender!.ScheduleMessageAsync(retry.ToServiceBusMessage(), clock.GetUtcNow() + delay, ct);
        await args.CompleteMessageAsync(args.Message, ct);
        log.LogWarning("Batch for ticket {TicketId}: attempt {Attempt} failed ({Error}); retry {Next} in {Delay} s", message.TicketId, attempts, error, attempts + 1, delay.TotalSeconds);
    }

    private static string Trim(string s) => s.Length > 1000 ? s[..1000] : s;

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sender?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
