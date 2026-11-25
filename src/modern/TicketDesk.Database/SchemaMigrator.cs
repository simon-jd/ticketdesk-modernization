using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace TicketDesk.Database;

/// <summary>
/// V = versioned expand script (applied once, never edited afterwards), R = repeatable script for programmable objects
/// (procedures; re-applied whenever its text changes), C = contract script (applied only with the contract stage,
/// after the legacy app is retired).
/// </summary>
public enum ScriptKind { Versioned = 'V', Repeatable = 'R', Contract = 'C' }

public sealed record MigrationScript(string Name, ScriptKind Kind, int Order, string Sql, string Checksum, bool UseTransaction)
{
    /// <summary>Batches split on lines that contain only <c>GO</c>, like sqlcmd.</summary>
    public IReadOnlyList<string> Batches => GoSeparator().Split(Sql).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray();

    private static Regex GoSeparator() => new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
}

public sealed record AppliedScript(string Name, ScriptKind Kind, string Checksum, DateTimeOffset AppliedAt, string AppliedBy, int DurationMs);

public sealed record MigrationPlan(IReadOnlyList<AppliedScript> Applied, IReadOnlyList<MigrationScript> Pending, IReadOnlyList<MigrationScript> ContractPending, IReadOnlyList<string> Drift);

public static partial class ScriptCatalog
{
    [GeneratedRegex(@"^(?<dir>expand|programmable|contract)/(?<kind>[VRC])(?<num>\d{4})?__(?<name>[a-z0-9_]+)\.sql$")]
    private static partial Regex NamePattern();

