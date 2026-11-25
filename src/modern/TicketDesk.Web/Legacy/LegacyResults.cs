using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace TicketDesk.Web.Legacy;

/// <summary>
/// A redirect as ASP.NET (System.Web) sends it for MVC 5 <c>Redirect</c>/<c>RedirectToAction</c>: status, <c>Location</c>
/// as given, and the small "Object moved" HTML body with <c>text/html; charset=utf-8</c>. Clients ignore the body, but the
/// replay compares it, and keeping it costs nothing.
/// </summary>
public sealed class LegacyRedirectResult(string url, bool permanent = false) : IActionResult, IResult
{
    public string Url { get; } = url;
    public bool Permanent { get; } = permanent;

    public static string Body(string url) =>
        "<html><head><title>Object moved</title></head><body>\r\n" +
        $"<h2>Object moved to <a href=\"{HtmlAttributeEncode(url)}\">here</a>.</h2>\r\n" +
        "</body></html>\r\n";

    /// <summary>System.Web's HttpUtility.HtmlAttributeEncode: only &amp;, &quot;, &lt; and &#39; are encoded.</summary>
    internal static string HtmlAttributeEncode(string s) =>
        s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace("'", "&#39;");

    public Task ExecuteResultAsync(ActionContext context) => ExecuteAsync(context.HttpContext);

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.StatusCode = Permanent ? (int)HttpStatusCode.MovedPermanently : (int)HttpStatusCode.Found;
        response.Headers.Location = Url;
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(Body(Url), httpContext.RequestAborted);
    }
}
