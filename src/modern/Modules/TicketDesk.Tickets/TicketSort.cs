using System.Linq.Expressions;
using TicketDesk.Data.Entities;

namespace TicketDesk.Tickets;

public sealed record SortKey(string Field, bool Descending)
{
    public override string ToString() => (Descending ? "-" : "") + Field;
}

/// <summary>
/// Sort-column whitelist (fixes S5). The legacy ticket center took <c>columnName</c> from the query string, persisted it in
/// <c>UserSettings.ListSettingsJson</c> and pasted it into an Entity SQL <c>ORDER BY it.{columnName}</c>. Here only the
/// fields below are accepted; anything else is a validation error, and every sort ends with <c>ticketId</c> so paging
/// is deterministic.
/// </summary>
public static class TicketSort
{
    public const int MaxKeys = 5;

    private abstract class Field(string name)
    {
        public string Name { get; } = name;
        public abstract IOrderedQueryable<Ticket> OrderBy(IQueryable<Ticket> q, bool desc);
        public abstract IOrderedQueryable<Ticket> ThenBy(IOrderedQueryable<Ticket> q, bool desc);
    }

    private sealed class Field<TKey>(string name, Expression<Func<Ticket, TKey>> key) : Field(name)
    {
        public override IOrderedQueryable<Ticket> OrderBy(IQueryable<Ticket> q, bool desc) => desc ? q.OrderByDescending(key) : q.OrderBy(key);
        public override IOrderedQueryable<Ticket> ThenBy(IOrderedQueryable<Ticket> q, bool desc) => desc ? q.ThenByDescending(key) : q.ThenBy(key);
    }

    private static readonly Field[] Fields =
    [
        new Field<int>("ticketId", t => t.TicketId),
        new Field<int>("projectId", t => t.ProjectId),
        new Field<string>("title", t => t.Title),
        new Field<TicketStatus>("status", t => t.TicketStatus),
        new Field<string?>("priority", t => t.Priority),
        new Field<string>("ticketType", t => t.TicketType),
        new Field<string>("category", t => t.Category),
        new Field<string>("owner", t => t.Owner),
        new Field<string?>("assignedTo", t => t.AssignedTo),
        new Field<string>("createdBy", t => t.CreatedBy),
        new Field<DateTimeOffset>("createdDate", t => t.CreatedDate),
        new Field<DateTimeOffset>("lastUpdateDate", t => t.LastUpdateDate),
        new Field<DateTimeOffset>("currentStatusDate", t => t.CurrentStatusDate),
        new Field<DateTimeOffset?>("dueDate", t => t.DueDate),
        new Field<DateTimeOffset?>("targetDate", t => t.TargetDate),
        new Field<DateTimeOffset?>("resolutionDate", t => t.ResolutionDate),
    ];

    private static readonly Dictionary<string, Field> ByName = BuildIndex();

    private static Dictionary<string, Field> BuildIndex()
    {
        var index = Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        index["ticketStatus"] = index["status"]; // legacy column name
        return index;
    }

    public static IReadOnlyList<string> AllowedFields { get; } = Fields.Select(f => f.Name).ToArray();

    /// <summary>Parses <c>field,-field,...</c> (a leading <c>-</c> sorts descending). Field names are case-insensitive.</summary>
    public static bool TryParse(string? sort, out IReadOnlyList<SortKey> keys, out string? error)
    {
        keys = [];
        error = null;
        if (string.IsNullOrWhiteSpace(sort)) return true;
        var parsed = new List<SortKey>();
        foreach (var raw in sort.Split(','))
        {
            var token = raw.Trim();
            var desc = token.StartsWith('-');
            var name = token.TrimStart('-', '+');
            if (name.Length == 0)
            {
                error = "Empty sort field.";
                return false;
            }
            if (!ByName.TryGetValue(name, out var field))
            {
                error = $"Unknown sort field '{Truncate(name)}'. Allowed: {string.Join(", ", AllowedFields)}.";
                return false;
            }
            if (parsed.Any(k => k.Field == field.Name))
            {
                error = $"Sort field '{field.Name}' is given more than once.";
                return false;
            }
            parsed.Add(new SortKey(field.Name, desc));
        }
        if (parsed.Count > MaxKeys)
        {
            error = $"At most {MaxKeys} sort fields.";
            return false;
        }
        keys = parsed;
        return true;
    }

    /// <summary>The keys with <c>ticketId</c> appended as the final tie-breaker unless already present (usp_TicketList does the same).</summary>
    public static IReadOnlyList<SortKey> WithTieBreaker(IReadOnlyList<SortKey> keys)
    {
        var all = keys.ToList();
        if (!all.Any(k => k.Field == "ticketId")) all.Add(new SortKey("ticketId", false));
        return all;
    }

    /// <summary>Applies the keys and appends <c>ticketId</c> as the final tie-breaker unless it is already present.</summary>
    public static IOrderedQueryable<Ticket> Apply(IQueryable<Ticket> query, IReadOnlyList<SortKey> keys, out IReadOnlyList<SortKey> applied)
    {
        var all = WithTieBreaker(keys);
        IOrderedQueryable<Ticket>? ordered = null;
        foreach (var key in all)
        {
            var field = ByName[key.Field];
            ordered = ordered == null ? field.OrderBy(query, key.Descending) : field.ThenBy(ordered, key.Descending);
        }
        applied = all;
        return ordered!;
    }

    private static string Truncate(string s) => s.Length <= 40 ? s : s[..40] + "...";
}
