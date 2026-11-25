using Microsoft.Data.SqlClient;
using TicketDesk.Database;
using TicketDesk.Seed;

namespace TicketDesk.Testing;

/// <summary>
/// Throw-away database on the TD_SQL_TEST_CONN server: EF6-implied TicketDesk schema (tools/Seed), deterministic
/// synthetic data at scale XS (200 tickets, 24 users, 3 projects; seed 7), then the new app's migrations and procedures
/// including the contract stage (td-db migrate --contract --legacy-retired, as on every database the new app runs on since
/// the legacy app is retired), dropped afterwards. Use as an xUnit fixture.
/// </summary>
public class ScratchDatabase : IAsyncLifetime
{
    /// <summary>Test-only password of the synthetic users in the scratch database (hashed by the seeder in Identity v2 format).</summary>
    public const string UserPassword = "Scratch-Seed-Password-1";

    private string? _name;

    public string? ConnectionString { get; private set; }
    public Generator? Generator { get; private set; }
    public bool Available => ConnectionString != null;

    /// <summary>False for a database with the legacy schema only (migration tests start from there).</summary>
    protected virtual bool ApplyModernSchema => true;

    /// <summary>False for the expand stage only (the tests of the expand scripts and of the legacy writes they served).</summary>
    protected virtual bool ApplyContract => true;

    /// <summary>False for an empty installation: the schema and its stock rows (roles, the default project), no users or tickets.</summary>
    protected virtual bool LoadData => true;

    public IReadOnlyList<SeedUser> Users => Generator?.Users ?? [];

    public SeedUser UserInRole(string role, int index = 0) => Users.Where(u => u.Role == role).ElementAt(index);

    public virtual async ValueTask InitializeAsync()
    {
        if (SqlTestEnvironment.ServerConnection == null) return;
        _name = $"td_modern_test_{Guid.NewGuid():N}";
        var conn = SqlTestEnvironment.ForDatabase(_name);
        await SqlSeeder.EnsureDatabaseAsync(conn);
        var seeder = new SqlSeeder(conn);
        await seeder.ApplyImpliedSchemaAsync();
        if (!LoadData)
        {
            if (ApplyModernSchema) await new SchemaMigrator(conn, TextWriter.Null).MigrateAsync(includeContract: ApplyContract);
            ConnectionString = conn;
            return;
        }
        var cfg = await seeder.ReadConfigAsync();
        Generator = new Generator(new SeedSettings
        {
            Scale = ScalePreset.XS, Seed = 7, UserPassword = UserPassword, RoleIds = cfg.RoleIds, FirstProjectId = cfg.FirstProjectId,
            TicketTypes = cfg.TicketTypes, Categories = cfg.Categories, Priorities = cfg.Priorities, ChunkSize = 64,
        });
        await seeder.ResetAsync("seed.ticketdesk.test");
        await seeder.LoadAsync(Generator, TimeSpan.Zero);
        if (ApplyModernSchema) await new SchemaMigrator(conn, TextWriter.Null).MigrateAsync(includeContract: ApplyContract);
        ConnectionString = conn;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_name == null) return;
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(SqlTestEnvironment.ForDatabase("master"));
        await c.OpenAsync();
        await using var cmd = new SqlCommand($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END", c);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters) =>
        Sql.ScalarAsync<T>(ConnectionString!, sql, parameters);

    public Task<List<object?[]>> RowsAsync(string sql, params (string Name, object? Value)[] parameters) =>
        Sql.RowsAsync(ConnectionString!, sql, parameters);
}

public static class Sql
{
    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        await using var cmd = Command(c, sql, parameters);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull or null ? default! : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public static async Task<List<object?[]>> RowsAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        await using var cmd = Command(c, sql, parameters);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            reader.GetValues(row!);
            for (var i = 0; i < row.Length; i++) if (row[i] is DBNull) row[i] = null;
            rows.Add(row);
        }
        return rows;
    }

    private static SqlCommand Command(SqlConnection c, string sql, (string Name, object? Value)[] parameters)
    {
        var cmd = new SqlCommand(sql, c);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
}

/// <summary>
/// An empty installation as the retired first-run setup left it to the operator: td-seed schema (the EF6-implied schema with
/// the stock roles and the default project) and td-db migrate --contract --legacy-retired; no users, no tickets.
/// </summary>
public class EmptyInstallDatabase : ScratchDatabase
{
    protected override bool LoadData => false;
}
