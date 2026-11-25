using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;

namespace TicketDesk.Search;

/// <summary>
/// The SQL Server English system stoplist (<c>sys.fulltext_system_stopwords</c>, language 1033): words on it are not in the
/// full-text index, so the parser drops them like the legacy analyzer dropped its stop words. Read once per process.
/// </summary>
public sealed class FullTextStopwords
{
    private volatile IReadOnlySet<string>? _words;

    public async Task<IReadOnlySet<string>> GetAsync(TicketDeskDbContext db, CancellationToken ct)
    {
        if (_words is { } w) return w;
        var list = await db.Database.SqlQueryRaw<string>("SELECT stopword AS [Value] FROM sys.fulltext_system_stopwords WHERE language_id = 1033").ToListAsync(ct);
        return _words = list.Select(s => s.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>
/// Ranked ticket search on the full-text indexes of td-db V0011. Scoring follows the legacy Lucene document's field boosts:
/// title ×2, tags ×2.5, details ×1, event comments ×0.75 (summed over a ticket's events, as Lucene indexed them as one
/// field), and a word that is a ticket id ×3 of the top rank (id was boosted 3 and matched exactly). Each field's rank is
/// SQL Server's CONTAINSTABLE RANK (0–1000) for the OR of the positive terms; +required/AND terms must occur in some field,
/// -excluded/NOT terms in none (the multi-field semantics of Lucene's MultiFieldQueryParser). Top 20 by score, ties by
/// ticket id (the legacy index was built in ticket id order), filtered by the selected project and by the viewer (S12).
/// The tag rank is divided by the square root of the ticket's number of tags (<c>TicketTags</c> rows), Lucene's length norm
/// for that field: CONTAINSTABLE gives a tag word the same rank in "printer" and in "vpn,printer,onboarding-010", so without
/// it most hits of a common word tie. On the golden queries (td-sql copy of baseline-s) it lifted the overlap with the
/// legacy top 20 from 2 to 18 of 20 for "printer" (12 of 20 for "network" either way); at scale L it costs about 50 ms on
/// a common word (docs/evidence/search-worker-20261110/search-perf-L). The tag counts are hash-joined (a per-hit seek read
/// 621k pages for "printer" at L); the local join hint also fixes the join order, which keeps "+a +b" at 0.4 s instead of
/// 2.8 s. No norm on title, details and comments: it needs every hit's text (a clustered-index read per hit) and changed
/// nothing on these queries.
/// CONTAINSTABLE rather than FREETEXTTABLE: FREETEXT matches inflections ("printers" for "printer"), which the legacy
/// StandardAnalyzer never did, so exact words keep the result sets closer to the legacy ones.
/// </summary>
public sealed class FullTextTicketSearch(TicketDeskDbContext db, FullTextStopwords stopwords) : ITicketSearch
{
    public const int DefaultTop = 20;

    internal static readonly (SearchField Field, string Column, double Weight)[] TicketColumns =
    [
        (SearchField.Title, "Title", 2.0),
        (SearchField.Details, "Details", 1.0),
        (SearchField.Tags, "TagList", 2.5),
    ];

    internal const double EventWeight = 0.75;
    internal const double IdScore = 3000.0; // RANK tops out at 1000; id carried boost 3

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var query = SearchQuery.Parse(request.Term, await stopwords.GetAsync(db, ct));
        if (!query.HasPositive) return [];
        var (sql, parameters) = BuildSql(query, request);
        var rows = await db.Database.SqlQueryRaw<SearchRow>(sql, parameters.Cast<object>().ToArray()).ToListAsync(ct);
        return rows.Select(r => new SearchHit(r.TicketId, r.Score)).ToList();
    }

    /// <summary>Row shape of the search statement.</summary>
    internal sealed class SearchRow
    {
        public int TicketId { get; set; }
        public double Score { get; set; }
    }

    /// <summary>The statement and its parameters (only parameters carry user text, always as quoted CONTAINS terms).</summary>
    internal static (string Sql, IReadOnlyList<SqlParameter> Parameters) BuildSql(SearchQuery query, SearchRequest request)
    {
        var p = new List<SqlParameter>();
        string Add(string prefix, object value, SqlDbType type, int size = 0)
        {
            var name = $"@{prefix}{p.Count}";
            p.Add(size > 0 ? new SqlParameter(name, type, size) { Value = value } : new SqlParameter(name, type) { Value = value });
            return name;
        }
        static string Or(IEnumerable<SearchClause> cs) => string.Join(" OR ", cs.Select(c => c.ContainsTerm));

        var positive = query.Clauses.Where(c => c.Occur != SearchOccur.MustNot).ToList();
        var parts = new List<string>();
        foreach (var (field, column, weight) in TicketColumns)
        {
            var terms = positive.Where(c => c.Field != SearchField.Id && c.AppliesTo(field)).ToList();
            if (terms.Count == 0) continue;
            var cond = Add("c", Or(terms), SqlDbType.NVarChar, 4000);
            parts.Add(field == SearchField.Tags
                ? $"SELECT ft.[KEY], ft.[RANK] * {Num(weight)} / SQRT(CASE WHEN n.Tags > 0 THEN n.Tags ELSE 1 END) FROM CONTAINSTABLE(dbo.Tickets, {column}, {cond}) ft " +
                  "LEFT HASH JOIN (SELECT TicketId, COUNT(*) AS Tags FROM dbo.TicketTags GROUP BY TicketId) n ON n.TicketId = ft.[KEY]"
                : $"SELECT ft.[KEY], ft.[RANK] * {Num(weight)} FROM CONTAINSTABLE(dbo.Tickets, {column}, {cond}) ft");
        }
        var eventTerms = positive.Where(c => c.Field != SearchField.Id && c.AppliesTo(SearchField.Events)).ToList();
        if (eventTerms.Count > 0)
        {
            var cond = Add("c", Or(eventTerms), SqlDbType.NVarChar, 4000);
            parts.Add($"SELECT e.TicketId, ft.[RANK] * {Num(EventWeight)} FROM CONTAINSTABLE(dbo.TicketEvents, Comment, {cond}) ft JOIN dbo.TicketEvents e ON e.EventId = ft.[KEY]");
        }
        var ids = positive.Select(c => c.TicketId).OfType<int>().Distinct().ToList();
        if (ids.Count > 0)
            parts.Add($"SELECT t.TicketId, {Num(IdScore)} FROM dbo.Tickets t WHERE t.TicketId IN ({string.Join(", ", ids.Select(id => Add("id", id, SqlDbType.Int)))})");

        var where = new List<string>();
        var joins = "";
        if (request.ProjectId != 0) where.Add($"t.ProjectId = {Add("project", request.ProjectId, SqlDbType.Int)}");
        if (!request.Viewer.SeesAllTickets)
        {
            // the S8 rule (TicketAttachmentsHandler): owner, creator, assignee or subscriber. The user's subscriptions are read
            // once (td-db V0012: IX_TicketSubscribers_SubscriberId) and hash-joined; an EXISTS per hit was a seek per hit
            var me = Add("viewer", request.Viewer.UserId, SqlDbType.NVarChar, 256);
            joins = $"\nLEFT HASH JOIN (SELECT s.TicketId FROM dbo.TicketSubscribers s WHERE s.SubscriberId = {me} GROUP BY s.TicketId) mine ON mine.TicketId = t.TicketId";
            where.Add($"(t.Owner = {me} OR t.CreatedBy = {me} OR t.AssignedTo = {me} OR mine.TicketId IS NOT NULL)");
        }
        foreach (var c in query.Clauses.Where(c => c.Occur != SearchOccur.Should))
        {
            var match = Matches(c, Add);
            where.Add(c.Occur == SearchOccur.Must ? match : $"NOT {match}");
        }
        if (parts.Count == 0) parts.Add("SELECT CAST(NULL AS int), CAST(NULL AS float) WHERE 1 = 0");
        var top = Add("top", Math.Clamp(request.Top, 1, 1000), SqlDbType.Int);
        var sql = new StringBuilder()
            .Append("WITH hits (TicketId, Score) AS (\n    ").Append(string.Join("\n    UNION ALL ", parts)).Append("\n)\n")
            .Append($"SELECT TOP ({top}) h.TicketId, CAST(SUM(h.Score) AS float) AS Score\n")
            .Append("FROM hits h JOIN dbo.Tickets t ON t.TicketId = h.TicketId").Append(joins).Append('\n');
        if (where.Count > 0) sql.Append("WHERE ").Append(string.Join("\n  AND ", where)).Append('\n');
        sql.Append("GROUP BY h.TicketId\nORDER BY Score DESC, h.TicketId;");
        return (sql.ToString(), p);
    }

    /// <summary>The clause occurs in one of its fields of ticket t.</summary>
    private static string Matches(SearchClause c, Func<string, object, SqlDbType, int, string> add)
    {
        var any = new List<string>();
        if (c.Field != SearchField.Id)
        {
            var term = add("m", c.ContainsTerm, SqlDbType.NVarChar, 4000);
            var columns = TicketColumns.Where(x => c.AppliesTo(x.Field)).Select(x => "t." + x.Column).ToList();
            if (columns.Count == 1) any.Add($"CONTAINS({columns[0]}, {term})");
            else if (columns.Count > 1) any.Add($"CONTAINS(({string.Join(", ", columns)}), {term})");
            if (c.AppliesTo(SearchField.Events))
                any.Add($"EXISTS (SELECT 1 FROM dbo.TicketEvents e WHERE e.TicketId = t.TicketId AND CONTAINS(e.Comment, {term}))");
        }
        if (c.TicketId is { } id) any.Add($"t.TicketId = {add("id", id, SqlDbType.Int, 0)}");
        return any.Count == 0 ? "(1 = 0)" : "(" + string.Join(" OR ", any) + ")";
    }

    private static string Num(double d) => d.ToString("0.0###", CultureInfo.InvariantCulture);
}
