using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TicketDesk.Seed;

public sealed record SchemaIssue(string Severity, string Table, string? Column, string Message);

public sealed record TableStat(string Table, long Rows, long Checksum);

public sealed record LoadResult(IReadOnlyDictionary<string, long> RowsByTable, TimeSpan Elapsed, TimeSpan ResetElapsed);

public sealed record TargetConfig(IReadOnlyDictionary<string, string> RoleIds, int FirstProjectId,
    IReadOnlyList<string> TicketTypes, IReadOnlyList<string> Categories, IReadOnlyList<string> Priorities);

/// <summary>Talks to SQL Server: schema check/creation, idempotent reset, SqlBulkCopy load, row counts and checksums.</summary>
public sealed class SqlSeeder(string connectionString, Action<string>? log = null)
{
    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new SqlConnection(connectionString);
        await c.OpenAsync(ct);
        return c;
    }

    private static SqlCommand Cmd(SqlConnection c, string sql, int timeout = 0) => new(sql, c) { CommandTimeout = timeout };

    public static async Task EnsureDatabaseAsync(string connectionString, CancellationToken ct = default)
    {
        var b = new SqlConnectionStringBuilder(connectionString);
        var db = b.InitialCatalog;
        if (string.IsNullOrEmpty(db)) throw new ArgumentException("connection string has no Database/Initial Catalog");
        b.InitialCatalog = "master";
        await using var c = new SqlConnection(b.ConnectionString);
        await c.OpenAsync(ct);
        await using var cmd = Cmd(c, $"""
            IF DB_ID(@db) IS NULL
            BEGIN
                EXEC('CREATE DATABASE [' + @db + ']');
                EXEC('ALTER DATABASE [' + @db + '] SET RECOVERY SIMPLE');
            END
            """);
        cmd.Parameters.AddWithValue("@db", db);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> CountUserTablesAsync(CancellationToken ct = default)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Cmd(c, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Creates the EF6-implied schema in an empty database (refuses when tables already exist).</summary>
    public async Task ApplyImpliedSchemaAsync(CancellationToken ct = default)
    {
        if (await CountUserTablesAsync(ct) > 0) throw new InvalidOperationException("database is not empty; refusing to apply the implied schema");
        await using var stream = typeof(SqlSeeder).Assembly.GetManifestResourceStream("TicketDesk.Seed.Sql.schema-ef6-implied.sql")
                                 ?? throw new InvalidOperationException("embedded schema script missing");
        var script = await new StreamReader(stream).ReadToEndAsync(ct);
        await using var c = await OpenAsync(ct);
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = Cmd(c, batch);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Compares the seeder's table definitions with INFORMATION_SCHEMA. Errors block seeding.</summary>
    public async Task<List<SchemaIssue>> VerifyAsync(CancellationToken ct = default)
    {
        var issues = new List<SchemaIssue>();
        var cols = new Dictionary<string, Dictionary<string, (string Type, int? Len, bool Nullable, bool HasDefault, bool Identity, bool Computed)>>(StringComparer.OrdinalIgnoreCase);
        await using (var c = await OpenAsync(ct))
        await using (var cmd = Cmd(c, """
            SELECT s.name + '.' + t.name, col.name, ty.name, col.max_length, col.is_nullable,
                   CASE WHEN col.default_object_id <> 0 THEN 1 ELSE 0 END, col.is_identity, col.is_computed
            FROM sys.columns col
            JOIN sys.tables t ON t.object_id = col.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.types ty ON ty.user_type_id = col.user_type_id
            """))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var table = r.GetString(0);
                if (!cols.TryGetValue(table, out var map)) cols[table] = map = new(StringComparer.OrdinalIgnoreCase);
                var type = r.GetString(2);
                int? len = r.GetInt16(3) == -1 ? null : type.StartsWith('n') ? r.GetInt16(3) / 2 : r.GetInt16(3);
                map[r.GetString(1)] = (type, len, r.GetBoolean(4), r.GetInt32(5) == 1, r.GetBoolean(6), r.GetBoolean(7));
            }
        }

        foreach (var def in TicketDeskSchema.All)
        {
            var key = $"{def.Schema}.{def.Name}";
            if (!cols.TryGetValue(key, out var actual)) { issues.Add(new("error", key, null, "table missing")); continue; }
            foreach (var col in def.Columns)
            {
                if (!actual.TryGetValue(col.Name, out var a)) { issues.Add(new("error", key, col.Name, "column missing")); continue; }
                var expectedType = col.SqlType.Split('(')[0];
                if (!a.Type.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("error", key, col.Name, $"type {a.Type}, seeder writes {col.SqlType}"));
                else if (col.MaxLength is int len && a.Len is int alen && alen < len)
                    issues.Add(new("error", key, col.Name, $"length {alen} < {len} the seeder may write"));
                else if (col.MaxLength is int len2 && a.Len is int alen2 && alen2 != len2)
                    issues.Add(new("info", key, col.Name, $"length {alen2}, seeder assumes {len2}"));
                if (!col.Nullable && a.Nullable) issues.Add(new("info", key, col.Name, "nullable in the database, always written by the seeder"));
                if (col.Nullable && !a.Nullable) issues.Add(new("error", key, col.Name, "NOT NULL in the database but the seeder may write NULL"));
            }
            foreach (var (name, a) in actual)
            {
                if (def.Columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var autoFilled = a.Nullable || a.HasDefault || a.Identity || a.Computed || a.Type is "timestamp" or "rowversion";
                issues.Add(autoFilled
                    ? new("info", key, name, $"not written by the seeder ({a.Type}{(a.Nullable ? ", nullable" : "")}{(a.HasDefault ? ", has default" : "")})")
                    : new("error", key, name, $"NOT NULL {a.Type} without default is not written by the seeder"));
            }
        }
        foreach (var (table, columns) in TicketDeskSchema.ReadOnly)
        {
            if (!cols.TryGetValue(table, out var actual)) { issues.Add(new("error", table, null, "table missing (read by the seeder)")); continue; }
            foreach (var col in columns.Where(c => !actual.ContainsKey(c))) issues.Add(new("error", table, col, "column missing (read by the seeder)"));
        }
        return issues;
    }

    /// <summary>Reads role ids, the default project and the configured select lists from the target database.</summary>
    public async Task<TargetConfig> ReadConfigAsync(CancellationToken ct = default)
    {
        await using var c = await OpenAsync(ct);
        var roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = Cmd(c, "SELECT Name, Id FROM dbo.IdentityRoles"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) roles[r.GetString(0)] = r.GetString(1);
        var missing = TicketDeskSchema.StockRoles.Where(n => !roles.ContainsKey(n)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"stock roles missing in dbo.IdentityRoles: {string.Join(", ", missing)} (run the legacy first-run setup or apply the implied schema)");

        int firstProject;
        await using (var cmd = Cmd(c, "SELECT MIN(ProjectId) FROM dbo.Projects"))
        {
            var v = await cmd.ExecuteScalarAsync(ct);
            if (v is DBNull or null) throw new InvalidOperationException("dbo.Projects is empty (the Default project is created by migration Td2.5.017)");
            firstProject = (int)v;
        }

        IReadOnlyList<string> types = ["Problem", "Question", "Request"], cats = ["Software", "Hardware", "Network"], prios = ["High", "Medium", "Low"];
        await using (var cmd = Cmd(c, "SELECT SelectListSettingsJson FROM dbo.ApplicationSettings WHERE ApplicationName = N'TicketDesk'"))
        {
            if (await cmd.ExecuteScalarAsync(ct) is string json && json.Length > 0)
            {
                using var doc = JsonDocument.Parse(json);
                List<string>? Read(string name) => doc.RootElement.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Array
                    ? e.EnumerateArray().Select(x => x.GetString()!).Where(x => !string.IsNullOrEmpty(x)).ToList() : null;
                types = Read("TicketTypesList") is { Count: > 0 } t ? t : types;
                cats = Read("CategoryList") is { Count: > 0 } k ? k : cats;
                prios = Read("PriorityList") is { Count: > 0 } p ? p : prios;
            }
        }
        return new TargetConfig(roles, firstProject, types, cats, prios);
    }

    /// <summary>
    /// Removes all ticket data, the synthetic users (by e-mail domain) and every project except the first one, then resets
    /// identity counters. Running reset + seed twice yields the same database content.
    /// </summary>
    public async Task<TimeSpan> ResetAsync(string emailDomain, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var c = await OpenAsync(ct);
        var like = "%@" + emailDomain;
        string[] steps =
        [
            "TRUNCATE TABLE dbo.TicketEventNotifications",
            "TRUNCATE TABLE dbo.TicketTags",
            "WHILE 1 = 1 BEGIN DELETE TOP (200000) FROM dbo.TicketEvents; IF @@ROWCOUNT = 0 BREAK; END",
            "WHILE 1 = 1 BEGIN DELETE TOP (200000) FROM dbo.TicketSubscribers; IF @@ROWCOUNT = 0 BREAK; END",
            "WHILE 1 = 1 BEGIN DELETE TOP (100000) FROM dbo.Tickets; IF @@ROWCOUNT = 0 BREAK; END",
            "DELETE FROM dbo.UserSettings WHERE UserId IN (SELECT Id FROM dbo.IdentityUsers WHERE Email LIKE @like)",
            "DELETE FROM dbo.IdentityUserRoles WHERE UserId IN (SELECT Id FROM dbo.IdentityUsers WHERE Email LIKE @like)",
            "DELETE FROM dbo.IdentityUsers WHERE Email LIKE @like",
            "UPDATE dbo.UserSettings SET SelectedProjectId = 0 WHERE SelectedProjectId IS NOT NULL AND SelectedProjectId <> (SELECT MIN(ProjectId) FROM dbo.Projects)",
            "DELETE FROM dbo.Projects WHERE ProjectId <> (SELECT MIN(ProjectId) FROM dbo.Projects)",
            // RESEED 0 after rows existed makes the next value 1; skip tables whose identity was never used (next value would be 0).
            """
            DECLARE @t sysname;
            DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT v FROM (VALUES (N'dbo.Tickets'), (N'dbo.TicketEvents'), (N'dbo.TicketTags')) x(v);
            OPEN c; FETCH NEXT FROM c INTO @t;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID(@t) AND last_value IS NOT NULL)
                    EXEC (N'DBCC CHECKIDENT (''' + @t + N''', RESEED, 0) WITH NO_INFOMSGS');
                FETCH NEXT FROM c INTO @t;
            END
            DECLARE @maxp nvarchar(12) = CONVERT(nvarchar(12), (SELECT MAX(ProjectId) FROM dbo.Projects));
            EXEC (N'DBCC CHECKIDENT (''dbo.Projects'', RESEED, ' + @maxp + N') WITH NO_INFOMSGS');
            """,
        ];
        foreach (var sql in steps)
        {
            await using var cmd = Cmd(c, sql);
            if (sql.Contains("@like")) cmd.Parameters.AddWithValue("@like", like);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        log?.Invoke($"reset done in {sw.Elapsed.TotalSeconds:F1}s");
        return sw.Elapsed;
    }

    public async Task<LoadResult> LoadAsync(Generator g, TimeSpan resetElapsed, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var counts = TicketDeskSchema.All.ToDictionary(t => t.FullName, _ => 0L);
        await using var c = await OpenAsync(ct);

        async Task Bulk(TableDef def, DataTable table)
        {
            if (table.Rows.Count == 0) return;
            var options = SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepNulls | (def.KeepIdentity ? SqlBulkCopyOptions.KeepIdentity : 0);
            using var bulk = new SqlBulkCopy(c, options, null) { DestinationTableName = def.FullName, BulkCopyTimeout = 0, BatchSize = 0 };
            foreach (var col in def.Columns) bulk.ColumnMappings.Add(col.Name, col.Name);
            await bulk.WriteToServerAsync(table, ct);
            counts[def.FullName] += table.Rows.Count;
        }

        await Bulk(TicketDeskSchema.Projects, g.ProjectsTable());
        await Bulk(TicketDeskSchema.IdentityUsers, g.UsersTable());
        await Bulk(TicketDeskSchema.IdentityUserRoles, g.UserRolesTable());
        log?.Invoke($"projects/users loaded ({g.Users.Count} users) in {sw.Elapsed.TotalSeconds:F1}s");

        // generate the next chunk while the current one is being written
        using var chunks = g.TicketChunks().GetEnumerator();
        Task<SeedChunk?> Next() => Task.Run(() => chunks.MoveNext() ? chunks.Current : null, ct);
        var pending = Next();
        var done = 0;
        while (await pending is { } chunk)
        {
            pending = Next();
            foreach (var (def, table) in chunk.InLoadOrder()) await Bulk(def, table);
            done += chunk.Tickets.Rows.Count;
            if (done % 100_000 == 0 || done == g.Settings.Scale.Tickets) log?.Invoke($"  {done:N0} tickets loaded ({sw.Elapsed.TotalSeconds:F1}s)");
        }

        // bulk copy skipped FK checks; re-validate so the constraints stay trusted (the optimizer relies on that)
        foreach (var table in new[] { "dbo.Tickets", "dbo.TicketTags", "dbo.TicketSubscribers", "dbo.TicketEvents", "dbo.TicketEventNotifications", "dbo.IdentityUserRoles" })
        {
            await using var cmd = Cmd(c, $"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT ALL");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        log?.Invoke($"constraints re-validated ({sw.Elapsed.TotalSeconds:F1}s)");
        return new LoadResult(counts, sw.Elapsed, resetElapsed);
    }

    /// <summary>Row counts and CHECKSUM_AGG(BINARY_CHECKSUM(seeded columns)) per table; seeded users only for identity tables.</summary>
    public async Task<List<TableStat>> StatsAsync(string emailDomain, CancellationToken ct = default)
    {
        var stats = new List<TableStat>();
        await using var c = await OpenAsync(ct);
        foreach (var def in TicketDeskSchema.All)
        {
            var cols = string.Join(", ", def.Columns.Select(x => $"[{x.Name}]"));
            var where = def.Name switch
            {
                "IdentityUsers" => "WHERE Email LIKE @like",
                "IdentityUserRoles" => "WHERE UserId IN (SELECT Id FROM dbo.IdentityUsers WHERE Email LIKE @like)",
                _ => "",
            };
            await using var cmd = Cmd(c, $"SELECT COUNT_BIG(*), ISNULL(CHECKSUM_AGG(BINARY_CHECKSUM({cols})), 0) FROM {def.FullName} {where}");
            cmd.Parameters.AddWithValue("@like", "%@" + emailDomain);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            stats.Add(new TableStat(def.FullName, r.GetInt64(0), r.GetInt32(1)));
        }
        return stats;
    }

    public async Task<int> UntrustedForeignKeysAsync(CancellationToken ct = default)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Cmd(c, "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1");
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
