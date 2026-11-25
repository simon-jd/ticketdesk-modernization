using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Attachments;
using TicketDesk.Data.Entities;
using TicketDesk.Identity;
using TicketDesk.Tickets.Activities;
using TicketDesk.Tickets.Pages;
using TicketDesk.Tickets.Settings;
using TicketDesk.Tickets.Writes;
using TicketDesk.Users;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>An activity form (legacy partial <c>_ActivityForm</c>).</summary>
public sealed record ActivityFormModel(Ticket Ticket, TicketActivity Activity, Guid TempId, bool IsEditorDefaultHtml, bool CommentRequired,
    bool IsMultiProject, TicketDeskSettings Settings, TicketActor Actor, IReadOnlyList<UserRef> HelpDeskUsers, IReadOnlyList<UserRef> InternalUsers,
    IReadOnlyList<Project> Projects, string? Comment = null);

/// <summary>
/// Legacy <c>TicketActivityController</c>: the activity buttons (wave 2), the activity form and its 15 posts (wave 4, one
/// group: the form carries the new app's antiforgery token). Success is an empty 200, a refused activity re-renders the
/// form (200 with a body), as the legacy client script expects.
/// </summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class TicketActivityController(TicketPageService pages, TicketUsers users, TicketWriter writer, IAttachmentStore store) : LegacyPageController
{
    private CancellationToken Ct => HttpContext.RequestAborted;
    private TicketActor Actor => TicketActor.From(User);

    [Route("ticket-activity/activity-buttons")]
    [MigratedRoute("activity.buttons")]
    public async Task<IActionResult> ActivityButtons(int ticketId)
    {
        var ticket = await pages.FindAsync(ticketId, Ct);
        return ticket == null ? NotFound() : PartialView("_ActivityButtons", TicketActivityRules.Valid(ticket, Actor));
    }

    [Route("ticket-activity/load-activity")]
    [MigratedRoute("activity.load")]
    public async Task<IActionResult> LoadActivity(TicketActivity activity, int ticketId, Guid? tempId)
    {
        var ticket = await pages.FindAsync(ticketId, Ct);
        if (ticket == null) return NotFound();
        return PartialView("_ActivityForm", await FormAsync(ticket, activity, tempId ?? Guid.NewGuid(), null));
    }

    private async Task<ActivityFormModel> FormAsync(Ticket ticket, TicketActivity activity, Guid tempId, string? comment)
    {
        var settings = await pages.SettingsAsync(Ct);
        var needsUsers = activity is TicketActivity.Assign or TicketActivity.Pass or TicketActivity.ReAssign or TicketActivity.EditTicketInfo;
        var multi = activity == TicketActivity.EditTicketInfo && await pages.ProjectCountAsync(Ct) > 1;
        return new ActivityFormModel(ticket, activity, tempId, settings.IsEditorDefaultHtml, TicketActivityRules.IsCommentRequired(activity), multi,
            settings, Actor,
            needsUsers && activity != TicketActivity.EditTicketInfo ? await users.HelpDeskUsersAsync(Ct) : [],
            activity == TicketActivity.EditTicketInfo ? await users.InternalUsersAsync(Ct) : [],
            multi ? await pages.ProjectsAsync(Ct) : [], comment);
    }

    // ---------------------------------------------------------------- the 15 activity posts

    [HttpPost, Route("ticket-activity/add-comment"), MigratedRoute("activity.add-comment")]
    public Task<IActionResult> AddComment(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.AddComment) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/assign"), MigratedRoute("activity.assign")]
    public Task<IActionResult> Assign(int ticketId) => Perform(ticketId, Assignment(TicketActivity.Assign));

    [HttpPost, Route("ticket-activity/cancel-more-info"), MigratedRoute("activity.cancel-more-info")]
    public Task<IActionResult> CancelMoreInfo(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.CancelMoreInfo) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/close"), MigratedRoute("activity.close")]
    public Task<IActionResult> Close(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.Close) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/force-close"), MigratedRoute("activity.force-close")]
    public Task<IActionResult> ForceClose(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.ForceClose) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/give-up"), MigratedRoute("activity.give-up")]
    public Task<IActionResult> GiveUp(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.GiveUp) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/pass"), MigratedRoute("activity.pass")]
    public Task<IActionResult> Pass(int ticketId) => Perform(ticketId, Assignment(TicketActivity.Pass));

    [HttpPost, Route("ticket-activity/reassign"), MigratedRoute("activity.reassign")]
    public Task<IActionResult> ReAssign(int ticketId) => Perform(ticketId, Assignment(TicketActivity.ReAssign));

    [HttpPost, Route("ticket-activity/request-more-info"), MigratedRoute("activity.request-more-info")]
    public Task<IActionResult> RequestMoreInfo(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.RequestMoreInfo) { Comment = Comment() });

    /// <summary>L3: the form's checkbox is named <c>reactivate</c> (legacy markup); the legacy action bound <c>assignToMe</c> and never saw it.</summary>
    [HttpPost, Route("ticket-activity/reopen"), MigratedRoute("activity.reopen")]
    public Task<IActionResult> ReOpen(int ticketId) =>
        Perform(ticketId, new TicketActivityInput(TicketActivity.ReOpen) { Comment = Comment(), AssignToMe = Flag("assignToMe") || Flag("reactivate") });

    /// <summary>The legacy action re-rendered a failed form as ReOpen (copy-paste bug); here it is the Resolve form again.</summary>
    [HttpPost, Route("ticket-activity/resolve"), MigratedRoute("activity.resolve")]
    public Task<IActionResult> Resolve(int ticketId) => Perform(ticketId, new TicketActivityInput(TicketActivity.Resolve) { Comment = Comment() });

    [HttpPost, Route("ticket-activity/supply-more-info"), MigratedRoute("activity.supply-more-info")]
    public Task<IActionResult> SupplyMoreInfo(int ticketId) =>
        Perform(ticketId, new TicketActivityInput(TicketActivity.SupplyMoreInfo) { Comment = Comment(), Reactivate = Flag("reactivate") });

    [HttpPost, Route("ticket-activity/take-over"), MigratedRoute("activity.take-over")]
    public Task<IActionResult> TakeOver(int ticketId) =>
        Perform(ticketId, new TicketActivityInput(TicketActivity.TakeOver) { Comment = Comment(), Priority = Field("priority") });

    [HttpPost, Route("ticket-activity/edit-ticket-info"), MigratedRoute("activity.edit-ticket-info")]
    public async Task<IActionResult> EditTicketInfo(int ticketId)
    {
        var projectId = int.TryParse(Field("projectId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
        var edit = new TicketEditInput(projectId, await pages.ProjectNameAsync(projectId, Ct), Field("title"), TextRules.StripHtmlWhenEmpty(Field("details")),
            Field("priority"), Field("dueDateAsString"), Field("ticketType"), Field("category"), Field("owner"), Field("tagList"),
            Decimal("estimatedDuration"), Decimal("actualDuration"), Field("targetDateAsString"), Field("resolutionDateAsString"));
        return await Perform(ticketId, new TicketActivityInput(TicketActivity.EditTicketInfo) { Comment = Comment(), Edit = edit });
    }

    /// <summary>
    /// Deletes the files listed in <c>deleteFiles</c>, commits the uploads of <c>tempId</c> and records both in the comment
    /// (legacy markup). The permission is checked before any file is touched (legacy: after).
    /// </summary>
    [HttpPost, Route("ticket-activity/modify-attachments"), MigratedRoute("activity.modify-attachments")]
    public async Task<IActionResult> ModifyAttachments(int ticketId)
    {
        var ticket = await writer.FindAsync(ticketId, Ct);
        if (ticket == null) return NotFound();
        if (!TicketActivityRules.IsValid(ticket, Actor, TicketActivity.ModifyAttachments)) return await FailedForm(ticket, TicketActivity.ModifyAttachments);
        const string CrLf = "\r\n";
        var sb = new StringBuilder(Comment());
        var deleteFiles = Field("deleteFiles");
        if (!string.IsNullOrEmpty(deleteFiles))
        {
            sb.Append(CrLf).Append("<dl><dt>").Append(CrLf).Append(ControllerStrings.RemovedFiles).Append(CrLf).Append("</dt>").Append(CrLf);
            foreach (var file in deleteFiles.Split(','))
            {
                await store.DeleteAsync(ticketId, file, Ct);
                sb.Append("<dd>    ").Append(file).Append("</dd>").Append(CrLf);
            }
            sb.Append("</dl>").Append(CrLf);
        }
        var added = Guid.TryParse(Field("tempId"), out var tempId) ? await store.CommitPendingAsync(tempId, ticketId, Ct) : [];
        if (added.Count > 0)
        {
            sb.Append(CrLf).Append("<dl><dt>").Append(CrLf).Append(ControllerStrings.NewFiles).Append(CrLf).Append("</dt>").Append(CrLf);
            foreach (var file in added) sb.Append("<dd>    ").Append(file).Append("</dd>").Append(CrLf);
            sb.Append("</dl>").Append(CrLf);
        }
        var result = await writer.PerformAsync(ticket, Actor, new TicketActivityInput(TicketActivity.ModifyAttachments) { Comment = sb.ToString() }, Ct);
        return result == TicketWriteResult.Saved ? new EmptyResult() : await FailedForm(ticket, TicketActivity.ModifyAttachments);
    }

    private async Task<IActionResult> Perform(int ticketId, TicketActivityInput input)
    {
        var ticket = await writer.FindAsync(ticketId, Ct);
        if (ticket == null) return NotFound();
        var result = await writer.PerformAsync(ticket, Actor, input, Ct);
        return result == TicketWriteResult.Saved ? new EmptyResult() : await FailedForm(await pages.FindAsync(ticketId, Ct) ?? ticket, input.Activity, input.Comment);
    }

    /// <summary>Legacy fail case: the form again (200), for the client script to show.</summary>
    private async Task<IActionResult> FailedForm(Ticket ticket, TicketActivity activity, string? comment = null) =>
        PartialView("_ActivityForm", await FormAsync(ticket, activity, Guid.NewGuid(), comment));

    private TicketActivityInput Assignment(TicketActivity activity) =>
        new(activity) { Comment = Comment(), AssignedTo = Field("assignedTo"), Priority = Field("priority") };

    // the legacy binder: field names are case-insensitive, an empty field is "" (not null)
    private string? Field(string name) => Request.HasFormContentType && Request.Form.TryGetValue(name, out var v) ? v.ToString() : null;

    /// <summary>Legacy <c>SummernoteModelBinder</c>: summernote's empty markup counts as no comment.</summary>
    private string? Comment() => TextRules.StripHtmlWhenEmpty(Field("comment"));

    private bool Flag(string name) => Request.HasFormContentType && Request.Form.TryGetValue(name, out var v) &&
                                      v.Any(x => string.Equals(x, "true", StringComparison.OrdinalIgnoreCase));

    private decimal? Decimal(string name) =>
        decimal.TryParse(Field(name), NumberStyles.Number, CultureInfo.CurrentCulture, out var d) ? d : null;
}
