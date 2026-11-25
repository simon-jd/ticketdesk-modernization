using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Tests;

/// <summary>The route inventory, the shipped registry file and the bundle snapshot are consistent with each other.</summary>
public sealed class RouteInventoryTests
{
    private static RouteInventory Inventory => RouteInventory.LoadEmbedded();

    [Fact]
    public void Inventory_has_every_routes_csv_row_with_a_unique_id()
    {
        var inv = Inventory;
        Assert.Equal(89, inv.Rows.Count);
        Assert.Equal(87, inv.Rows.Count(r => r.IsRoutable));
        Assert.Equal(inv.Rows.Count, inv.Rows.Select(r => r.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // wave sizes of inventory.md §15
        var waves = inv.Rows.GroupBy(r => r.Wave).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["0"] = 2, ["1"] = 4, ["2"] = 29, ["3"] = 6, ["4"] = 20, ["5"] = 19, ["retire"] = 9 }, waves);
    }

    [Fact]
    public void Shipped_registry_file_puts_the_three_Angular_pages_on_Angular_and_nothing_else()
    {
        var path = Path.Combine(Golden.RepoRoot!, "src", "modern", "TicketDesk.Web", MigrationOptions.DefaultRegistryFile);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var routes = doc.RootElement.GetProperty("Migration").GetProperty("Routes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        Assert.All(routes.Keys, id => Assert.NotNull(Inventory.Find(id)));
        // only the page switch is left: no Legacy (retired), and every route that is not listed is its server page
        Assert.Equal(["ticket.new.get", "ticket.view", "tickets.index"], routes.Where(r => r.Value == "Angular").Select(r => r.Key).Order(StringComparer.Ordinal));
        Assert.All(routes.Values, v => Assert.Contains(v, new[] { "New", "Angular" }));
    }

    [Fact]
    public void Ported_child_rows_name_inventory_rows()
    {
        Assert.All(MigrationRegistry.PortedChildRows, id => Assert.False(Inventory.Find(id)!.IsRoutable));
    }

    /// <summary>Every [MigratedRoute] endpoint answers the URL and verb of its routes.csv row.</summary>
    [Fact]
    public async Task Migrated_endpoints_match_their_inventory_rows()
    {
        await using var app = new TicketDeskApp(TicketDeskApp.Settings());
        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<MigratedRouteAttribute>() != null).ToList();
        Assert.NotEmpty(endpoints);
        foreach (var e in endpoints)
        {
            var row = Inventory.Find(e.Metadata.GetMetadata<MigratedRouteAttribute>()!.Id);
            Assert.NotNull(row);
            var template = "/" + (e.RoutePattern.RawText ?? "").Trim('/');
            if (row.Id.StartsWith("bundles.", StringComparison.Ordinal) || row.Route.Contains("{action=index}"))
                // one endpoint per bundle; "{action=index}" class routes are spelled out as literal paths
                Assert.StartsWith("/" + row.Route.Split('{')[0].Trim('/'), template);
            else
                Assert.Equal(("/" + row.Route.Trim('/')).ToLowerInvariant(), template.ToLowerInvariant());
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            // ANY rows may be narrowed (S3: no state change by GET); fixed-verb rows keep their verb
            if (row.Verb != "ANY") Assert.Contains(row.Verb, methods ?? []);
        }
    }

