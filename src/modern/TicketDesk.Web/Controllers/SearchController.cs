using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Search;
using TicketDesk.Tickets.Pages;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>The search page: ranked results, and the project notice when there is more than one project.</summary>
public sealed record SearchPageModel(IReadOnlyList<TicketSearchResult> Results, bool IsMultiProject, string? ProjectNotice);

/// <summary>
/// Legacy <c>SearchController</c> (quick search box of the layout, <c>search?term=</c> and <c>search/{term}</c>), now on SQL
/// Server full-text (<see cref="ITicketSearch"/>) instead of the Lucene index on the VM disk. Results are limited to the
/// user's selected project, as before. Like the legacy search (role check and project only), every internal user finds every
/// ticket; <c>Search:RestrictToOwnTickets</c> switches on the S12 rule (other internal users only their own tickets), off by
/// default to keep the legacy behaviour (coordinator decision 2026-11-10, follow-up S13).
/// </summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class SearchController(ITicketSearch search, TicketSearchResults results, IProjectSelection projects, TicketPageService pages,
    Microsoft.Extensions.Options.IOptionsSnapshot<SearchOptions> options) : LegacyPageController
{
    [HttpGet("search")]
    [MigratedRoute("search.query")]
    public Task<IActionResult> Query(string? term) => Search(term);

    [HttpGet("search/{term}")]
    [MigratedRoute("search.path")]
    public Task<IActionResult> Path(string term) => Search(term);

    private async Task<IActionResult> Search(string? term)
    {
        var ct = HttpContext.RequestAborted;
        ViewData["term"] = term; // the layout's search box shows it (MVC 5 Html.TextBox read the bound "term")
        if (string.IsNullOrEmpty(term)) return View("Index", new SearchPageModel([], false, null));
        var userId = User.GetUserId() ?? "";
        var projectId = await projects.GetSelectedProjectIdAsync(userId, ct);
        var hits = await search.SearchAsync(new SearchRequest(term, projectId, new SearchViewer(userId, !options.Value.RestrictToOwnTickets || User.IsHelpDeskOrAdmin())), ct);
        var list = await results.LoadAsync(hits.Select(h => h.TicketId).ToList(), ct);
        string? notice = null;
        var multi = await pages.ProjectCountAsync(ct) > 1;
        if (multi)
            notice = projectId == 0
                ? ControllerStrings.ShowingFromAllProjects
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, ControllerStrings.ShowingFromProject, await pages.ProjectNameAsync(projectId, ct));
        return View("Index", new SearchPageModel(list, multi, notice));
    }
}
