using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketDesk.Identity;

namespace TicketDesk.Reports;

/// <summary>
/// Help-desk reports (new in the modern app; the legacy app had none): open-ticket aging, assignee workload, time to
/// close. Help desk and administrators only, like the legacy help-desk lists.
/// </summary>
public static class ReportsModule
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ReportQueries>();
        return services;
    }

    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder api)
    {
        var reports = api.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization(TicketDeskPolicies.HelpDesk);

        reports.MapGet("/open-tickets", OpenTickets)
            .WithName("OpenTicketsReport")
            .WithSummary("Open tickets by project, status and age bucket.")
            .WithDescription("Age = whole hours from CreatedDate to asOf; buckets <1d, 1-7d, 7-30d, 30-90d, >90d.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        reports.MapGet("/assignee-workload", AssigneeWorkload)
            .WithName("AssigneeWorkloadReport")
            .WithSummary("Open tickets per assignee, time since assignment, and tickets closed in the last 30 days.")
            .WithDescription("Rows are the top N assignees by open tickets; hours since assignment come from Tickets.AssignedDate.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        reports.MapGet("/time-to-close", TimeToClose)
            .WithName("TimeToCloseReport")
            .WithSummary("Hours from creation to closing for tickets closed in a window: mean, p50, p90, max.")
            .WithDescription("Per project and overall; a ticket counts in the window of its current closing date (CurrentStatusDate of a closed ticket).")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return api;
    }

    internal static async Task<Ok<OpenTicketsReport>> OpenTickets([AsParameters] OpenTicketsQuery query, ReportQueries reports, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await reports.OpenTicketsAsync(ReportRequests.Create(query, clock.GetUtcNow()), ct));

    internal static async Task<Results<Ok<AssigneeWorkloadReport>, ValidationProblem>> AssigneeWorkload(
        [AsParameters] AssigneeWorkloadQuery query, ReportQueries reports, TimeProvider clock, CancellationToken ct)
    {
        var request = ReportRequests.TryCreate(query, clock.GetUtcNow(), out var errors);
        if (request == null) return TypedResults.ValidationProblem(errors);
        return TypedResults.Ok(await reports.AssigneeWorkloadAsync(request, ct));
    }

    internal static async Task<Results<Ok<TimeToCloseReport>, ValidationProblem>> TimeToClose(
        [AsParameters] TimeToCloseQuery query, ReportQueries reports, TimeProvider clock, CancellationToken ct)
    {
        var request = ReportRequests.TryCreate(query, clock.GetUtcNow(), out var errors);
        if (request == null) return TypedResults.ValidationProblem(errors);
        return TypedResults.Ok(await reports.TimeToCloseAsync(request, ct));
    }
}
