using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Identity;

namespace TicketDesk.Projects;

public sealed record ProjectDto(int ProjectId, string ProjectName, string? ProjectDescription);

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services) =>
        services.AddScoped<IProjectSelection, ProjectSelection>().AddScoped<IProjectAdmin, ProjectAdmin>();

    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/projects", ListProjects)
            .WithName("ListProjects")
            .WithTags("Projects")
            .WithSummary("Lists all projects.")
            .WithDescription("Ordered by name, like the legacy project menu (NavigationController.ProjectMenu).")
            .RequireAuthorization(TicketDeskPolicies.InternalUser)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        return api;
    }

    internal static async Task<Ok<List<ProjectDto>>> ListProjects(TicketDeskDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Projects.AsNoTracking()
            .OrderBy(p => p.ProjectName).ThenBy(p => p.ProjectId)
            .Select(p => new ProjectDto(p.ProjectId, p.ProjectName, p.ProjectDescription))
            .ToListAsync(ct));
}
