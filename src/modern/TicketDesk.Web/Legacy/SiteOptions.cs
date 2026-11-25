namespace TicketDesk.Web.Legacy;

/// <summary>
/// Bound from configuration section <c>TicketDesk</c>: the legacy <c>appSettings</c> that change what the migrated pages
/// render (<c>ticketdesk:HomeEnabled</c>, <c>ticketdesk:RegisterEnabled</c>). Must match the legacy deployment while
/// both apps serve pages, or the menus differ between pages.
/// </summary>
public sealed class SiteOptions
{
    public const string SectionName = "TicketDesk";

    /// <summary>Legacy default true: <c>/</c> shows the home page; false sends a 301 to <c>/tickets</c>.</summary>
    public bool HomeEnabled { get; set; } = true;

    /// <summary>
    /// Legacy default false: the user menu shows the Register link only when true. The new app also refuses the register
    /// POST when false (S4; the legacy POST ignored the setting).
    /// </summary>
    public bool RegisterEnabled { get; set; }

    /// <summary>Legacy <c>ticketdesk:DemoModeEnabled</c>: demo credentials on the sign-in page, no password/profile changes.</summary>
    public bool DemoModeEnabled { get; set; }

    /// <summary>
    /// Time zone of "today" for due/overdue markers (the legacy app used <c>DateTime.Today</c>, the server's local date).
    /// UTC like the lab VM and the Azure VM; must match the legacy server while both apps render lists.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";

    public DateTime Today(TimeProvider? clock = null) =>
        TimeZoneInfo.ConvertTime((clock ?? TimeProvider.System).GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).Date;
}
