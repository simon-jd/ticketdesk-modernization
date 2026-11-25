using Microsoft.Data.SqlClient;

namespace TicketDesk.Seed.Tests;

/// <summary>
/// Creates a throw-away database on the server named by TD_SQL_TEST_CONN (e.g. the td-sql container), applies the
/// EF6-implied schema and drops the database afterwards. Without the variable the tests are skipped.
/// </summary>
public sealed class ScratchDatabase : IAsyncLifetime
{
    public static readonly string? ServerConn = Environment.GetEnvironmentVariable("TD_SQL_TEST_CONN");
    public string? Conn { get; private set; }
    private string? _name;

    public async ValueTask InitializeAsync()
    {
        if (ServerConn == null) return;
        _name = $"td_seed_test_{Guid.NewGuid():N}";
        Conn = new SqlConnectionStringBuilder(ServerConn) { InitialCatalog = _name }.ConnectionString;
        await SqlSeeder.EnsureDatabaseAsync(Conn);
        await new SqlSeeder(Conn).ApplyImpliedSchemaAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (ServerConn == null || _name == null) return;
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(new SqlConnectionStringBuilder(ServerConn) { InitialCatalog = "master" }.ConnectionString);
        await c.OpenAsync();
        await using var cmd = new SqlCommand($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}];", c);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var c = new SqlConnection(Conn);
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c);
        return await cmd.ExecuteScalarAsync();
    }
}

public class SqlSeederTests(ScratchDatabase db) : IClassFixture<ScratchDatabase>
{
    private const string Skip = "set TD_SQL_TEST_CONN to run SQL Server integration tests";

    private async Task<(SqlSeeder Seeder, Generator Gen)> Prepare()
    {
        var seeder = new SqlSeeder(db.Conn!);
        var cfg = await seeder.ReadConfigAsync(TestContext.Current.CancellationToken);
        var gen = new Generator(new SeedSettings
        {
            Scale = ScalePreset.XS, Seed = 7, UserPassword = "Seed-Test-Password-1", RoleIds = cfg.RoleIds, FirstProjectId = cfg.FirstProjectId,
            TicketTypes = cfg.TicketTypes, Categories = cfg.Categories, Priorities = cfg.Priorities, ChunkSize = 64,
        });
        return (seeder, gen);
    }

    [Fact]
    public async Task Implied_schema_passes_verification()
    {
        Assert.SkipWhen(ScratchDatabase.ServerConn == null, Skip);
        var issues = await new SqlSeeder(db.Conn!).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
    }

    [Fact]
    public async Task Seeding_twice_with_reset_gives_identical_content_and_trusted_keys()
    {
        Assert.SkipWhen(ScratchDatabase.ServerConn == null, Skip);
        var ct = TestContext.Current.CancellationToken;
        var (seeder, gen) = await Prepare();

        await seeder.ResetAsync("seed.ticketdesk.test", ct);
        var first = await seeder.LoadAsync(gen, TimeSpan.Zero, ct);
        var stats1 = await seeder.StatsAsync("seed.ticketdesk.test", ct);
        Assert.Equal(200, stats1.Single(s => s.Table == "[dbo].[Tickets]").Rows);
        Assert.Equal(24, stats1.Single(s => s.Table == "[dbo].[IdentityUsers]").Rows);
        Assert.Equal(first.RowsByTable["[dbo].[TicketEvents]"], stats1.Single(s => s.Table == "[dbo].[TicketEvents]").Rows);
        Assert.Equal(0, await seeder.UntrustedForeignKeysAsync(ct));

        await seeder.ResetAsync("seed.ticketdesk.test", ct);
        await seeder.LoadAsync(gen, TimeSpan.Zero, ct);
        var stats2 = await seeder.StatsAsync("seed.ticketdesk.test", ct);
        Assert.Equal(stats1, stats2);
    }