    [Fact]
    public void Bundle_snapshot_is_the_unmodified_legacy_output()
    {
        var bundles = new LegacyBundles();
        Assert.Equal(26, bundles.All.Count);
        foreach (var b in bundles.All)
        {
            var bytes = bundles.Content(b);
            Assert.Equal(b.Bytes, bytes.Length);
            Assert.Equal(b.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            // System.Web.Optimization's v= : UrlTokenEncode(SHA-256 of the bundle text as UTF-16LE)
            Assert.Equal(b.V, UrlTokenEncode(SHA256.HashData(Encoding.Unicode.GetBytes(Encoding.UTF8.GetString(bytes)))));
        }
        Assert.True(bundles.All.Count(b => b.LinkVerified) >= 4);
    }

    /// <summary>
    /// The plain static files the pages and the bundled CSS reference (fonts, select2 sprites, logo, Dropzone's thumbnail,
    /// favicon) are served by the new app, byte for byte the legacy web project's files: nothing is left for a legacy app.
    /// </summary>
    [Fact]
    public async Task Static_files_the_bundles_and_pages_reference_are_served_as_the_legacy_files()
    {
        var bundles = new LegacyBundles();
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/content/images/general-file.png", "/favicon.ico", "/robots.txt" };
        foreach (var b in bundles.All.Where(b => b.ContentType.StartsWith("text/css", StringComparison.Ordinal)))
        {
            var css = Encoding.UTF8.GetString(bundles.Content(b));
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(css, @"url\(['""]?([^'"")]+)['""]?\)"))
            {
                var url = m.Groups[1].Value;
                if (url.StartsWith("data:", StringComparison.Ordinal)) continue;
                referenced.Add(new Uri(new Uri("https://x" + b.Path), url).AbsolutePath); // relative to /content/<bundle>
            }
        }
        Assert.Contains("/fonts/glyphicons-halflings-regular.woff2", referenced);
        Assert.Contains("/Content/css/select2.png", referenced);

        await using var app = new TicketDeskApp(TicketDeskApp.Settings());
        using var client = app.RawClient();
        var legacyRoot = Path.Combine(Golden.RepoRoot!, "TicketDesk", "TicketDesk.Web.Client");
        foreach (var path in referenced)
        {
            using var r = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{path}: {(int)r.StatusCode}");
            Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
            var body = await r.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            // IIS matched paths case-insensitively; so does routing, and the files keep the legacy folder spelling
            var file = Directory.EnumerateFiles(legacyRoot, "*", SearchOption.AllDirectories)
                .Single(f => string.Equals("/" + Path.GetRelativePath(legacyRoot, f).Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken), body);
        }
    }

    private static string UrlTokenEncode(byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        var pad = b64.Length - b64.TrimEnd('=').Length;
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_') + pad;
    }
}

/// <summary>
/// The new app alone (Round 4): no YARP fallback, no Legacy mode. Every migrated route answers whatever the registry says;
/// the registry only switches a page between its server and its Angular version (AngularPagesTests covers that switch).
/// </summary>
public sealed class MigrationRoutingTests : IDisposable
{
    private readonly string _registry = Path.Combine(Path.GetTempPath(), $"td-registry-{Guid.NewGuid():N}.json");
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => File.Delete(_registry);

    private void WriteRegistry(params (string Id, string Mode)[] routes)
    {
        var json = JsonSerializer.Serialize(new { Migration = new { Routes = routes.ToDictionary(r => r.Id, r => r.Mode) } });
        File.WriteAllText(_registry + ".tmp", json);
        File.Move(_registry + ".tmp", _registry, overwrite: true); // atomic, like a ConfigMap update
    }

    private TicketDeskApp App(Action<Dictionary<string, string?>>? configure = null)
    {
        var s = TicketDeskApp.Settings();
        s["Migration:RegistryFile"] = _registry;
        configure?.Invoke(s);
        return new TicketDeskApp(s, kestrel: true);
    }

    private static string? Backend(HttpResponseMessage r) =>
        r.Headers.TryGetValues(MigrationRegistration.BackendHeader, out var v) ? v.Single() : null;

