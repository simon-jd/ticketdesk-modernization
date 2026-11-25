namespace TicketDesk.Data.Entities;

// The notifications.* tables of the legacy TdPushNotificationContext (EF6 automatic migrations, so no snapshot: mapped from
// the restored legacy database). Delivery itself stays with the legacy app until Round 3 (risk 5: queued messages are
// BinaryFormatter blobs that .NET 10 cannot read).

/// <summary>Row of <c>notifications.ApplicationPushNotificationSettings</c> (one row, <c>TicketDesk</c>).</summary>
public class ApplicationPushNotificationSetting
{
    public string ApplicationName { get; set; } = ApplicationSetting.TicketDeskApplicationName;
    public string? PushNotificationSettingsJson { get; set; }
}

/// <summary>Row of <c>notifications.SubscriberPushNotificationSettings</c> (per user, plus <c>new ticket broadcast</c>).</summary>
public class SubscriberPushNotificationSetting
{
    public string SubscriberId { get; set; } = "";
    public bool IsEnabled { get; set; }
    public ICollection<PushNotificationDestination> PushNotificationDestinations { get; set; } = new List<PushNotificationDestination>();
}

/// <summary>Row of <c>notifications.PushNotificationDestinations</c> (an e-mail address of a subscriber).</summary>
public class PushNotificationDestination
{
    public int DestinationId { get; set; }
    public string? SubscriberName { get; set; }
    public string? DestinationAddress { get; set; }
    public string? DestinationType { get; set; }
    public string? SubscriberId { get; set; }
}

/// <summary>Row of <c>notifications.PushNotificationItems</c>: one queued message per subscriber destination.</summary>
public class PushNotificationItem
{
    public int PushNotificationItemId { get; set; }
    public int ContentSourceId { get; set; }
    public string ContentSourceType { get; set; } = "";
    public string SubscriberId { get; set; } = "";
    public int DestinationId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? ScheduledSendDate { get; set; }
    public int DeliveryStatus { get; set; }
    public int RetryCount { get; set; }
    public string? MessageContent { get; set; }
}

/// <summary>Row of <c>notifications.TicketPushNotificationItems</c>: the ticket events a queued item consolidates.</summary>
public class TicketPushNotificationItem
{
    public int PushNotificationItemId { get; set; }
    public string? TicketEventsList { get; set; }
    public string? CanceledEventsList { get; set; }
}
