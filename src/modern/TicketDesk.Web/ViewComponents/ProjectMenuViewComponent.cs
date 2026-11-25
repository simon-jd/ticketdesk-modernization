using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Projects;

namespace TicketDesk.Web.ViewComponents;

/// <summary>Legacy child action <c>NavigationController.ProjectMenu</c> (routes.csv <c>navigation.project-menu</c>).</summary>
public sealed class ProjectMenuViewComponent(IProjectSelection selection) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var menu = await selection.GetMenuAsync(UserClaimsPrincipal.GetUserId(), HttpContext.RequestAborted);
        return menu == null ? Content("") : View(menu);
    }
}
