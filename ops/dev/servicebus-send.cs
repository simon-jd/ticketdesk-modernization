// Sends one ticket-notification message (the shape TicketDesk.Notifications publishes) to a Service Bus queue.
//   TD_SB_CONN="$(ops/dev/servicebus-emulator.sh conn)" dotnet run ops/dev/servicebus-send.cs -- QUEUE TICKET_ID EVENT_ID[,EVENT_ID] [new]
// A .NET 10 file-based app (dev tool for the worker container smoke; the web app publishes through ITicketChangeNotifier).
#:package Azure.Messaging.ServiceBus@7.21.0
using Azure.Messaging.ServiceBus;

var conn = Environment.GetEnvironmentVariable("TD_SB_CONN") ?? throw new InvalidOperationException("set TD_SB_CONN");
var (queue, ticket, events, isNew) = (args[0], int.Parse(args[1]), args[2].Split(',').Select(int.Parse).ToArray(), args.Length > 3 && args[3] == "new");
// ids only, so the JSON is written by hand (file-based apps run without reflection-based System.Text.Json)
var body = $"{{\"ticketId\":{ticket},\"isNewTicket\":{(isNew ? "true" : "false")},\"eventIds\":[{string.Join(',', events)}],\"createdUtc\":\"{DateTimeOffset.UtcNow:O}\",\"attempt\":0}}";
await using var client = new ServiceBusClient(conn);
await using var sender = client.CreateSender(queue);
await sender.SendMessageAsync(new ServiceBusMessage(body)
{
    ContentType = "application/vnd.ticketdesk.ticket-notification+json",
    MessageId = $"ticket-{ticket}-events-{string.Join('-', events)}-attempt-0",
});
Console.WriteLine($"sent to {queue}: {body}");