    [Fact]
    public async Task Reset_leaves_only_the_default_project_and_real_users()
    {
        Assert.SkipWhen(ScratchDatabase.ServerConn == null, Skip);
        var ct = TestContext.Current.CancellationToken;
        var (seeder, gen) = await Prepare();
        await db.ScalarAsync("""
            INSERT INTO dbo.IdentityUsers (Id, DisplayName, Email, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, UserName)
            VALUES ('real-admin', 'Real Admin', 'admin@lab.test', 0, 0, 0, 1, 0, 'admin@lab.test')
            """);
        await seeder.ResetAsync("seed.ticketdesk.test", ct);
        await seeder.LoadAsync(gen, TimeSpan.Zero, ct);
        await seeder.ResetAsync("seed.ticketdesk.test", ct);

        Assert.Equal(0, await db.ScalarAsync("SELECT COUNT(*) FROM dbo.Tickets"));
        Assert.Equal(0, await db.ScalarAsync("SELECT COUNT(*) FROM dbo.TicketEventNotifications"));
        Assert.Equal(1, await db.ScalarAsync("SELECT COUNT(*) FROM dbo.Projects"));
        Assert.Equal(1, await db.ScalarAsync("SELECT COUNT(*) FROM dbo.IdentityUsers"));
        Assert.Equal(4, await db.ScalarAsync("SELECT COUNT(*) FROM dbo.IdentityRoles"));
        Assert.Equal(1m, await db.ScalarAsync("SELECT IDENT_CURRENT('dbo.Projects')"));

        // the next ticket the app creates gets id 1 again
        await db.ScalarAsync("""
            INSERT INTO dbo.Tickets (TicketType, Category, Title, Details, IsHtml, CreatedBy, Owner, TicketStatus, CurrentStatusDate, CurrentStatusSetBy, LastUpdateBy, LastUpdateDate, AffectsCustomer)
            VALUES ('Problem', 'Hardware', 't', 'd', 0, 'real-admin', 'real-admin', 0, SYSDATETIMEOFFSET(), 'real-admin', 'real-admin', SYSDATETIMEOFFSET(), 0)
            """);
        Assert.Equal(1, await db.ScalarAsync("SELECT MAX(TicketId) FROM dbo.Tickets"));
        await db.ScalarAsync("DELETE FROM dbo.Tickets; DELETE FROM dbo.IdentityUsers WHERE Id = 'real-admin'");
    }

    [Fact]
    public async Task Verification_reports_columns_the_seeder_cannot_fill()
    {
        Assert.SkipWhen(ScratchDatabase.ServerConn == null, Skip);
        var ct = TestContext.Current.CancellationToken;
        await db.ScalarAsync("ALTER TABLE dbo.TicketTags ADD Weight int NOT NULL CONSTRAINT DF_Weight DEFAULT 0, Color nvarchar(10) NULL");
        try
        {
            var issues = await new SqlSeeder(db.Conn!).VerifyAsync(ct);
            Assert.Contains(issues, i => i.Severity == "info" && i.Column == "Weight");
            Assert.DoesNotContain(issues, i => i.Severity == "error");
            await db.ScalarAsync("ALTER TABLE dbo.TicketTags ADD Mandatory int NOT NULL CONSTRAINT DF_M DEFAULT 0; ALTER TABLE dbo.TicketTags DROP CONSTRAINT DF_M;");
            issues = await new SqlSeeder(db.Conn!).VerifyAsync(ct);
            Assert.Contains(issues, i => i.Severity == "error" && i.Column == "Mandatory");
        }
        finally
        {
            await db.ScalarAsync("""
                ALTER TABLE dbo.TicketTags DROP CONSTRAINT DF_Weight;
                ALTER TABLE dbo.TicketTags DROP COLUMN Weight, Color;
                IF COL_LENGTH('dbo.TicketTags', 'Mandatory') IS NOT NULL ALTER TABLE dbo.TicketTags DROP COLUMN Mandatory;
                """);
        }
    }
}
