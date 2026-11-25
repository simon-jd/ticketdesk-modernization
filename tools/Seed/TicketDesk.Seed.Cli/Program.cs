using System.Diagnostics;
using System.Text.Json;
using TicketDesk.Seed;

// td-seed — deterministic synthetic data for the TicketDesk 2.5 schema.
return await SeedCli.RunAsync(args);

internal static class SeedCli
{
    private const string Usage = """
        td-seed <command> [options]

          verify                       compare the seeder's tables with the target schema (exit 1 on errors)
          schema [--create-database]   create the EF6-implied schema in an EMPTY database
          reset                        delete ticket data, synthetic users and extra projects; reset identities
          run --scale XS|S|M|L [--seed N] [--no-reset] [--json FILE]
                                       reset (unless --no-reset), bulk load, print row counts + checksums
          stats [--json FILE]          row counts and checksums of the seeded tables
          fingerprint --scale XS|S|M|L [--seed N]
                                       generate in memory only and print a SHA-256 of all rows (determinism check)

        connection:  TD_SQL_CONN environment variable (or --conn, which is visible in the process list)
        password:    TD_SEED_USER_PASSWORD (or --password-file FILE with TD_SEED_USER_PASSWORD=...) — the password
                     every synthetic user gets, stored as an ASP.NET Identity v2 hash
        """;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help") { Console.WriteLine(Usage); return args.Length == 0 ? 2 : 0; }
        var opts = ParseOptions(args.Skip(1).ToArray());
        if (opts == null) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            return args[0] switch
            {
                "verify" => await Verify(opts),
                "schema" => await Schema(opts),
                "reset" => await Reset(opts),
                "run" => await Run(opts),
                "stats" => await Stats(opts),
                "fingerprint" => Fingerprint(opts),
                _ => Bad($"unknown command '{args[0]}'"),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Bad(string msg) { Console.Error.WriteLine(msg + "\n\n" + Usage); return 2; }

    private static Dictionary<string, string>? ParseOptions(string[] a)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--", StringComparison.Ordinal)) return null;
            var key = a[i][2..];
            if (key is "no-reset" or "create-database") d[key] = "true";
            else if (i + 1 < a.Length) d[key] = a[++i];
            else return null;
        }
        return d;
    }

    private static string Conn(Dictionary<string, string> o) =>
        o.GetValueOrDefault("conn") ?? Environment.GetEnvironmentVariable("TD_SQL_CONN")
        ?? throw new ArgumentException("set TD_SQL_CONN (e.g. TD_SQL_CONN=\"$(ops/dev/td-sql.sh conn TicketDesk)\")");

    private static string Password(Dictionary<string, string> o)
    {
        if (o.TryGetValue("password-file", out var f))
        {
            foreach (var line in File.ReadAllLines(f))
                if (line.StartsWith("TD_SEED_USER_PASSWORD=", StringComparison.Ordinal)) return line["TD_SEED_USER_PASSWORD=".Length..].Trim();
            throw new ArgumentException($"{f} has no TD_SEED_USER_PASSWORD line");
        }
        return Environment.GetEnvironmentVariable("TD_SEED_USER_PASSWORD")
               ?? throw new ArgumentException("set TD_SEED_USER_PASSWORD or pass --password-file");
    }

    private static string Domain(Dictionary<string, string> o) => o.GetValueOrDefault("email-domain") ?? "seed.ticketdesk.test";

    private static async Task<int> Verify(Dictionary<string, string> o)
    {
        var issues = await new SqlSeeder(Conn(o)).VerifyAsync();
        foreach (var i in issues.OrderBy(i => i.Severity != "error").ThenBy(i => i.Table).ThenBy(i => i.Column))
            Console.WriteLine($"{i.Severity,-5} {i.Table}{(i.Column == null ? "" : "." + i.Column)}: {i.Message}");
        var errors = issues.Count(i => i.Severity == "error");
        Console.WriteLine($"schema check: {errors} error(s), {issues.Count - errors} note(s)");
        return errors == 0 ? 0 : 1;
    }

    private static async Task<int> Schema(Dictionary<string, string> o)
    {
        var conn = Conn(o);
        if (o.ContainsKey("create-database")) await SqlSeeder.EnsureDatabaseAsync(conn);
        await new SqlSeeder(conn).ApplyImpliedSchemaAsync();
        Console.WriteLine("EF6-implied schema created");
        return 0;
    }

    private static async Task<int> Reset(Dictionary<string, string> o)
    {
        var t = await new SqlSeeder(Conn(o), Console.WriteLine).ResetAsync(Domain(o));
        Console.WriteLine($"reset in {t.TotalSeconds:F1}s");
        return 0;
    }

    private static async Task<int> Run(Dictionary<string, string> o)
    {
        var scale = ScalePreset.Parse(o.GetValueOrDefault("scale") ?? throw new ArgumentException("--scale is required"));
        var seed = long.Parse(o.GetValueOrDefault("seed") ?? "42");
        var seeder = new SqlSeeder(Conn(o), Console.WriteLine);

        var issues = await seeder.VerifyAsync();
        var errors = issues.Where(i => i.Severity == "error").ToList();
        if (errors.Count > 0)
        {
            foreach (var e in errors) Console.Error.WriteLine($"schema error {e.Table}.{e.Column}: {e.Message}");
            return 1;
        }
        var cfg = await seeder.ReadConfigAsync();
        var settings = new SeedSettings
        {
            Scale = scale, Seed = seed, UserPassword = Password(o), RoleIds = cfg.RoleIds, FirstProjectId = cfg.FirstProjectId,
            TicketTypes = cfg.TicketTypes, Categories = cfg.Categories, Priorities = cfg.Priorities, EmailDomain = Domain(o),
        };
        Console.WriteLine($"seeding scale {scale.Name} ({scale.Tickets:N0} tickets, {scale.Users:N0} users, {scale.Projects} projects), seed {seed}");
        var total = Stopwatch.StartNew();
        var resetTime = o.ContainsKey("no-reset") ? TimeSpan.Zero : await seeder.ResetAsync(settings.EmailDomain);
        var result = await seeder.LoadAsync(new Generator(settings), resetTime);
        var stats = await seeder.StatsAsync(settings.EmailDomain);
        var untrusted = await seeder.UntrustedForeignKeysAsync();
        total.Stop();
        var summary = new
        {
            scale = scale.Name, seed, tickets = scale.Tickets, users = scale.Users, projects = scale.Projects,
            resetSeconds = Math.Round(resetTime.TotalSeconds, 2), loadSeconds = Math.Round(result.Elapsed.TotalSeconds, 2),
            totalSeconds = Math.Round(total.Elapsed.TotalSeconds, 2), rowsWritten = result.RowsByTable, tables = stats,
            untrustedOrDisabledForeignKeys = untrusted,
        };
        var json = JsonSerializer.Serialize(summary, Json);
        Console.WriteLine(json);
        if (o.TryGetValue("json", out var file)) await File.WriteAllTextAsync(file, json);
        return untrusted == 0 ? 0 : 1;
    }

    private static async Task<int> Stats(Dictionary<string, string> o)
    {
        var stats = await new SqlSeeder(Conn(o)).StatsAsync(Domain(o));
        var json = JsonSerializer.Serialize(stats, Json);
        Console.WriteLine(json);
        if (o.TryGetValue("json", out var file)) await File.WriteAllTextAsync(file, json);
        return 0;
    }

    private static int Fingerprint(Dictionary<string, string> o)
    {
        var scale = ScalePreset.Parse(o.GetValueOrDefault("scale") ?? "XS");
        var settings = new SeedSettings
        {
            Scale = scale, Seed = long.Parse(o.GetValueOrDefault("seed") ?? "42"), UserPassword = "fingerprint-only",
            RoleIds = TicketDeskSchema.StockRoles.ToDictionary(r => r, r => r),
        };
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"{scale.Name} seed {settings.Seed}: {new Generator(settings).Fingerprint()} ({sw.Elapsed.TotalSeconds:F1}s)");
        return 0;
    }
}
