using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Attachments;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Pages;
using TicketDesk.Tickets.Settings;
using TicketDesk.Tickets.Writes;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using Validation = TicketDesk.Localization.Validation;

namespace TicketDesk.Web.Controllers;

/// <summary>The ticket page (layout page with its panels).</summary>
public sealed record TicketPageModel(TicketDetailsView Details, TicketActivity Activities, IReadOnlyList<AttachmentInfo> Files,
    IReadOnlyList<TicketEventView> Events, bool IsSubscribed, bool IsEditorDefaultHtml);

/// <summary>The new-ticket form (GET, and the POST when it comes back with errors).</summary>
public sealed record NewTicketModel(Ticket Ticket, Guid TempId, TicketActor Actor, TicketDeskSettings Settings, bool IsMultiProject,
    IReadOnlyList<Project> Projects, IReadOnlyList<UserRef> InternalUsers, IReadOnlyList<UserRef> HelpDeskUsers,
    IReadOnlyDictionary<string, string> Errors, string? EstimatedDurationText = null, string? ActualDurationText = null);

/// <summary>Fields of the new-ticket form (legacy <c>Ticket</c> model binding: empty strings bind as null).</summary>
public sealed class NewTicketForm
{
    public string? ProjectId { get; set; }
    public string? Title { get; set; }
    public string? Details { get; set; }
    public bool IsHtml { get; set; }
    public string? TicketType { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Owner { get; set; }
    public string? AssignedTo { get; set; }
    public string? TagList { get; set; }
    public bool AffectsCustomer { get; set; }
    public string? DueDateAsString { get; set; }
    public string? TargetDateAsString { get; set; }
    public string? ResolutionDateAsString { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? ActualDuration { get; set; }
}

/// <summary>
/// Legacy <c>TicketController</c>: the ticket page and its panels (wave 2), the subscription toggle (wave 3), new ticket
/// (wave 4, GET and POST together: the form carries the new app's antiforgery token).
/// </summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class TicketController(TicketPageService pages, TicketUsers users, TicketWriter writer, IAttachmentStore store,
    IProjectSelection projects, IAuthorizationService authorization, TicketDesk.Tickets.TicketCenter.TicketCenterService center) : LegacyPageController
{
    private CancellationToken Ct => HttpContext.RequestAborted;
    private TicketActor Actor => TicketActor.From(User);

    /// <summary>Legacy class route <c>ticket/{action=index}</c> on the parameterless Index: back to the ticket center.</summary>
    [Route("ticket")]
    [Route("ticket/index")]
    [MigratedRoute("ticket.list-redirect")]
    public IActionResult ListRedirect() => new LegacyRedirectResult("/tickets");

    [Route("ticket/{id:int}")]
    [MigratedRoute("ticket.view")]
    public async Task<IActionResult> Index(int id)
    {
        var ticket = await pages.FindAsync(id, Ct);
        if (ticket == null) return new LegacyRedirectResult("/tickets");
        var details = await pages.DetailsAsync(ticket, Ct);
        var events = (await pages.EventsAsync(id, Ct))!;
        var files = await MaySeeFilesAsync(id) ? await store.ListAsync(id, Ct) : [];
        var settings = await pages.SettingsAsync(Ct);
        var userId = User.GetUserId();
        return View(new TicketPageModel(details, TicketActivityRules.Valid(ticket, Actor), files, events,
            ticket.TicketSubscribers.Any(s => s.SubscriberId == userId), settings.IsEditorDefaultHtml));
    }

    [Route("ticket/ticket-details")]
    [MigratedRoute("ticket.details")]
    public async Task<IActionResult> TicketDetails(int ticketId)
    {
        var details = await pages.DetailsAsync(ticketId, Ct);
        return details == null ? NotFound() : PartialView("_TicketDetails", details);
    }

    [Route("ticket/ticket-events")]
    [MigratedRoute("ticket.events")]
    public async Task<IActionResult> TicketEvents(int ticketId)
    {
        var events = await pages.EventsAsync(ticketId, Ct);
        return events == null ? NotFound() : PartialView("_TicketEvents", events);
    }

    /// <summary>The page's subscribe/unsubscribe button (AJAX POST with the page's token header, S3).</summary>
    [HttpPost]
    [Route("ticket/change-ticket-subscription")]
    [MigratedRoute("ticket.subscription")]
    public async Task<IActionResult> ChangeTicketSubscription(int ticketId)
    {
        var subscribed = await writer.ToggleSubscriptionAsync(ticketId, User.GetUserId() ?? "", Ct);
        return subscribed == null ? NotFound() : Json(new { isSubscribed = subscribed.Value });
    }

    [HttpGet]
    [Route("ticket/new")]
    [MigratedRoute("ticket.new.get")]
    public async Task<IActionResult> New()
    {
        var settings = await pages.SettingsAsync(Ct);
        var ticket = new Ticket { Owner = User.GetUserId() ?? "", IsHtml = settings.IsEditorDefaultHtml };
        return View(await NewModelAsync(ticket, Guid.NewGuid(), settings, new Dictionary<string, string>()));
    }

    [HttpPost]
    [Route("ticket/new")]
    [MigratedRoute("ticket.new.post")]
    public async Task<IActionResult> New([FromForm] NewTicketForm form, [FromForm] Guid tempId)
    {
        var settings = await pages.SettingsAsync(Ct);
        var errors = new Dictionary<string, string>();
        var ticket = Bind(form, errors);
        if (errors.Count == 0)
        {
            if (await writer.CreateAsync(ticket, Actor, Ct))
            {
                await store.CommitPendingAsync(tempId, ticket.TicketId, Ct);
                return new LegacyRedirectResult($"/ticket/{ticket.TicketId.ToString(CultureInfo.InvariantCulture)}");
            }
            // legacy: ValidateEntity refused the Create permission (DbEntityValidationException, caught): the form again
        }
        return View(await NewModelAsync(ticket, tempId, settings, errors, form.EstimatedDuration, form.ActualDuration));
    }

    /// <summary>The legacy binder + data annotations (only incoming fields are validated: [ValidateOnlyIncomingValues]).</summary>
    private Ticket Bind(NewTicketForm f, Dictionary<string, string> errors)
    {
        string Display(string r) => Views.FormHtml.Display(r);
        void Require(string field, string? value, string resource)
        {
            if (Request.Form.ContainsKey(field) && string.IsNullOrEmpty(value)) errors.TryAdd(field, string.Format(CultureInfo.CurrentCulture, Validation.FieldRequired, Display(resource)));
        }
        void Max(string field, string? value, int max, string resource)
        {
            if (value != null && value.Length > max) errors.TryAdd(field, string.Format(CultureInfo.CurrentCulture, Validation.FieldMaximumLength, Display(resource), max));
        }
        decimal? Number(string field, string? value, string resource)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var d)) return d;
            errors.TryAdd(field, $"The value '{value}' is not valid for {Display(resource)}.");
            return null;
        }
        var t = new Ticket
        {
            Title = f.Title ?? "", TicketType = f.TicketType ?? "", Category = f.Category ?? "", Priority = f.Priority, Owner = f.Owner ?? "",
            AssignedTo = f.AssignedTo, TagList = f.TagList, IsHtml = f.IsHtml, AffectsCustomer = f.AffectsCustomer,
            DueDate = LegacyDates.Parse(f.DueDateAsString, null), TargetDate = LegacyDates.Parse(f.TargetDateAsString, null),
            ResolutionDate = LegacyDates.Parse(f.ResolutionDateAsString, null),
            EstimatedDuration = Number("EstimatedDuration", f.EstimatedDuration, "TicketEstimatedDuration"),
            ActualDuration = Number("ActualDuration", f.ActualDuration, "TicketActualDuration"),
        };
        if (int.TryParse(f.ProjectId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var projectId)) t.ProjectId = projectId;
        else if (Request.Form.ContainsKey("ProjectId")) errors.TryAdd("ProjectId", string.Format(CultureInfo.CurrentCulture, Validation.FieldRequired, Display("TicketProject")));
        Require("Title", f.Title, "TicketTitle");
        Require("TicketType", f.TicketType, "TicketTicketType");
        Require("Category", f.Category, "TicketCategory");
        Require("Owner", f.Owner, "TicketOwner");
        Max("Title", f.Title, 500, "TicketTitle");
        Max("TicketType", f.TicketType, 50, "TicketTicketType");
        Max("Category", f.Category, 50, "TicketCategory");
        Max("TagList", f.TagList, 100, "TicketTagList");
        Max("Priority", f.Priority, 25, "TicketPriority");
        Max("Owner", f.Owner, 256, "TicketOwner");
        Max("AssignedTo", f.AssignedTo, 256, "TicketAssignedTo");
        // legacy: summernote's empty markup counts as empty; Details is [Required]
        t.Details = (t.IsHtml ? TextRules.StripHtmlWhenEmpty(f.Details) : f.Details) ?? "";
        if (string.IsNullOrEmpty(t.Details)) errors.TryAdd("Details", t.IsHtml ? TicketDesk.Localization.Controllers.Strings.RequiredField
            : string.Format(CultureInfo.CurrentCulture, Validation.FieldRequired, Display("TicketDetails")));
        if (!string.IsNullOrEmpty(t.TagList) && t.TagList.Split(',').Any(tag => tag.Trim().Length == 0 || tag.Trim().Length > 100))
            errors.TryAdd("", string.Format(CultureInfo.CurrentCulture, Validation.FieldRequired, Display("TicketTagList")));
        return t;
    }

    private async Task<NewTicketModel> NewModelAsync(Ticket ticket, Guid tempId, TicketDeskSettings settings, Dictionary<string, string> errors,
        string? estimated = null, string? actual = null)
    {
        var projectCount = await pages.ProjectCountAsync(Ct);
        var isMulti = projectCount > 1;
        if (ticket.ProjectId == 0)
        {
            // legacy SetProjectInfoForModelAsync: the user's selected project when there are several, else the only one
            ticket.ProjectId = isMulti ? await projects.GetSelectedProjectIdAsync(User.GetUserId(), Ct)
                : (await pages.ProjectsAsync(Ct)).OrderBy(p => p.ProjectId).Select(p => p.ProjectId).FirstOrDefault();
        }
        return new NewTicketModel(ticket, tempId, Actor, settings, isMulti, isMulti ? await pages.ProjectsAsync(Ct) : [],
            await users.InternalUsersAsync(Ct), await users.HelpDeskUsersAsync(Ct), errors,
            estimated ?? ticket.EstimatedDuration?.ToString(CultureInfo.CurrentCulture), actual ?? ticket.ActualDuration?.ToString(CultureInfo.CurrentCulture));
    }

    private async Task<bool> MaySeeFilesAsync(int ticketId)
    {
        var access = await center.GetAccessAsync(ticketId, Ct);
        return access != null && (await authorization.AuthorizeAsync(User, access, TicketDesk.Tickets.TicketAttachmentsRequirement.Instance)).Succeeded;
    }
}

/// <summary>Legacy <c>SummerNoteStringExtensions.StripHtmlWhenEmpty</c>: markup without text (summernote's empty paragraphs) is null.</summary>
public static partial class TextRules
{
    [System.Text.RegularExpressions.GeneratedRegex("<[^>]*>")]
    private static partial System.Text.RegularExpressions.Regex Tags();

    public static string? StripHtmlWhenEmpty(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var text = Tags().Replace(content, "").ToLowerInvariant();
        return text.Replace("\n", " ").Replace("&nbsp;", " ").Trim().Length > 0 ? content : null;
    }
}
