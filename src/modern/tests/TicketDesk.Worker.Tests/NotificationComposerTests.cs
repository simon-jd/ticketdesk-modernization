using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Notifications;
using TicketDesk.Testing;

namespace TicketDesk.Worker.Tests;

/// <summary>Who gets mail for a batch (the legacy rules), and the rendered ticket e-mail.</summary>
public sealed class NotificationComposerTests(NotificationDatabase db) : IClassFixture<NotificationDatabase>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Composition> ComposeAsync(TicketNotificationMessage m)
    {
        await using var sp = db.WorkerServices();
        await using var scope = sp.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationComposer>().ComposeAsync(m, Ct);
    }

    [Fact]
    public async Task A_new_ticket_mails_the_subscribers_with_destinations_and_the_broadcast_list_but_not_the_author()
    {
        Assert.SkipUnless(db.Available, "needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        var (id, change) = await db.CreateTicketAsync();
        var c = await ComposeAsync(new TicketNotificationMessage(id, change.IsNew, change.EventIds, DateTimeOffset.UtcNow));
        // author (help desk, IsRead) excluded by anti-noise; owner + assignee from their notification rows; broadcast because the ticket is new
        Assert.Equal([db.Destination[db.Owner], db.Destination[db.Assignee], db.BroadcastDestination], c.Mails.Select(m => m.DestinationId));
        Assert.Equal($"Ticket #{id} : Projector <b>fan</b> noise", c.Mails[0].Content.Subject);
        Assert.Equal($"[New] Ticket #{id} : Projector <b>fan</b> noise", c.Mails[2].Content.Subject);
        Assert.Equal(new MailSender("ticketdesk@example.test", "TicketDesk", "127.0.0.1", 1, false, null, null), c.Sender);
        var html = c.Mails[0].Content.Html;
        Assert.Contains($"/ticket/{id}\">#{id}</a>", html);
        Assert.Contains("Projector &lt;b&gt;fan&lt;/b&gt; noise", html); // title encoded
        Assert.Contains("<strong>fan</strong>", html);                    // details rendered from Markdown like the ticket page
        Assert.Contains("<span class=\"tag\">projector</span><span class=\"tag\">noise</span>", html);
        Assert.Contains("Status: Active", c.Mails[0].Content.Text);

        // a retry copy only carries what is left to send
        var retry = await ComposeAsync(new TicketNotificationMessage(id, true, change.EventIds, DateTimeOffset.UtcNow) { Attempt = 1, PendingDestinationIds = [db.BroadcastDestination] });
        Assert.Equal([db.BroadcastDestination], retry.Mails.Select(m => m.DestinationId));
    }

    [Fact]
    public async Task Nothing_is_sent_while_push_notifications_are_off_and_unknown_tickets_are_reported()
    {
        Assert.SkipUnless(db.Available, "needs TD_SQL_TEST_CONN and TD_MODERN_SEEDED_DB=TicketDesk (the restored baseline data)");
        var (id, change) = await db.CreateTicketAsync();
        await db.SetPushSettingsAsync(enabled: false);
        try
        {
            var off = await ComposeAsync(new TicketNotificationMessage(id, true, change.EventIds, DateTimeOffset.UtcNow));
            Assert.Empty(off.Mails);
            Assert.Contains("disabled", off.SkipReason);
        }
        finally
        {
            await db.SetPushSettingsAsync(enabled: true);
        }
        var missing = await ComposeAsync(new TicketNotificationMessage(int.MaxValue, false, [1], DateTimeOffset.UtcNow));
        Assert.True(missing.TicketMissing);
    }

    [Fact]
    public void Messages_round_trip_and_garbage_is_recognised()
    {
        var m = new TicketNotificationMessage(7, true, [11, 12], DateTimeOffset.UnixEpoch) { Attempt = 2, PendingDestinationIds = [3] };
        Assert.Equal(m with { }, TicketNotificationMessage.TryParse(m.ToBody()) is { } p ? p with { EventIds = m.EventIds, PendingDestinationIds = m.PendingDestinationIds } : null);
        Assert.Equal([11, 12], TicketNotificationMessage.TryParse(m.ToBody())!.EventIds);
        Assert.Null(TicketNotificationMessage.TryParse(BinaryData.FromString("not json")));
        Assert.Null(TicketNotificationMessage.TryParse(BinaryData.FromString("{\"ticketId\":0,\"eventIds\":[]}")));
        var sb = m.ToServiceBusMessage();
        Assert.Equal("ticket-7-events-11-12-attempt-2", sb.MessageId);
        Assert.Equal(TicketNotificationMessage.ContentType, sb.ContentType);
    }
}
