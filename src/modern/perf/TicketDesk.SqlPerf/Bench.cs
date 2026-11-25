using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TicketDesk.SqlPerf;

public sealed record BenchOptions(int WarmRuns, int ColdRuns, int TimeoutSeconds, string OutDir, IReadOnlySet<string>? Only);

/// <summary>One execution of a scenario (all its commands, in order, on one connection).</summary>
public sealed record RunResult(
    int Run, double ClientMs, long ServerElapsedMs, long ServerCpuMs, long CompileCpuMs, long CompileElapsedMs,
    long LogicalReads, long LobLogicalReads, long PhysicalReads, long ReadAheadReads, long Rows,
    IReadOnlyList<CommandStatistics> Commands);

public sealed record Summary(int N, double ClientMedianMs, double ClientP95Ms, double ServerElapsedMedianMs, double ServerElapsedP95Ms,
    double CpuMedianMs, double CpuP95Ms, double LogicalReadsMedian, double PhysicalReadsMedian, double ReadAheadMedian, double CompileCpuMedianMs);

public sealed record ModeResult(string Mode, Summary Summary, IReadOnlyList<RunResult> Runs);

public sealed record ScenarioResult(string Name, string Description, int Commands, IReadOnlyList<string> Plans, IReadOnlyList<ModeResult> Modes);

public sealed record BenchResult(string Set, string Database, string Started, string Finished, string Environment, BenchOptionsInfo Options, IReadOnlyList<ScenarioResult> Scenarios);

public sealed record BenchOptionsInfo(int WarmRuns, int ColdRuns, string ColdMeans, string WarmMeans, string Percentile);

/// <summary>
/// Replays captured commands with SqlClient (parameterized commands go out as sp_executesql RPCs, like EF Core's; stored
/// procedures as RPCs). Per scenario: one run with SET STATISTICS XML ON to save the actual plans (not timed), then warm
/// runs (after one discarded warm-up) and cold runs (CHECKPOINT + DBCC DROPCLEANBUFFERS before each; plan cache kept),
/// all with SET STATISTICS IO, TIME ON.
/// </summary>
public sealed class Bench(string connectionString, BenchOptions options)
{
    private const string ShowplanColumn = "Microsoft SQL Server 2005 XML Showplan";

    private readonly string _conn = new SqlConnectionStringBuilder(connectionString) { Pooling = false, ApplicationName = "td-sqlperf" }.ConnectionString;

    public async Task<BenchResult> RunAsync(CaseFile cases, string environment)
    {
        var started = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(options.OutDir);
        Directory.CreateDirectory(Path.Combine(options.OutDir, "plans"));
        Directory.CreateDirectory(Path.Combine(options.OutDir, "raw"));
        var results = new List<ScenarioResult>();
        foreach (var s in cases.Scenarios.Where(s => options.Only == null || options.Only.Contains(s.Name)))
        {
            Console.Error.WriteLine($"[{DateTimeOffset.UtcNow:HH:mm:ss}] {cases.Set}/{s.Name}: {s.Commands.Count} command(s)");
            var plans = await CapturePlansAsync(cases.Set, s);
            var raw = new StringBuilder();
            var modes = new List<ModeResult>();
            if (options.ColdRuns > 0) modes.Add(await RunModeAsync(s, "cold", options.ColdRuns, raw));
            if (options.WarmRuns > 0) modes.Add(await RunModeAsync(s, "warm", options.WarmRuns, raw));
            await File.WriteAllTextAsync(Path.Combine(options.OutDir, "raw", $"{cases.Set}.{s.Name}.txt"), raw.ToString());
            foreach (var m in modes)
                Console.Error.WriteLine($"    {m.Mode}: median {m.Summary.ServerElapsedMedianMs} ms server / {m.Summary.ClientMedianMs:F1} ms client, cpu {m.Summary.CpuMedianMs} ms, reads {m.Summary.LogicalReadsMedian}");
            results.Add(new ScenarioResult(s.Name, s.Description, s.Commands.Count, plans, modes));
        }
        var result = new BenchResult(cases.Set, cases.Database, started.ToString("O"), DateTimeOffset.UtcNow.ToString("O"), environment,
            new BenchOptionsInfo(options.WarmRuns, options.ColdRuns,
                "CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept)",
                "one discarded warm-up run, then the measured runs back to back",
                "median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20)"),
            results);
        await File.WriteAllTextAsync(Path.Combine(options.OutDir, $"results-{cases.Set}.json"), JsonSerializer.Serialize(result, CaseFile.Json) + "\n");
        return result;
    }

