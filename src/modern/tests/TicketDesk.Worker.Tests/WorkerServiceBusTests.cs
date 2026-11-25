using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using TicketDesk.Notifications;

namespace TicketDesk.Worker.Tests;

/// <summary>
/// The whole path against the Service Bus emulator (<c>ops/dev/servicebus-emulator.sh up</c>; TD_SERVICEBUS_TEST_CONN =
/// its connection string, queue <c>ticket-notifications-test</c>): the write path publishes, the worker host delivers,
/// retries with backoff, and dead-letters what cannot be delivered. One test at a time (they share the queue).
/// </summary>
[Collection(ServiceBusQueue.Name)]
public sealed class WorkerServiceBusTests(NotificationDatabase db) : IClassFixture<NotificationDatabase>
{
    private const string Queue = "ticket-notifications-test";
    private static string? Conn => Environment.GetEnvironmentVariable("TD_SERVICEBUS_TEST_CONN") is { Length: > 0 } c ? c : null;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private void Need()
    {
        Assert.SkipUnless(db.Available, "needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        Assert.SkipUnless(Conn != null, "set TD_SERVICEBUS_TEST_CONN (ops/dev/servicebus-emulator.sh conn) to run the Service Bus tests");
    }

    private Dictionary<string, string?> Settings(string pickup, int maxAttempts = 5, double baseDelaySeconds = 2) => new()
    {
        ["Notifications:ServiceBus:ConnectionString"] = Conn,
        ["Notifications:ServiceBus:QueueName"] = Queue,
        ["Worker:Mail:PickupDirectory"] = pickup,
        ["Worker:MaxAttempts"] = maxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Worker:BaseDelay"] = TimeSpan.FromSeconds(baseDelaySeconds).ToString("c"),
    };

    private async Task<IHost> StartWorkerAsync(Dictionary<string, string?> settings, IMailTransport? transport = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:TicketDesk"] = db.ConnectionString });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders();
        builder.Services.AddWorkerServices(builder.Configuration);
        if (transport != null) builder.Services.AddSingleton(transport);
        var host = builder.Build();
        await host.StartAsync(Ct);
        return host;
    }

    private ServiceProvider Publisher()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:ServiceBus:ConnectionString"] = Conn, ["Notifications:ServiceBus:QueueName"] = Queue,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationsModule(config);
        return services.BuildServiceProvider();
    }

