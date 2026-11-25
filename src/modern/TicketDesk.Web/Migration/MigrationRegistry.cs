using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace TicketDesk.Web.Migration;

/// <summary>Which front end of the new app answers a page that has two: the server (Razor) page or the Angular page.</summary>
public enum RouteMode
{
    /// <summary>The server (Razor) page. Also the mode of every route the registry does not list.</summary>
    New,
    /// <summary>
    /// The Angular front end (<c>src/web</c>) where the route has an Angular page; anywhere else, and while the front end is not
    /// built, exactly like <see cref="New"/>. Switch-back: <see cref="New"/> gives the server page again.
    /// </summary>
    Angular,
}

/// <summary>
/// Bound from configuration section <c>Migration</c>. The route list normally lives in its own JSON file
/// (<see cref="RegistryFile"/>, watched for changes) so operations can switch a page between its Angular and its server version
/// without a redeploy. Since the legacy app is retired (Round 4) every migrated route is answered by the new app; the file only
/// says which pages the Angular front end answers.
/// </summary>
public sealed class MigrationOptions
{
    public const string SectionName = "Migration";
    public const string DefaultRegistryFile = "migration-registry.json";

    /// <summary>Path of the registry file (relative to the content root). Empty: no file, only other configuration sources.</summary>
    public string? RegistryFile { get; set; }

    /// <summary>
    /// routes.csv id → mode (<see cref="RouteMode"/> names). Ids that are not listed are <see cref="RouteMode.New"/>. Read as text
    /// so that a value the app no longer has (<c>Legacy</c>, from a registry file of the coexistence rounds) is logged and ignored
    /// instead of failing the reload.
    /// </summary>
    public Dictionary<string, string> Routes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record RouteStatus(string Id, string Wave, string Verb, string Route, bool Routable, bool Implemented, bool Retired, RouteMode Mode, string? Note);

public sealed record WaveStatus(string Wave, int Total, int Migrated);

/// <summary>
/// The legacy route inventory and how the new app answers it: <see cref="Migrated"/> rows have an endpoint (or a ported child
/// action), <see cref="Retired"/> rows (wave <c>retire</c>) were dropped with the legacy app and get a 410/404 answer.
/// </summary>
public sealed record MigrationStatus(int Total, int Routable, int Migrated, int Retired, IReadOnlyList<WaveStatus> Waves, IReadOnlyList<RouteStatus> Routes, IReadOnlyList<string> UnknownIds)
{
    /// <summary>Pages answered by the Angular front end (routes in mode Angular that have an Angular page, front end built).</summary>
    public int AngularPages { get; init; }
}

/// <summary>
/// The page switch of the new app: for a route that has both a server page and an Angular page, which one answers, from the
/// current <see cref="MigrationOptions"/> (re-read when the registry file changes). During coexistence this registry also
/// decided per request between the new app and the legacy app behind the YARP fallback (mode Legacy, page groups, the legacy
/// sign-in cookie); that went with the legacy app in Round 4 (docs/dev/rollback.md).
/// </summary>
public sealed class MigrationRegistry
{
    public const string MeterName = "TicketDesk.Migration";

    /// <summary>The wave of the routes.csv rows that were dropped, not migrated (first-run setup, demo data, e-mail test).</summary>
    public const string RetiredWave = "retire";

    /// <summary>Child-action rows the new app renders itself inside its own pages (no URL of their own, no switch).</summary>
    public static readonly IReadOnlySet<string> PortedChildRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "navigation.project-menu" };

    private readonly RouteInventory _inventory;
    private readonly IServiceProvider _services;
    private readonly ILogger<MigrationRegistry> _logger;
    private volatile Snapshot _current;
    private HashSet<string>? _implemented;
    private HashSet<string>? _angularPages;

    private sealed record Snapshot(IReadOnlyDictionary<string, RouteMode> Modes, IReadOnlyList<string> UnknownIds);

    public MigrationRegistry(RouteInventory inventory, IOptionsMonitor<MigrationOptions> options, IServiceProvider services, IMeterFactory meters,
        ILogger<MigrationRegistry> logger)
    {
        _inventory = inventory;
        _services = services;
        _logger = logger;
        _current = Build(options.CurrentValue);
        options.OnChange(o =>
        {
            _current = Build(o);
            _logger.LogInformation("Migration registry reloaded: {Angular} route(s) on the Angular front end", _current.Modes.Count(kv => kv.Value == RouteMode.Angular));
        });
        var meter = meters.Create(MeterName);
        meter.CreateObservableGauge("ticketdesk.migration.routes_migrated", () => Status().Migrated, description: "Legacy routes answered by the new app");
        meter.CreateObservableGauge("ticketdesk.migration.routes_total", () => _inventory.Rows.Count, description: "Rows of the legacy route inventory");
    }

    public RouteInventory Inventory => _inventory;