    [Fact]
    public async Task Without_the_registry_file_every_migrated_route_is_answered_by_the_new_app()
    {
        await using var app = App(s => s["Migration:RegistryFile"] = Path.Combine(Path.GetTempPath(), "td-no-such-registry.json"));
        using var client = app.RawClient();
        using (var r = await client.GetAsync("/about", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("modern; route=home.about", Backend(r));
        }
        using (var r = await client.GetAsync("/bundles/common", Ct)) Assert.Equal("modern; route=bundles.scripts", Backend(r));
        Assert.Equal(80, app.Services.GetRequiredService<MigrationRegistry>().Status().Migrated);
    }

    [Fact]
    public async Task Legacy_or_unknown_modes_in_the_registry_are_ignored_and_the_server_page_answers()
    {
        WriteRegistry(("home.about", "Legacy"), ("bundles.styles", "Sideways"));
        await using var app = App();
        using var client = app.RawClient();
        using (var r = await client.GetAsync("/about", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("modern; route=home.about", Backend(r));
        }
        using (var r = await client.GetAsync("/content/styles", Ct)) Assert.Equal("modern; route=bundles.styles", Backend(r));
        var registry = app.Services.GetRequiredService<MigrationRegistry>();
        Assert.Equal(RouteMode.New, registry.Mode("home.about"));
        Assert.Equal(RouteMode.New, registry.Mode("bundles.styles"));
    }

    [Fact]
    public async Task Paths_the_new_app_does_not_have_are_404_and_nothing_is_proxied()
    {
        await using var app = App();
        using var client = app.RawClient();
        foreach (var path in new[] { "/no-such-page", "/Scripts/jquery-2.1.4.js", "/api/no-such-endpoint", "/systemweb-adapters/authenticate" })
        {
            using var r = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.Null(Backend(r));
            Assert.False(r.Headers.Contains("Server")); // no Kestrel banner, and no IIS one either: nothing came from a legacy app
        }
    }

    [Fact]
    public async Task Status_counts_migrated_and_retired_routes_per_wave()
    {
        WriteRegistry(("tickets.index", "Angular"), ("no.such.route", "New"));
        await using var app = App();
        using var client = app.RawClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/migration/routes", Ct);
        Assert.Equal(89, status.GetProperty("total").GetInt32());
        Assert.Equal(87, status.GetProperty("routable").GetInt32());
        Assert.Equal(80, status.GetProperty("migrated").GetInt32());
        Assert.Equal(9, status.GetProperty("retired").GetInt32());
        var waves = status.GetProperty("waves").EnumerateArray().ToDictionary(w => w.GetProperty("wave").GetString()!, w => (w.GetProperty("total").GetInt32(), w.GetProperty("migrated").GetInt32()));
        Assert.Equal(["0", "1", "2", "3", "4", "5", "retire"], status.GetProperty("waves").EnumerateArray().Select(w => w.GetProperty("wave").GetString()));
        Assert.All(waves.Where(w => w.Key != "retire"), w => Assert.Equal(w.Value.Item1, w.Value.Item2)); // every migration wave complete
        Assert.Equal((9, 0), waves["retire"]);
        Assert.Equal(["no.such.route"], status.GetProperty("unknownIds").EnumerateArray().Select(x => x.GetString()));
        var row = status.GetProperty("routes").EnumerateArray().Single(r => r.GetProperty("id").GetString() == "tickets.index");
        Assert.Equal("Angular", row.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Migrated_responses_carry_the_legacy_security_headers()
    {
        await using var app = App();
        using var client = app.RawClient();
        using var page = await client.GetAsync("/about", Ct);
        // S3 (Round 5): every page with the layout issues an antiforgery token (its menus post), so ASP.NET Core's no-cache wins
        Assert.Equal("no-store, no-cache", page.Headers.CacheControl!.ToString());
        Assert.Equal("DENY", page.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("1; mode=block", page.Headers.GetValues("X-XSS-Protection").Single());
        Assert.Equal("nosniff", page.Headers.GetValues("X-Content-Type-Options").Single());
        // MVC 5's default where the endpoint sets nothing (here the 405 of the culture switch's old GET)
        using var other = await client.GetAsync("/language?name=de-DE", Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, other.StatusCode);
        Assert.Equal("private", other.Headers.CacheControl!.ToString());
        Assert.Equal("DENY", other.Headers.GetValues("X-Frame-Options").Single());
        using var css = await client.GetAsync("/content/styles", Ct);
        Assert.Equal("public", css.Headers.CacheControl!.ToString());
        Assert.Equal("text/css; charset=utf-8", css.Content.Headers.ContentType!.ToString());
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
    }
}
