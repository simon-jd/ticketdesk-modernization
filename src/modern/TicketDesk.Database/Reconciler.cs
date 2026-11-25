using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace TicketDesk.Database;

public sealed record ColumnInfo(string Name, string Type, short MaxLength, byte Precision, byte Scale, bool Nullable, bool Computed, bool Identity)
{
    public bool IsRowVersion => Type == "timestamp";
    public string Signature => $"{Name}:{Type}({MaxLength},{Precision},{Scale}){(Nullable ? " null" : "")}{(Computed ? " computed" : "")}{(Identity ? " identity" : "")}";
}

public sealed record TableSide(long Rows, string Hash, double Seconds);

public sealed record TableReconciliation(
    string Table, TableSide? Source, TableSide? Target, IReadOnlyList<string> HashedColumns, IReadOnlyList<string> ExcludedColumns,
    IReadOnlyList<string> Problems)
{
    public bool Match => Problems.Count == 0;
}

public sealed record ReconciliationReport(string SourceDatabase, string TargetDatabase, DateTimeOffset Started, double Seconds, IReadOnlyList<TableReconciliation> Tables)
{
    public bool Match => Tables.All(t => t.Match);
}

/// <summary>
/// Per-table reconciliation of two databases: same tables, same columns, same row counts and the same content hash.
/// The content hash is a SHA-256 over the rows in primary-key order, each row reduced on the server to
/// SHA-256(length-prefixed text of every column), so only 32 bytes per row cross the network. rowversion columns are
/// excluded (an import assigns new values; that is expected, not a difference) and reported as excluded.
/// </summary>
public sealed class Reconciler(string sourceConnection, string targetConnection, int parallelism = 4)
{
    public IReadOnlySet<string> ExcludeTables { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public async Task<ReconciliationReport> RunAsync(CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var sourceTables = await TablesAsync(sourceConnection, ct);
        var targetTables = await TablesAsync(targetConnection, ct);
        var names = sourceTables.Keys.Union(targetTables.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(n => !ExcludeTables.Contains(n)).Order(StringComparer.OrdinalIgnoreCase).ToList();

        var results = new TableReconciliation[names.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, names.Count), new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            async (i, token) =>
            {
                var name = names[i];
                sourceTables.TryGetValue(name, out var sCols);
                targetTables.TryGetValue(name, out var tCols);
                results[i] = await CompareTableAsync(name, sCols, tCols, token);
            });
        return new ReconciliationReport(Database(sourceConnection), Database(targetConnection), started, Math.Round(sw.Elapsed.TotalSeconds, 2), results);
    }

    private async Task<TableReconciliation> CompareTableAsync(string table, IReadOnlyList<ColumnInfo>? s, IReadOnlyList<ColumnInfo>? t, CancellationToken ct)
    {
        var problems = new List<string>();
        if (s == null || t == null)
        {
            problems.Add(s == null ? "table missing in source" : "table missing in target");
            var side = s ?? t!;
            var conn = s != null ? sourceConnection : targetConnection;
            var only = await HashAsync(conn, table, side.Where(c => !c.IsRowVersion).ToList(), ct);
            return new TableReconciliation(table, s != null ? only : null, t != null ? only : null, [], [], problems);
        }

        var sSig = s.Select(c => c.Signature).ToList();
        var tSig = t.Select(c => c.Signature).ToList();
        foreach (var missing in sSig.Except(tSig)) problems.Add($"column differs or missing in target: {missing}");
        foreach (var extra in tSig.Except(sSig)) problems.Add($"column differs or missing in source: {extra}");

        // hash the columns both sides have with the same name, in source column order
        var common = s.Where(c => !c.IsRowVersion && t.Any(x => x.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase) && !x.IsRowVersion)).ToList();
        var excluded = s.Concat(t).Where(c => c.IsRowVersion).Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var src = HashAsync(sourceConnection, table, common, ct);
        var dst = HashAsync(targetConnection, table, common, ct);
        var (sr, tr) = (await src, await dst);
        if (sr.Rows != tr.Rows) problems.Add($"row count {sr.Rows:N0} in source, {tr.Rows:N0} in target");
        else if (sr.Hash != tr.Hash) problems.Add("content hash differs");
        return new TableReconciliation(table, sr, tr, common.Select(c => c.Name).ToList(), excluded, problems);
    }

