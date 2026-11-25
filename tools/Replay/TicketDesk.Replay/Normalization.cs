using System.Text.RegularExpressions;

namespace TicketDesk.Replay;

public sealed class NormalizerOptions
{
    public bool Guids { get; init; } = true;
    public bool Timestamps { get; init; } = true;
    public bool RelativeDates { get; init; } = true;
    public bool CacheBusters { get; init; } = true;
    public bool Variables { get; init; } = true;
    public bool Antiforgery { get; init; } = true;
    public bool Whitespace { get; init; } = true;

    /// <summary>Hidden-input names whose values are per-request tokens (MVC 5 and ASP.NET Core both use this name).</summary>
    public List<string> AntiforgeryFieldNames { get; init; } = ["__RequestVerificationToken"];

    /// <summary>Elements removed before an HTML comparison (CSS selectors).</summary>
    public List<string> IgnoreSelectors { get; init; } = [];

    /// <summary>Response headers that are compared (others are recorded but ignored).</summary>
    public List<string> ComparedHeaders { get; init; } =
        ["Content-Type", "Location", "Cache-Control", "Content-Disposition", "X-Frame-Options", "X-Content-Type-Options", "X-XSS-Protection"];

    /// <summary>Compare cookie names and attributes (never values) set via Set-Cookie.</summary>
    public bool CompareSetCookie { get; init; } = true;

    /// <summary>Variable values shorter than this are not substituted (a ticket id "7" would match too much text).</summary>
    public int MinVariableLength { get; init; } = 2;
}

/// <summary>
/// String-level normalizers applied to text nodes, attribute values, JSON strings and headers.
/// Each replaces run-specific values with a stable placeholder so two runs (or two apps) compare equal.
/// </summary>
public sealed partial class TextNormalizer(NormalizerOptions options)
{
    public NormalizerOptions Options { get; } = options;

    private const string Mon = "(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\\.?";
    private const string Day = "(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)[a-z]*";

    [GeneratedRegex(@"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}")]
    private static partial Regex GuidRx();

    [GeneratedRegex(@"\b" + Day + @", \d{1,2} " + Mon + @" \d{4} \d{2}:\d{2}:\d{2} GMT\b")]
    private static partial Regex HttpDateRx();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?")]
    private static partial Regex IsoDateTimeRx();

    [GeneratedRegex(@"/Date\(-?\d+(?:[+-]\d{4})?\)/")]
    private static partial Regex MsJsonDateRx();

    [GeneratedRegex(@"\b(?:\d{1,2}/\d{1,2}/\d{2,4}|\d{1,2}\.\d{1,2}\.\d{4})(?<time>,? \d{1,2}:\d{2}(?::\d{2})?(?: ?[AaPp]\.?[Mm]\.?)?)?")]
    private static partial Regex NumericDateRx();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDateRx();

    [GeneratedRegex(@"\b(?:" + Day + @",? )?" + Mon + @" \d{1,2},? \d{4}(?<time>,? (?:at )?\d{1,2}:\d{2}(?::\d{2})?(?: ?[AaPp][Mm])?)?")]
    private static partial Regex LongDateRx();

    [GeneratedRegex(@"\b(?:" + Day + @",? )?\d{1,2}\.? " + Mon + @" \d{4}\b")]
    private static partial Regex DayMonthYearRx();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2})? ?[AaPp][Mm]\b|\b\d{2}:\d{2}:\d{2}\b")]
    private static partial Regex TimeRx();

    [GeneratedRegex(@"\b(?:\d+|an?|one|a few) (?:second|minute|hour|day|week|month|year)s? ago\b|\bjust now\b|\bin (?:\d+|an?|a few) (?:second|minute|hour|day|week|month|year)s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeRx();

    [GeneratedRegex(@"(?<k>[?&](?:v|_|t|ver|version|cb)=)[A-Za-z0-9_\-%.+/=]+")]
    private static partial Regex CacheBusterRx();

    public string Normalize(string? input, IReadOnlyDictionary<string, string>? vars = null)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";
        var s = input;
        if (Options.CacheBusters) s = CacheBusterRx().Replace(s, "${k}{v}");
        if (Options.Guids) s = GuidRx().Replace(s, "{guid}");
        if (Options.Timestamps)
        {
            s = HttpDateRx().Replace(s, "{httpdate}");
            s = IsoDateTimeRx().Replace(s, "{datetime}");
            s = MsJsonDateRx().Replace(s, "{datetime}");
            s = NumericDateRx().Replace(s, m => m.Groups["time"].Success ? "{datetime}" : "{date}");
            s = LongDateRx().Replace(s, m => m.Groups["time"].Success ? "{datetime}" : "{date}");
            s = DayMonthYearRx().Replace(s, "{date}");
            s = IsoDateRx().Replace(s, "{date}");
            s = TimeRx().Replace(s, "{time}");
        }
        if (Options.RelativeDates) s = RelativeRx().Replace(s, "{relative}");
        if (Options.Variables && vars is { Count: > 0 }) s = ReplaceVariables(s, vars);
        return s;
    }

    private string ReplaceVariables(string s, IReadOnlyDictionary<string, string> vars)
    {
        foreach (var (name, value) in vars.Where(v => v.Value.Length >= Options.MinVariableLength).OrderByDescending(v => v.Value.Length))
        {
            if (GuidRx().IsMatch(value) && Options.Guids) continue; // already {guid}
            s = Regex.Replace(s, $@"(?<![A-Za-z0-9]){Regex.Escape(value)}(?![A-Za-z0-9])", "{var:" + name + "}");
            var encoded = Uri.EscapeDataString(value);
            if (encoded != value) s = s.Replace(encoded, "{var:" + name + "}", StringComparison.Ordinal);
        }
        return s;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Ws();

    public string CollapseWhitespace(string s) => Options.Whitespace ? Ws().Replace(s, " ").Trim() : s;

    /// <summary>Normalizes a Location header: drops scheme/host so legacy and new hosts compare, then applies the string normalizers.</summary>
    public string NormalizeLocation(string? location, string target, IReadOnlyDictionary<string, string>? vars)
    {
        if (string.IsNullOrEmpty(location)) return "";
        var loc = location;
        if (Uri.TryCreate(location, UriKind.Absolute, out var abs) && Uri.TryCreate(target, UriKind.Absolute, out var t)
            && abs.Authority.Equals(t.Authority, StringComparison.OrdinalIgnoreCase))
        {
            loc = abs.PathAndQuery;
        }
        return Normalize(loc, vars);
    }

    /// <summary>Set-Cookie reduced to "name; attr; attr" (lower-case, sorted, without value/expires/max-age).</summary>
    public static string NormalizeSetCookie(string setCookie)
    {
        var parts = setCookie.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0].Split('=', 2)[0];
        var attrs = parts.Skip(1)
            .Select(p => p.Split('=', 2))
            .Where(kv => !kv[0].Equals("expires", StringComparison.OrdinalIgnoreCase) && !kv[0].Equals("max-age", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Length == 2 ? $"{kv[0].ToLowerInvariant()}={kv[1].ToLowerInvariant()}" : kv[0].ToLowerInvariant())
            .OrderBy(a => a, StringComparer.Ordinal);
        return string.Join("; ", new[] { name }.Concat(attrs));
    }
}
