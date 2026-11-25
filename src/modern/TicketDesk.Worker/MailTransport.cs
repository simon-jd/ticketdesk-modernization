using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TicketDesk.Notifications;

namespace TicketDesk.Worker;

public interface IMailTransport
{
    Task SendAsync(MailSender sender, OutgoingMail mail, TicketNotificationMessage batch, CancellationToken ct);
}

/// <summary>Builds the MIME message (multipart/alternative, text then HTML, as Postal sent it) with trace headers.</summary>
public static class TicketMail
{
    public const string SenderHeader = "X-TicketDesk-Sender";

    public static MimeMessage Build(MailSender sender, OutgoingMail mail, TicketNotificationMessage batch)
    {
        var msg = new MimeMessage { Subject = mail.Content.Subject };
        msg.From.Add(new MailboxAddress(sender.FromDisplayName, sender.FromAddress));
        msg.To.Add(new MailboxAddress(mail.DisplayName ?? "", mail.Address));
        // who sent it and for which write: the coexistence check counts mails per (ticket, events, recipient) by these
        msg.Headers.Add(SenderHeader, "worker");
        msg.Headers.Add("X-TicketDesk-Ticket", batch.TicketId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        msg.Headers.Add("X-TicketDesk-Events", string.Join(",", batch.EventIds));
        msg.Body = new BodyBuilder { TextBody = mail.Content.Text, HtmlBody = mail.Content.Html }.ToMessageBody();
        return msg;
    }
}

/// <summary>SMTP through MailKit (the settings page's server unless <see cref="MailOptions.SmtpHost"/> overrides it).</summary>
public sealed class SmtpMailTransport(IOptions<WorkerOptions> options) : IMailTransport
{
    public async Task SendAsync(MailSender sender, OutgoingMail mail, TicketNotificationMessage batch, CancellationToken ct)
    {
        var o = options.Value.Mail;
        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(o.SmtpHost ?? sender.SmtpServer, o.SmtpPort ?? sender.SmtpPort,
            sender.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(sender.UserName)) await client.AuthenticateAsync(sender.UserName, sender.Password ?? "", ct);
        await client.SendAsync(TicketMail.Build(sender, mail, batch), ct);
        await client.DisconnectAsync(true, ct);
    }
}

/// <summary>One .eml file per mail in a directory (lab and tests; no network).</summary>
public sealed class PickupDirectoryTransport(IOptions<WorkerOptions> options) : IMailTransport
{
    public async Task SendAsync(MailSender sender, OutgoingMail mail, TicketNotificationMessage batch, CancellationToken ct)
    {
        var dir = options.Value.Mail.PickupDirectory!;
        Directory.CreateDirectory(dir);
        var name = $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-t{batch.TicketId}-d{mail.DestinationId}-{Guid.NewGuid():N}.eml";
        var tmp = Path.Combine(dir, name + ".tmp");
        await using (var f = File.Create(tmp)) await TicketMail.Build(sender, mail, batch).WriteToAsync(f, ct);
        File.Move(tmp, Path.Combine(dir, name));
    }
}