    private async Task<List<string>> CapturePlansAsync(string set, Scenario s)
    {
        var files = new List<string>();
        await using var c = new SqlConnection(_conn);
        await c.OpenAsync();
        await Exec(c, "SET STATISTICS XML ON;");
        for (var i = 0; i < s.Commands.Count; i++)
        {
            await using var cmd = s.Commands[i].ToSqlCommand(c, options.TimeoutSeconds);
            await using var reader = await cmd.ExecuteReaderAsync();
            var n = 0;
            do
            {
                if (reader.FieldCount == 1 && reader.GetName(0) == ShowplanColumn)
                {
                    while (await reader.ReadAsync())
                    {
                        var name = $"{set}.{s.Name}.cmd{i + 1}.stmt{++n}.sqlplan";
                        await File.WriteAllTextAsync(Path.Combine(options.OutDir, "plans", name), reader.GetString(0));
                        files.Add("plans/" + name);
                    }
                }
                else
                {
                    while (await reader.ReadAsync()) { }
                }
            } while (await reader.NextResultAsync());
        }
        return files;
    }

    private async Task<ModeResult> RunModeAsync(Scenario s, string mode, int runs, StringBuilder raw)
    {
        await using var c = new SqlConnection(_conn);
        var messages = new List<string>();
        c.InfoMessage += (_, e) => { foreach (SqlError err in e.Errors) messages.Add(err.Message); };
        await c.OpenAsync();
        await using var admin = new SqlConnection(_conn);
        await admin.OpenAsync();
        await Exec(c, "SET STATISTICS IO ON; SET STATISTICS TIME ON;");

        if (mode == "warm") await RunOnceAsync(c, s, messages, 0); // warm-up, discarded
        var results = new List<RunResult>();
        for (var run = 1; run <= runs; run++)
        {
            if (mode == "cold") await Exec(admin, "CHECKPOINT; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;");
            var r = await RunOnceAsync(c, s, messages, run);
            results.Add(r.Result);
            raw.Append("==== ").Append(mode).Append(" run ").Append(run).Append('\n');
            foreach (var (cmdIndex, msgs) in r.Messages)
            {
                raw.Append("-- command ").Append(cmdIndex + 1).Append('\n');
                foreach (var m in msgs) raw.Append(m).Append('\n');
            }
        }
        return new ModeResult(mode, Summarize(results), results);
    }

    private async Task<(RunResult Result, List<(int, List<string>)> Messages)> RunOnceAsync(SqlConnection c, Scenario s, List<string> messages, int run)
    {
        var stats = new List<CommandStatistics>();
        var perCommandMessages = new List<(int, List<string>)>();
        double clientMs = 0;
        long rows = 0;
        for (var i = 0; i < s.Commands.Count; i++)
        {
            messages.Clear();
            await using var cmd = s.Commands[i].ToSqlCommand(c, options.TimeoutSeconds);
            var sw = Stopwatch.StartNew();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                var values = new object[256];
                do
                {
                    while (await reader.ReadAsync())
                    {
                        if (values.Length < reader.FieldCount) values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        rows++;
                    }
                } while (await reader.NextResultAsync());
            }
            sw.Stop();
            clientMs += sw.Elapsed.TotalMilliseconds;
            var copy = messages.ToList();
            perCommandMessages.Add((i, copy));
            stats.Add(StatisticsParser.Parse(copy));
        }
        var result = new RunResult(run, Math.Round(clientMs, 3), stats.Sum(x => x.ExecElapsedMs), stats.Sum(x => x.ExecCpuMs),
            stats.Sum(x => x.CompileCpuMs), stats.Sum(x => x.CompileElapsedMs), stats.Sum(x => x.LogicalReads), stats.Sum(x => x.LobLogicalReads),
            stats.Sum(x => x.PhysicalReads), stats.Sum(x => x.ReadAheadReads), rows, stats);
        return (result, perCommandMessages);
    }

    public static Summary Summarize(IReadOnlyList<RunResult> runs)
    {
        double Median(IEnumerable<double> v) => Stats.Median(v.ToList());
        double P95(IEnumerable<double> v) => Stats.NearestRank(v.ToList(), 0.95);
        return new Summary(runs.Count,
            Math.Round(Median(runs.Select(r => r.ClientMs)), 3), Math.Round(P95(runs.Select(r => r.ClientMs)), 3),
            Median(runs.Select(r => (double)r.ServerElapsedMs)), P95(runs.Select(r => (double)r.ServerElapsedMs)),
            Median(runs.Select(r => (double)r.ServerCpuMs)), P95(runs.Select(r => (double)r.ServerCpuMs)),
            Median(runs.Select(r => (double)r.LogicalReads)), Median(runs.Select(r => (double)r.PhysicalReads)),
            Median(runs.Select(r => (double)r.ReadAheadReads)), Median(runs.Select(r => (double)r.CompileCpuMs)));
    }

    private static async Task Exec(SqlConnection c, string sql)
    {
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 600 };
        await cmd.ExecuteNonQueryAsync();
    }
}

public static class Stats
{
    public static double Median(List<double> v)
    {
        if (v.Count == 0) return double.NaN;
        v.Sort();
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    /// <summary>Nearest-rank percentile: the smallest value with at least p of the values at or below it.</summary>
    public static double NearestRank(List<double> v, double p)
    {
        if (v.Count == 0) return double.NaN;
        v.Sort();
        var rank = (int)Math.Ceiling(p * v.Count);
        return v[Math.Clamp(rank, 1, v.Count) - 1];
    }
}
