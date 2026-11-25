using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace TicketDesk.Web.Legacy;

/// <summary>
/// Markup that MVC 5 helpers generated in the legacy views (DropDownList, Ajax.ActionLink, the legacy sortable column
/// header, X.PagedList's unobtrusive-AJAX pager), written out so the new pages produce the same elements and attributes.
/// </summary>
public static class LegacyHtml
{
    private static string E(string? s) => HtmlEncoder.Default.Encode(s ?? "");

    public sealed record Option(string Value, string Text);

    /// <summary>
    /// <c>Html.DropDownList(name, selectList, htmlAttributes)</c>: options separated by CRLF, the selected one first in
    /// attribute order. <paramref name="classAttribute"/> keeps the legacy spelling ("Class" from a dictionary, "class" from
    /// an anonymous object); <paramref name="disabled"/> renders <c>Disabled="True"</c> like the legacy dictionary did.
    /// </summary>
    public static IHtmlContent DropDownList(string name, IEnumerable<Option> options, string? selected, string classAttribute, string classValue, bool disabled = false)
    {
        var sb = new StringBuilder();
        sb.Append($"<select {classAttribute}=\"{E(classValue)}\"");
        if (disabled) sb.Append(" Disabled=\"True\"");
        sb.Append($" id=\"{E(name)}\" name=\"{E(name)}\">");
        foreach (var o in options)
        {
            sb.Append(o.Value == selected ? $"<option selected=\"selected\" value=\"{E(o.Value)}\">" : $"<option value=\"{E(o.Value)}\">");
            sb.Append(E(o.Text)).Append("</option>\r\n");
        }
        sb.Append("</select>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Unobtrusive-AJAX attributes of <c>new AjaxOptions { UpdateTargetId = "ticketList", ... }</c>.</summary>
    public static string AjaxAttributes(string begin, string? method = null) =>
        $"data-ajax=\"true\" data-ajax-begin=\"{begin}\" data-ajax-failure=\"ticketCenter.completeChangeList\"" +
        (method != null ? $" data-ajax-method=\"{method}\"" : "") +
        " data-ajax-mode=\"replace\" data-ajax-success=\"ticketCenter.completeChangeList\" data-ajax-update=\"#ticketList\"";

    /// <summary>
    /// Legacy <c>SortableColumnHeaderHelper.SortableColumnHeader</c>. The header links post (S3, Round 5: <c>tickets/sortList</c>
    /// stores the sort; jquery.unobtrusive-ajax sends <c>data-ajax-method</c>, the page's token goes in the RequestVerificationToken
    /// header, <c>_AntiforgeryAjax</c>).
    /// </summary>
    public static IHtmlContent SortableColumnHeader(string href, string linkText, string title, int? sortIndex, bool? ascending)
    {
        var sb = new StringBuilder();
        sb.Append("<span style='white-space:nowrap;'>\r\n");
        sb.Append($"<a OnMouseDown=\"ticketCenter.sorts.setShiftStatus(event);\" Title=\"{E(title)}\" {AjaxAttributes("ticketCenter.sorts.beginChangeSort", "POST")} href=\"{E(href)}\">{E(linkText)}</a>");
        if (sortIndex != null) sb.Append($"<sup>[{sortIndex}]</sup>");
        sb.Append("\r\n");
        if (ascending != null)
            sb.Append(ascending.Value ? "<img src='/Content/Images/arrow_top.png' alt='Ascending' />" : "<img src='/Content/Images/arrow_down.png' alt='Descending' />");
        sb.Append("\r\n</span>\r\n");
        return new HtmlString(sb.ToString());
    }

    /// <summary>
    /// <c>Html.PagedListPager(list, page => url, PagedListRenderOptions.EnableUnobtrusiveAjaxReplacing(ajaxOptions))</c> with
    /// X.PagedList's defaults: at most 10 page numbers, first/previous/next/last links when needed, ellipses.
    /// </summary>
    public static IHtmlContent Pager(int pageNumber, int pageCount, Func<int, string> url, string ajaxAttributes)
    {
        const int max = 10;
        var sb = new StringBuilder("<div class=\"pagination-container\"><ul class=\"pagination\">");
        var first = 1;
        var count = pageCount;
        if (pageCount > max)
        {
            first = Math.Max(1, pageNumber - max / 2);
            count = max;
            if (first + count - 1 > pageCount) first = pageCount - max + 1;
        }
        var last = first + count - 1;
        string Link(int page, string text, string? cls = null, string? rel = null) =>
            (cls != null ? $"<li class=\"{cls}\">" : "<li>") + $"<a {ajaxAttributes} href=\"{E(url(page))}\"" + (rel != null ? $" rel=\"{rel}\"" : "") + $">{text}</a></li>";

        if (first > 1) sb.Append(Link(1, "««", "PagedList-skipToFirst"));
        if (pageNumber > 1 && pageCount > 0) sb.Append(Link(pageNumber - 1, "«", "PagedList-skipToPrevious", "prev"));
        if (first > 1) sb.Append("<li class=\"disabled PagedList-ellipses\"><a>&#8230;</a></li>");
        for (var p = first; p <= last && pageCount > 0; p++)
            sb.Append(p == pageNumber ? $"<li class=\"active\"><span>{p}</span></li>" : Link(p, p.ToString()));
        if (last < pageCount) sb.Append("<li class=\"disabled PagedList-ellipses\"><a>&#8230;</a></li>");
        if (pageNumber < pageCount) sb.Append(Link(pageNumber + 1, "»", "PagedList-skipToNext", "next"));
        if (last < pageCount) sb.Append(Link(pageCount, "»»", "PagedList-skipToLast"));
        sb.Append("</ul></div>");
        return new HtmlString(sb.ToString());
    }
}
