using Microsoft.AspNetCore.Mvc;

namespace TicketDesk.Web.Controllers;

/// <summary>Base of the controllers that replace legacy MVC 5 pages: HTML, not part of the API description.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
public abstract class LegacyPageController : Controller;

public static class StateChangingRoutes
{
    /// <summary>
    /// S3: what a GET or HEAD of a URL that changes state answers since the action is a POST with the antiforgery token
    /// (<c>tickets/filterList</c> since Round 2; <c>language</c>, <c>tickets/sortList</c>, <c>tickets/reset-user-lists</c>,
    /// <c>navigation/change-project</c> since Round 5): <b>405</b> with <c>Allow: POST</c> and no body. Nothing runs, so an old
    /// bookmark, a prefetch or a link on another site changes nothing.
    /// </summary>
    public static IActionResult PostOnly(this ControllerBase controller)
    {
        controller.Response.Headers.Allow = "POST";
        return controller.StatusCode(StatusCodes.Status405MethodNotAllowed);
    }
}
