using System.ComponentModel;
using System.Globalization;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Settings;
using TicketDesk.Users;

namespace TicketDesk.Tickets;

public sealed record ProjectOption(int ProjectId, string ProjectName);

/// <summary>Which ticket fields the caller may set (legacy <c>TicketExtensions</c> / the new-ticket view).</summary>
public sealed record TicketFieldPermissions(bool CanSetOwner, bool CanSetAssignedTo, bool CanSetPriority, bool CanSetTags)
{
    public static TicketFieldPermissions For(TicketActor a, TicketDeskSettings s) => new(
        CanSetOwner: (a.IsInternal && s.AllowInternalUsersToSetOwner) || a.IsHelpDesk || a.IsAdministrator,
        CanSetAssignedTo: (a.IsInternal && s.AllowInternalUsersToSetAssigned) || a.IsHelpDesk || a.IsAdministrator,
        CanSetPriority: a.IsAdministrator || a.IsHelpDesk || (a.IsInternal && s.AllowInternalUsersToEditPriority),
        CanSetTags: a.IsHelpDesk || s.AllowInternalUsersToEditTags);
}

/// <summary>The choices of the ticket fields for the signed-in user (new-ticket form, list filters).</summary>
public sealed record TicketOptions(
    [property: Description("Projects by name.")]
    IReadOnlyList<ProjectOption> Projects,
    IReadOnlyList<string> TicketTypes,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Priorities,
    [property: Description("Possible owners: internal users, help desk and admins, by display name.")]
    IReadOnlyList<UserRef> InternalUsers,
    [property: Description("Possible assignees: help desk and admins, by display name.")]
    IReadOnlyList<UserRef> HelpDeskUsers,
    TicketFieldPermissions Permissions);

