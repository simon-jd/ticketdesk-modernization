using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>td-db's journal: what runs, in which order, once, and what happens when an applied script is edited.</summary>
public sealed class MigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Scripts_are_ordered_expand_then_programmable_then_contract()
    {
        var scripts = ScriptCatalog.Load();
        Assert.Equal(
            ["expand/V0001__tickets_assigned_date.sql", "expand/V0002__tickets_assigned_date_backfill.sql", "expand/V0003__ticket_list_and_report_indexes.sql",
             "expand/V0004__tickets_rebuild_after_backfill.sql", "expand/V0005__assigned_date_trigger_null_changes.sql",
             "expand/V0006__open_unassigned_include_assignee.sql", "expand/V0010__data_protection_keys.sql", "expand/V0011__ticket_full_text_search.sql",
             "expand/V0012__ticket_subscribers_by_subscriber.sql", "expand/V0013__snapshot_isolation_for_reads.sql", "programmable/R__usp_report_assignee_workload.sql",
             "programmable/R__usp_report_open_tickets_aging.sql", "programmable/R__usp_report_time_to_close.sql", "programmable/R__usp_ticket_list.sql",
             "contract/C0001__assigned_date_contract.sql"],
            scripts.Select(s => s.Name));
        // long-running data and index steps commit in batches instead of one transaction
        Assert.Equal(["expand/V0002__tickets_assigned_date_backfill.sql", "expand/V0003__ticket_list_and_report_indexes.sql", "expand/V0004__tickets_rebuild_after_backfill.sql",
                      "expand/V0006__open_unassigned_include_assignee.sql",
                      "expand/V0011__ticket_full_text_search.sql", // full-text DDL cannot run inside a user transaction
                      "expand/V0012__ticket_subscribers_by_subscriber.sql",
                      "expand/V0013__snapshot_isolation_for_reads.sql"], // ALTER DATABASE cannot run inside a user transaction
            scripts.Where(s => !s.UseTransaction).Select(s => s.Name));
        Assert.All(scripts, s => Assert.Matches("^[0-9a-f]{64}$", s.Checksum));
    }

    [Fact]
    public async Task Migrate_applies_expand_and_programmable_once_and_leaves_contract_pending()
    {
        await using var db = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        var migrator = new SchemaMigrator(db.ConnectionString!, TextWriter.Null);

        var first = await migrator.MigrateAsync(ct: Ct);
        Assert.Equal(ScriptCatalog.Load().Where(s => s.Kind != ScriptKind.Contract).Select(s => s.Name), first);
        Assert.Empty(await migrator.MigrateAsync(ct: Ct));

        var plan = await migrator.PlanAsync(Ct);
        Assert.Empty(plan.Pending);
        Assert.Empty(plan.Drift);
        Assert.Equal(["contract/C0001__assigned_date_contract.sql"], plan.ContractPending.Select(s => s.Name));
        Assert.Equal(first.Count, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.__ModernSchemaHistory"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name = 'TR_Tickets_AssignedDate'"));
        Assert.Equal(4, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'usp[_]%'"));
    }

    [Fact]
    public async Task An_edited_applied_script_is_drift_and_blocks_migrate()
    {
        await using var db = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).MigrateAsync(ct: Ct);

        var edited = ScriptCatalog.Load().Select(s => s.Name.StartsWith("expand/V0001", StringComparison.Ordinal)
            ? s with { Sql = s.Sql + "\n-- edited", Checksum = new string('0', 64) } : s).ToList();
        var migrator = new SchemaMigrator(db.ConnectionString!, TextWriter.Null) { Scripts = edited };
        var plan = await migrator.PlanAsync(Ct);
        Assert.Contains(plan.Drift, d => d.Contains("V0001", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(ct: Ct));
    }

    [Fact]
    public async Task A_changed_repeatable_script_is_applied_again()
    {
        await using var db = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).MigrateAsync(ct: Ct);
        var changed = ScriptCatalog.Load().Select(s => s.Name.EndsWith("R__usp_ticket_list.sql", StringComparison.Ordinal)
            ? s with { Sql = s.Sql + "\n-- v2", Checksum = new string('1', 64) } : s).ToList();
        var applied = await new SchemaMigrator(db.ConnectionString!, TextWriter.Null) { Scripts = changed }.MigrateAsync(ct: Ct);
        Assert.Equal(["programmable/R__usp_ticket_list.sql"], applied);
    }

    [Fact]
    public async Task V0006_includes_AssignedTo_in_the_open_unassigned_index_and_keeps_its_filter()
    {
        await using var db = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).MigrateAsync(ct: Ct);
        var columns = await db.RowsAsync("""
            SELECT c.name, ic.is_included_column, i.filter_definition
            FROM sys.indexes AS i
            JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('dbo.Tickets') AND i.name = 'IX_Tickets_Open_Unassigned'
            ORDER BY ic.is_included_column, ic.key_ordinal, c.name
            """);
        Assert.Equal(["TargetDate", "DueDate", "LastUpdateDate", "AssignedTo"], columns.Select(r => (string)r[0]!));
        Assert.Equal([false, false, false, true], columns.Select(r => (bool)r[1]!));
        Assert.All(columns, r => Assert.Equal("([TicketStatus]<>(3) AND [AssignedTo] IS NULL)", (string)r[2]!));
    }

    [Fact]
    public async Task Only_the_tag_count_asks_for_the_parallel_plan()
    {
        // The code fix that replaces the Query Store forced plan (docs/sql/tuning.md): the tag shape's count statement
        // carries the hint, other shapes' statements do not. Read back from this scratch database's Query Store (the plan
        // cache is instance-wide and other tests' DROP DATABASE flushes it).
        await using var db = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).MigrateAsync(ct: Ct);
        await SqlHelpers.ExecAsync(db.ConnectionString!, "ALTER DATABASE CURRENT SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE, QUERY_CAPTURE_MODE = ALL);");
        await SqlHelpers.ExecAsync(db.ConnectionString!, "DECLARE @c int; EXEC dbo.usp_TicketList @Tag = N'printer', @TotalCount = @c OUTPUT;");
        await SqlHelpers.ExecAsync(db.ConnectionString!, "DECLARE @c int; EXEC dbo.usp_TicketList @List = 'opentickets', @TotalCount = @c OUTPUT;");
        // Query Store shows a new query asynchronously: under the suite's load the second call's statement was missing right
        // after the call and there 3 s later (Round 5, docs/evidence/followups-20261113/flakes/). So wait (bounded) until both
        // count statements are captured, then check them.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        List<string> counts;
        while ((counts = await CountStatementsAsync(db)).Count < 2 && sw.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        Assert.Equal(2, counts.Count);
        Assert.Single(counts, t => t.Contains("g.TagName = @Tag", StringComparison.Ordinal)
                                   && t.Contains("OPTION (USE HINT('ENABLE_PARALLEL_PLAN_PREFERENCE'))", StringComparison.Ordinal));
        Assert.Single(counts, t => !t.Contains("g.TagName", StringComparison.Ordinal) && !t.Contains("USE HINT", StringComparison.Ordinal));
    }

    /// <summary>The distinct texts of the procedure's dynamic count statements in the database's Query Store.</summary>
    private static async Task<List<string>> CountStatementsAsync(ScratchDatabase db) => (await db.RowsAsync("""
        SELECT qt.query_sql_text
        FROM sys.query_store_query_text AS qt JOIN sys.query_store_query AS q ON q.query_text_id = qt.query_text_id
        WHERE q.object_id = 0 -- the dynamic statements, not the procedure's own
          AND qt.query_sql_text LIKE N'%SELECT @TotalCount = COUNT(*) FROM dbo.Tickets%'
          AND qt.query_sql_text NOT LIKE N'%query_store%' -- this query itself
        """)).Select(r => (string)r[0]!).Distinct().ToList();

    [Fact]
    public async Task Cli_refuses_the_contract_stage_without_the_retirement_flag()
    {
        var err = new StringWriter();
        Assert.Equal(2, await Cli.RunAsync(["migrate", "--contract"], TextWriter.Null, err, _ => "Server=unused"));
        Assert.Contains("--legacy-retired", err.ToString());
        Assert.Equal(2, await Cli.RunAsync(["status"], TextWriter.Null, TextWriter.Null, _ => null)); // no connection string
    }
}
