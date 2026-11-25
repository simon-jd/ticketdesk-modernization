using System.Globalization;
using System.Text;
using System.Text.Json;
using TicketDesk.SqlPerf;

// td-sqlperf: connection string from TD_PERF_CONN (never on the command line).
//   capture --set naive|tuned --out cases.json [--as-of 2026-10-31T17:00:00Z] [--source "git rev / note"]
//   bench   --cases cases.json --out DIR [--warm 20] [--cold 5] [--only a,b] [--timeout 600]
//   compare --before results-a.json --after results-b.json --out FILE.md [--title T]
//   plans   --dir DIR/plans --out plans.txt
//   verify  [--as-of ...]          naive LINQ vs tuned procedures on the same database, exit 1 on any difference
var a = Args.Parse(args);
var conn = Environment.GetEnvironmentVariable("TD_PERF_CONN");
switch (a.Command)
{
    case "capture":
    {
        Require(conn);
        var asOf = a.Get("as-of") is { } s ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture) : Scenarios.DefaultAsOf;
        var file = await Scenarios.CaptureAsync(conn!, a.Need("set"), asOf, a.Get("source") ?? "");
        file.Save(a.Need("out"));
        return 0;
    }
    case "bench":
    {
        Require(conn);
        var cases = CaseFile.Load(a.Need("cases"));
        var only = a.Get("only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var opts = new BenchOptions(a.Int("warm", 20), a.Int("cold", 5), a.Int("timeout", 600), a.Need("out"), only);
        var result = await new Bench(conn!, opts).RunAsync(cases, Environment.GetEnvironmentVariable("TD_PERF_ENV") ?? "");
        await File.WriteAllTextAsync(Path.Combine(opts.OutDir, $"summary-{cases.Set}.md"), Markdown.Summary(result));
        return 0;
    }
    case "compare":
    {
        var before = JsonSerializer.Deserialize<BenchResult>(File.ReadAllText(a.Need("before")), CaseFile.Json)!;
        var after = JsonSerializer.Deserialize<BenchResult>(File.ReadAllText(a.Need("after")), CaseFile.Json)!;
        await File.WriteAllTextAsync(a.Need("out"), Markdown.Compare(before, after, a.Get("title") ?? $"{before.Set} vs {after.Set}"));
        return 0;
    }
    case "verify":
    {
        // naive LINQ vs tuned procedures on the same database: same results?
        Require(conn);
        var asOf = a.Get("as-of") is { } s ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture) : Scenarios.DefaultAsOf;
        return await Verify.RunAsync(conn!, asOf, Console.Out);
    }
    case "plans":
    {
        // renders every .sqlplan in DIR as an operator tree (actual rows, executions, logical reads, seek/where predicates)
        var dir = a.Need("dir");
        var sb = new StringBuilder();
        foreach (var file in Directory.GetFiles(dir, "*.sqlplan").Order(StringComparer.Ordinal))
            sb.Append("### ").Append(Path.GetFileName(file)).Append('\n').Append(PlanText.Render(File.ReadAllText(file))).Append('\n');
        await File.WriteAllTextAsync(a.Need("out"), sb.ToString());
        return 0;
    }
    default:
        Console.Error.WriteLine("usage: td-sqlperf capture|bench|compare|plans ... (see Program.cs header); connection string in TD_PERF_CONN");
        return 2;
}

static void Require(string? conn)
{
    if (string.IsNullOrWhiteSpace(conn)) throw new InvalidOperationException("TD_PERF_CONN is not set");
}

internal sealed class Args
{
    public string Command { get; private init; } = "";
    private readonly Dictionary<string, string> _opts = new(StringComparer.Ordinal);

