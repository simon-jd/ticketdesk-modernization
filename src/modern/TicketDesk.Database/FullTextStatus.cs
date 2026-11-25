using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace TicketDesk.Database;

public sealed record FullTextIndexState(string Table, bool CrawlCompleted, int PopulateStatus, long PendingChanges, long ItemCount,
    long FailCount, DateTime? CrawlStart, DateTime? CrawlEnd)
{
    /// <summary>Initial crawl done, no population running, no change waiting: searches see every committed row.</summary>
    public bool Ready => CrawlCompleted && PopulateStatus == 0 && PendingChanges == 0;
}

/// <summary>
/// The full-text indexes of V0011 (population is asynchronous: CREATE FULLTEXT INDEX returns before the crawl, and with
/// CHANGE_TRACKING AUTO every later write is indexed in the background). A deploy waits for <see cref="WaitAsync"/> before
/// the search routes are switched to the new app; tests wait for it after writing rows they search for.
/// </summary>
public static class FullTextStatus
{
    /// <summary>
    /// The state of every full-text index. The read runs at the lowest deadlock priority on connections of its own pool: its
    /// metadata locks (FULLTEXT_INDEX, S) can deadlock with the auto population's own transaction (IFTSAutoNested), and at equal
    /// priority SQL Server chose that one as the victim, which left the index at "crawl not completed" / populate status 6 for a
    /// minute or more although every row was indexed (td-sql system_health, Round 5). Now the read is the victim
    /// (<see cref="SqlException.Number"/> 1205), and <see cref="WaitAsync"/> reads again.
    /// </summary>
    public static async Task<IReadOnlyList<FullTextIndexState>> ReadAsync(string connectionString, CancellationToken ct = default)
    {
        // a pool of its own (ApplicationName), so the session setting below never reaches another caller's connection
        var cs = new SqlConnectionStringBuilder(connectionString) { ApplicationName = "td-db full-text status" }.ConnectionString;
        await using var c = new SqlConnection(cs);
        await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            SET DEADLOCK_PRIORITY -10;
            SELECT OBJECT_SCHEMA_NAME(i.object_id) + N'.' + OBJECT_NAME(i.object_id), i.has_crawl_completed,
                   CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextPopulateStatus') AS int),
                   CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextPendingChanges') AS bigint),
                   CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextItemCount') AS bigint),
                   CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextFailCount') AS bigint),
                   i.crawl_start_date, i.crawl_end_date
            FROM sys.fulltext_indexes i ORDER BY 1
            """, c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<FullTextIndexState>();
        while (await r.ReadAsync(ct))
            list.Add(new FullTextIndexState(r.GetString(0), r.GetBoolean(1), r.IsDBNull(2) ? -1 : r.GetInt32(2), r.IsDBNull(3) ? -1 : r.GetInt64(3),
                r.IsDBNull(4) ? -1 : r.GetInt64(4), r.IsDBNull(5) ? -1 : r.GetInt64(5),
                r.IsDBNull(6) ? null : r.GetDateTime(6), r.IsDBNull(7) ? null : r.GetDateTime(7)));
        return list;
    }

    /// <summary>
    /// Polls until every full-text index is <see cref="FullTextIndexState.Ready"/>; false on timeout (or no index). A read chosen
    /// as deadlock victim (see <see cref="ReadAsync"/>) counts as "not ready yet".
    /// </summary>
    public static async Task<bool> WaitAsync(string connectionString, TimeSpan timeout, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            IReadOnlyList<FullTextIndexState> states;
            try { states = await ReadAsync(connectionString, ct); }
            catch (SqlException e) when (e.Number == 1205) { states = []; }
            if (states.Count > 0 && states.All(s => s.Ready)) return true;
            if (sw.Elapsed > timeout) return false;
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }
    }

    public static string Format(IReadOnlyList<FullTextIndexState> states) => states.Count == 0
        ? "no full-text index (V0011 not applied?)\n"
        : string.Concat(states.Select(s =>
            $"{s.Table,-18} {(s.Ready ? "ready" : "populating"),-10} items {s.ItemCount,9}  pending {s.PendingChanges,7}  failed {s.FailCount}  " +
            $"crawl {(s.CrawlStart is { } a && s.CrawlEnd is { } b ? $"{(b - a).TotalSeconds:F1} s ({a:u} .. {b:u})" : "not finished")}\n"));
}