    /// <summary>The scripts embedded in this assembly (Migrations/expand, Migrations/programmable, Migrations/contract).</summary>
    public static IReadOnlyList<MigrationScript> Load()
    {
        var asm = typeof(ScriptCatalog).Assembly;
        var scripts = new List<MigrationScript>();
        foreach (var resource in asm.GetManifestResourceNames().Where(n => n.StartsWith("Migrations", StringComparison.Ordinal)))
        {
            var name = resource.Replace('\\', '/')["Migrations/".Length..];
            var m = NamePattern().Match(name);
            if (!m.Success) throw new InvalidOperationException($"migration script '{name}' does not follow <expand|programmable|contract>/<V|R|C>NNNN__name.sql");
            var kind = (ScriptKind)m.Groups["kind"].Value[0];
            var expectedDir = kind switch { ScriptKind.Versioned => "expand", ScriptKind.Repeatable => "programmable", _ => "contract" };
            if (m.Groups["dir"].Value != expectedDir) throw new InvalidOperationException($"'{name}': {kind} scripts belong in {expectedDir}/");
            if (kind != ScriptKind.Repeatable && !m.Groups["num"].Success) throw new InvalidOperationException($"'{name}': versioned scripts need a 4-digit number");
            using var stream = asm.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd().ReplaceLineEndings("\n");
            scripts.Add(new MigrationScript(name, kind, m.Groups["num"].Success ? int.Parse(m.Groups["num"].Value) : 0, sql,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant(),
                !sql.Contains("-- td-db: no-transaction", StringComparison.Ordinal)));
        }
        var dup = scripts.Where(s => s.Kind != ScriptKind.Repeatable).GroupBy(s => (s.Kind, s.Order)).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidOperationException($"duplicate {dup.Key.Kind} script number {dup.Key.Order:D4}");
        return scripts.OrderBy(s => s.Kind switch { ScriptKind.Versioned => 0, ScriptKind.Repeatable => 1, _ => 2 })
            .ThenBy(s => s.Order).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Applies the scripts to the shared database. The legacy EF6 app never sees this journal (EF6 only compares its own
/// <c>__MigrationHistory</c> model hash), and the new app never migrates at startup: schema changes are a deploy step.
/// DDL runs with a short LOCK_TIMEOUT and is retried, so a schema change waits behind live traffic for seconds at most
/// instead of queueing every request behind its schema-modification lock.
/// </summary>
public sealed class SchemaMigrator(string connectionString, TextWriter log, int lockTimeoutMs = 10_000, int lockRetries = 3)
{
    public const string JournalTable = "dbo.__ModernSchemaHistory";

    private const string CreateJournal = """
        IF OBJECT_ID(N'dbo.__ModernSchemaHistory', N'U') IS NULL
        CREATE TABLE dbo.__ModernSchemaHistory (
            ScriptName nvarchar(200) NOT NULL CONSTRAINT PK___ModernSchemaHistory PRIMARY KEY,
            Kind char(1) NOT NULL,
            Checksum char(64) NOT NULL,
            AppliedAt datetimeoffset(0) NOT NULL CONSTRAINT DF___ModernSchemaHistory_AppliedAt DEFAULT SYSDATETIMEOFFSET(),
            AppliedBy nvarchar(128) NOT NULL CONSTRAINT DF___ModernSchemaHistory_AppliedBy DEFAULT SUSER_SNAME(),
            DurationMs int NOT NULL);
        """;

    public IReadOnlyList<MigrationScript> Scripts { get; init; } = ScriptCatalog.Load();

    public async Task<IReadOnlyList<AppliedScript>> ReadJournalAsync(CancellationToken ct = default)
    {
        await using var c = await OpenAsync(ct);
        await using var exists = new SqlCommand("SELECT OBJECT_ID(N'dbo.__ModernSchemaHistory', N'U')", c);
        if (await exists.ExecuteScalarAsync(ct) is DBNull or null) return [];
        await using var cmd = new SqlCommand("SELECT ScriptName, Kind, Checksum, AppliedAt, AppliedBy, DurationMs FROM dbo.__ModernSchemaHistory ORDER BY AppliedAt, ScriptName", c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AppliedScript>();
        while (await r.ReadAsync(ct))
            list.Add(new AppliedScript(r.GetString(0), (ScriptKind)r.GetString(1)[0], r.GetString(2), r.GetDateTimeOffset(3), r.GetString(4), r.GetInt32(5)));
        return list;
    }

    public async Task<MigrationPlan> PlanAsync(CancellationToken ct = default)
    {
        var applied = await ReadJournalAsync(ct);
        var byName = applied.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var drift = new List<string>();
        var pending = new List<MigrationScript>();
        var contract = new List<MigrationScript>();
        foreach (var s in Scripts)
        {
            byName.TryGetValue(s.Name, out var a);
            if (s.Kind == ScriptKind.Repeatable)
            {
                if (a == null || a.Checksum != s.Checksum) pending.Add(s);
            }
            else if (a != null)
            {
                if (a.Checksum != s.Checksum) drift.Add($"{s.Name} was applied with checksum {a.Checksum[..12]}… but the script now hashes to {s.Checksum[..12]}… (applied scripts must not be edited; add a new one)");
            }
            else if (s.Kind == ScriptKind.Versioned) pending.Add(s);
            else contract.Add(s);
        }
        foreach (var a in applied.Where(a => Scripts.All(s => s.Name != a.Name)))
            drift.Add($"{a.Name} is in the journal but not in this build (applied by a newer version?)");
        return new MigrationPlan(applied, pending, contract, drift);
    }

    /// <summary>Applies pending expand (V) and programmable (R) scripts; contract (C) scripts only when <paramref name="includeContract"/>.</summary>
    public async Task<IReadOnlyList<string>> MigrateAsync(bool includeContract = false, CancellationToken ct = default)
    {
        await using (var c = await OpenAsync(ct))
        await using (var cmd = new SqlCommand(CreateJournal, c))
            await cmd.ExecuteNonQueryAsync(ct);

        var plan = await PlanAsync(ct);
        if (plan.Drift.Count > 0) throw new InvalidOperationException("schema journal drift:\n  " + string.Join("\n  ", plan.Drift));
        var todo = plan.Pending.Concat(includeContract ? plan.ContractPending : []).ToList();
        foreach (var s in todo) await ApplyAsync(s, ct);
        if (todo.Count == 0) log.WriteLine("nothing to apply");
        return todo.Select(s => s.Name).ToList();
    }

    private async Task ApplyAsync(MigrationScript s, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await using var c = await OpenAsync(ct);
                c.InfoMessage += (_, e) => { foreach (SqlError m in e.Errors) log.WriteLine($"    {m.Message}"); };
                await using (var setup = new SqlCommand($"SET XACT_ABORT ON; SET LOCK_TIMEOUT {lockTimeoutMs};", c))
                    await setup.ExecuteNonQueryAsync(ct);
                await using var tx = s.UseTransaction ? (SqlTransaction)await c.BeginTransactionAsync(ct) : null;
                foreach (var batch in s.Batches)
                {
                    await using var cmd = new SqlCommand(batch, c, tx) { CommandTimeout = 0 };
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await using (var journal = new SqlCommand("""
                    MERGE dbo.__ModernSchemaHistory AS h
                    USING (SELECT @name AS ScriptName) AS s ON h.ScriptName = s.ScriptName
                    WHEN MATCHED THEN UPDATE SET Checksum = @checksum, AppliedAt = SYSDATETIMEOFFSET(), AppliedBy = SUSER_SNAME(), DurationMs = @ms
                    WHEN NOT MATCHED THEN INSERT (ScriptName, Kind, Checksum, DurationMs) VALUES (@name, @kind, @checksum, @ms);
                    """, c, tx))
                {
                    journal.Parameters.AddWithValue("@name", s.Name);
                    journal.Parameters.AddWithValue("@kind", ((char)s.Kind).ToString());
                    journal.Parameters.AddWithValue("@checksum", s.Checksum);
                    journal.Parameters.AddWithValue("@ms", (int)sw.ElapsedMilliseconds);
                    await journal.ExecuteNonQueryAsync(ct);
                }
                if (tx != null) await tx.CommitAsync(ct);
                log.WriteLine($"applied {s.Name} in {sw.Elapsed.TotalSeconds:F2} s");
                return;
            }
            catch (SqlException e) when (e.Number == 1222 && attempt <= lockRetries)
            {
                log.WriteLine($"{s.Name}: lock timeout after {lockTimeoutMs} ms (attempt {attempt}); retrying");
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new SqlConnection(connectionString);
        await c.OpenAsync(ct);
        return c;
    }
}
