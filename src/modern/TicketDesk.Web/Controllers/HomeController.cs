using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketDesk.Localization;
using TicketDesk.Web.Legacy;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>Legacy <c>HomeController</c> (waves 1 and 3): home, about, access denied, culture switch. All anonymous.</summary>
public sealed class HomeController(IOptionsMonitor<SiteOptions> site) : LegacyPageController
{
    // The legacy action has two attribute routes ("" and "index"); each is its own row in routes.csv, so each gets its own
    // endpoint (and switch). Both keep the action name Index so the layout marks the Home menu item active.
    [Route("")]
    [ActionName("Index")]
    [MigratedRoute("home.root")]
    public IActionResult Root() => Index();

    [Route("index")]
    [MigratedRoute("home.index")]
    public IActionResult Index() =>
        site.CurrentValue.HomeEnabled ? View("Index") : new LegacyRedirectResult("/tickets", permanent: true);

    [Route("about")]
    [MigratedRoute("home.about")]
    public IActionResult About() => View();

    /// <summary>Target of the legacy TdAuthorize redirect for a signed-in user without the role (and the new app's AccessDeniedPath).</summary>
    [Route("access-denied")]
    [MigratedRoute("home.access-denied")]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Wave 3: <c>/language?name=xx</c> stores the culture in the <c>_culture</c> cookie for a year and goes back to the
    /// referring page. A POST with the antiforgery token since Round 5 (S3: the language menu's items are forms, no page links
    /// here any more); the legacy app took any verb.
    /// </summary>
    [HttpPost("language")]
    [MigratedRoute("home.language")]
    public IActionResult SetLanguage(string? name)
    {
        // the legacy cookie holds the name as given; the culture filter maps it to an implemented culture on each request
        Response.Cookies.Append(LegacyCulture.CookieName, name ?? "", new CookieOptions
        {
            Path = "/",
            Expires = new DateTimeOffset(DateTime.Today.AddYears(1)),
            IsEssential = true,
        });
        var referrer = Request.Headers.Referer.ToString();
        return Uri.TryCreate(referrer, UriKind.Absolute, out var uri)
            ? new LegacyRedirectResult(uri.AbsoluteUri)
            : new LegacyRedirectResult("/");
    }

    /// <summary>S3 (Round 5): a GET or HEAD of the old link changes nothing: 405 with <c>Allow: POST</c>.</summary>
    [HttpGet("language"), HttpHead("language")]
    [MigratedRoute("home.language")]
    public IActionResult SetLanguageByGet() => this.PostOnly();
}
