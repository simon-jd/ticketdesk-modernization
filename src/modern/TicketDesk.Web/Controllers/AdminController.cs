using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Search;
using TicketDesk.Users;
using TicketDesk.Web.Migration;
using ControllerStrings = TicketDesk.Localization.Controllers.Strings;

namespace TicketDesk.Web.Controllers;

/// <summary>Read-only admin and account pages of wave 2 (legacy UserAdministration, DataManagement, SearchManager, Account).</summary>
public sealed class AdminController(IUserDirectory users, IProjectSelection projects, SearchIndexStatus searchIndex) : LegacyPageController
{
    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/projects")]
    [MigratedRoute("admin.projects")]
    public async Task<IActionResult> Projects() => View(await projects.ListWithTicketCountsAsync(HttpContext.RequestAborted));

    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/users/{page:int?}")]
    [MigratedRoute("admin.users")]
    public async Task<IActionResult> Users(int? page) => View("Users", await users.GetAccountsPageAsync(page ?? 1, HttpContext.RequestAborted));

    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/users/page/{page:int?}")]
    [MigratedRoute("admin.users-page")]
    public async Task<IActionResult> UsersPage(int? page) => PartialView("_UserList", await users.GetAccountsPageAsync(page ?? 1, HttpContext.RequestAborted));

    /// <summary>The page links to remove/create demo data, which are retired (state-changing GETs) and stay on the legacy app.</summary>
    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/data-management/demo")]
    [MigratedRoute("admin.demo")]
    public IActionResult Demo() => View();

    /// <summary>
    /// Search management after Lucene (L4): the legacy page linked to remove/populate the Lucene index; SQL Server keeps the
    /// full-text indexes current by itself, so the page reports their state and has no actions.
    /// </summary>
    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/search-manager")]
    [MigratedRoute("admin.search-manager")]
    public async Task<IActionResult> SearchManager() => View(await searchIndex.ReadAsync(HttpContext.RequestAborted));

    /// <summary>
    /// The legacy remove-index / populate-index GETs (state-changing, and the cause of L4) are retired with Lucene: the new
    /// app answers them itself, so a bookmark can no longer reach the legacy index through the gateway. 410 Gone with a
    /// pointer to the status page.
    /// </summary>
    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/search-manager/remove-index")]
    [MigratedRoute("admin.search-manager.remove")]
    public IActionResult SearchManagerRemoveIndex() => SearchManagerRetired();

    [Authorize(Policy = TicketDeskPolicies.Administrator)]
    [Route("admin/search-manager/populate-index")]
    [MigratedRoute("admin.search-manager.populate")]
    public IActionResult SearchManagerPopulateIndex() => SearchManagerRetired();

    private ViewResult SearchManagerRetired()
    {
        Response.StatusCode = StatusCodes.Status410Gone;
        return View("SearchManagerRetired");
    }

    public enum AccountMessageId { ChangePasswordSuccess, ProfileSaveSuccess, Error }

    /// <summary>Legacy <c>AccountController.Manage</c>: any signed-in user (TdAuthorize without roles).</summary>
    [Authorize]
    [Route("account/manage")]
    [MigratedRoute("account.manage")]
    public IActionResult Manage(AccountMessageId? message)
    {
        ViewData["StatusMessage"] = message switch
        {
            AccountMessageId.ChangePasswordSuccess => ControllerStrings.ChangePasswordSuccess,
            AccountMessageId.Error => ControllerStrings.Error,
            AccountMessageId.ProfileSaveSuccess => ControllerStrings.ProfileSaveSuccess,
            _ => "",
        };
        return View();
    }
}
