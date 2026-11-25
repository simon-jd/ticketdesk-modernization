namespace TicketDesk.Worker;

/// <summary>Section <c>Worker</c>.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Delivery attempts per recipient before the batch is dead-lettered (the first one included).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Backoff of the n-th retry: BaseDelay × 2^(n-1) (lab default 2 s: 2, 4, 8, 16 s).</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    public MailOptions Mail { get; set; } = new();
}

/// <summary>
/// Where mail goes. Default: the SMTP delivery provider configured on the push-notification settings page (server, port,
/// SSL, user, from address; password from <c>PushNotifications:Secrets:Smtp:SmtpPassword</c>, S6). Lab overrides: a pickup
/// directory (one .eml file per mail, no network) or another SMTP host/port (the local sink, ops/dev/smtp-sink.py).
/// </summary>
public sealed class MailOptions
{
    public string? PickupDirectory { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
}
