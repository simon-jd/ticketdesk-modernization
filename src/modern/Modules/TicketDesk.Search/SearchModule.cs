using Microsoft.Extensions.DependencyInjection;

namespace TicketDesk.Search;

/// <summary>Who searches; <paramref name="SeesAllTickets"/> false limits the results to the user's own tickets (S12).</summary>
public sealed record SearchViewer(string UserId, bool SeesAllTickets);

/// <summary>
/// Section <c>Search</c>. <see cref="RestrictToOwnTickets"/> (default false = legacy behaviour: the legacy search, like the
/// ticket page, lists any ticket to any internal user) limits other internal users' results to tickets they own, created,
/// are assigned to or subscribe to, the S8 rule; help desk and administrators always see all. A follow-up decision (S13).
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";
    public bool RestrictToOwnTickets { get; set; }
}

/// <summary>One search: the box's text, the user's selected project (0 = all projects) and the viewer.</summary>
public sealed record SearchRequest(string? Term, int ProjectId, SearchViewer Viewer, int Top = FullTextTicketSearch.DefaultTop);

/// <summary>A ticket that matched, with its combined full-text rank (higher first).</summary>
public sealed record SearchHit(int TicketId, double Score);

/// <summary>
/// Ticket search (legacy <c>TdSearchContext.Current.IndexSearcher.SearchAsync</c> over a Lucene.Net 3.0.3 index on the VM
/// disk). Now SQL Server full-text over the shared tables (td-db V0011): no index of its own to write, rebuild or lock.
/// </summary>
public interface ITicketSearch
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken ct);
}

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        services.AddOptions<SearchOptions>().BindConfiguration(SearchOptions.SectionName);
        services.AddSingleton<FullTextStopwords>();
        services.AddScoped<ITicketSearch, FullTextTicketSearch>();
        services.AddScoped<SearchIndexStatus>();
        return services;
    }
}
