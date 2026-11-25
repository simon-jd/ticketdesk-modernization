using Microsoft.EntityFrameworkCore;
using TicketDesk.Data.Entities;
using TicketDesk.Testing;

namespace TicketDesk.Data.Tests;

/// <summary>EF Core reads the database seeded by ops/dev/seed.sh (TicketDeskSynth, scale S): every table, every row.</summary>
public sealed class SeededReadTests(SeededDatabase seeded)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private TicketDeskDbContext NewContext()
    {
        Assert.SkipUnless(seeded.ConnectionString != null, $"{SqlTestEnvironment.SkipReason} and seed {SqlTestEnvironment.SeededDatabaseName}");
        return Db.Create(seeded.ConnectionString!);
    }

    [Fact]
    public async Task Row_counts_through_EF_match_SQL_for_every_mapped_table()
    {
        await using var db = NewContext();
        var viaEf = new Dictionary<string, long>
        {
            ["Projects"] = await db.Projects.LongCountAsync(Ct),
            ["Tickets"] = await db.Tickets.LongCountAsync(Ct),
            ["TicketEvents"] = await db.TicketEvents.LongCountAsync(Ct),
            ["TicketEventNotifications"] = await db.TicketEventNotifications.LongCountAsync(Ct),
            ["TicketSubscribers"] = await db.TicketSubscribers.LongCountAsync(Ct),
            ["TicketTags"] = await db.TicketTags.LongCountAsync(Ct),
            ["UserSettings"] = await db.UserSettings.LongCountAsync(Ct),
            ["ApplicationSettings"] = await db.ApplicationSettings.LongCountAsync(Ct),
            ["IdentityUsers"] = await db.Users.LongCountAsync(Ct),
            ["IdentityRoles"] = await db.Roles.LongCountAsync(Ct),
            ["IdentityUserRoles"] = await db.UserRoles.LongCountAsync(Ct),
            ["IdentityUserClaims"] = await db.UserClaims.LongCountAsync(Ct),
            ["IdentityUserLogins"] = await db.UserLogins.LongCountAsync(Ct),
        };
        // notifications.* (push notifications) exists in the restored legacy database, not in a td-seed database
        var notifications = await Sql.ScalarAsync<int>(seeded.ConnectionString!, "SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'notifications'") > 0;
        if (notifications)
        {
            viaEf["notifications.ApplicationPushNotificationSettings"] = await db.Set<TicketDesk.Data.Entities.ApplicationPushNotificationSetting>().LongCountAsync(Ct);
            viaEf["notifications.SubscriberPushNotificationSettings"] = await db.Set<TicketDesk.Data.Entities.SubscriberPushNotificationSetting>().LongCountAsync(Ct);
            viaEf["notifications.PushNotificationDestinations"] = await db.Set<TicketDesk.Data.Entities.PushNotificationDestination>().LongCountAsync(Ct);
            viaEf["notifications.PushNotificationItems"] = await db.Set<TicketDesk.Data.Entities.PushNotificationItem>().LongCountAsync(Ct);
            viaEf["notifications.TicketPushNotificationItems"] = await db.Set<TicketDesk.Data.Entities.TicketPushNotificationItem>().LongCountAsync(Ct);
        }
        Assert.Equal(db.Model.GetEntityTypes().Count(e => notifications || e.GetSchema() != "notifications"), viaEf.Count);
        foreach (var (table, count) in viaEf)
        {
            var name = table.Contains('.') ? $"notifications.[{table.Split('.')[1]}]" : $"dbo.[{table}]";
            Assert.Equal(await Sql.ScalarAsync<long>(seeded.ConnectionString!, $"SELECT COUNT_BIG(*) FROM {name}"), count);
        }
        Assert.True(viaEf["Tickets"] > 0, "the seeded database has no tickets");
    }

    [Fact]
    public async Task Every_row_materializes_with_known_enum_values()
    {
        await using var db = NewContext();
        // materializing all rows proves every column converts (types, nullability, datetimeoffset, decimal, rowversion)
        var tickets = await db.Tickets.AsNoTracking().ToListAsync(Ct);
        Assert.All(tickets, t => Assert.True(Enum.IsDefined(t.TicketStatus)));
        Assert.All(tickets, t => Assert.Equal(8, t.Version.Length));
        var activities = await db.TicketEvents.AsNoTracking().Select(e => e.ForActivity).Distinct().ToListAsync(Ct);
        var known = Enum.GetValues<TicketActivity>().Aggregate(0, (a, v) => a | (int)v);
        Assert.All(activities, a => Assert.Equal(0, (int)a & ~known));
        var users = await db.Users.AsNoTracking().ToListAsync(Ct);
        Assert.All(users, u => Assert.True(u.LockoutEndDateUtc is null || u.LockoutEndDateUtc.Value.Kind == DateTimeKind.Utc));
        Assert.NotEmpty(await db.TicketEventNotifications.AsNoTracking().Take(1000).ToListAsync(Ct));
    }

    [Fact]
    public async Task The_busiest_ticket_loads_as_a_graph()
    {
        await using var db = NewContext();
        var busiest = await Sql.ScalarAsync<int>(seeded.ConnectionString!,
            "SELECT TOP 1 TicketId FROM dbo.TicketEvents GROUP BY TicketId ORDER BY COUNT(*) DESC, TicketId");
        var ticket = await db.Tickets.AsNoTracking().Where(t => t.TicketId == busiest)
            .Include(t => t.Project)
            .Include(t => t.TicketEvents).ThenInclude(e => e.TicketEventNotifications)
            .Include(t => t.TicketTags)
            .Include(t => t.TicketSubscribers)
            .AsSplitQuery()
            .SingleAsync(Ct);
        Assert.NotNull(ticket.Project);
        Assert.Equal(await Sql.ScalarAsync<int>(seeded.ConnectionString!, "SELECT COUNT(*) FROM dbo.TicketEvents WHERE TicketId = @id", ("@id", busiest)), ticket.TicketEvents.Count);
        Assert.Equal(await Sql.ScalarAsync<int>(seeded.ConnectionString!, "SELECT COUNT(*) FROM dbo.TicketEventNotifications WHERE TicketId = @id", ("@id", busiest)),
            ticket.TicketEvents.Sum(e => e.TicketEventNotifications.Count));
        Assert.Equal(await Sql.ScalarAsync<int>(seeded.ConnectionString!, "SELECT COUNT(*) FROM dbo.TicketSubscribers WHERE TicketId = @id", ("@id", busiest)), ticket.TicketSubscribers.Count);
    }

    [Fact]
    public async Task Users_per_role_through_EF_match_SQL()
    {
        await using var db = NewContext();
        var viaEf = await db.UserRoles.GroupBy(ur => ur.Role!.Name).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, Ct);
        var viaSql = (await Sql.RowsAsync(seeded.ConnectionString!,
                "SELECT r.Name, COUNT(*) FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId GROUP BY r.Name"))
            .ToDictionary(r => (string)r[0]!, r => (int)r[1]!);
        Assert.Equal(viaSql.OrderBy(x => x.Key), viaEf.OrderBy(x => x.Key));
        Assert.Equal(4, await db.Roles.CountAsync(r => r.Discriminator == TicketDeskRole.TicketDeskDiscriminator, Ct));
    }
}
