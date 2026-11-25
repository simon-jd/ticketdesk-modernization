using Microsoft.Data.SqlClient;

namespace TicketDesk.Testing;

/// <summary>
/// SQL Server integration tests run against the server in TD_SQL_TEST_CONN (e.g. the td-sql container:
/// <c>TD_SQL_TEST_CONN="$(ops/dev/td-sql.sh conn master)"</c>) and are skipped without it, like tools/.
/// </summary>
public static class SqlTestEnvironment
{
    public const string SkipReason = "set TD_SQL_TEST_CONN to run SQL Server integration tests";

    public static string? ServerConnection => Environment.GetEnvironmentVariable("TD_SQL_TEST_CONN") is { Length: > 0 } c ? c : null;

    /// <summary>Database seeded by ops/dev/seed.sh (default TicketDeskSynth; the restored legacy TicketDesk DB once it exists).</summary>
    public static string SeededDatabaseName => Environment.GetEnvironmentVariable("TD_MODERN_SEEDED_DB") is { Length: > 0 } d ? d : "TicketDeskSynth";

    /// <summary>Password of the seeded synthetic users (from $TD_W/.secrets/td-seed.env); optional.</summary>
    public static string? SeedUserPassword => Environment.GetEnvironmentVariable("TD_SEED_USER_PASSWORD") is { Length: > 0 } p ? p : null;

    public static string ForDatabase(string database) =>
        new SqlConnectionStringBuilder(ServerConnection) { InitialCatalog = database }.ConnectionString;

    /// <summary>Connection string of the seeded database, or null when the server or the database is missing.</summary>
    public static async Task<string?> SeededDatabaseAsync()
    {
        if (ServerConnection == null) return null;
        await using var c = new SqlConnection(ForDatabase("master"));
        await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT DB_ID(@n)", c);
        cmd.Parameters.AddWithValue("@n", SeededDatabaseName);
        return await cmd.ExecuteScalarAsync() is DBNull or null ? null : ForDatabase(SeededDatabaseName);
    }
}
