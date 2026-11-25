using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;

namespace TicketDesk.Search;

/// <summary>State of one full-text index (td-db V0011) as the search-management page shows it.</summary>
public sealed class FullTextIndexInfo
{
    public string TableName { get; set; } = "";
    public bool CrawlCompleted { get; set; }
    public int PopulateStatus { get; set; }
    public long ItemCount { get; set; }
    public long PendingChanges { get; set; }
    public DateTime? CrawlEnd { get; set; }

    public bool Ready => CrawlCompleted && PopulateStatus == 0 && PendingChanges == 0;
}

/// <summary>
/// Replaces the legacy search-manager actions (remove / populate the Lucene index, L4): SQL Server keeps the full-text
/// indexes current by itself (change tracking AUTO), so there is nothing to rebuild; the page only reports their state.
/// </summary>
public sealed class SearchIndexStatus(TicketDeskDbContext db)
{
    public async Task<IReadOnlyList<FullTextIndexInfo>> ReadAsync(CancellationToken ct) =>
        await db.Database.SqlQueryRaw<FullTextIndexInfo>("""
            SELECT OBJECT_SCHEMA_NAME(i.object_id) + N'.' + OBJECT_NAME(i.object_id) AS TableName, i.has_crawl_completed AS CrawlCompleted,
                   ISNULL(CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextPopulateStatus') AS int), -1) AS PopulateStatus,
                   ISNULL(CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextItemCount') AS bigint), -1) AS ItemCount,
                   ISNULL(CAST(OBJECTPROPERTYEX(i.object_id, 'TableFullTextPendingChanges') AS bigint), -1) AS PendingChanges,
                   i.crawl_end_date AS CrawlEnd
            FROM sys.fulltext_indexes i
            """).OrderBy(x => x.TableName).ToListAsync(ct);
}
