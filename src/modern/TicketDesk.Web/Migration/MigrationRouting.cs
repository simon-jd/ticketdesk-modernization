using Microsoft.AspNetCore.Routing.Matching;

namespace TicketDesk.Web.Migration;

/// <summary>
/// Routing policy for the pages that exist twice, as a server (Razor) page and as an Angular page with the same template and
/// id: per request it keeps exactly one of the two (see <see cref="MigrationRegistry.ServesAngular"/>), so a registry reload
/// switches a page on the next request. Every other endpoint is left alone. (During coexistence this policy also removed
/// migrated endpoints the registry held back, so routing fell through to the YARP catch-all and the legacy app.)
/// </summary>
internal sealed class MigrationMatcherPolicy(MigrationRegistry registry) : MatcherPolicy, IEndpointSelectorPolicy
{
    public override int Order => 0;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(e => e.Metadata.GetMetadata<AngularPageMetadata>() != null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i)) continue;
            var route = candidates[i].Endpoint.Metadata.GetMetadata<MigratedRouteAttribute>();
            if (route == null || !registry.HasAngularPage(route.Id)) continue;
            // a route with an Angular page has two endpoints with one template: keep the Angular page or the server page, never both
            var angularPage = candidates[i].Endpoint.Metadata.GetMetadata<AngularPageMetadata>() != null;
            if (angularPage != registry.ServesAngular(route.Id, httpContext)) candidates.SetValidity(i, false);
        }
        return Task.CompletedTask;
    }
}

public static class MigrationRegistration
{
    public const string BackendHeader = "X-TicketDesk-Backend";

    /// <summary>Set by the authorization result handler when a migrated endpoint's policy succeeded.</summary>
    public const string LegacyAuthorizedItem = "TicketDesk.Migration.Authorized";

    private static bool LegacyRouteHadAuthorize(HttpContext context, string id)
    {
        var auth = context.RequestServices.GetRequiredService<MigrationRegistry>().Inventory.Find(id)?.Auth ?? "";
        return !auth.StartsWith("anonymous", StringComparison.Ordinal) && !auth.StartsWith("setup-enabled", StringComparison.Ordinal);
    }

    /// <summary>Adds the registry file (watched for changes) to configuration. Call before the options are bound.</summary>
    public static void AddMigrationRegistryFile(this ConfigurationManager configuration)
    {
        var file = configuration[$"{MigrationOptions.SectionName}:{nameof(MigrationOptions.RegistryFile)}"] ?? MigrationOptions.DefaultRegistryFile;
        if (string.IsNullOrWhiteSpace(file)) return;
        // optional: without the file every page is answered by its server page
        configuration.AddJsonFile(file, optional: true, reloadOnChange: true);
    }

    public static IServiceCollection AddMigrationRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MigrationOptions>().Bind(configuration.GetSection(MigrationOptions.SectionName));
        services.AddSingleton(_ => RouteInventory.LoadEmbedded());
        services.AddSingleton<MigrationRegistry>();
        services.AddSingleton<MatcherPolicy, MigrationMatcherPolicy>();
        return services;
    }

    /// <summary>Set (any value) by a view that renders a form whose legacy counterpart carried an MVC 5 antiforgery token.</summary>
    public const string LegacyFormTokenItem = "TicketDesk.LegacyFormToken";

    /// <summary>
    /// Headers on every response the new app produces for a migrated route: the legacy Web.config's custom headers
    /// (<c>X-Frame-Options</c>, <c>X-XSS-Protection</c>, <c>X-Content-Type-Options</c>), MVC 5's default
    /// <c>Cache-Control: private</c> unless the endpoint set one, and a marker naming the route (and the Angular front end).
    /// </summary>

    public static IApplicationBuilder UseMigratedRouteHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<MigratedRouteAttribute>() is { } route)
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;
                // MVC 5's AuthorizeAttribute (TdAuthorize) set a proxy max-age of 0 on every response of an authorized
                // request; routes without it in the legacy app (routes.csv auth "anonymous...") sent plain "private"
                if (!h.ContainsKey("Cache-Control"))
                    h.CacheControl = context.Items.ContainsKey(LegacyAuthorizedItem) && LegacyRouteHadAuthorize(context, route.Id) ? "s-maxage=0, private" : "private";
                // IIS appended DENY to whatever the app sent; MVC 5 had sent SAMEORIGIN first on every page that rendered a legacy
                // form token: the ticket pages mark themselves with LegacyFormTokenItem (their views), the identity and admin
                // forms (wave 5) with [MigratedRoute(LegacyTokenPage = true)] (HTML 200 answers; redirects stay DENY)
                var tokenPage = context.Items.ContainsKey(LegacyFormTokenItem)
                                || (route.LegacyTokenPage && context.Response.StatusCode == StatusCodes.Status200OK
                                    && (context.Response.ContentType ?? "").StartsWith("text/html", StringComparison.OrdinalIgnoreCase));
                h["X-Frame-Options"] = tokenPage ? new Microsoft.Extensions.Primitives.StringValues(["SAMEORIGIN", "DENY"]) : "DENY";
                h["X-XSS-Protection"] = "1; mode=block";
                h["X-Content-Type-Options"] = "nosniff";
                h[BackendHeader] = "modern; route=" + route.Id + (context.GetEndpoint()?.Metadata.GetMetadata<AngularPageMetadata>() != null ? "; ui=angular" : "");
                return Task.CompletedTask;
            });
        }
        return next(context);
    });

    public static void MapMigrationStatus(this IEndpointRouteBuilder api) =>
        api.MapGet("/migration/routes", (MigrationRegistry registry) => TypedResults.Ok(registry.Status()))
            .WithName("GetMigrationStatus")
            .WithSummary("Legacy routes and which app answers them (routes migrated / total).")
            .WithTags("Migration");
}
