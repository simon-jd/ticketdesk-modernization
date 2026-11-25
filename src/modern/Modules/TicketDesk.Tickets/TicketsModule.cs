using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketDesk.Identity;

namespace TicketDesk.Tickets;

public static class TicketsModule
{
    public static IServiceCollection AddTicketsModule(this IServiceCollection services)
    {
        services.AddScoped<TicketQueries>();
        services.AddScoped<TicketCenter.TicketCenterService>();
        services.AddSingleton<Rendering.LegacyTextRenderer>();
        services.AddScoped<Pages.TicketUsers>();
        services.AddScoped<Pages.TicketPageService>();
        services.AddScoped<Pages.TicketSearchResults>();
        services.AddScoped<Writes.TicketWriter>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthorizationHandler, TicketAttachmentsHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTicketsEndpoints(this IEndpointRouteBuilder api)
    {
        var tickets = api.MapGroup("/tickets")
            .WithTags("Tickets")
            .RequireAuthorization(TicketDeskPolicies.InternalUser);

        tickets.MapGet("", ListTickets)
            .WithName("ListTickets")
            .WithSummary("Pages, filters and sorts tickets.")
            .WithDescription("Sort fields are whitelisted (S5); unknown fields return 400. Every sort ends with ticketId.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        tickets.MapGet("/{id:int}", GetTicket)
            .WithName("GetTicket")
            .WithSummary("One ticket with its events, tags and attachment metadata.")
            .WithDescription("Attachment metadata is returned only to help desk, admins and the ticket's owner, creator, assignee or subscribers (S8).")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        tickets.MapGet("/options", GetTicketOptions)
            .WithName("GetTicketOptions")
            .WithSummary("Choices of the ticket fields for the caller: projects, types, categories, priorities, users, field permissions.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        tickets.MapPost("", CreateTicket)
            .WithName("CreateTicket")
            .WithSummary("Creates a ticket (the legacy new-ticket form's rules, plus value lists and field permissions).")
            .WithDescription("Needs the antiforgery request token in the RequestVerificationToken header. The ticket gets the Create " +
                             "(or CreateOnBehalfOf) event, subscribers and notifications like a ticket created on the legacy form.")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        tickets.MapPost("/{id:int}/activities", PerformActivity)
            .WithName("PerformTicketActivity")
            .WithSummary("Performs a workflow activity on a ticket (comment, assign, resolve, close, reopen, ...).")
            .WithDescription("Needs the antiforgery request token in the RequestVerificationToken header. Only the activities the " +
                             "ticket's `activities` list offers the caller succeed; anything else is a 409 (the ticket changed, or the role " +
                             "may not). EditTicketInfo and ModifyAttachments stay on the server page.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return api;
    }

    internal static async Task<IResult> PerformActivity(int id, TicketActivityRequest request, ClaimsPrincipal user, Pages.TicketPageService pages,
        Pages.TicketUsers users, Writes.TicketWriter writer, CancellationToken ct)
    {
        var input = TicketActivitiesApi.TryCreate(request, await pages.SettingsAsync(ct), await users.HelpDeskUsersAsync(ct), out var errors);
        if (input == null) return TypedResults.ValidationProblem(errors);
        var ticket = await writer.FindAsync(id, ct);
        if (ticket == null) return TypedResults.NotFound();
        return await writer.PerformAsync(ticket, Activities.TicketActor.From(user), input, ct) switch
        {
            Writes.TicketWriteResult.Saved => TypedResults.NoContent(),
            Writes.TicketWriteResult.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This activity is not available for this ticket now.",
                detail: "Reload the ticket: its state changed, or your role may not do this."),
        };
    }

    internal static async Task<Ok<TicketOptions>> GetTicketOptions(ClaimsPrincipal user, Pages.TicketPageService pages, Pages.TicketUsers users, CancellationToken ct)
    {
        var settings = await pages.SettingsAsync(ct);
        var projects = (await pages.ProjectsAsync(ct)).Select(p => new ProjectOption(p.ProjectId, p.ProjectName)).ToList();
        return TypedResults.Ok(new TicketOptions(projects, settings.TicketTypesList, settings.CategoryList, settings.PriorityList,
            await users.InternalUsersAsync(ct), await users.HelpDeskUsersAsync(ct), TicketFieldPermissions.For(Activities.TicketActor.From(user), settings)));
    }

    internal static async Task<Results<Created<TicketCreated>, ValidationProblem, ForbidHttpResult>> CreateTicket(NewTicketRequest request,
        ClaimsPrincipal user, Pages.TicketPageService pages, Pages.TicketUsers users, Writes.TicketWriter writer, Attachments.IAttachmentStore store,
        CancellationToken ct)
    {
        var actor = Activities.TicketActor.From(user);
        var settings = await pages.SettingsAsync(ct);
        var projects = (await pages.ProjectsAsync(ct)).Select(p => new ProjectOption(p.ProjectId, p.ProjectName)).ToList();
        var ticket = NewTicketValidation.TryCreate(request, actor, settings, projects, await users.InternalUsersAsync(ct), await users.HelpDeskUsersAsync(ct), out var errors);
        if (ticket == null) return TypedResults.ValidationProblem(errors);
        if (!await writer.CreateAsync(ticket, actor, ct)) return TypedResults.Forbid();
        if (request.TempId is { } tempId) await store.CommitPendingAsync(tempId, ticket.TicketId, ct);
        return TypedResults.Created($"/api/tickets/{ticket.TicketId}", new TicketCreated(ticket.TicketId));
    }

    internal static async Task<Results<Ok<TicketPage>, ValidationProblem>> ListTickets(
        [AsParameters] TicketListQuery query, TicketQueries queries, ClaimsPrincipal user, CancellationToken ct)
    {
        var request = TicketListRequest.TryCreate(query, out var errors);
        if (request == null) return TypedResults.ValidationProblem(errors);
        return TypedResults.Ok(await queries.ListAsync(request, user.GetUserId() ?? "", ct));
    }

    internal static async Task<Results<Ok<TicketDetail>, NotFound>> GetTicket(int id, TicketQueries queries, ClaimsPrincipal user, CancellationToken ct)
    {
        var ticket = await queries.GetAsync(id, user, ct);
        return ticket == null ? TypedResults.NotFound() : TypedResults.Ok(ticket);
    }
}
