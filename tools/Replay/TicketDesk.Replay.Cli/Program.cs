using System.Text.Json;
using TicketDesk.Replay;

// td-replay — record scripted journeys, replay a recording against another target, compare two recordings.
return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Usage = """
        td-replay <command> [options]

          record   --target URL --out FILE.jsonl [--journeys all|a,b] [--journey-file FILE.json]
          replay   --from FILE.jsonl --target URL --out FILE.jsonl [--journeys a,b]
          compare  --baseline A.jsonl --candidate B.jsonl --out-dir DIR [--fail-on-mismatch]
                   [--ignore-selector CSS]... [--no-set-cookie] [--max-diffs N (listed per exchange in report.md; all are in report.json)]
          journeys list | journeys export --out FILE.json

        common:  --secrets-file FILE (KEY=VALUE lines; repeatable)   --secret-alias NAME=KEY (repeatable)
                 --routes routes.csv (group by route template)      --insecure (accept the lab TLS cert)
                 --run-id ID   --quiet

        Journeys sign in with {secret:TD_ADMIN_USER} / {secret:TD_ADMIN_PASSWORD}; map them to keys of your
        secrets file with --secret-alias TD_ADMIN_USER=SOME_KEY. Secret values are never written to recordings.
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Console.WriteLine(Usage); return args.Length == 0 ? 2 : 0; }
        var cmd = args[0];
        Args a;
        try { a = Args.Parse(args.Skip(cmd == "journeys" ? 2 : 1)); }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        try
        {
            return cmd switch
            {
                "record" => await RecordAsync(a, fromFile: false),
                "replay" => await RecordAsync(a, fromFile: true),
                "compare" => await CompareAsync(a),
                "journeys" => Journeys(args.Length > 1 ? args[1] : "list", a),
                _ => Fail($"unknown command '{cmd}'\n\n{Usage}"),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or FileNotFoundException or InvalidDataException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static int Fail(string msg) { Console.Error.WriteLine(msg); return 2; }

    private static SecretStore Secrets(Args a)
    {
        var store = SecretStore.FromSources(a.All("secrets-file"));
        foreach (var alias in a.All("secret-alias"))
        {
            var parts = alias.Split('=', 2);
            if (parts.Length != 2) throw new ArgumentException($"--secret-alias expects NAME=KEY, got '{alias}'");
            store.Set(parts[0], store.Get(parts[1]));
        }
        return store;
    }

    private static async Task<int> RecordAsync(Args a, bool fromFile)
    {
        var target = new Uri(a.Required("target").TrimEnd('/') + "/");
        var outFile = a.Required("out");
        IEnumerable<JourneySpec> journeys;
        if (fromFile)
        {
            var source = await ReplayJson.ReadJsonlAsync(a.Required("from"));
            var all = Recording.ToJourneys(source);
            var only = a.Optional("journeys");
            journeys = only is null or "all" ? all : all.Where(j => only.Split(',').Contains(j.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        else if (a.Optional("journey-file") is { } jf) journeys = ReplayJson.ReadJourneys(jf);
        else journeys = TicketDeskJourneys.Select(a.Optional("journeys"));

        var quiet = a.Flag("quiet");
        var runner = new JourneyRunner(new RunnerOptions
        {
            Target = target,
            RunId = a.Optional("run-id") ?? DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"),
            Secrets = Secrets(a),
            Routes = a.Optional("routes") is { } r ? RouteTable.FromCsv(r) : new RouteTable(),
            Insecure = a.Flag("insecure"),
            Log = quiet ? null : Console.WriteLine,
        });
        var recorded = new List<RecordedExchange>();
        foreach (var j in journeys)
        {
            if (!quiet) Console.WriteLine($"journey {j.Name}");
            recorded.AddRange(await runner.RunJourneyAsync(j));
        }
        await ReplayJson.WriteJsonlAsync(outFile, recorded);
        var errors = recorded.Count(x => x.Error != null);
        if (runner.ServerPageFetches > 0)
            Console.WriteLine($"server pages read behind Angular answers (not recorded): {runner.ServerPageFetches}");
        if (runner.PostedLinkSteps > 0)
            Console.WriteLine($"recorded GET steps sent as POST (the page offers them as a post now, S3): {runner.PostedLinkSteps}");
        Console.WriteLine($"{(fromFile ? "replayed" : "recorded")} {recorded.Count} exchanges in {recorded.Select(x => x.Journey).Distinct().Count()} journeys " +
                          $"against {target} -> {outFile} ({errors} journey errors)");
        return errors == 0 ? 0 : 1;
    }

    private static async Task<int> CompareAsync(Args a)
    {
        var baselineFile = a.Required("baseline");
        var candidateFile = a.Required("candidate");
        var options = new NormalizerOptions
        {
            IgnoreSelectors = a.All("ignore-selector").ToList(),
            CompareSetCookie = !a.Flag("no-set-cookie"),
        };
        var comparer = new ExchangeComparer(options);
        var report = comparer.Compare(await ReplayJson.ReadJsonlAsync(baselineFile), await ReplayJson.ReadJsonlAsync(candidateFile), baselineFile, candidateFile);
        var outDir = a.Required("out-dir");
        // --max-diffs: how many diffs of an exchange report.md lists; report.json keeps all of them (what the summary counts)
        await ReportWriter.WriteAsync(report, outDir, maxDiffsShown: int.TryParse(a.Optional("max-diffs"), out var md) ? md : 25);
        Console.WriteLine($"compared {report.Compared}, matched {report.Matched}, mismatched {report.Mismatched} " +
                          $"(missing {report.MissingInCandidate}, extra {report.ExtraInCandidate}) -> {Path.Combine(outDir, "report.md")}");
        return a.Flag("fail-on-mismatch") && report.Mismatched > 0 ? 1 : 0;
    }

    private static int Journeys(string sub, Args a)
    {
        switch (sub)
        {
            case "list":
                foreach (var j in TicketDeskJourneys.All) Console.WriteLine($"{j.Name,-20} {j.Steps.Count,3} steps  {j.Description}");
                return 0;
            case "export":
                File.WriteAllText(a.Required("out"), JsonSerializer.Serialize(TicketDeskJourneys.All, ReplayJson.Indented));
                Console.WriteLine($"wrote {TicketDeskJourneys.All.Count} journeys to {a.Required("out")}");
                return 0;
            default:
                return Fail($"unknown journeys subcommand '{sub}'");
        }
    }
}

/// <summary>Tiny --key value / --flag parser (repeatable keys allowed).</summary>
internal sealed class Args
{
    private static readonly HashSet<string> Flags = ["insecure", "quiet", "fail-on-mismatch", "no-set-cookie"];
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(IEnumerable<string> args)
    {
        var a = new Args();
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (!t.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"unexpected argument '{t}'");
            var key = t[2..];
            string value;
            var eq = key.IndexOf('=');
            if (eq > 0) { value = key[(eq + 1)..]; key = key[..eq]; }
            else if (Flags.Contains(key)) value = "true";
            else if (i + 1 < list.Count) value = list[++i];
            else throw new ArgumentException($"option --{key} needs a value");
            if (!a._values.TryGetValue(key, out var vs)) a._values[key] = vs = [];
            vs.Add(value);
        }
        return a;
    }

    public string Required(string key) => Optional(key) ?? throw new ArgumentException($"missing required option --{key}");
    public string? Optional(string key) => _values.TryGetValue(key, out var v) ? v[^1] : null;
    public IEnumerable<string> All(string key) => _values.TryGetValue(key, out var v) ? v : [];
    public bool Flag(string key) => _values.ContainsKey(key);
}
