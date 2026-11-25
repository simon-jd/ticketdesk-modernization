using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>td-db V0011: the full-text catalog and indexes for search, expand-only, rerunnable.</summary>
public sealed class FullTextMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task V0011_indexes_title_details_tags_and_comments_and_reruns_as_a_no_op()
    {
        await using var db = await TempDatabase.CreateAsync<ScratchDatabase>();
        var columns = await db.RowsAsync("""
            SELECT OBJECT_NAME(c.object_id), COL_NAME(c.object_id, c.column_id), c.language_id, i.change_tracking_state_desc, cat.name, ki.name
            FROM sys.fulltext_index_columns c JOIN sys.fulltext_indexes i ON i.object_id = c.object_id
            JOIN sys.fulltext_catalogs cat ON cat.fulltext_catalog_id = i.fulltext_catalog_id
            JOIN sys.indexes ki ON ki.object_id = i.object_id AND ki.index_id = i.unique_index_id
            ORDER BY 1, 2
            """);
        Assert.Equal(
            ["TicketEvents.Comment 1033 AUTO ftc_TicketDesk UX_TicketEvents_EventId", "Tickets.Details 1033 AUTO ftc_TicketDesk PK_dbo.Tickets",
             "Tickets.TagList 1033 AUTO ftc_TicketDesk PK_dbo.Tickets", "Tickets.Title 1033 AUTO ftc_TicketDesk PK_dbo.Tickets"],
            columns.Select(r => $"{r[0]}.{r[1]} {r[2]} {r[3]} {r[4]} {r[5]}"));
        Assert.True(await db.ScalarAsync<bool>("SELECT is_unique FROM sys.indexes WHERE name = 'UX_TicketEvents_EventId'"));

        // rerun: every step checks first (a deploy that failed halfway continues)
        var script = ScriptCatalog.Load().Single(s => s.Name == "expand/V0011__ticket_full_text_search.sql");
        Assert.False(script.UseTransaction);
        foreach (var batch in script.Batches) await db.ScalarAsync<int>(batch + "\nSELECT 0;");
        Assert.Equal(2, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.fulltext_indexes"));
    }

    /// <summary>
    /// The rows the legacy app's statements write after the migration are found by CONTAINSTABLE: the test waits for exactly
    /// that (bounded, 3 min), not for the indexes' population state. That state is no signal here (Round 5b): a fresh scratch
    /// database's population was not reported complete within 2 min in 3 of 60 runs while nothing else was wrong, and a
    /// status read can deadlock with the population. The state is read only to explain a failure.
    /// </summary>
    [Fact]
    public async Task Rows_written_after_the_migration_become_searchable_without_any_hook()
    {
        await using var db = await TempDatabase.CreateAsync<ScratchDatabase>();
        var ticket = await db.ScalarAsync<int>("SELECT MIN(TicketId) FROM dbo.Tickets");
        // the legacy app's statements: EF6 updates the listed columns and inserts events with an identity EventId
        await db.ScalarAsync<int>("UPDATE dbo.Tickets SET Title = N'Quixotrix projector' WHERE TicketId = @t; SELECT @@ROWCOUNT;", ("@t", ticket));
        await db.ScalarAsync<int>("""
            INSERT [dbo].[TicketEvents]([TicketId], [EventDescription], [Comment], [IsHtml], [EventBy], [EventDate], [ForActivity])
            VALUES (@t, N'commented', N'the vexillograph is back', 0, N'x', SYSDATETIMEOFFSET(), 0);
            SELECT [EventId] FROM [dbo].[TicketEvents] WHERE @@ROWCOUNT > 0 AND [EventId] = scope_identity() AND [TicketId] = @t;
            """, ("@t", ticket));
        const string title = "SELECT [KEY] FROM CONTAINSTABLE(dbo.Tickets, Title, N'\"quixotrix\"')";
        const string comment = "SELECT e.TicketId FROM CONTAINSTABLE(dbo.TicketEvents, Comment, N'\"vexillograph\"') ft JOIN dbo.TicketEvents e ON e.EventId = ft.[KEY]";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int byTitle, byComment;
        while (((byTitle = await db.ScalarAsync<int>(title)) != ticket | (byComment = await db.ScalarAsync<int>(comment)) != ticket) && sw.Elapsed < TimeSpan.FromMinutes(3))
            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        var why = byTitle == ticket && byComment == ticket ? "" : $"after {sw.Elapsed.TotalSeconds:F0} s: by title {byTitle}, by comment {byComment}, ticket {ticket}; full-text state:\n" +
            FullTextStatus.Format(await FullTextStatus.ReadAsync(db.ConnectionString!, Ct));
        Assert.True(byTitle == ticket, why);
        Assert.True(byComment == ticket, why);
    }
}
