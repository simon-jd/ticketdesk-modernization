using System.ComponentModel;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TicketDesk.Identity;
using TicketDesk.Projects;
using TicketDesk.Tickets;
using TicketDesk.Web.Controllers;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Spa;

public sealed record TicketListInfo(string Name, string DisplayName);

/// <summary>What the Angular front end needs to know about the signed-in user and the migration state at start-up.</summary>
public sealed record SessionInfo(
    string UserId,
    string DisplayName,
    bool IsHelpDesk,
    bool IsAdministrator,
    [property: Description("The ticket-center lists of this user, in menu order (help desk: all five).")]
    IReadOnlyList<TicketListInfo> Lists,
    string DefaultList,
    [property: Description("The project chosen in the legacy project menu, or null for all projects (also when there is only one).")]
    int? SelectedProjectId,
    bool MultiProject,
    [property: Description("The server's date for due/overdue markers (TicketDesk:TimeZone), yyyy-MM-dd.")]
    string Today,
    [property: Description("Migration-registry ids of the pages the Angular front end answers now; links to other pages load from the server.")]
    IReadOnlyList<string> AngularPages);

public static class SessionEndpoints
{
    /// <summary>The ids of the pages that have an Angular version (src/web/src/app/app.routes.ts uses the same ids).</summary>
    public static readonly IReadOnlyList<string> AngularPageIds = ["tickets.index", "ticket.view", "ticket.new.get"];

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/session", GetSession)
            .WithName("GetSession")
            .WithTags("Session")
            .WithSummary("The signed-in user, their ticket lists and which pages the Angular front end answers.")
            .RequireAuthorization(TicketDeskPolicies.InternalUser)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        return api;
    }

    internal static async Task<SessionInfo> GetSession(HttpContext context, ClaimsPrincipal user, IProjectSelection projects,
        MigrationRegistry registry, IOptionsSnapshot<SiteOptions> site, CancellationToken ct)
    {
        var helpDesk = user.IsHelpDeskOrAdmin();
        var userId = user.GetUserId() ?? "";
        var several = await projects.HasSeveralProjectsAsync(ct);
        var selected = several ? await projects.GetSelectedProjectIdAsync(userId, ct) : 0;
        return new SessionInfo(
            userId,
            user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "",
            helpDesk,
            user.IsInRole(TicketDeskRoles.Administrators),
            TicketLists.ForUser(helpDesk).Select(l => new TicketListInfo(l.Name, TicketCenterViewModel.DefaultName(l.Name) ?? l.Name)).ToList(),
            TicketLists.DefaultFor(helpDesk),
            selected == 0 ? null : selected,
            several,
            site.Value.Today().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            AngularPageIds.Where(id => registry.ServesAngular(id, context)).ToList());
    }
}
