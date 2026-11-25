using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using TicketDesk.Identity;
using TicketDesk.Tickets;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Spa;

/// <summary>
/// The Angular front end (<c>src/web</c>, built into <c>wwwroot/app</c>): its static assets under <see cref="AssetPath"/> and its
/// page shell (<c>index.html</c>) for the migrated pages it replaces. The shell is the same for every page; the Angular router
/// picks the page from the URL, and the data comes from <c>/api</c>.
/// </summary>
public sealed class AngularShell
{
    /// <summary>URL prefix of the built assets (Angular <c>baseHref</c>; the router itself works from <c>/</c>).</summary>
    public const string AssetPath = "/app";

    /// <summary>
    /// Cookie with the antiforgery request token for the Angular HttpClient (<c>withXsrfConfiguration</c>), which sends it back
    /// as the <c>RequestVerificationToken</c> header on state-changing <c>/api</c> calls. Readable by script by design; the
    /// antiforgery cookie token itself stays HttpOnly.
    /// </summary>
    public const string XsrfCookie = "XSRF-TOKEN";

    private readonly IFileProvider _files;
    private readonly ILogger<AngularShell> _logger;
    private (string Html, DateTimeOffset Modified)? _template;

    /// <summary>Configuration key of the build folder (default <c>wwwroot/app</c> under the content root).</summary>
    public const string RootKey = "FrontEnd:Root";

    public AngularShell(IWebHostEnvironment env, IConfiguration configuration, ILogger<AngularShell> logger)
    {
        var root = configuration[RootKey] is { Length: > 0 } configured
            ? Path.GetFullPath(configured, env.ContentRootPath)
            : Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "app");
        _files = Directory.Exists(root) ? new PhysicalFileProvider(root) : new NullFileProvider();
        _logger = logger;
    }

    public IFileProvider Files => _files;

    /// <summary>The front end is built (<c>wwwroot/app/index.html</c> exists). Without it the registry keeps the server pages.</summary>
    public bool Available => _files.GetFileInfo("index.html").Exists;

    private string Template()
    {
        var file = _files.GetFileInfo("index.html");
        var cached = _template;
        if (cached is { } c && c.Modified == file.LastModified) return c.Html;
        using var reader = new StreamReader(file.CreateReadStream());
        var html = reader.ReadToEnd();
        if (!html.Contains("<td-root", StringComparison.Ordinal)) _logger.LogWarning("wwwroot/app/index.html has no <td-root> element");
        _template = (html, file.LastModified);
        return html;
    }

    /// <summary>
    /// Writes the shell: <c>index.html</c> with a per-request CSP nonce on the root element (Angular adds it to the style elements
    /// it creates), a strict Content-Security-Policy, no caching, and the antiforgery tokens (HttpOnly cookie + <see cref="XsrfCookie"/>).
    /// </summary>
    public async Task WriteAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var html = Template().Replace("<td-root", $"<td-root ngCspNonce=\"{nonce}\"", StringComparison.Ordinal);
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true,
        });
        var h = context.Response.Headers;
        h.CacheControl = "no-cache, no-store";
        h.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; " +
                                  $"style-src 'self' 'nonce-{nonce}'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; " +
                                  "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
        h["Referrer-Policy"] = "same-origin";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }
}

public static class AngularShellRegistration
{
    public static IServiceCollection AddAngularFrontEnd(this IServiceCollection services)
    {
        services.AddSingleton<AngularShell>();
        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true; // only the static bundle goes through it (UseAngularAssets): no secrets, no BREACH exposure
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        });
        // the bundle is cached for a year (hashed names), so a better ratio is worth the few milliseconds per first request
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Optimal);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Optimal);
        return services;
    }

    /// <summary>
    /// Serves the built assets under <see cref="AngularShell.AssetPath"/> (hashed file names: cached for a year). Call before
    /// <c>UseRouting</c>: otherwise the YARP catch-all endpoint is already selected and the static-file middleware steps aside.
    /// </summary>
    public static IApplicationBuilder UseAngularAssets(this IApplicationBuilder app)
    {
        var shell = app.ApplicationServices.GetRequiredService<AngularShell>();
        return app.UseWhen(c => c.Request.Path.StartsWithSegments(AngularShell.AssetPath), branch =>
        {
            branch.UseResponseCompression();
            branch.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = AngularShell.AssetPath,
                FileProvider = shell.Files,
                ContentTypeProvider = new FileExtensionContentTypeProvider(),
                OnPrepareResponse = r =>
                {
                    var name = r.File.Name;
                    // index.html is served as the page shell, never as a cacheable asset
                    r.Context.Response.Headers.CacheControl = name is "index.html" ? "no-cache, no-store" :
                        IsHashed(name) ? "public, max-age=31536000, immutable" : "public, max-age=3600";
                    r.Context.Response.Headers.XContentTypeOptions = "nosniff";
                },
            });
        });
    }

    /// <summary>Angular's output hashing: <c>main-ABCDEFGH.js</c>, <c>chunk-Bz0XaQQI.js</c>, <c>styles-ABCDEFGH.css</c>.</summary>
    internal static bool IsHashed(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"-[A-Za-z0-9_-]{8}\.(js|css)$");

    /// <summary>
    /// The Angular page endpoints, next to the server pages with the same templates and ids (the routing policy keeps one of them
    /// per request). Each runs the server page's checks before it answers with the shell, so redirects and refusals stay the same:
    /// a list the user does not have goes to /access-denied (L2), a missing ticket to /tickets.
    /// </summary>
    public static IEndpointRouteBuilder MapAngularPages(this IEndpointRouteBuilder app)
    {
        var pages = app.MapGroup("").RequireAuthorization(TicketDeskPolicies.InternalUser).ExcludeFromDescription();

        pages.MapGet("tickets/{listName?}/{page:int?}", (HttpContext context, string? listName, AngularShell shell) =>
        {
            var lists = TicketLists.ForUser(context.User.IsHelpDeskOrAdmin());
            if (listName != null && !lists.Any(l => l.Name.Equals(listName, StringComparison.OrdinalIgnoreCase)))
                return (IResult)new LegacyRedirectResult("/access-denied");
            return new ShellResult(shell);
        }).WithMetadata(new MigratedRouteAttribute("tickets.index"), AngularPageMetadata.Instance);

        pages.MapGet("ticket/{id:int}", async (int id, TicketQueries tickets, AngularShell shell, CancellationToken ct) =>
            await tickets.ExistsAsync(id, ct) ? (IResult)new ShellResult(shell) : new LegacyRedirectResult("/tickets"))
            .WithMetadata(new MigratedRouteAttribute("ticket.view"), AngularPageMetadata.Instance);

        pages.MapGet("ticket/new", (AngularShell shell) => new ShellResult(shell))
            .WithMetadata(new MigratedRouteAttribute("ticket.new.get"), AngularPageMetadata.Instance);

        // /app/** belongs to the new app like /api/**: an asset that is not in the build is a 404 here, never proxied
        app.Map(AngularShell.AssetPath + "/{**rest}", () => Results.NotFound()).ExcludeFromDescription();

        return app;
    }

    private sealed class ShellResult(AngularShell shell) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => shell.WriteAsync(httpContext);
    }
}
