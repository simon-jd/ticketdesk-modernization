using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data;

namespace TicketDesk.Database.Tests;

/// <summary>
/// The contract stage after the legacy app is retired (Round 4): C0001 on an expand-stage database, and the app's model (no
/// HasTrigger since then) writing on the result.
/// </summary>
public sealed class ContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Contract_drops_the_trigger_and_IX_ProjectId_and_enforces_the_invariant()
    {
        await using var db = await TempDatabase.CreateAsync<ExpandOnlyDatabase>();
        var applied = await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).MigrateAsync(includeContract: true, ct: Ct);
        Assert.Equal(["contract/C0001__assigned_date_contract.sql"], applied);

        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE name = 'TR_Tickets_AssignedDate'"));
        Assert.Equal(0, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'IX_ProjectId'"));
        Assert.Equal(1, await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_Tickets_AssignedDate' AND is_not_trusted = 0"));

        // with the trigger gone, a writer that forgets AssignedDate is stopped by the constraint instead of being fixed silently
        var e = await Assert.ThrowsAsync<SqlException>(() => SqlHelpers.ExecAsync(db.ConnectionString!,
            "UPDATE TOP (1) dbo.Tickets SET AssignedTo = @a, AssignedDate = NULL WHERE AssignedTo IS NULL", ("@a", db.UserInRole("TdHelpDeskUsers").Id)));
        Assert.Equal(547, e.Number);
        Assert.Empty((await new SchemaMigrator(db.ConnectionString!, TextWriter.Null).PlanAsync(Ct)).ContractPending);

        // EF Core's own statements (UPDATE ... OUTPUT, no trigger declared) work again; a write that sets the date passes the
        // constraint, one that leaves it behind does not
        var builder = new DbContextOptionsBuilder<TicketDeskDbContext>();
        builder.UseTicketDeskSqlServer(db.ConnectionString!);
        var id = await db.ScalarAsync<int>("SELECT TOP 1 TicketId FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo IS NULL ORDER BY TicketId");
        var at = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        await using (var ctx = new TicketDeskDbContext(builder.Options))
        {
            var t = await ctx.Tickets.SingleAsync(x => x.TicketId == id, Ct);
            var version = t.Version;
            t.AssignedTo = db.UserInRole("TdHelpDeskUsers").Id;
            t.AssignedDate = at;
            t.LastUpdateDate = at;
            Assert.Equal(1, await ctx.SaveChangesAsync(Ct));
            Assert.NotEqual(version, t.Version); // the new rowversion came back through OUTPUT
        }
        await using (var ctx = new TicketDeskDbContext(builder.Options))
        {
            var t = await ctx.Tickets.SingleAsync(x => x.TicketId == id, Ct);
            t.AssignedTo = null; // and forgets AssignedDate
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync(Ct));
            Assert.Equal(547, Assert.IsType<SqlException>(ex.InnerException).Number);
        }
    }
}
