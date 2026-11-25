using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Microsoft.Net.Http.Headers;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Legacy;

public sealed record LegacyBundle(string Path, string File, string ContentType, string CacheControl, string V, int Bytes, string Sha256, bool LinkVerified);

/// <summary>
/// Wave 0: the legacy System.Web.Optimization bundles (<c>/content/{name}</c>, <c>/bundles/{name}</c>), served by the new
/// app from a snapshot of the legacy output (<c>LegacyBundles/</c>, written by <c>ops/dev/export-legacy-bundles.py</c>),
/// with the same URLs, the same <c>v=</c> version hashes and the same content type and caching headers.
/// <para>
/// Why a snapshot and not a rebuild: WebGrease's minified output cannot be reproduced byte for byte by a .NET 10 bundler,
/// so a rebuilt bundle would change every hash and every byte for no user benefit; the legacy code is frozen while both apps
/// run, so the snapshot cannot drift. Why not keep proxying: the pages the new app renders need the version hashes anyway,
/// and serving the bundles here takes static traffic off the legacy app and removes a dependency the new app would
/// otherwise keep until the legacy app is retired. Plain static files (fonts, images, <c>/Content/*</c> referenced by the CSS)
/// are not routes of the inventory; since the legacy app is retired the new app serves them too (<see cref="LegacyStaticFiles"/>).
/// </para>
/// </summary>
public sealed class LegacyBundles
{
    private const string ResourcePrefix = "TicketDesk.Web.LegacyBundles.";
    private static readonly DateTimeOffset Loaded = DateTimeOffset.UtcNow;
    private readonly Dictionary<string, LegacyBundle> _byPath;
    private readonly Dictionary<string, byte[]> _content = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LegacyBundle> All { get; }

    private sealed record Manifest(List<LegacyBundle> Bundles);

    public LegacyBundles()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var manifest = asm.GetManifestResourceStream(ResourcePrefix + "manifest.json")
                             ?? throw new InvalidOperationException("LegacyBundles/manifest.json is not embedded");
        var m = JsonSerializer.Deserialize<Manifest>(manifest, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("LegacyBundles/manifest.json is empty");
        All = m.Bundles;
        _byPath = m.Bundles.ToDictionary(b => b.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var b in m.Bundles)
        {
            using var s = asm.GetManifestResourceStream(ResourcePrefix + b.File) ?? throw new InvalidOperationException($"bundle file {b.File} is not embedded");
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            _content[b.Path] = ms.ToArray();
        }
    }

    public byte[] Content(LegacyBundle bundle) => _content[bundle.Path];

    public LegacyBundle? Find(string virtualPath) => _byPath.GetValueOrDefault(ToUrl(virtualPath));

    private static string ToUrl(string virtualPath) => virtualPath.StartsWith("~/", StringComparison.Ordinal) ? virtualPath[1..] : virtualPath;

    /// <summary><c>@Styles.Render("~/content/styles")</c>: the link tag with the version hash.</summary>
    public IHtmlContent Styles(string virtualPath) =>
        new HtmlString($"<link href=\"{Url(virtualPath)}\" rel=\"stylesheet\"/>\r\n");

    /// <summary>
    /// <c>@Scripts.Render("~/bundles/common")</c>. A path that is not a bundle (e.g. <c>common_locale_de-DE</c>, which the
    /// legacy app never registered) renders as a plain script tag without hash, as System.Web.Optimization does.
    /// </summary>
    public IHtmlContent Scripts(string virtualPath) =>
        new HtmlString($"<script src=\"{Url(virtualPath)}\"></script>\r\n");

    private string Url(string virtualPath)
    {
        var url = ToUrl(virtualPath);
        var encoded = HtmlEncoder.Default.Encode(url);
        // an empty bundle (e.g. select2_locale_en-US) is linked with an empty hash by System.Web.Optimization
        return _byPath.TryGetValue(url, out var b) ? $"{encoded}?v={(b.Bytes == 0 ? "" : b.V)}" : encoded;
    }

    public void Map(IEndpointRouteBuilder endpoints)
    {
        foreach (var bundle in All)
        {
            var id = bundle.Path.StartsWith("/content/", StringComparison.OrdinalIgnoreCase) ? "bundles.styles" : "bundles.scripts";
            var body = Content(bundle);
            endpoints.MapMethods(bundle.Path, [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
                {
                    var h = ctx.Response.Headers;
                    h.CacheControl = bundle.CacheControl;
                    // System.Web.Optimization: cacheable for a year (the v= hash changes when the content does)
                    h.Expires = DateTimeOffset.UtcNow.AddYears(1).ToString("R");
                    h.LastModified = Loaded.ToString("R");
                    h.Vary = HeaderNames.UserAgent;
                    return Results.Bytes(body, bundle.ContentType);
                })
                .WithMetadata(new MigratedRouteAttribute(id))
                .ExcludeFromDescription();
        }
    }
}
