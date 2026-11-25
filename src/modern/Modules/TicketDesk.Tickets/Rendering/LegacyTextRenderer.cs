using AngleSharp.Css.Dom;
using Ganss.Xss;
using TicketDesk.Tickets.Rendering.MarkdownSharp;

namespace TicketDesk.Tickets.Rendering;

/// <summary>
/// Ticket details and event comments as the legacy views rendered them (<c>TicketExtensions.HtmlDetails</c>,
/// <c>TicketEventExtensions.HtmlComment</c>): Markdown (MarkdownSharp 1.13 with the legacy options; a byte-equal port in
/// <c>MarkdownSharp.cs</c>, because the DLL misbehaves under ICU) unless the text is HTML, then an HTML sanitizer.
/// <para>
/// The sanitizer is a current Ganss.Xss HtmlSanitizer (a normal package that NuGet audit sees; plan/DECISIONS.md
/// 2026-11-10), not the legacy 3.2.103 (4 advisories). It is configured with the legacy version's default allow-lists
/// (tags, attributes, style properties, at-rules, URI schemes and URI attributes, read from the 3.2.103 DLL), so what
/// still differs is the newer version's own behaviour: expected difference R1, measured over the 55,829 texts of the
/// baseline data (<c>docs/evidence/tickets-20261110/sanitizer/</c>).
/// </para>
/// </summary>
public sealed class LegacyTextRenderer
{
    /// <summary>HtmlSanitizer 3.2.103 defaults: the legacy app sanitized with <c>new HtmlSanitizer()</c>.</summary>
    public static class LegacyAllowList
    {
        public static readonly string[] Tags =
        [
            "a", "abbr", "acronym", "address", "area", "article", "aside", "b", "bdi", "big",
            "blockquote", "body", "br", "button", "caption", "center", "cite", "code", "col", "colgroup",
            "data", "datalist", "dd", "del", "details", "dfn", "dir", "div", "dl", "dt",
            "em", "fieldset", "figcaption", "figure", "font", "footer", "form", "h1", "h2", "h3",
            "h4", "h5", "h6", "head", "header", "hr", "html", "i", "img", "input",
            "ins", "kbd", "keygen", "label", "legend", "li", "main", "map", "mark", "menu",
            "menuitem", "meter", "nav", "ol", "optgroup", "option", "output", "p", "pre", "progress",
            "q", "rp", "rt", "ruby", "s", "samp", "section", "select", "small", "span",
            "strike", "strong", "sub", "summary", "sup", "table", "tbody", "td", "textarea", "tfoot",
            "th", "thead", "time", "tr", "tt", "u", "ul", "var", "wbr"
        ];

        public static readonly string[] Attributes =
        [
            "abbr", "accept", "accept-charset", "accesskey", "action", "align", "alt", "autocomplete", "autosave", "axis",
            "bgcolor", "border", "cellpadding", "cellspacing", "challenge", "char", "charoff", "charset", "checked", "cite",
            "clear", "color", "cols", "colspan", "compact", "contenteditable", "coords", "datetime", "dir", "disabled",
            "draggable", "dropzone", "enctype", "for", "frame", "headers", "height", "high", "href", "hreflang",
            "hspace", "ismap", "keytype", "label", "lang", "list", "longdesc", "low", "max", "maxlength",
            "media", "method", "min", "multiple", "name", "nohref", "noshade", "novalidate", "nowrap", "open",
            "optimum", "pattern", "placeholder", "prompt", "pubdate", "radiogroup", "readonly", "rel", "required", "rev",
            "reversed", "rows", "rowspan", "rules", "scope", "selected", "shape", "size", "span", "spellcheck",
            "src", "start", "step", "style", "summary", "tabindex", "target", "title", "type", "usemap",
            "valign", "value", "vspace", "width", "wrap"
        ];

        public static readonly string[] StyleProperties =
        [
            "background", "background-attachment", "background-color", "background-image", "background-position", "background-repeat",
            "border", "border-bottom", "border-bottom-color", "border-bottom-style", "border-bottom-width", "border-collapse",
            "border-color", "border-left", "border-left-color", "border-left-style", "border-left-width", "border-right",
            "border-right-color", "border-right-style", "border-right-width", "border-spacing", "border-style", "border-top",
            "border-top-color", "border-top-style", "border-top-width", "border-width", "bottom", "caption-side",
            "clear", "clip", "color", "content", "counter-increment", "counter-reset",
            "cursor", "direction", "display", "empty-cells", "float", "font",
            "font-family", "font-size", "font-style", "font-variant", "font-weight", "height",
            "left", "letter-spacing", "line-height", "list-style", "list-style-image", "list-style-position",
            "list-style-type", "margin", "margin-bottom", "margin-left", "margin-right", "margin-top",
            "max-height", "max-width", "min-height", "min-width", "opacity", "orphans",
            "outline", "outline-color", "outline-style", "outline-width", "overflow", "padding",
            "padding-bottom", "padding-left", "padding-right", "padding-top", "page-break-after", "page-break-before",
            "page-break-inside", "quotes", "right", "table-layout", "text-align", "text-decoration",
            "text-indent", "text-transform", "top", "unicode-bidi", "vertical-align", "visibility",
            "white-space", "widows", "width", "word-spacing", "z-index"
        ];

        public static readonly string[] Schemes = ["http", "https"];

        public static readonly string[] UriAttributes = ["action", "background", "dynsrc", "href", "lowsrc", "src"];

        public static readonly CssRuleType[] AtRules = [CssRuleType.Namespace, CssRuleType.Style];
    }

    private static readonly HtmlSanitizerOptions Options = new()
    {
        AllowedTags = new HashSet<string>(LegacyAllowList.Tags, StringComparer.OrdinalIgnoreCase),
        AllowedAttributes = new HashSet<string>(LegacyAllowList.Attributes, StringComparer.OrdinalIgnoreCase),
        AllowedCssProperties = new HashSet<string>(LegacyAllowList.StyleProperties, StringComparer.OrdinalIgnoreCase),
        AllowedSchemes = new HashSet<string>(LegacyAllowList.Schemes, StringComparer.OrdinalIgnoreCase),
        UriAttributes = new HashSet<string>(LegacyAllowList.UriAttributes, StringComparer.OrdinalIgnoreCase),
        AllowedAtRules = new HashSet<CssRuleType>(LegacyAllowList.AtRules),
    };

    /// <summary><c>Ticket.HtmlDetails()</c> / <c>TicketEvent.HtmlComment()</c>.</summary>
    public string Render(string? content, bool isHtml) => Sanitize(isHtml ? content : HtmlFromMarkdown(content));

    /// <summary>Legacy <c>StringExtensions.HtmlFromMarkdown</c> (a new converter per call, as before: it is not thread-safe).</summary>
    public static string HtmlFromMarkdown(string? content)
    {
        var md = new Markdown(new MarkdownOptions
        {
            AutoHyperlink = true,
            AutoNewLines = true,
            EncodeProblemUrlCharacters = true,
            LinkEmails = true,
        });
        return md.Transform(content!); // MarkdownSharp returns "" for null or empty text
    }

    /// <summary>The configured sanitizer, a new instance per call (cheap; nothing is shared between requests).</summary>
    public static string Sanitize(string? html) => string.IsNullOrEmpty(html) ? "" : new HtmlSanitizer(Options).Sanitize(html);
}
