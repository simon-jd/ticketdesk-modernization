using System.Text.Json;

namespace TicketDesk.Database;

/// <summary>
/// td-db. Connection strings come from environment variables only (never the command line).
/// <code>
///   td-db status                                    TD_DB_CONN
///   td-db migrate [--contract --legacy-retired]     TD_DB_CONN; expand (V) + programmable (R); contract (C) only when asked
///   td-db scripts                                   list the embedded scripts and checksums
///   td-db fulltext [--wait SECONDS]                 TD_DB_CONN; full-text index state (V0011); --wait: until populated, exit 1 on timeout
///   td-db reconcile [--json FILE] [--parallel N] [--exclude dbo.T,...]   TD_DB_SOURCE, TD_DB_TARGET
///   td-db create-admin --email E --display-name N (--password-stdin | --password-file F)
///                                                   TD_DB_CONN; the first administrator of a database that has none
/// </code>
/// Exit codes: 0 ok / match, 1 drift, reconciliation mismatch or create-admin refused, 2 usage or runtime error.
/// </summary>
public static class Cli
{
    public const string Usage =
        "usage: td-db status | migrate [--contract --legacy-retired] | scripts | fulltext [--wait S] | reconcile [--json FILE] [--parallel N] [--exclude T,...]" +
        " | create-admin --email E --display-name N (--password-stdin | --password-file FILE)   (td-db COMMAND --help)";

    public const string CreateAdminUsage = """
        usage: td-db create-admin --email E --display-name N (--password-stdin | --password-file FILE)

        The first administrator of a database that has none (an empty installation: td-seed schema --create-database, then
        td-db migrate --contract --legacy-retired). Connection: TD_DB_CONN. The password comes from standard input (one line)
        or from the first line of FILE, never from the command line; 6 to 100 characters. The account: user name = e-mail, the
        display name, the TdAdministrators role, an Identity V3 hash (the app's hasher), the e-mail destination for
        notifications. Refused (exit 1) while any administrator exists or an account has that e-mail; existing accounts are
        never changed. Exit 0 created, 1 refused, 2 usage or error.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, Func<string, string?> env, TextReader? stdin = null)
    {
        try
        {
            var cmd = args.FirstOrDefault() ?? "";
            if (cmd is "help" or "--help" or "-h")
            {
                stdout.WriteLine(Usage);
                return 0;
            }
            var opts = Options(args.Skip(1).ToArray());
            if (opts.ContainsKey("help") && cmd == "create-admin")
            {
                stdout.WriteLine(CreateAdminUsage);
                return 0;
            }
            string Env(string name) => env(name) is { Length: > 0 } v ? v : throw new InvalidOperationException($"{name} is not set");
            switch (cmd)
            {
                case "scripts":
                    foreach (var s in ScriptCatalog.Load())
                        stdout.WriteLine($"{(char)s.Kind} {s.Name,-60} {s.Checksum[..12]} {(s.UseTransaction ? "tx" : "no-tx")} {s.Batches.Count} batch(es)");
                    return 0;
                case "status":
                {
                    var plan = await new SchemaMigrator(Env("TD_DB_CONN"), stdout).PlanAsync();
                    foreach (var a in plan.Applied) stdout.WriteLine($"applied  {a.Name,-60} {a.AppliedAt:u} {a.DurationMs} ms by {a.AppliedBy}");
                    foreach (var p in plan.Pending) stdout.WriteLine($"pending  {p.Name}");
                    foreach (var p in plan.ContractPending) stdout.WriteLine($"contract {p.Name} (after legacy retirement)");
                    foreach (var d in plan.Drift) stdout.WriteLine($"DRIFT    {d}");
                    return plan.Drift.Count > 0 ? 1 : 0;
                }
                case "migrate":
                {
                    var contract = opts.ContainsKey("contract");
                    if (contract && !opts.ContainsKey("legacy-retired"))
                    {
                        stderr.WriteLine("contract scripts remove what the legacy app still uses; pass --legacy-retired once it is gone");
                        return 2;
                    }
                    var applied = await new SchemaMigrator(Env("TD_DB_CONN"), stdout).MigrateAsync(contract);
                    stdout.WriteLine($"{applied.Count} script(s) applied");
                    return 0;
                }
                case "fulltext":
                {
                    var conn = Env("TD_DB_CONN");
                    var ok = true;
                    if (opts.TryGetValue("wait", out var w))
                        ok = await FullTextStatus.WaitAsync(conn, TimeSpan.FromSeconds(w == "true" ? 600 : int.Parse(w)));
                    stdout.Write(FullTextStatus.Format(await FullTextStatus.ReadAsync(conn)));
                    return ok ? 0 : 1;
                }
                case "reconcile":
                {
                    var exclude = (opts.GetValueOrDefault("exclude") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var parallel = opts.TryGetValue("parallel", out var p) ? int.Parse(p) : 4;
                    var report = await new Reconciler(Env("TD_DB_SOURCE"), Env("TD_DB_TARGET"), parallel) { ExcludeTables = exclude }.RunAsync();
                    stdout.Write(Reconciler.Format(report));
                    if (opts.TryGetValue("json", out var json))
                        await File.WriteAllTextAsync(json, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
                    return report.Match ? 0 : 1;
                }
                case "create-admin":
                {
                    if (opts.ContainsKey("password"))
                    {
                        stderr.WriteLine("td-db: a password is never an argument (the process list and the shell history show it): use --password-stdin or --password-file FILE");
                        return 2;
                    }
                    if (opts.ContainsKey("password-stdin") == opts.ContainsKey("password-file") || (opts.TryGetValue("password-stdin", out var flag) && flag != "true"))
                    {
                        stderr.WriteLine(CreateAdminUsage);
                        return 2;
                    }
                    var email = opts.GetValueOrDefault("email");
                    var name = opts.GetValueOrDefault("display-name");
                    var password = await FirstAdministrator.ReadPasswordAsync(opts.ContainsKey("password-stdin") ? stdin ?? Console.In : null, opts.GetValueOrDefault("password-file"));
                    FirstAdministrator.Validate(email, name, password);
                    try
                    {
                        var created = await FirstAdministrator.CreateAsync(Env("TD_DB_CONN"), email!, name!, password);
                        stdout.WriteLine($"created administrator {email} (id {created.UserId}); " +
                                         (created.EmailDestination ? "e-mail destination added" : "no notifications tables: e-mail destination not added"));
                        stdout.WriteLine("sign in at /user/sign-in; further accounts: Admin, Users");
                        return 0;
                    }
                    catch (FirstAdministratorRefusedException e)
                    {
                        stderr.WriteLine($"td-db: {e.Message}");
                        return 1;
                    }
                }
                default:
                    stderr.WriteLine(Usage);
                    return 2;
            }
        }
        catch (Exception e)
        {
            stderr.WriteLine($"td-db: {e.Message}");
            return 2;
        }
    }

    private static Dictionary<string, string> Options(string[] a)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < a.Length; i++)
        {
            // the value is not echoed: it might be a password typed in the wrong place
            if (!a[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"unexpected argument #{i + 2} (options start with --)");
            d[a[i][2..]] = i + 1 < a.Length && !a[i + 1].StartsWith("--", StringComparison.Ordinal) ? a[++i] : "true";
        }
        return d;
    }
}
