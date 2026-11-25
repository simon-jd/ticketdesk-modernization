using Microsoft.Data.SqlClient;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>
/// A throw-away copy (BACKUP/RESTORE on the same server) of the restored legacy database (<c>TD_MODERN_SEEDED_DB</c>,
/// normally <c>TicketDesk</c> = the lab VM's <c>baseline-s</c> data, migrated with td-db, or a copy of it named
/// <c>TicketDesk_*</c>), so tests can replay golden journeys that write (list settings, project selection) and compare with
/// what the legacy app answered on the same data. The copy is brought to the app's schema, contract stage included
/// (td-db migrate --contract --legacy-retired), after <see cref="PrepareAsync"/>. Unavailable (tests skip) without
/// TD_SQL_TEST_CONN or when the seeded database is not the restored legacy one.
/// </summary>
public class BaselineDatabase : IAsyncLifetime
{
    public string? ConnectionString { get; private set; }
    private string? _name;
    private string? _backup;

    public async ValueTask InitializeAsync()
    {
        var source = await SqlTestEnvironment.SeededDatabaseAsync();
        var sourceName = SqlTestEnvironment.SeededDatabaseName;
        if (source == null || !IsBaselineName(sourceName)) return;
        _name = $"td_routes_{Guid.NewGuid():N}";
        await using var c = new SqlConnection(SqlTestEnvironment.ForDatabase("master"));
        await c.OpenAsync();
        var backupDir = (string)(await new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))", c).ExecuteScalarAsync())!;
        var dataDir = (string)(await new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))", c).ExecuteScalarAsync())!;
        _backup = backupDir.TrimEnd('/') + $"/{_name}.bak";
        await Exec(c, $"BACKUP DATABASE [{sourceName}] TO DISK = N'{_backup}' WITH COPY_ONLY, INIT");
        var moves = new List<string>();
        await using (var cmd = new SqlCommand($"RESTORE FILELISTONLY FROM DISK = N'{_backup}'", c))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            var i = 0;
            while (await r.ReadAsync())
                moves.Add($"MOVE N'{r.GetString(0)}' TO N'{dataDir.TrimEnd('/')}/{_name}_{i++}{(r.GetString(2) == "L" ? ".ldf" : ".mdf")}'");
        }
        await Exec(c, $"RESTORE DATABASE [{_name}] FROM DISK = N'{_backup}' WITH {string.Join(", ", moves)}");
        await PrepareAsync(SqlTestEnvironment.ForDatabase(_name));
        await new TicketDesk.Database.SchemaMigrator(SqlTestEnvironment.ForDatabase(_name), TextWriter.Null).MigrateAsync(includeContract: true);
        ConnectionString = SqlTestEnvironment.ForDatabase(_name);
    }

    /// <summary>The restored legacy database or a copy of it (e.g. TicketDesk_retire, the contract proof of Round 4).</summary>
    public static bool IsBaselineName(string name) =>
        name == "TicketDesk" || (name.StartsWith("TicketDesk_", StringComparison.Ordinal) && name[11..].All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'));

    /// <summary>Runs on the copy before it is handed out (e.g. migrations the shared database does not have yet).</summary>
    protected virtual Task PrepareAsync(string connectionString) => Task.CompletedTask;

    protected static async Task Exec(SqlConnection c, string sql)
    {
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 300 };
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_name == null) return;
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection(SqlTestEnvironment.ForDatabase("master"));
        await c.OpenAsync();
        await Exec(c, $"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        if (_backup != null)
        {
            try { await Exec(c, $"EXEC xp_delete_files N'{_backup}'"); } catch (SqlException) { /* the backup file stays in the container's backup folder */ }
        }
    }
}