    private Snapshot Build(MigrationOptions o)
    {
        var modes = new Dictionary<string, RouteMode>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();
        foreach (var (id, value) in o.Routes)
        {
            if (_inventory.Find(id) == null) unknown.Add(id);
            else if (Enum.TryParse<RouteMode>(value, ignoreCase: true, out var mode) && Enum.IsDefined(mode)) modes[id] = mode;
            else if (string.Equals(value, "Legacy", StringComparison.OrdinalIgnoreCase))
                _logger.LogWarning("Migration registry: {Id} is set to Legacy, but the legacy app is retired; the new app's server page answers", id);
            else
                _logger.LogWarning("Migration registry: unknown mode {Mode} for {Id} (New or Angular); the server page answers", value, id);
        }
        if (unknown.Count > 0) _logger.LogWarning("Migration registry lists ids that are not in routes.csv (ignored): {Ids}", string.Join(", ", unknown));
        return new Snapshot(modes, unknown);
    }

    /// <summary>The configured mode of a route (<see cref="RouteMode.New"/> when the registry does not list it).</summary>
    public RouteMode Mode(string id) => _current.Modes.GetValueOrDefault(id);

    /// <summary>
    /// Request header with which a client asks for the server-rendered page of a route that is on Angular (value
    /// <see cref="ServerUiValue"/>). Only the golden replay sends it, to read the form a legacy-protocol client posts next out
    /// of server HTML; browsers never do. The server page is the switch-back target and authorizes exactly like the Angular page.
    /// </summary>
    public const string UiHeader = "X-TicketDesk-Ui";
    public const string ServerUiValue = "server";

    /// <summary>
    /// This request gets the Angular page of the route: mode Angular, the route has an Angular page, the front end is built
    /// and the request did not ask for the server page.
    /// </summary>
    public bool ServesAngular(string id, HttpContext context) =>
        Mode(id) == RouteMode.Angular && FrontEndAvailable() && HasAngularPage(id)
        && !string.Equals(context.Request.Headers[UiHeader], ServerUiValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ids that have an Angular page endpoint (<see cref="AngularPageMetadata"/>), from the endpoint data source.</summary>
    public bool HasAngularPage(string id)
    {
        if (_angularPages == null)
        {
            var source = _services.GetService<EndpointDataSource>();
            if (source == null) return false;
            _angularPages = source.Endpoints.Where(e => e.Metadata.GetMetadata<AngularPageMetadata>() != null)
                .Select(e => e.Metadata.GetMetadata<MigratedRouteAttribute>()?.Id).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        return _angularPages.Contains(id);
    }

    /// <summary>The Angular front end is built (<c>wwwroot/app/index.html</c> exists).</summary>
    private bool FrontEndAvailable() => _services.GetService<Spa.AngularShell>()?.Available ?? false;

    /// <summary>Ids that have an endpoint in this app (from the endpoint data source, computed once after startup).</summary>
    private HashSet<string> Implemented()
    {
        if (_implemented != null) return _implemented;
        var source = _services.GetService<EndpointDataSource>();
        if (source == null) return [];
        var ids = source.Endpoints.Select(e => e.Metadata.GetMetadata<MigratedRouteAttribute>()?.Id).OfType<string>()
            .Concat(PortedChildRows).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _implemented = ids;
    }

    public MigrationStatus Status()
    {
        var snapshot = _current;
        var implemented = Implemented();
        var routes = _inventory.Rows.Select(r =>
        {
            var mode = snapshot.Modes.GetValueOrDefault(r.Id);
            var retired = r.Wave == RetiredWave;
            var impl = implemented.Contains(r.Id);
            string? note = null;
            if (retired) note = RetiredRoutes.Notes.GetValueOrDefault(r.Id);
            else if (!impl) note = "no endpoint in the new app";
            else if (mode == RouteMode.Angular && !HasAngularPage(r.Id)) note = "no Angular page for this route: the server endpoint answers";
            else if (mode == RouteMode.Angular && !FrontEndAvailable()) note = "Angular front end not built (wwwroot/app/index.html): the server page answers";
            return new RouteStatus(r.Id, r.Wave, r.Verb, r.Route, r.IsRoutable, impl, retired, mode, note);
        }).ToList();
        bool Migrated(RouteStatus s) => s.Implemented && !s.Retired;
        var waves = routes.GroupBy(r => r.Wave).Select(g => new WaveStatus(g.Key, g.Count(), g.Count(Migrated)))
            .OrderBy(w => int.TryParse(w.Wave, out var n) ? n : int.MaxValue).ThenBy(w => w.Wave, StringComparer.Ordinal).ToList();
        var angular = FrontEndAvailable() ? routes.Count(r => r.Mode == RouteMode.Angular && HasAngularPage(r.Id)) : 0;
        return new MigrationStatus(routes.Count, routes.Count(r => r.Routable), routes.Count(Migrated), routes.Count(r => r.Retired), waves, routes, snapshot.UnknownIds)
            { AngularPages = angular };
    }
}
