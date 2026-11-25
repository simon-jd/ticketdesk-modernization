using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Tickets.TicketCenter;
using TicketDesk.Users;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;
using DomainStrings = TicketDesk.Localization.Domain.Strings;

namespace TicketDesk.Web.Controllers;

public sealed record TicketCenterViewModel(
    IReadOnlyList<ListSetting> UserLists,
    ListSetting Current,
    TicketListPage Tickets,
    bool DisplayProjects,
    IReadOnlyList<UserRef> InternalUsers,
    IReadOnlyList<UserRef> HelpDeskUsers,
    DateTime Today)
{
    public int CurrentPage => Tickets.PageNumber;

    /// <summary>Legacy <c>ListDisplayName</c> getter: the five default lists always show their localized name.</summary>
    public static string DisplayName(ListSetting list) => DefaultName(list.ListName) ?? list.ListDisplayName ?? list.ListName;

    public static string? DefaultName(string listName) => listName switch
    {
        "unassigned" => DomainStrings.DefaultListNameUnassigned,
        "assignedToMe" => DomainStrings.DefaultListNameAssignedToMe,
        "mytickets" => DomainStrings.DefaultListNameMyTickets,
        "opentickets" => DomainStrings.DefaultListNameOpenTickets,
        "historytickets" => DomainStrings.DefaultListNameHistoryTickets,
        _ => null,
    };
}

