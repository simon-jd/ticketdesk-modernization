using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDesk.Data.Configurations;
using TicketDesk.Data.Entities;
using TicketDesk.Testing;

namespace TicketDesk.Data.Tests;

/// <summary>
/// EF6 -> EF Core behaviour differences that matter for TicketDesk, each pinned against SQL Server
/// (docs/migration/ef6-to-efcore.md links here). Writes happen inside transactions that are rolled back, except the
/// concurrency test, which needs two connections and cleans up after itself.
/// </summary>
public sealed class EfBehaviourTests(ScratchDatabase scratch)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private TicketDeskDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] i)
    {
        Assert.SkipUnless(scratch.Available, SqlTestEnvironment.SkipReason);
        return Db.Create(scratch.ConnectionString!, i);
    }

    private async Task<int> TicketWithEventsTagsAndSubscribersAsync() => await scratch.ScalarAsync<int>("""
        SELECT TOP 1 t.TicketId FROM dbo.Tickets t
        WHERE EXISTS (SELECT 1 FROM dbo.TicketTags g WHERE g.TicketId = t.TicketId)
          AND (SELECT COUNT(*) FROM dbo.TicketEvents e WHERE e.TicketId = t.TicketId) > 1
          AND EXISTS (SELECT 1 FROM dbo.TicketEventNotifications n WHERE n.TicketId = t.TicketId)
        ORDER BY t.TicketId
        """);

    [Fact]
    public async Task Lazy_loading_is_off_navigations_stay_empty_without_Include()
    {
        await using var db = NewContext();
        var id = await TicketWithEventsTagsAndSubscribersAsync();
        var plain = await db.Tickets.AsNoTracking().SingleAsync(t => t.TicketId == id, Ct);
        Assert.Empty(plain.TicketEvents); // EF6 would have lazy-loaded the virtual collection here
        Assert.Null(plain.Project);
        var loaded = await db.Tickets.AsNoTracking().Include(t => t.TicketEvents).SingleAsync(t => t.TicketId == id, Ct);
        Assert.Equal(await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.TicketEvents WHERE TicketId = @id", ("@id", id)), loaded.TicketEvents.Count);
    }

    private static bool LooksUrgent(string title) => title.Contains('!');

    [Fact]
    public async Task Untranslatable_predicate_throws_instead_of_client_evaluating()
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Tickets.Where(t => LooksUrgent(t.Title)).CountAsync(Ct));
        // only the final projection may run client code
        var titles = await db.Tickets.OrderBy(t => t.TicketId).Take(3).Select(t => LooksUrgent(t.Title)).ToListAsync(Ct);
        Assert.Equal(3, titles.Count);
    }

    [Fact]
    public async Task String_equality_follows_the_database_collation()
    {
        await using var db = NewContext();
        var user = scratch.Users[0];
        // SQL Server CI_AS: case-insensitive and trailing spaces ignored, unlike string.Equals in C#
        Assert.True(await db.Users.AnyAsync(u => u.UserName == user.UserName.ToUpperInvariant(), Ct));
        Assert.True(await db.Users.AnyAsync(u => u.UserName == user.UserName + "  ", Ct));
        var collation = await scratch.ScalarAsync<string>("SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('dbo.IdentityUsers') AND name = 'UserName'");
        Assert.Contains("_CI_", collation);
    }

    [Fact]
    public async Task Null_comparison_uses_csharp_semantics_where_entity_sql_used_sql_semantics()
    {
        await using var db = NewContext();
        string? nobody = null;
        var isNull = await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE AssignedTo IS NULL");
        Assert.True(isNull > 0);
        Assert.Equal(isNull, await db.Tickets.CountAsync(t => t.AssignedTo == nobody, Ct)); // EF Core: IS NULL
        // what the legacy Entity SQL "it.AssignedTo = @AssignedTo" with a null parameter sends: '= NULL' matches nothing
        Assert.Equal(0, await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tickets WHERE AssignedTo = @p", ("@p", null)));
    }

    [Fact]
    public async Task Several_collection_includes_must_choose_split_or_single_query()
    {
        var counter = new CommandCounter();
        await using var db = NewContext(counter);
        var id = await TicketWithEventsTagsAndSubscribersAsync();
        IQueryable<Ticket> Graph() => db.Tickets.AsNoTracking().Where(t => t.TicketId == id)
            .Include(t => t.TicketEvents).Include(t => t.TicketTags).Include(t => t.TicketSubscribers);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Graph().SingleAsync(Ct)); // warning configured as error

        counter.Count = 0;
        var split = await Graph().AsSplitQuery().SingleAsync(Ct);
        Assert.Equal(4, counter.Count); // root + one query per collection

        counter.Count = 0;
        var single = await Graph().AsSingleQuery().SingleAsync(Ct);
        Assert.Equal(1, counter.Count); // one JOINed query (EF6 behaviour), rows multiply

        Assert.Equal(split.TicketEvents.Count, single.TicketEvents.Count);
        Assert.Equal(split.TicketTags.Count, single.TicketTags.Count);
        Assert.Equal(split.TicketSubscribers.Count, single.TicketSubscribers.Count);
    }

    [Fact]
    public async Task Rowversion_conflict_throws_DbUpdateConcurrencyException()
    {
        int projectId;
        await using (var setup = NewContext())
        {
            var p = new Project { ProjectName = "concurrency-" + Guid.NewGuid().ToString("N")[..8] };
            setup.Projects.Add(p);
            await setup.SaveChangesAsync(Ct);
            projectId = p.ProjectId;
        }
        try
        {
            await using var a = NewContext();
            await using var b = NewContext();
            var pa = await a.Projects.SingleAsync(p => p.ProjectId == projectId, Ct);
            var pb = await b.Projects.SingleAsync(p => p.ProjectId == projectId, Ct);
            pa.ProjectDescription = "first writer";
            await a.SaveChangesAsync(Ct);
            pb.ProjectDescription = "second writer";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync(Ct));
        }
        finally
        {
            await using var cleanup = NewContext();
            await cleanup.Projects.Where(p => p.ProjectId == projectId).ExecuteDeleteAsync(Ct);
        }
    }

    [Fact]
    public async Task Database_generated_dates_are_never_written_and_EventId_comes_from_identity()
    {
        await using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var old = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var user = scratch.Users[0].Id;
        var ticket = new Ticket
        {
            ProjectId = scratch.Generator!.ProjectIds[0], TicketType = "Problem", Category = "Software", Title = "generated dates", Details = "d",
            CreatedBy = user, CreatedDate = old, Owner = user, TicketStatus = TicketStatus.Active, CurrentStatusDate = DateTimeOffset.UtcNow,
            CurrentStatusSetBy = user, LastUpdateBy = user, LastUpdateDate = DateTimeOffset.UtcNow,
        };
        ticket.TicketEvents.Add(new TicketEvent { EventBy = user, EventDate = old, ForActivity = TicketActivity.Create, EventDescription = "created" });
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);

        Assert.True(ticket.CreatedDate.Year >= 2026, "CreatedDate must come from SYSDATETIMEOFFSET(), not from the entity");
        var evt = ticket.TicketEvents.Single();
        Assert.True(evt.EventId > 0);
        Assert.True(evt.EventDate.Year >= 2026);

        var created = ticket.CreatedDate;
        ticket.CreatedDate = old;
        ticket.Title = "changed";
        await db.SaveChangesAsync(Ct);
        var stored = await db.Database.SqlQuery<DateTimeOffset>($"SELECT CreatedDate AS Value FROM dbo.Tickets WHERE TicketId = {ticket.TicketId}").SingleAsync(Ct);
        Assert.Equal(created, stored);
    }

    [Fact]
    public async Task DateTimeOffset_keeps_its_offset_and_compares_by_instant()
    {
        await using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var id = await TicketWithEventsTagsAndSubscribersAsync();
        var pacific = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.FromHours(-8)); // what DateTime.Now gave on a PST server
        await db.Tickets.Where(t => t.TicketId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.DueDate, pacific), Ct);

        var read = await db.Tickets.AsNoTracking().Where(t => t.TicketId == id).Select(t => t.DueDate).SingleAsync(Ct);
        Assert.Equal(TimeSpan.FromHours(-8), read!.Value.Offset);
        var sameInstantUtc = pacific.ToUniversalTime(); // what a UTC container writes for the same moment
        Assert.True(await db.Tickets.AnyAsync(t => t.TicketId == id && t.DueDate == sameInstantUtc, Ct));
    }

    [Fact]
    public async Task LockoutEndDateUtc_reads_as_utc_and_DateTime_MaxValue_is_clamped()
    {
        await using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var id = scratch.Users[1].Id;
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct);
        user.LockoutEndDateUtc = DateTime.MaxValue; // legacy "disabled" marker; unclamped it overflows the datetime column
        await db.SaveChangesAsync(Ct);

        var raw = await db.Database.SqlQuery<DateTime>($"SELECT LockoutEndDateUtc AS Value FROM dbo.IdentityUsers WHERE Id = {id}").SingleAsync(Ct);
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, 997), raw);

        db.ChangeTracker.Clear();
        var reread = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
        Assert.Equal(DateTimeKind.Utc, reread.LockoutEndDateUtc!.Value.Kind);
        Assert.Equal(SqlUtcDateTime.MaxValue, reread.LockoutEndDateUtc);
    }

    [Fact]
    public async Task Database_cascades_ticket_deletes_and_restricts_project_deletes()
    {
        await using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var id = await TicketWithEventsTagsAndSubscribersAsync();
        var projectId = await db.Tickets.Where(t => t.TicketId == id).Select(t => t.ProjectId).SingleAsync(Ct);

        // set-based delete (no tracked dependents): the database cascades to events, notifications, tags, subscribers
        Assert.Equal(1, await db.Tickets.Where(t => t.TicketId == id).ExecuteDeleteAsync(Ct));
        Assert.Equal(0, await db.TicketEvents.CountAsync(e => e.TicketId == id, Ct));
        Assert.Equal(0, await db.TicketEventNotifications.CountAsync(n => n.TicketId == id, Ct));
        Assert.Equal(0, await db.TicketTags.CountAsync(g => g.TicketId == id, Ct));
        Assert.Equal(0, await db.TicketSubscribers.CountAsync(s => s.TicketId == id, Ct));

        // Tickets -> Projects has no cascade (Td2.5.017): deleting a project that still has tickets fails
        var e = await Assert.ThrowsAsync<SqlException>(() => db.Projects.Where(p => p.ProjectId == projectId).ExecuteDeleteAsync(Ct));
        Assert.Equal(547, e.Number);
    }

    [Fact]
    public async Task Nothing_writes_migration_history()
    {
        await using var db = NewContext();
        _ = await db.Tickets.CountAsync(Ct);
        Assert.Null(await scratch.ScalarAsync<int?>("SELECT OBJECT_ID('dbo.__EFMigrationsHistory')"));
        Assert.Equal(0, await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.__MigrationHistory"));
    }
}
