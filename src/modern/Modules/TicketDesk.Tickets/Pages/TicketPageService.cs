using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Rendering;

namespace TicketDesk.Tickets.Pages;

/// <summary>A ticket event as the events panel shows it.</summary>
public sealed record TicketEventView(TicketEvent Event, string? EventByName, string CommentHtml);

/// <summary>The details panel: the ticket (with project and subscribers), user names and the rendered details.</summary>
public sealed record TicketDetailsView(Ticket Ticket, IReadOnlyDictionary<string, string> Names, bool DisplayProjects, string DetailsHtml)
{
    public string Name(string? userId) => userId != null && Names.TryGetValue(userId, out var n) ? n : "";
}

/// <summary>Read side of the ticket page (legacy <c>TicketController.Index</c>, <c>TicketDetails</c>, <c>TicketEvents</c>).</summary>
public sealed class TicketPageService(TicketDeskDbContext db, TicketUsers users, LegacyTextRenderer renderer)
{
    /// <summary>The ticket with project and subscribers (no tracking), or null.</summary>
    public Task<Ticket?> FindAsync(int ticketId, CancellationToken ct) =>
        db.Tickets.AsNoTracking().Include(t => t.Project).Include(t => t.TicketSubscribers).AsSplitQuery().FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);

    public async Task<TicketDetailsView?> DetailsAsync(int ticketId, CancellationToken ct)
    {
        var t = await FindAsync(ticketId, ct);
        return t == null ? null : await DetailsAsync(t, ct);
    }

    public async Task<TicketDetailsView> DetailsAsync(Ticket t, CancellationToken ct)
    {
        var names = await users.NamesAsync(
            new[] { t.Owner, t.AssignedTo, t.LastUpdateBy, t.CurrentStatusSetBy, t.CreatedBy }.Concat(t.TicketSubscribers.Select(s => s.SubscriberId)), ct);
        // legacy: ViewBag.DisplayProjects = Context.Projects.Any()
        var displayProjects = await db.Projects.AnyAsync(ct);
        return new TicketDetailsView(t, names, displayProjects, renderer.Render(t.Details, t.IsHtml));
    }

    /// <summary>The events, newest first (legacy view: <c>OrderByDescending(c =&gt; c.EventDate)</c>), or null without the ticket.</summary>
    public async Task<IReadOnlyList<TicketEventView>?> EventsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Tickets.AnyAsync(t => t.TicketId == ticketId, ct)) return null;
        var events = await db.TicketEvents.AsNoTracking().Where(e => e.TicketId == ticketId).OrderBy(e => e.EventId).ToListAsync(ct);
        var names = await users.NamesAsync(events.Select(e => e.EventBy), ct);
        return events.OrderByDescending(e => e.EventDate)
            .Select(e => new TicketEventView(e, names.GetValueOrDefault(e.EventBy), renderer.Render(e.Comment, e.IsHtml)))
            .ToList();
    }

    public Task<int> ProjectCountAsync(CancellationToken ct) => db.Projects.CountAsync(ct);

    /// <summary>Projects by name (legacy <c>GetProjectList</c>) with ids.</summary>
    public async Task<IReadOnlyList<Project>> ProjectsAsync(CancellationToken ct) =>
        await db.Projects.AsNoTracking().OrderBy(p => p.ProjectName).ToListAsync(ct);

    public Task<string?> ProjectNameAsync(int projectId, CancellationToken ct) =>
        db.Projects.Where(p => p.ProjectId == projectId).Select(p => p.ProjectName).FirstOrDefaultAsync(ct);

    public Task<Settings.TicketDeskSettings> SettingsAsync(CancellationToken ct) => Settings.TicketDeskSettings.LoadAsync(db, ct);
}