/// <summary>
/// Legacy <c>TicketCenterController</c>: the ticket lists (wave 2: list page and pager) and the per-user list settings it
/// changes (wave 3: filter, sort, reset). The settings live in <c>UserSettings.ListSettingsJson</c>, shared with the legacy
/// app in its own JSON format.
/// </summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class TicketCenterController(TicketCenterService center, IProjectSelection projects, IUserDirectory users,
    Microsoft.Extensions.Options.IOptionsSnapshot<SiteOptions> site) : LegacyPageController
{
    private string UserId => User.GetUserId() ?? "";
    private bool IsHelpDesk => User.IsHelpDeskOrAdmin();

    private Task<List<ListSetting>> ListsAsync() =>
        center.GetListsAsync(UserId, IsHelpDesk, n => TicketCenterViewModel.DefaultName(n) ?? n, HttpContext.RequestAborted);

    [Route("tickets/{listName?}/{page:int?}")]
    [MigratedRoute("tickets.index")]
    public async Task<IActionResult> Index(string? listName, int? page)
    {
        listName ??= IsHelpDesk ? "unassigned" : "mytickets";
        var model = await ModelAsync(await ListsAsync(), listName, page ?? 1);
        // L2: the legacy app threw (500) for a list the user does not have (e.g. unassigned for an internal user)
        return model == null ? new LegacyRedirectResult("/access-denied") : View(model);
    }

    [Route("tickets/pageList/{listName=mytickets}/{page:int?}")]
    [MigratedRoute("tickets.page")]
    public Task<IActionResult> PageList(string listName, int? page) => ListPartialAsync(listName, page ?? 1);

    /// <summary>The filter form posts here (with the new app's antiforgery token, rendered by the list page).</summary>
    [HttpPost]
    [Route("tickets/filterList/{listName=opentickets}/{page:int?}")]
    [MigratedRoute("tickets.filter")]
    public async Task<IActionResult> FilterList(string listName, int? pageSize, string? ticketStatus, string? owner, string? assignedTo)
    {
        // legacy: pageSize is a non-nullable action parameter, so a post without it failed
        if (pageSize is not (10 or 20 or 30 or 50 or 100)) return BadRequest();
        var lists = await ListsAsync();
        var list = lists.FirstOrDefault(l => l.ListName.Equals(listName, StringComparison.OrdinalIgnoreCase));
        if (list == null) return new LegacyRedirectResult("/access-denied");
        list.ModifyFilterSettings(pageSize.Value, ticketStatus, owner, assignedTo);
        await center.SaveListsAsync(UserId, lists, HttpContext.RequestAborted);
        return await ListPartialAsync(listName, 1);
    }

    /// <summary>S3: changing settings by GET is not offered; the legacy app took any verb here.</summary>
    [HttpGet, HttpHead]
    [Route("tickets/filterList/{listName=opentickets}/{page:int?}")]
    [MigratedRoute("tickets.filter")]
    public IActionResult FilterListByGet() => this.PostOnly();

    /// <summary>
    /// The column headers post here (AJAX, <c>data-ajax-method="POST"</c>, the page's token in the RequestVerificationToken
    /// header; S3 since Round 5, a GET in the legacy app). Only the whitelisted columns can be stored (S5); anything else is a
    /// 400 and nothing is saved.
    /// </summary>
    [HttpPost]
    [Route("tickets/sortList/{listName=opentickets}/{page:int?}")]
    [MigratedRoute("tickets.sort")]
    public async Task<IActionResult> SortList(string listName, int? page, string? columnName, bool isMultiSort = false)
    {
        if (!TicketCenterService.IsSortable(columnName))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown sort column.");
        var lists = await ListsAsync();
        var list = lists.FirstOrDefault(l => l.ListName.Equals(listName, StringComparison.OrdinalIgnoreCase));
        if (list == null) return new LegacyRedirectResult("/access-denied");
        list.ChangeSort(columnName!, isMultiSort);
        await center.SaveListsAsync(UserId, lists, HttpContext.RequestAborted);
        return await ListPartialAsync(listName, page ?? 1);
    }

    /// <summary>S3 (Round 5): changing the sort by GET is not offered any more.</summary>
    [HttpGet, HttpHead]
    [Route("tickets/sortList/{listName=opentickets}/{page:int?}")]
    [MigratedRoute("tickets.sort")]
    public IActionResult SortListByGet() => this.PostOnly();

    /// <summary>The reset dialog's button posts here (a form with the token since Round 5; a link in the legacy app).</summary>
    [HttpPost]
    [Route("tickets/reset-user-lists")]
    [MigratedRoute("tickets.reset-lists")]
    public async Task<IActionResult> ResetUserLists()
    {
        var defaults = ListSettings.Defaults(UserId, IsHelpDesk, n => TicketCenterViewModel.DefaultName(n) ?? n);
        await center.SaveListsAsync(UserId, defaults, HttpContext.RequestAborted);
        return new LegacyRedirectResult("/tickets");
    }

    [HttpGet, HttpHead]
    [Route("tickets/reset-user-lists")]
    [MigratedRoute("tickets.reset-lists")]
    public IActionResult ResetUserListsByGet() => this.PostOnly();

    private async Task<IActionResult> ListPartialAsync(string listName, int page)
    {
        var model = await ModelAsync(await ListsAsync(), listName, page);
        return model == null ? new LegacyRedirectResult("/access-denied") : PartialView("_TicketList", model);
    }

    private async Task<TicketCenterViewModel?> ModelAsync(List<ListSetting> lists, string listName, int page)
    {
        var current = lists.FirstOrDefault(l => l.ListName.Equals(listName, StringComparison.InvariantCultureIgnoreCase));
        if (current == null) return null;
        page = Math.Max(page, 1); // the legacy pager threw for page 0
        var ct = HttpContext.RequestAborted;
        var selected = await projects.GetSelectedProjectIdAsync(UserId, ct);
        var tickets = await center.GetPageAsync(current, selected, page, ct);
        var several = await projects.HasSeveralProjectsAsync(ct);
        var internalUsers = await users.GetUsersInRoleAsync(TicketDeskRoles.InternalUsers, ct);
        var helpDesk = await users.GetUsersInRoleAsync(TicketDeskRoles.HelpDeskUsers, ct);
        return new TicketCenterViewModel(lists.OrderBy(l => l.ListMenuDisplayOrder).ToList(), current, tickets, several, internalUsers, helpDesk,
            site.Value.Today());
    }
}
