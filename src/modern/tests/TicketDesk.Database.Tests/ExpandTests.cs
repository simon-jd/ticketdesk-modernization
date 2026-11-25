using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;
using TicketDesk.Testing;

namespace TicketDesk.Database.Tests;

/// <summary>
/// Expand stage of Tickets.AssignedDate while the legacy app still wrote: the backfill, the dual-write trigger under
/// the statements EF6 actually sends (UPDATE/INSERT followed by a SELECT guarded by @@ROWCOUNT), legacy session
/// settings against the new filtered indexes, and why the app's model needs the contract stage now that it declares no trigger.
/// </summary>
public sealed class ExpandTests(ExpandOnlyDatabase db) : IClassFixture<ExpandOnlyDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private string Conn => db.ConnectionString!;

    private string Staff(int i)
    {
        var staff = db.Users.Where(u => u.Role == "TdHelpDeskUsers").ToList();
        return staff[i % staff.Count].Id;
    }

    private const string Ef6Update = """
        UPDATE [dbo].[Tickets]
        SET [AssignedTo] = @0, [LastUpdateBy] = @1, [LastUpdateDate] = @2
        WHERE (([TicketId] = @3) AND ([Version] = @4))
        SELECT [Version]
        FROM [dbo].[Tickets]
        WHERE @@ROWCOUNT > 0 AND [TicketId] = @3
        """;

    private async Task<(int Id, byte[] Version, string By)> PickTicketAsync(string where)
    {
        var r = (await db.RowsAsync($"SELECT TOP 1 TicketId, Version, Owner FROM dbo.Tickets WHERE {where} ORDER BY NEWID()"))[0];
        return ((int)r[0]!, (byte[])r[1]!, (string)r[2]!);
    }

    /// <summary>Runs an EF6-shaped batch and returns the Version rows its trailing SELECT produced.</summary>
    private async Task<List<byte[]>> Ef6BatchAsync(string sql, params (string, object?)[] p)
    {
        await using var c = new SqlConnection(Conn);
        await c.OpenAsync(Ct);
        await using var cmd = new SqlCommand(sql, c);
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var rows = new List<byte[]>();
        await using var r = await cmd.ExecuteReaderAsync(Ct);
        while (await r.ReadAsync(Ct)) rows.Add((byte[])r[0]);
        return rows;
    }

    [Fact]
    public async Task Backfill_matches_the_assignment_event_rule_for_every_ticket()
    {
        // own database: the other tests reassign tickets, after which the trigger (not the rule) owns the value
        await using var fresh = await TempDatabase.CreateAsync<ExpandOnlyDatabase>();
        var wrong = await fresh.ScalarAsync<int>("""
            SELECT COUNT(*) FROM dbo.Tickets AS t
            OUTER APPLY (SELECT MAX(e.EventDate) AS D FROM dbo.TicketEvents AS e WHERE e.TicketId = t.TicketId
                         AND ((e.ForActivity & (16 | 64 | 128 | 256)) <> 0 OR ((e.ForActivity & 1024) <> 0 AND e.EventBy = t.AssignedTo))) AS x
            WHERE (t.AssignedTo IS NULL AND t.AssignedDate IS NOT NULL)
               OR (t.AssignedTo IS NOT NULL AND (t.AssignedDate IS NULL OR t.AssignedDate <> COALESCE(x.D, t.CreatedDate)))
            """);
        Assert.Equal(0, wrong);
        Assert.True(await fresh.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE AssignedDate IS NOT NULL") > 100);
    }

    [Fact]
    public async Task Legacy_reassignment_batch_still_gets_its_version_row_and_the_trigger_sets_AssignedDate()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var newAssignee = Staff(1);
        var (id, version, by) = await PickTicketAsync($"AssignedTo IS NOT NULL AND AssignedTo <> '{newAssignee}' AND TicketStatus <> 3");
        var at = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.FromHours(-8));
        var rows = await Ef6BatchAsync(Ef6Update, ("@0", newAssignee), ("@1", by), ("@2", at), ("@3", id), ("@4", version));
        Assert.Single(rows); // @@ROWCOUNT after the UPDATE is the UPDATE's own count, not the trigger's
        Assert.NotEqual(version, rows[0]);
        Assert.Equal(rows[0], await db.ScalarAsync<byte[]>("SELECT Version FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));
        Assert.Equal(at, await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));

        // the stale version now fails EF6's concurrency check exactly as before (zero rows back)
        Assert.Empty(await Ef6BatchAsync(Ef6Update, ("@0", by), ("@1", by), ("@2", at), ("@3", id), ("@4", version)));
    }

    [Fact]
    public async Task Unassigning_clears_AssignedDate_and_other_updates_leave_it_alone()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var (id, version, by) = await PickTicketAsync("AssignedTo IS NOT NULL AND TicketStatus <> 3");
        var before = await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id));

        await SqlHelpers.ExecAsync(Conn, "UPDATE dbo.Tickets SET Title = Title + N'!', LastUpdateDate = SYSDATETIMEOFFSET() WHERE TicketId = @id", ("@id", id));
        Assert.Equal(before, await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));

        // same assignee written again (EF6 sends every modified property; the value did not change)
        await SqlHelpers.ExecAsync(Conn, "UPDATE dbo.Tickets SET AssignedTo = AssignedTo, LastUpdateDate = SYSDATETIMEOFFSET() WHERE TicketId = @id", ("@id", id));
        Assert.Equal(before, await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));

        var current = await db.ScalarAsync<byte[]>("SELECT Version FROM dbo.Tickets WHERE TicketId = @id", ("@id", id));
        Assert.Single(await Ef6BatchAsync(Ef6Update, ("@0", null), ("@1", by), ("@2", DateTimeOffset.UtcNow), ("@3", id), ("@4", current)));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE TicketId = @id AND AssignedDate IS NOT NULL", ("@id", id)));
    }

    [Fact]
    public async Task A_writer_that_sets_AssignedDate_itself_keeps_its_value()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var (id, _, _) = await PickTicketAsync("TicketStatus <> 3");
        var explicitDate = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await SqlHelpers.ExecAsync(Conn, "UPDATE dbo.Tickets SET AssignedTo = @a, AssignedDate = @d, LastUpdateDate = SYSDATETIMEOFFSET() WHERE TicketId = @id",
            ("@a", Staff(2)), ("@d", explicitDate), ("@id", id));
        Assert.Equal(explicitDate, await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));
    }

    [Fact]
    public async Task Set_based_reassignment_sets_each_row_from_its_own_LastUpdateDate()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var assignee = Staff(3);
        await SqlHelpers.ExecAsync(Conn, """
            UPDATE t SET AssignedTo = @a, LastUpdateDate = DATEADD(minute, t.TicketId % 97, CAST('2026-11-02T08:00:00-05:00' AS datetimeoffset(7)))
            FROM dbo.Tickets AS t WHERE t.TicketId IN (SELECT TOP 5 TicketId FROM dbo.Tickets WHERE TicketStatus = 3 AND AssignedTo <> @a ORDER BY TicketId)
            """, ("@a", assignee));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE AssignedTo = @a AND TicketStatus = 3 AND LastUpdateDate >= '2026-11-02T08:00:00-05:00' AND AssignedDate <> LastUpdateDate", ("@a", assignee)));
        Assert.Equal(5, await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE AssignedTo = @a AND TicketStatus = 3 AND LastUpdateDate >= '2026-11-02T08:00:00-05:00'", ("@a", assignee)));
    }

    [Fact]
    public async Task Legacy_insert_batch_still_reads_back_its_row_and_an_assigned_new_ticket_gets_CreatedDate()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var staff = Staff(0);
        await using var c = new SqlConnection(Conn);
        await c.OpenAsync(Ct);
        await using var cmd = new SqlCommand("""
            INSERT [dbo].[Tickets]([ProjectId], [TicketType], [Category], [Title], [Details], [IsHtml], [TagList], [CreatedBy], [Owner], [AssignedTo],
                [TicketStatus], [CurrentStatusDate], [CurrentStatusSetBy], [LastUpdateBy], [LastUpdateDate], [Priority], [AffectsCustomer])
            VALUES (@0, N'Question', N'Hardware', N'expand test', N'details', 0, NULL, @1, @1, @1, 0, SYSDATETIMEOFFSET(), @1, @1, SYSDATETIMEOFFSET(), NULL, 0)
            SELECT [TicketId], [CreatedDate], [Version]
            FROM [dbo].[Tickets]
            WHERE @@ROWCOUNT > 0 AND [TicketId] = scope_identity()
            """, c);
        cmd.Parameters.AddWithValue("@0", db.Generator!.ProjectIds[0]);
        cmd.Parameters.AddWithValue("@1", staff);
        int id;
        DateTimeOffset created;
        await using (var r = await cmd.ExecuteReaderAsync(Ct))
        {
            Assert.True(await r.ReadAsync(Ct)); // scope_identity() is the INSERT's, the trigger's UPDATE does not disturb it
            (id, created) = (r.GetInt32(0), r.GetDateTimeOffset(1));
            Assert.False(await r.ReadAsync(Ct));
        }
        Assert.Equal(created, await db.ScalarAsync<DateTimeOffset>("SELECT AssignedDate FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));
    }

    [Fact]
    public async Task Legacy_session_settings_still_write_but_QUOTED_IDENTIFIER_OFF_is_refused_by_the_filtered_indexes()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var (id, _, _) = await PickTicketAsync("TicketStatus <> 3");
        await using var c = new SqlConnection(Conn);
        await c.OpenAsync(Ct);
        // what System.Data.SqlClient (EF6) and Microsoft.Data.SqlClient send: ARITHABORT OFF; ANSI_WARNINGS ON makes it effectively ON
        await using (var ok = new SqlCommand("SET ARITHABORT OFF; UPDATE dbo.Tickets SET LastUpdateDate = SYSDATETIMEOFFSET() WHERE TicketId = @id", c))
        {
            ok.Parameters.AddWithValue("@id", id);
            Assert.Equal(1, await ok.ExecuteNonQueryAsync(Ct));
        }
        // an old tool session with QUOTED_IDENTIFIER OFF cannot write to a table with filtered indexes (error 1934)
        await using var bad = new SqlCommand("SET QUOTED_IDENTIFIER OFF; UPDATE dbo.Tickets SET LastUpdateDate = SYSDATETIMEOFFSET() WHERE TicketId = @id", c);
        bad.Parameters.AddWithValue("@id", id);
        var e = await Assert.ThrowsAsync<SqlException>(() => bad.ExecuteNonQueryAsync(Ct));
        Assert.Equal(1934, e.Number);
    }

    [Fact]
    public async Task EF_Core_without_HasTrigger_cannot_update_tickets_while_the_expand_trigger_exists()
    {
        // Round 4 removed tb.HasTrigger(...) together with the trigger (contract C0001). On an expand-only database the app's
        // UPDATE ... OUTPUT INSERTED.[Version] is rejected (334): the contract stage must run before this release, never after.
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        var (id, _, _) = await PickTicketAsync("TicketStatus <> 3 AND AssignedTo IS NULL");
        var builder = new DbContextOptionsBuilder<TicketDeskDbContext>();
        builder.UseTicketDeskSqlServer(Conn);
        await using var ctx = new TicketDeskDbContext(builder.Options);
        Assert.Empty(ctx.Model.FindEntityType(typeof(Data.Entities.Ticket))!.GetDeclaredTriggers());
        var ticket = await ctx.Tickets.SingleAsync(t => t.TicketId == id, Ct);
        var at = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        ticket.AssignedTo = Staff(0);
        ticket.AssignedDate = at;
        ticket.LastUpdateDate = at;
        var e = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync(Ct));
        Assert.Equal(334, Assert.IsType<SqlException>(e.InnerException).Number);
        Assert.Null(await db.ScalarAsync<string?>("SELECT AssignedTo FROM dbo.Tickets WHERE TicketId = @id", ("@id", id)));

        // the statement shape itself, by hand
        var raw = await Assert.ThrowsAsync<SqlException>(() => SqlHelpers.ExecAsync(Conn,
            "UPDATE dbo.Tickets SET Title = Title OUTPUT INSERTED.Version WHERE TicketId = @id", ("@id", id)));
        Assert.Equal(334, raw.Number);
    }
}
