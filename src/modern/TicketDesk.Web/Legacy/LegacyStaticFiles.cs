using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace TicketDesk.Web.Legacy;

public sealed record LegacyStaticFile(string Path, string ContentType, byte[] Body, string ETag);

/// <summary>
/// The legacy site's plain static files that the migrated pages still reference: the fonts and images the bundled CSS points
/// at (Bootstrap 3's Glyphicons, Font Awesome 4.4, the select2 sprites, the mobile logo), Dropzone's default thumbnail,
/// <c>favicon.ico</c> and <c>robots.txt</c>. They are not routes of the inventory; during coexistence the YARP fallback fetched
/// them from the legacy app. Since the legacy app is retired they are a byte-identical copy (<c>LegacyStatic/</c>, from
/// <c>TicketDesk/TicketDesk.Web.Client</c>, see its README) embedded in this assembly and served at the same URLs (routing
/// matches literal paths case-insensitively, as IIS did: the CSS asks for <c>/content/Images/...</c> and <c>/Content/css/...</c>).
/// </summary>
public sealed class LegacyStaticFiles
{
    private const string ResourcePrefix = "TicketDesk.Web.LegacyStatic/";
    private static readonly DateTimeOffset Loaded = DateTimeOffset.UtcNow;

    public IReadOnlyList<LegacyStaticFile> All { get; }

    public LegacyStaticFiles()
    {
        var asm = Assembly.GetExecutingAssembly();
        var types = new FileExtensionContentTypeProvider();
        var files = new List<LegacyStaticFile>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var path = "/" + name[ResourcePrefix.Length..].Replace('\\', '/');
            using var s = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var body = ms.ToArray();
            var type = types.TryGetContentType(path, out var t) ? t : "application/octet-stream";
            files.Add(new LegacyStaticFile(path, type, body, "\"" + Convert.ToHexStringLower(SHA256.HashData(body))[..16] + "\""));
        }
        All = files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
    }

    public void Map(IEndpointRouteBuilder endpoints)
    {
        foreach (var file in All)
        {
            endpoints.MapMethods(file.Path, [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
                {
                    ctx.Response.Headers.CacheControl = "public, max-age=86400";
                    ctx.Response.Headers.XContentTypeOptions = "nosniff";
                    return Results.Bytes(file.Body, file.ContentType, lastModified: Loaded, entityTag: EntityTagHeaderValue.Parse(file.ETag));
                })
                .ExcludeFromDescription();
        }
    }
}