    /// <summary>Removes whatever an earlier run left in the queue and its dead-letter queue.</summary>
    private async Task DrainAsync()
    {
        await using var client = new ServiceBusClient(Conn);
        foreach (var sub in new[] { SubQueue.None, SubQueue.DeadLetter })
        {
            await using var r = client.CreateReceiver(Queue, new ServiceBusReceiverOptions { SubQueue = sub, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
            while ((await r.ReceiveMessagesAsync(50, TimeSpan.FromSeconds(1), Ct)).Count > 0) { }
        }
    }

    private async Task<List<ServiceBusReceivedMessage>> DeadLettersAsync(int expected, TimeSpan timeout)
    {
        await using var client = new ServiceBusClient(Conn);
        await using var r = client.CreateReceiver(Queue, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        var list = new List<ServiceBusReceivedMessage>();
        var sw = Stopwatch.StartNew();
        while (list.Count < expected && sw.Elapsed < timeout) list.AddRange(await r.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(1), Ct));
        return list;
    }

    private static async Task<List<MimeMessage>> WaitForMailAsync(string dir, int count, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.eml") : [];
            if (files.Length >= count)
            {
                await Task.Delay(1000); // nothing more should arrive
                return Directory.GetFiles(dir, "*.eml").Order().Select(f => MimeMessage.Load(f)).ToList();
            }
            await Task.Delay(200);
        }
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.eml").Select(f => MimeMessage.Load(f)).ToList() : [];
    }

    [Fact]
    public async Task A_ticket_written_by_the_app_is_mailed_once_to_each_recipient_through_the_queue()
    {
        Need();
        await DrainAsync();
        var pickup = Directory.CreateTempSubdirectory("td-worker-mail-").FullName;
        await using var publisher = Publisher();
        var (id, change) = await db.CreateTicketAsync(publisher.GetRequiredService<ITicketChangeNotifier>());
        var sw = Stopwatch.StartNew();
        using var host = await StartWorkerAsync(Settings(pickup));
        var mails = await WaitForMailAsync(pickup, 3, TimeSpan.FromSeconds(60));
        TestContext.Current.SendDiagnosticMessage($"ticket {id}, events {string.Join(",", change.EventIds)}: {mails.Count} mails in {sw.Elapsed.TotalSeconds:F1} s");
        await host.StopAsync(Ct);
        Assert.Equal(3, mails.Count);
        Assert.All(mails, m => Assert.Equal("worker", m.Headers[TicketMail.SenderHeader]!));
        Assert.All(mails, m => Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), m.Headers["X-TicketDesk-Ticket"]));
        Assert.Equal(3, mails.Select(m => m.To.Mailboxes.Single().Address).Distinct().Count());
        Assert.Single(mails, m => (m.Subject ?? "").StartsWith("[New] ", StringComparison.Ordinal));
        Assert.Empty(await DeadLettersAsync(1, TimeSpan.FromSeconds(2)));
        Directory.Delete(pickup, true);
    }

    [Fact]
    public async Task Poison_messages_and_unknown_tickets_are_dead_lettered_at_once()
    {
        Need();
        await DrainAsync();
        await using (var client = new ServiceBusClient(Conn))
        await using (var sender = client.CreateSender(Queue))
        {
            await sender.SendMessageAsync(new ServiceBusMessage("not a notification") { MessageId = "poison-1" }, Ct);
            await sender.SendMessageAsync(new TicketNotificationMessage(int.MaxValue, false, [1], DateTimeOffset.UtcNow).ToServiceBusMessage(), Ct);
        }
        var pickup = Directory.CreateTempSubdirectory("td-worker-mail-").FullName;
        using var host = await StartWorkerAsync(Settings(pickup));
        var dead = await DeadLettersAsync(2, TimeSpan.FromSeconds(30));
        await host.StopAsync(Ct);
        Assert.Equal(["invalid-message", "unknown-ticket"], dead.Select(d => d.DeadLetterReason).Order());
        Assert.All(dead, d => Assert.Equal(1, d.DeliveryCount)); // no pointless retries of a poison message
        Assert.Empty(Directory.GetFiles(pickup));
        Directory.Delete(pickup, true);
    }

    /// <summary>
    /// SMTP fails: one destination twice (then works), one always. The working destination gets its mail once, the flaky one
    /// once after two retries, the broken one never: after MaxAttempts the batch copy that holds only it is dead-lettered.
    /// The retries come at BaseDelay × 2^n.
    /// </summary>
    [Fact]
    public async Task Failed_destinations_alone_are_retried_with_backoff_and_then_dead_lettered()
    {
        Need();
        await DrainAsync();
        var (id, change) = await db.CreateTicketAsync();
        var broken = db.Destination[db.Assignee];
        var flaky = db.BroadcastDestination;
        var transport = new ScriptedTransport(d => d == broken || (d == flaky && ScriptedTransport.Attempts(d) <= 2));
        var pickup = Directory.CreateTempSubdirectory("td-worker-mail-").FullName;
        using var host = await StartWorkerAsync(Settings(pickup, maxAttempts: 4, baseDelaySeconds: 1), transport);
        await using (var client = new ServiceBusClient(Conn))
        await using (var sender = client.CreateSender(Queue))
            await sender.SendMessageAsync(new TicketNotificationMessage(id, change.IsNew, change.EventIds, DateTimeOffset.UtcNow).ToServiceBusMessage(), Ct);
        var dead = await DeadLettersAsync(1, TimeSpan.FromSeconds(60));
        await host.StopAsync(Ct);

        var d = Assert.Single(dead);
        Assert.Equal("delivery-failed", d.DeadLetterReason);
        Assert.Equal(broken.ToString(System.Globalization.CultureInfo.InvariantCulture), d.ApplicationProperties["pendingDestinationIds"]);
        Assert.Equal(4, d.ApplicationProperties["attempts"]);
        Assert.Equal([broken], TicketNotificationMessage.TryParse(d.Body)!.PendingDestinationIds);
        Assert.Equal(1, transport.Sent(db.Destination[db.Owner]));
        Assert.Equal(1, transport.Sent(flaky));
        Assert.Equal(0, transport.Sent(broken));
        Assert.Equal(4, ScriptedTransport.Attempts(broken));
        var gaps = transport.Gaps(broken);
        TestContext.Current.SendDiagnosticMessage($"retry gaps for the broken destination: {string.Join(", ", gaps.Select(g => $"{g:F1} s"))}");
        Assert.Equal(3, gaps.Count);
        for (var i = 0; i < gaps.Count; i++) Assert.InRange(gaps[i], Math.Pow(2, i) * 0.9, Math.Pow(2, i) + 3.0);
        Directory.Delete(pickup, true);
    }

    /// <summary>Fails the destinations the predicate names (after counting the attempt), records sends and attempt times.</summary>
    private sealed class ScriptedTransport(Func<int, bool> fail) : IMailTransport
    {
        private static readonly ConcurrentDictionary<int, List<DateTime>> Tries = new();
        private readonly ConcurrentDictionary<int, int> _sent = new();

        public static int Attempts(int destination) => Tries.TryGetValue(destination, out var l) ? l.Count : 0;
        public int Sent(int destination) => _sent.GetValueOrDefault(destination);
        public List<double> Gaps(int destination) =>
            Tries[destination].Zip(Tries[destination].Skip(1), (a, b) => (b - a).TotalSeconds).ToList();

        public Task SendAsync(MailSender sender, OutgoingMail mail, TicketNotificationMessage batch, CancellationToken ct)
        {
            Tries.GetOrAdd(mail.DestinationId, _ => []).Add(DateTime.UtcNow);
            if (fail(mail.DestinationId)) throw new MailKit.Net.Smtp.SmtpCommandException(MailKit.Net.Smtp.SmtpErrorCode.MessageNotAccepted, MailKit.Net.Smtp.SmtpStatusCode.MailboxBusy, "lab: mailbox busy");
            _sent.AddOrUpdate(mail.DestinationId, 1, (_, n) => n + 1);
            return Task.CompletedTask;
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServiceBusQueue
{
    public const string Name = "Service Bus test queue";
}
