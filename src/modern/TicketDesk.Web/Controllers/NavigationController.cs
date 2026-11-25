using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Tickets.TicketCenter;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>Legacy <c>NavigationController.ChangeProject</c> (wave 3); the project menu itself is a view component.</summary>
public sealed class NavigationController(IProjectSelection projects) : LegacyPageController
{
    /// <summary>
    /// S7: signed-in users only (the legacy action had no [Authorize] and ran with a null user id). S3 (Round 5): a POST with
    /// the antiforgery token; the project menu's items are forms (links in the legacy app).
    /// </summary>
    [Authorize]
    [HttpPost("navigation/change-project/{projectId:int}")]
    [MigratedRoute("navigation.change-project")]
    public async Task<IActionResult> ChangeProject(int projectId)
    {
        var userId = User.GetUserId() ?? "";
        var defaults = ListSettings.Serialize(ListSettings.Defaults(userId, User.IsHelpDeskOrAdmin(), n => TicketCenterViewModel.DefaultName(n) ?? n));
        await projects.SelectProjectAsync(userId, projectId, defaults, HttpContext.RequestAborted);
        var referrer = Request.Headers.Referer.ToString();
        return Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? new LegacyRedirectResult(uri.ToString()) : new LegacyRedirectResult("/");
    }

    /// <summary>S3: a GET or HEAD of the old link changes nothing (405, <c>Allow: POST</c>).</summary>
    [Authorize]
    [HttpGet("navigation/change-project/{projectId:int}"), HttpHead("navigation/change-project/{projectId:int}")]
    [MigratedRoute("navigation.change-project")]
    public IActionResult ChangeProjectByGet() => this.PostOnly();
}