/// <summary>Body of <c>POST /api/tickets</c>. Details are Markdown unless <see cref="IsHtml"/>; both are sanitized when shown.</summary>
public sealed class NewTicketRequest
{
    [Description("Required when there are several projects; defaults to the only project otherwise.")]
    public int? ProjectId { get; set; }
    public string? Title { get; set; }
    public string? Details { get; set; }
    public bool IsHtml { get; set; }
    [Description("One of the ticket types of GET /api/tickets/options.")]
    public string? TicketType { get; set; }
    [Description("One of the categories of GET /api/tickets/options.")]
    public string? Category { get; set; }
    [Description("One of the priorities, or null. Only with permissions.canSetPriority.")]
    public string? Priority { get; set; }
    [Description("User id; empty means the caller. Someone else only with permissions.canSetOwner.")]
    public string? Owner { get; set; }
    [Description("Help-desk user id, or null for unassigned. Only with permissions.canSetAssignedTo.")]
    public string? AssignedTo { get; set; }
    [Description("Comma-separated tags (each 1-100 characters, 100 in all). Only with permissions.canSetTags.")]
    public string? TagList { get; set; }
    public bool AffectsCustomer { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? TargetDate { get; set; }
    public DateOnly? ResolutionDate { get; set; }
    public decimal? EstimatedDuration { get; set; }
    public decimal? ActualDuration { get; set; }
    [Description("Pending uploads (POST /file/upload with this tempId) to attach to the new ticket.")]
    public Guid? TempId { get; set; }
}

public sealed record TicketCreated(int TicketId);

/// <summary>
/// Validation of <see cref="NewTicketRequest"/>: the legacy data annotations of <c>Ticket</c> (required fields, lengths, tag rules)
/// plus what the legacy form only enforced by hiding fields: values from the configured lists, owners and assignees from the
/// right roles, and the permission settings for owner, assignee, priority and tags. Errors are keyed by camelCase field name.
/// </summary>
public static class NewTicketValidation
{
    public static Ticket? TryCreate(NewTicketRequest r, TicketActor actor, TicketDeskSettings settings, IReadOnlyList<ProjectOption> projects,
        IReadOnlyList<UserRef> internalUsers, IReadOnlyList<UserRef> helpDeskUsers, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var e = errors;
        void Add(string field, string message) { if (!e.ContainsKey(field)) e[field] = [message]; }
        string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var p = TicketFieldPermissions.For(actor, settings);

        int projectId = 0;
        if (r.ProjectId is { } pid)
        {
            if (projects.Any(x => x.ProjectId == pid)) projectId = pid;
            else Add("projectId", "Unknown project.");
        }
        else if (projects.Count == 1) projectId = projects[0].ProjectId;
        else Add("projectId", "The Project field is required.");

        var title = Trim(r.Title);
        if (title == null) Add("title", "The Title field is required.");
        else if (title.Length > 500) Add("title", "The Title field may have at most 500 characters.");

        var details = r.IsHtml ? StripHtmlWhenEmpty(r.Details) : r.Details;
        if (string.IsNullOrWhiteSpace(details)) Add("details", "The Details field is required.");

        var type = Trim(r.TicketType);
        if (type == null) Add("ticketType", "The Ticket Type field is required.");
        else if (!settings.TicketTypesList.Contains(type)) Add("ticketType", "Unknown ticket type.");

        var category = Trim(r.Category);
        if (category == null) Add("category", "The Category field is required.");
        else if (!settings.CategoryList.Contains(category)) Add("category", "Unknown category.");

        var priority = Trim(r.Priority);
        if (priority != null && !p.CanSetPriority) Add("priority", "You may not set the priority.");
        else if (priority != null && !settings.PriorityList.Contains(priority)) Add("priority", "Unknown priority.");

        var owner = Trim(r.Owner) ?? actor.UserId;
        if (!string.Equals(owner, actor.UserId, StringComparison.OrdinalIgnoreCase))
        {
            if (!p.CanSetOwner) Add("owner", "You may not create tickets for someone else.");
            else if (!internalUsers.Any(u => string.Equals(u.Id, owner, StringComparison.OrdinalIgnoreCase))) Add("owner", "The owner must be a TicketDesk user.");
        }

        var assigned = Trim(r.AssignedTo);
        if (assigned != null && !p.CanSetAssignedTo) Add("assignedTo", "You may not assign tickets.");
        else if (assigned != null && !helpDeskUsers.Any(u => string.Equals(u.Id, assigned, StringComparison.OrdinalIgnoreCase))) Add("assignedTo", "Tickets can only be assigned to help-desk users.");

        string? tagList = null;
        if (Trim(r.TagList) is { } tags)
        {
            var parts = tags.Split(',').Select(t => t.Trim()).ToArray();
            if (!p.CanSetTags) Add("tagList", "You may not set tags.");
            else if (parts.Any(t => t.Length is 0 or > 100)) Add("tagList", "Each tag needs 1 to 100 characters.");
            else
            {
                tagList = string.Join(",", parts.Distinct(StringComparer.OrdinalIgnoreCase));
                if (tagList.Length > 100) Add("tagList", "The Tags field may have at most 100 characters.");
            }
        }

        if (r.EstimatedDuration < 0) Add("estimatedDuration", "Must be 0 or more.");
        if (r.ActualDuration < 0) Add("actualDuration", "Must be 0 or more.");

        if (errors.Count > 0) return null;
        return new Ticket
        {
            ProjectId = projectId, Title = title!, Details = details!, IsHtml = r.IsHtml, TicketType = type!, Category = category!,
            Priority = priority, Owner = owner, AssignedTo = assigned, TagList = tagList, AffectsCustomer = r.AffectsCustomer,
            DueDate = AtServerMidnight(r.DueDate), TargetDate = AtServerMidnight(r.TargetDate), ResolutionDate = AtServerMidnight(r.ResolutionDate),
            EstimatedDuration = r.EstimatedDuration, ActualDuration = r.ActualDuration,
        };
    }

    /// <summary>What the legacy binder made of a picked date (<c>LegacyDates.Parse</c>): midnight in the server's offset.</summary>
    public static DateTimeOffset? AtServerMidnight(DateOnly? date) => date is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)) : null;

    /// <summary>Legacy <c>StripHtmlWhenEmpty</c>: HTML without text counts as empty.</summary>
    public static string? StripHtmlWhenEmpty(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var text = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]*>", "").ToLower(CultureInfo.InvariantCulture);
        return text.Replace("\n", " ").Replace("&nbsp;", " ").Trim().Length > 0 ? content : null;
    }
}
