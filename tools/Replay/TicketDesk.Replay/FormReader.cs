using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace TicketDesk.Replay;

/// <summary>A form as a browser would submit it: resolved action, method and the successful controls in document order.</summary>
public sealed record ParsedForm(Uri Action, string Method, bool Multipart, List<KeyValuePair<string, string>> Fields, Dictionary<string, List<string>> SelectOptions);

public static class FormReader
{
    public const string FirstOption = "{option:first}";
    public const string LastOption = "{option:last}";

    /// <summary>"{option:first}", "{option:last}" or "{option:N}" (N-th non-empty option, 1-based).</summary>
    public static bool IsOptionToken(string value) =>
        value.StartsWith("{option:", StringComparison.Ordinal) && value.EndsWith('}');

    /// <summary>Picks a non-empty option for an option token; null when the token is out of range.</summary>
    public static string? PickOption(IReadOnlyList<string> options, string token)
    {
        var nonEmpty = options.Where(o => !string.IsNullOrEmpty(o)).ToList();
        var which = token["{option:".Length..^1];
        if (which == "first") return nonEmpty.FirstOrDefault() ?? "";
        if (which == "last") return nonEmpty.LastOrDefault() ?? "";
        if (int.TryParse(which, out var n) && n >= 1) return n <= nonEmpty.Count ? nonEmpty[n - 1] : null;
        throw new ArgumentException($"unknown option token '{token}' (use {{option:first}}, {{option:last}} or {{option:N}})");
    }

    public static ParsedForm Read(string html, Uri pageUri, string selector)
    {
        var doc = new HtmlParser().ParseDocument(html);
        var form = doc.QuerySelector(selector) as IHtmlFormElement
                   ?? throw new InvalidOperationException($"no <form> matches '{selector}' in response of {pageUri}");

        var actionAttr = form.GetAttribute("action");
        var action = string.IsNullOrWhiteSpace(actionAttr) ? pageUri : new Uri(pageUri, actionAttr);
        var method = (form.GetAttribute("method") ?? "GET").ToUpperInvariant();
        var multipart = string.Equals(form.GetAttribute("enctype"), "multipart/form-data", StringComparison.OrdinalIgnoreCase);

        var fields = new List<KeyValuePair<string, string>>();
        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in form.QuerySelectorAll("input, select, textarea"))
        {
            var name = el.GetAttribute("name");
            if (string.IsNullOrEmpty(name) || el.HasAttribute("disabled")) continue;
            switch (el)
            {
                case IHtmlInputElement input:
                    var type = (input.GetAttribute("type") ?? "text").ToLowerInvariant();
                    if (type is "submit" or "button" or "reset" or "image" or "file") continue;
                    if (type is "checkbox" or "radio")
                    {
                        if (input.HasAttribute("checked")) fields.Add(new(name, input.GetAttribute("value") ?? "on"));
                        continue;
                    }
                    fields.Add(new(name, input.GetAttribute("value") ?? ""));
                    break;
                case IHtmlSelectElement select:
                    var opts = select.QuerySelectorAll("option").ToList();
                    options[name] = opts.Select(o => o.GetAttribute("value") ?? o.TextContent).ToList();
                    var selected = opts.Where(o => o.HasAttribute("selected")).ToList();
                    if (selected.Count == 0 && !select.HasAttribute("multiple") && opts.Count > 0) selected.Add(opts[0]);
                    foreach (var o in selected) fields.Add(new(name, o.GetAttribute("value") ?? o.TextContent));
                    break;
                case IHtmlTextAreaElement ta:
                    // The HTML spec strips one leading newline from textarea content; AngleSharp's TextContent already does.
                    fields.Add(new(name, ta.TextContent));
                    break;
            }
        }
        return new ParsedForm(action, method, multipart, fields, options);
    }

    /// <summary>
    /// Applies overrides: replaces every field with that name (or appends it). An option token ("{option:first}",
    /// "{option:last}", "{option:N}") picks that non-empty option of the select and is skipped when the form has no such
    /// select (fields the app hides by role or setting).
    /// </summary>
    public static List<KeyValuePair<string, string>> ApplyOverrides(ParsedForm form, IEnumerable<KeyValuePair<string, string>> overrides)
    {
        var result = form.Fields.ToList();
        foreach (var (name, rawValue) in overrides)
        {
            var value = rawValue;
            if (IsOptionToken(value))
            {
                if (!form.SelectOptions.TryGetValue(name, out var opts)) continue;
                value = PickOption(opts, value)
                        ?? throw new InvalidOperationException($"select '{name}' has no option for {rawValue} ({opts.Count(o => o.Length > 0)} non-empty)");
            }
            var idx = result.FindIndex(f => string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase));
            result.RemoveAll(f => string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx > result.Count) idx = result.Count;
            result.Insert(idx, new(name, value));
        }
        return result;
    }
}
