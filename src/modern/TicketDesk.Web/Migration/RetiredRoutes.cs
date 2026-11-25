namespace TicketDesk.Web.Migration;

/// <summary>
/// The routes.csv rows of wave <c>retire</c>: dropped with the legacy app, not migrated. What the new app answers
/// (<c>Controllers/RetiredController.cs</c>) and what replaces each, as reported by <c>/api/migration/routes</c>; the expected
/// differences are X1 (first-run setup), X2 (demo data) and S2 (e-mail test page) in docs/migration/expected-differences.md.
/// </summary>
public static class RetiredRoutes
{
    private const string Setup = "410 Gone with a pointer. ";
    /// <summary>The wizard's administrator step since Round 5 (src/modern/TicketDesk.Database/FirstAdministrator.cs).</summary>
    public const string FirstAdmin = "the first administrator: td-db create-admin --email E --display-name N --password-stdin " +
                                     "(password on standard input or --password-file, refuses while an administrator exists).";
    private const string TicketDesk20 = "TicketDesk 2.0 databases are not supported: upgrade them with TicketDesk 2.5 first, then migrate (td-db).";

    public static readonly IReadOnlyDictionary<string, string> Notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["setup.index"] = Setup + "The setup wizard is not needed: the schema is applied by td-db migrate (deploy step), the first administrator of " +
                          "an empty database by td-db create-admin, further accounts under admin/users.",
        ["setup.child-actions"] = "No URL (child actions of the setup wizard): retired with it.",
        ["setup.create-database"] = Setup + "An empty database: td-seed schema --create-database, then td-db migrate --contract --legacy-retired, then " +
                                    FirstAdmin + " The wizard's password reset of an existing user (S1) has no replacement.",
        ["setup.upgrade-database"] = Setup + TicketDesk20,
        ["setup.check-upgrade-progress"] = Setup + TicketDesk20,
        ["setup.legacy-migration"] = Setup + TicketDesk20,
        ["admin.demo.create"] = Setup + "Demo data: td-seed run --scale XS|S (lab tooling, ops/dev/seed.sh).",
        ["admin.demo.remove"] = Setup + "Demo data removal: td-seed reset (lab tooling, ops/dev/seed.sh).",
        ["emailtest.index"] = "404 (S2). Ticket e-mails are rendered by TicketDesk.Worker (NotificationComposer, NotificationComposerTests).",
    };
}
