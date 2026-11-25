namespace TicketDesk.Identity;

/// <summary>The four legacy roles (rows of <c>IdentityRoles</c>). Admin implies help desk implies internal user.</summary>
public static class TicketDeskRoles
{
    public const string Administrators = "TdAdministrators";
    public const string HelpDeskUsers = "TdHelpDeskUsers";
    public const string InternalUsers = "TdInternalUsers";
    public const string PendingUsers = "TdPendingUsers";

    public static readonly IReadOnlyList<string> All = [Administrators, HelpDeskUsers, InternalUsers, PendingUsers];
}
