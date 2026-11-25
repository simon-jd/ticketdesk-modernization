using System.ComponentModel;
using TicketDesk.Attachments;
using TicketDesk.Data.Entities;
using TicketDesk.Users;

namespace TicketDesk.Tickets;

/// <summary>Query string of <c>GET /api/tickets</c>.</summary>
public sealed class TicketListQuery
{
    [Description("1-based page number (default 1).")]
    public int? Page { get; set; }

    [Description("Items per page, 1-100 (default 20, like the legacy lists).")]
    public int? PageSize { get; set; }

    [Description("Legacy ticket-center list: unassigned, assignedToMe, mytickets, opentickets, historytickets.")]
    public string? List { get; set; }

    [Description("Comma-separated sort fields, '-' prefix for descending, e.g. '-lastUpdateDate,title'. Whitelisted fields only.")]
    public string? Sort { get; set; }

    [Description("Status filter, repeatable: Active, MoreInfo, Resolved, Closed.")]
    public string[]? Status { get; set; }

    [Description("Project id filter.")]
    public int? ProjectId { get; set; }

    [Description("Owner user id filter.")]
    public string? Owner { get; set; }

    [Description("Assigned user id filter.")]
    public string? AssignedTo { get; set; }

    [Description("Tag name filter (exact, case-insensitive).")]
    public string? Tag { get; set; }
}

public sealed record TicketListItem(
    int TicketId,
    int ProjectId,
    string Title,
    string TicketType,
    string Category,
    string? Priority,
    TicketStatus Status,
    UserRef Owner,
    UserRef? AssignedTo,
    DateTimeOffset CreatedDate,
    DateTimeOffset LastUpdateDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? TargetDate,
    string? TagList);

public sealed record TicketPage(int Page, int PageSize, int TotalCount, int TotalPages, string Sort, IReadOnlyList<TicketListItem> Items);

public sealed record TicketEventDto(
    int EventId,
    UserRef EventBy,
    DateTimeOffset EventDate,
    string? EventDescription,
    string? Comment,
    bool IsHtml,
    TicketActivity ForActivity)
{
    [Description("The comment as the ticket page shows it: Markdown rendered (unless IsHtml) and sanitized with the legacy allow-lists.")]
    public required string CommentHtml { get; init; }
}

public sealed record TicketDetail(
    int TicketId,
    int ProjectId,
    string? ProjectName,
    string Title,
    string Details,
    bool IsHtml,
    string TicketType,
    string Category,
    string? Priority,
    TicketStatus Status,
    bool AffectsCustomer,
    UserRef Owner,
    UserRef? AssignedTo,
    UserRef CreatedBy,
    DateTimeOffset CreatedDate,
    UserRef CurrentStatusSetBy,
    DateTimeOffset CurrentStatusDate,
    UserRef LastUpdateBy,
    DateTimeOffset LastUpdateDate,
    DateTimeOffset? DueDate,
    DateTimeOffset? TargetDate,
    DateTimeOffset? ResolutionDate,
    decimal? EstimatedDuration,
    decimal? ActualDuration,
    string? TagList,
    IReadOnlyList<string> Tags,
    [property: Description("Newest first, like the legacy ticket-events panel.")]
    IReadOnlyList<TicketEventDto> Events,
    [property: Description("False when the caller may not see this ticket's attachments (S8); Attachments is then null.")]
    bool AttachmentsVisible,
    IReadOnlyList<AttachmentInfo>? Attachments)
{
    [Description("Details as the ticket page shows them: Markdown rendered (unless IsHtml) and sanitized with the legacy allow-lists.")]
    public required string DetailsHtml { get; init; }

    [Description("The caller is subscribed to this ticket's notifications.")]
    public required bool IsSubscribed { get; init; }

    [Description("The workflow activities the caller may perform now (POST /api/tickets/{id}/activities), in display order.")]
    public required IReadOnlyList<string> Activities { get; init; }
}
