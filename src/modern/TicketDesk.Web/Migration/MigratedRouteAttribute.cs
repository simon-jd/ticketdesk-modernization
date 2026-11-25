namespace TicketDesk.Web.Migration;

/// <summary>
/// Marks an endpoint of the new app as the replacement of one legacy route (an <c>id</c> of
/// <c>docs/migration/routes.csv</c>): its responses get the legacy security and caching headers and the
/// <c>X-TicketDesk-Backend</c> marker (<see cref="MigrationRegistration.UseMigratedRouteHeaders"/>), and the route counts as
/// migrated in <c>/api/migration/routes</c>. For a page that also has an Angular version, <see cref="MigrationRegistry"/>
/// picks which one answers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class MigratedRouteAttribute(string id) : Attribute
{
    public string Id { get; } = id;

    /// <summary>
    /// The legacy view rendered an MVC 5 antiforgery token, and MVC 5 then sent <c>X-Frame-Options: SAMEORIGIN</c> in front of
    /// the Web.config <c>DENY</c>: an HTML page (200) of such a route sends both values, redirects only DENY.
    /// </summary>
    public bool LegacyTokenPage { get; init; }

}

/// <summary>
/// Marks the Angular page endpoint of a migrated route (next to its server endpoint with the same template and
/// <see cref="MigratedRouteAttribute"/> id). The routing policy keeps exactly one of the two per request: the Angular page when
/// <see cref="MigrationRegistry.ServesAngular"/> says so, else the server endpoint.
/// </summary>
public sealed class AngularPageMetadata
{
    public static readonly AngularPageMetadata Instance = new();
}
