using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>
/// Per-table reconciliation (the Azure SQL move's acceptance check): two databases seeded identically match; any real
/// difference in a row, a count, a column, an offset or NULL-vs-empty is a mismatch on exactly that table; rowversion
/// values (which an import always changes) are not; the CLI exits 1 on a mismatch.
/// </summary>
public sealed class ReconcilerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // the seeder's schema step gives the four roles new GUIDs in every database, so two seeded databases differ there by
    // construction; a real source and its copy do not
    private static readonly HashSet<string> RoleTables = new(StringComparer.OrdinalIgnoreCase) { "dbo.IdentityRoles", "dbo.IdentityUserRoles" };

    private static async Task<(LegacyOnlyDatabase Source, LegacyOnlyDatabase Target)> PairAsync()
    {
        var s = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        var t = await TempDatabase.CreateAsync<LegacyOnlyDatabase>();
        return (s, t);
    }

    [Fact]
    public async Task Identical_databases_match_even_when_rowversions_differ()
    {
        var (s, t) = await PairAsync();
        await using var _ = s;
        await using var __ = t;
        await SqlHelpers.ExecAsync(t.ConnectionString!, "UPDATE dbo.Tickets SET Title = Title WHERE TicketId % 7 = 0"); // new rowversions only
        var report = await new Reconciler(s.ConnectionString!, t.ConnectionString!) { ExcludeTables = RoleTables }.RunAsync(Ct);
        Assert.True(report.Match, Reconciler.Format(report));
        var tickets = report.Tables.Single(x => x.Table == "dbo.Tickets");
        Assert.Equal(["Version"], tickets.ExcludedColumns);
        Assert.Equal(await s.ScalarAsync<long>("SELECT COUNT_BIG(*) FROM dbo.TicketEventNotifications"),
            report.Tables.Single(x => x.Table == "dbo.TicketEventNotifications").Target!.Rows);

        var output = new StringWriter();
        var exit = await Cli.RunAsync(["reconcile", "--exclude", "dbo.IdentityRoles,dbo.IdentityUserRoles"], output, output, n => n switch { "TD_DB_SOURCE" => s.ConnectionString, "TD_DB_TARGET" => t.ConnectionString, _ => null });
        Assert.Equal(0, exit);
        Assert.Contains("MATCH", output.ToString());
    }

    [Fact]
    public async Task Each_kind_of_difference_is_a_mismatch_on_that_table_only()
    {
        var (s, t) = await PairAsync();
        await using var _ = s;
        await using var __ = t;
        var tc = t.ConnectionString!;
        await SqlHelpers.ExecAsync(tc, "UPDATE dbo.Tickets SET Title = Title + N'.' WHERE TicketId = (SELECT MIN(TicketId) FROM dbo.Tickets)");
        await SqlHelpers.ExecAsync(tc, "DELETE TOP (1) FROM dbo.TicketTags");
        // same instant, different offset: a real difference for the app (it shows local times)
        await SqlHelpers.ExecAsync(tc, "UPDATE dbo.TicketEvents SET EventDate = SWITCHOFFSET(EventDate, '-08:00') WHERE EventId = (SELECT MIN(EventId) FROM dbo.TicketEvents)");
        await SqlHelpers.ExecAsync(tc, "UPDATE dbo.Projects SET ProjectDescription = N'' WHERE ProjectDescription IS NULL; IF @@ROWCOUNT = 0 UPDATE TOP (1) dbo.Projects SET ProjectDescription = NULL");
        await SqlHelpers.ExecAsync(tc, "ALTER TABLE dbo.UserSettings ADD Extra int NULL");

        var report = await new Reconciler(s.ConnectionString!, tc) { ExcludeTables = RoleTables }.RunAsync(Ct);
        Assert.False(report.Match);
        var bad = report.Tables.Where(x => !x.Match).ToDictionary(x => x.Table, x => string.Join("; ", x.Problems));
        Assert.Equal(["dbo.Projects", "dbo.TicketEvents", "dbo.TicketTags", "dbo.Tickets", "dbo.UserSettings"], bad.Keys.Order(StringComparer.Ordinal));
        Assert.Contains("content hash differs", bad["dbo.Tickets"]);
        Assert.Contains("content hash differs", bad["dbo.TicketEvents"]);
        Assert.Contains("content hash differs", bad["dbo.Projects"]);
        Assert.Contains("row count", bad["dbo.TicketTags"]);
        Assert.Contains("Extra", bad["dbo.UserSettings"]);

        var exit = await Cli.RunAsync(["reconcile", "--exclude", "dbo.IdentityRoles,dbo.IdentityUserRoles"], TextWriter.Null, TextWriter.Null, n => n switch { "TD_DB_SOURCE" => s.ConnectionString, "TD_DB_TARGET" => tc, _ => null });
        Assert.Equal(1, exit);
    }

    [Fact]
    public void Row_hash_encoding_is_length_prefixed_and_marks_NULL()
    {
        var sql = Reconciler.RowHashExpression([
            new ColumnInfo("A", "nvarchar", 20, 0, 0, true, false, false),
            new ColumnInfo("B", "datetimeoffset", 10, 34, 7, false, false, false),
            new ColumnInfo("C", "float", 8, 53, 0, true, false, false),
        ]);
        Assert.Contains("ISNULL(CONCAT(DATALENGTH(CONVERT(nvarchar(max), [A])), N':', CONVERT(nvarchar(max), [A])), N'~')", sql);
        Assert.Contains("CONVERT(nvarchar(max), [B], 121)", sql);
        Assert.Contains("CONVERT(nvarchar(max), [C], 3)", sql);
        Assert.StartsWith("HASHBYTES('SHA2_256', CONCAT(", sql);
    }
}
