using System.Data;

namespace TicketDesk.Seed;

public sealed record ColumnDef(string Name, string SqlType, bool Nullable, Type ClrType, int? MaxLength = null);

public sealed record TableDef(string Schema, string Name, IReadOnlyList<ColumnDef> Columns, bool KeepIdentity = false)
{
    public string FullName => $"[{Schema}].[{Name}]";

    public DataTable NewDataTable()
    {
        var t = new DataTable(Name);
        foreach (var c in Columns)
        {
            var col = t.Columns.Add(c.Name, c.ClrType);
            col.AllowDBNull = c.Nullable;
        }
        return t;
    }
}

/// <summary>
/// The TicketDesk 2.5 tables the seeder writes, as implied by the EF6 model (Domain migration Td2.5.022 snapshot and
/// ASP.NET Identity 2.2.1 defaults). rowversion columns are omitted (the server fills them). `td-seed verify` checks
/// these definitions against a live database.
/// </summary>
public static class TicketDeskSchema
{
    private static ColumnDef Str(string n, int? len, bool nullable = false) => new(n, len is null ? "nvarchar(max)" : $"nvarchar({len})", nullable, typeof(string), len);
    private static ColumnDef Int(string n, bool nullable = false) => new(n, "int", nullable, typeof(int));
    private static ColumnDef Bit(string n) => new(n, "bit", false, typeof(bool));
    private static ColumnDef Dto(string n, bool nullable = false) => new(n, "datetimeoffset", nullable, typeof(DateTimeOffset));
    private static ColumnDef Dec(string n) => new(n, "decimal", true, typeof(decimal));

    public static readonly TableDef Projects = new("dbo", "Projects",
        [Int("ProjectId"), Str("ProjectName", 100), Str("ProjectDescription", 500, true)], KeepIdentity: true);

    public static readonly TableDef IdentityUsers = new("dbo", "IdentityUsers",
    [
        Str("Id", 128), Str("Email", 256, true), Bit("EmailConfirmed"), Str("PasswordHash", null, true), Str("SecurityStamp", null, true),
        Str("PhoneNumber", null, true), Bit("PhoneNumberConfirmed"), Bit("TwoFactorEnabled"), new("LockoutEndDateUtc", "datetime", true, typeof(DateTime)),
        Bit("LockoutEnabled"), Int("AccessFailedCount"), Str("UserName", 256), Str("DisplayName", 100),
    ]);

    public static readonly TableDef IdentityUserRoles = new("dbo", "IdentityUserRoles", [Str("UserId", 128), Str("RoleId", 128)]);

    public static readonly TableDef Tickets = new("dbo", "Tickets",
    [
        Int("TicketId"), Int("ProjectId"), Str("TicketType", 50), Str("Category", 50), Str("Title", 500), Str("Details", null), Bit("IsHtml"),
        Str("TagList", 100, true), Str("CreatedBy", 256), Dto("CreatedDate"), Dto("TargetDate", true), Dto("ResolutionDate", true), Dto("DueDate", true),
        Dec("EstimatedDuration"), Dec("ActualDuration"), Str("Owner", 256), Str("AssignedTo", 256, true), Int("TicketStatus"), Dto("CurrentStatusDate"),
        Str("CurrentStatusSetBy", 256), Str("LastUpdateBy", 256), Dto("LastUpdateDate"), Str("Priority", 25, true), Bit("AffectsCustomer"),
    ], KeepIdentity: true);

    public static readonly TableDef TicketTags = new("dbo", "TicketTags", [Int("TicketTagId"), Str("TagName", 100), Int("TicketId")], KeepIdentity: true);

    public static readonly TableDef TicketSubscribers = new("dbo", "TicketSubscribers", [Int("TicketId"), Str("SubscriberId", 256)]);

    public static readonly TableDef TicketEvents = new("dbo", "TicketEvents",
    [
        Int("TicketId"), Int("EventId"), Str("EventDescription", 500, true), Str("Comment", null, true), Bit("IsHtml"), Str("EventBy", 256),
        Dto("EventDate"), Int("ForActivity"),
    ], KeepIdentity: true);

    public static readonly TableDef TicketEventNotifications = new("dbo", "TicketEventNotifications",
        [Int("TicketId"), Int("EventId"), Str("SubscriberId", 256), Bit("IsNew"), Bit("IsRead")]);

    /// <summary>Load order (parents first).</summary>
    public static readonly IReadOnlyList<TableDef> All =
        [Projects, IdentityUsers, IdentityUserRoles, Tickets, TicketTags, TicketSubscribers, TicketEvents, TicketEventNotifications];

    /// <summary>Tables that must exist but are not written (read for configuration / role ids).</summary>
    public static readonly IReadOnlyList<(string Table, string[] Columns)> ReadOnly =
    [
        ("dbo.IdentityRoles", ["Id", "Name"]),
        ("dbo.ApplicationSettings", ["ApplicationName", "SelectListSettingsJson"]),
        ("dbo.UserSettings", ["UserId", "ListSettingsJson", "SelectedProjectId"]),
    ];

    public static readonly string[] StockRoles = ["TdAdministrators", "TdHelpDeskUsers", "TdInternalUsers", "TdPendingUsers"];
}

/// <summary>Domain enums mirrored from TicketDesk.Domain.Model (values are stored as int).</summary>
public enum TicketStatus { Active = 0, MoreInfo = 1, Resolved = 2, Closed = 3 }

[Flags]
public enum TicketActivity
{
    None = 0, AddComment = 1, SupplyMoreInfo = 2, CancelMoreInfo = 4, RequestMoreInfo = 8, TakeOver = 16, Resolve = 32, Assign = 64,
    ReAssign = 128, Pass = 256, Close = 512, ReOpen = 1024, GiveUp = 2048, ForceClose = 4096, ModifyAttachments = 8192,
    EditTicketInfo = 16384, Create = 32768, CreateOnBehalfOf = 65536,
}
