using Microsoft.AspNetCore.Mvc.Rendering;

namespace TicketDesk.Web.Legacy;

public static class HtmlHelperExtensions
{
    /// <summary>Legacy <c>Html.IsActiveRoute(action, controller)</c>: "active" when the current route is that action, else "".</summary>
    public static string IsActiveRoute(this IHtmlHelper html, string actionName, string controllerName)
    {
        var values = html.ViewContext.RouteData.Values;
        return values["controller"] as string == controllerName && values["action"] as string == actionName ? "active" : "";
    }
}