    internal static string RowHashExpression(IReadOnlyList<ColumnInfo> columns)
    {
        if (columns.Count == 0) return "0x";
        var parts = columns.Select(c =>
        {
            var col = $"[{c.Name.Replace("]", "]]")}]";
            var text = c.Type switch
            {
                "binary" or "varbinary" or "image" => $"CONVERT(nvarchar(max), CONVERT(varbinary(max), {col}), 1)",
                "float" or "real" => $"CONVERT(nvarchar(max), {col}, 3)",
                "money" or "smallmoney" => $"CONVERT(nvarchar(max), {col}, 2)",
                "datetime" or "smalldatetime" or "datetime2" or "date" or "time" => $"CONVERT(nvarchar(max), {col}, 121)",
                // style 121 keeps the offset, so +00:00 and -08:00 for the same instant are different values
                "datetimeoffset" => $"CONVERT(nvarchar(max), {col}, 121)",
                "text" or "ntext" => $"CAST({col} AS nvarchar(max))",
                "xml" or "sql_variant" or "hierarchyid" or "geography" or "geometry" => $"CONVERT(nvarchar(max), {col})",
                _ => $"CONVERT(nvarchar(max), {col})",
            };
            // length prefix makes the encoding unambiguous; NULL is a distinct token
            return $"ISNULL(CONCAT(DATALENGTH({text}), N':', {text}), N'~')";
        });
        return $"HASHBYTES('SHA2_256', CONCAT({string.Join(", N'|', ", parts)}, N''))";
    }

    private static async Task<TableSide> HashAsync(string conn, string table, IReadOnlyList<ColumnInfo> columns, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await using var c = new SqlConnection(conn);
        await c.OpenAsync(ct);
        var order = await KeyColumnsAsync(c, table, ct);
        if (order.Count == 0) order = columns.Select(x => $"[{x.Name.Replace("]", "]]")}]").ToList();
        var sql = $"SELECT {RowHashExpression(columns)} FROM {QuoteTable(table)} ORDER BY {string.Join(", ", order)}";
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 0 };
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long rows = 0;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            var buffer = new byte[32];
            while (await r.ReadAsync(ct))
            {
                var n = r.IsDBNull(0) ? 0 : (int)r.GetBytes(0, 0, buffer, 0, buffer.Length);
                sha.AppendData(buffer, 0, n);
                rows++;
            }
        }
        return new TableSide(rows, Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant(), Math.Round(sw.Elapsed.TotalSeconds, 2));
    }

    private static async Task<List<string>> KeyColumnsAsync(SqlConnection c, string table, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("""
            SELECT QUOTENAME(col.name) FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(@t) AND i.is_primary_key = 1 ORDER BY ic.key_ordinal
            """, c);
        cmd.Parameters.AddWithValue("@t", table);
        var keys = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) keys.Add(r.GetString(0));
        return keys;
    }

    private static async Task<Dictionary<string, IReadOnlyList<ColumnInfo>>> TablesAsync(string conn, CancellationToken ct)
    {
        await using var c = new SqlConnection(conn);
        await c.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            SELECT s.name + '.' + t.name, col.name, ty.name, col.max_length, col.precision, col.scale, col.is_nullable, col.is_computed, col.is_identity
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.columns col ON col.object_id = t.object_id
            JOIN sys.types ty ON ty.user_type_id = col.user_type_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, col.column_id
            """, c);
        var tables = new Dictionary<string, IReadOnlyList<ColumnInfo>>(StringComparer.OrdinalIgnoreCase);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var name = r.GetString(0);
            if (!tables.TryGetValue(name, out var list)) tables[name] = list = new List<ColumnInfo>();
            ((List<ColumnInfo>)list).Add(new ColumnInfo(r.GetString(1), r.GetString(2), r.GetInt16(3), r.GetByte(4), r.GetByte(5), r.GetBoolean(6), r.GetBoolean(7), r.GetBoolean(8)));
        }
        return tables;
    }

    private static string QuoteTable(string table)
    {
        var dot = table.IndexOf('.');
        return $"[{table[..dot].Replace("]", "]]")}].[{table[(dot + 1)..].Replace("]", "]]")}]";
    }

    private static string Database(string conn) => new SqlConnectionStringBuilder(conn).InitialCatalog;

    public static string Format(ReconciliationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"reconcile {report.SourceDatabase} -> {report.TargetDatabase}: {(report.Match ? "MATCH" : "MISMATCH")} ({report.Tables.Count} tables, {report.Seconds:F2} s)");
        sb.AppendLine($"{"table",-38} {"source rows",14} {"target rows",14}  {"sha256 (first 16)",-17} result");
        foreach (var t in report.Tables)
        {
            var hash = t.Source?.Hash ?? t.Target?.Hash ?? "";
            sb.Append($"{t.Table,-38} {t.Source?.Rows.ToString("N0") ?? "-",14} {t.Target?.Rows.ToString("N0") ?? "-",14}  {hash[..Math.Min(16, hash.Length)],-17} ");
            sb.AppendLine(t.Match ? "ok" : "MISMATCH: " + string.Join("; ", t.Problems));
            if (t.ExcludedColumns.Count > 0) sb.AppendLine($"{"",38}   excluded (rowversion): {string.Join(", ", t.ExcludedColumns)}");
        }
        return sb.ToString();
    }
}
