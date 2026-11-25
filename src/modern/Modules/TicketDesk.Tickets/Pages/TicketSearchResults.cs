using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Rendering;

namespace TicketDesk.Tickets.Pages;

/// <summary>A row of the search page: the ticket (with project), the three names it shows and the details excerpt.</summary>
public sealed record TicketSearchResult(Ticket Ticket, string? AssignedToName, string OwnerName, string LastUpdateByName, string DetailsExcerpt);

/// <summary>The tickets the search page lists, in the order the search ranked them (legacy <c>Views/Search/Index.cshtml</c>).</summary>
public sealed partial class TicketSearchResults(TicketDeskDbContext db, TicketUsers users, LegacyTextRenderer renderer)
{
    public async Task<IReadOnlyList<TicketSearchResult>> LoadAsync(IReadOnlyList<int> ticketIds, CancellationToken ct)
    {
        if (ticketIds.Count == 0) return [];
        var ids = ticketIds.ToArray();
        var tickets = await db.Tickets.AsNoTracking().Include(t => t.Project).Where(t => ids.Contains(t.TicketId)).ToDictionaryAsync(t => t.TicketId, ct);
        var names = await users.NamesAsync(tickets.Values.SelectMany(t => new[] { t.Owner, t.AssignedTo, t.LastUpdateBy }), ct);
        string Name(string? id) => id != null && names.TryGetValue(id, out var n) ? n : "";
        return ticketIds.Where(tickets.ContainsKey).Select(id => tickets[id])
            .Select(t => new TicketSearchResult(t, t.AssignedTo == null ? null : Name(t.AssignedTo), Name(t.Owner), Name(t.LastUpdateBy), Excerpt(renderer.Render(t.Details, t.IsHtml))))
            .ToList();
    }

    /// <summary>Legacy: the rendered details without tags (<c>&lt;(.|\n)*?&gt;</c>), cut at 100 characters + "\n\n...".</summary>
    public static string Excerpt(string detailsHtml)
    {
        var text = Tags().Replace(detailsHtml, "");
        return text.Length > 100 ? text[..100] + "\n\n..." : text;
    }

    [GeneratedRegex(@"<(.|\n)*?>")]
    private static partial Regex Tags();
}
