using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace TicketDesk.Replay;

/// <summary>
/// S3, Round 5: the new app turned the links that changed state (sign-out, the language and project menus, the reset dialog,
/// the ticket list's sort headers) into posts with the antiforgery token. A recorded GET of such a URL is what the user's
/// click sent to the legacy app; against a page that now offers the same URL as a post, the replay sends what that click sends
/// now: the form's fields (its token) for a <c>&lt;form method="post"&gt;</c> whose action is the step's URL and that has
/// nothing to fill in (hidden fields and a button), or, for an AJAX
/// step, a POST (with the page's token as the header, see <see cref="JourneyRunner.AntiforgeryHeader"/>) when a link of the
/// same route is marked <c>data-ajax-method="POST"</c> (jquery.unobtrusive-ajax). Legacy pages have plain links, so a
/// recording made against the legacy app is unchanged.
/// </summary>
public static class PostedLinks
{
    /// <summary>What a click on the step's URL sends now: a POST to the step's URL with these fields (none for AJAX).</summary>
    public sealed record PostTarget(Uri PageUri, List<KeyValuePair<string, string>> Fields);

    /// <summary>The newest of <paramref name="pages"/> (oldest first) that offers <paramref name="target"/> as a post, or null.</summary>
    public static PostTarget? Find(IReadOnlyList<(Uri Uri, string Body)> pages, Uri target, bool ajax, RouteTable routes)
    {
        for (var i = pages.Count - 1; i >= 0; i--)
        {
            var (uri, body) = pages[i];
            var found = ajax ? FindAjaxLink(body, uri, target, routes) : FindForm(body, uri, target);
            if (found != null) return found;
        }
        return null;
    }

    private static PostTarget? FindForm(string html, Uri pageUri, Uri target)
    {
        // cheap test before parsing (every GET step looks through every page of its journey): an action attribute that starts
        // with the target's path, root-relative or absolute, as both apps write them
        if (!html.Contains("action=\"" + target.AbsolutePath, StringComparison.OrdinalIgnoreCase)
            && !html.Contains("action=\"" + target.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase)) return null;
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var form in doc.QuerySelectorAll("form[method]").OfType<IHtmlFormElement>())
        {
            if (!string.Equals(form.GetAttribute("method"), "post", StringComparison.OrdinalIgnoreCase)) continue;
            var action = form.GetAttribute("action");
            if (string.IsNullOrWhiteSpace(action) || !SameUrl(new Uri(pageUri, action), target)) continue;
            // a button, not a form to fill in: only hidden fields (a page's own form, e.g. edit-profile, posting to the page's
            // URL is not a replaced link, and a GET of that page stays a GET)
            if (form.QuerySelectorAll("select, textarea").Length > 0
                || form.QuerySelectorAll("input").Any(i => (i.GetAttribute("type") ?? "text").ToLowerInvariant() is not ("hidden" or "submit" or "button" or "image"))) continue;
            var fields = form.QuerySelectorAll("input[name]").OfType<IHtmlInputElement>()
                .Where(i => (i.GetAttribute("type") ?? "text").Equals("hidden", StringComparison.OrdinalIgnoreCase) && !i.HasAttribute("disabled"))
                .Select(i => new KeyValuePair<string, string>(i.GetAttribute("name")!, i.GetAttribute("value") ?? "")).ToList();
            return new PostTarget(pageUri, fields);
        }
        return null;
    }

    private static PostTarget? FindAjaxLink(string html, Uri pageUri, Uri target, RouteTable routes)
    {
        if (!html.Contains("data-ajax-method", StringComparison.OrdinalIgnoreCase)) return null;
        var route = Route(routes, target);
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var a in doc.QuerySelectorAll("a[href][data-ajax-method]"))
        {
            if (!string.Equals(a.GetAttribute("data-ajax-method"), "post", StringComparison.OrdinalIgnoreCase)) continue;
            if (Route(routes, new Uri(pageUri, a.GetAttribute("href")!)) == route) return new PostTarget(pageUri, []);
        }
        return null;
    }

    private static bool SameUrl(Uri a, Uri b) =>
        a.Authority.Equals(b.Authority, StringComparison.OrdinalIgnoreCase)
        && a.AbsolutePath.Equals(b.AbsolutePath, StringComparison.OrdinalIgnoreCase)
        && a.Query == b.Query;

    private static string Route(RouteTable routes, Uri uri) =>
        uri.Authority.ToLowerInvariant() + " " + (routes.Match("POST", uri.AbsolutePath) ?? RouteTable.Heuristic(uri.AbsolutePath)).ToLowerInvariant();
}