    public static Args Parse(string[] args)
    {
        var a = new Args { Command = args.FirstOrDefault() ?? "" };
        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"unexpected argument '{args[i]}'");
            a._opts[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
        }
        return a;
    }

    public string? Get(string k) => _opts.GetValueOrDefault(k);
    public string Need(string k) => Get(k) ?? throw new ArgumentException($"--{k} is required");
    public int Int(string k, int d) => Get(k) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : d;
}

internal static class Markdown
{
    private static string F(double v) => double.IsNaN(v) ? "-" : v.ToString(v >= 100 ? "N0" : "0.##", CultureInfo.InvariantCulture);

    public static string Summary(BenchResult r)
    {
        var sb = new StringBuilder();
        sb.Append("# td-sqlperf: ").Append(r.Set).Append(" on ").Append(r.Database).Append("\n\n");
        sb.Append("Started ").Append(r.Started).Append(", finished ").Append(r.Finished).Append(".  \n");
        sb.Append("Cold: ").Append(r.Options.ColdMeans).Append(". Warm: ").Append(r.Options.WarmMeans).Append(". ").Append(r.Options.Percentile).Append(".  \n");
        sb.Append("Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.\n\n");
        if (!string.IsNullOrWhiteSpace(r.Environment)) sb.Append("```\n").Append(r.Environment.Trim()).Append("\n```\n\n");
        sb.Append("| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|\n");
        foreach (var s in r.Scenarios)
            foreach (var m in s.Modes)
            {
                var x = m.Summary;
                sb.Append($"| {s.Name} | {m.Mode} | {x.N} | {F(x.ServerElapsedMedianMs)} / {F(x.ServerElapsedP95Ms)} | {F(x.CpuMedianMs)} / {F(x.CpuP95Ms)} | {F(x.LogicalReadsMedian)} | {F(x.PhysicalReadsMedian + x.ReadAheadMedian)} | {F(x.ClientMedianMs)} / {F(x.ClientP95Ms)} | {F(x.CompileCpuMedianMs)} |\n");
            }
        sb.Append("\nScenarios:\n\n");
        foreach (var s in r.Scenarios) sb.Append("- `").Append(s.Name).Append("`: ").Append(s.Description).Append(" (").Append(s.Commands).Append(" command(s); plans: ").Append(string.Join(", ", s.Plans.Select(p => $"`{p}`"))).Append(")\n");
        return sb.ToString();
    }

    public static string Compare(BenchResult before, BenchResult after, string title)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(title).Append("\n\n");
        sb.Append($"Before: `{before.Set}` ({before.Started}); after: `{after.Set}` ({after.Started}). Median of the measured runs; p95 nearest rank.\n\n");
        sb.Append("| Scenario | Mode | Server elapsed ms before -> after (median, p95) | CPU ms before -> after (median) | Logical reads before -> after | Reads ratio |\n");
        sb.Append("|---|---|---|---|---|---|\n");
        foreach (var b in before.Scenarios)
        {
            var aS = after.Scenarios.FirstOrDefault(x => x.Name == b.Name);
            if (aS == null) continue;
            foreach (var bm in b.Modes)
            {
                var am = aS.Modes.FirstOrDefault(m => m.Mode == bm.Mode);
                if (am == null) continue;
                var x = bm.Summary;
                var y = am.Summary;
                var ratio = y.LogicalReadsMedian > 0 ? x.LogicalReadsMedian / y.LogicalReadsMedian : double.NaN;
                sb.Append($"| {b.Name} | {bm.Mode} | {F(x.ServerElapsedMedianMs)} ({F(x.ServerElapsedP95Ms)}) -> {F(y.ServerElapsedMedianMs)} ({F(y.ServerElapsedP95Ms)}) | {F(x.CpuMedianMs)} -> {F(y.CpuMedianMs)} | {F(x.LogicalReadsMedian)} -> {F(y.LogicalReadsMedian)} | {(double.IsNaN(ratio) ? "-" : ratio.ToString("0.#", CultureInfo.InvariantCulture) + "x")} |\n");
            }
        }
        return sb.ToString();
    }
}
