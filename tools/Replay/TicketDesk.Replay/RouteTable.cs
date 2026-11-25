using System.Text.RegularExpressions;

namespace TicketDesk.Replay;

/// <summary>
/// Maps concrete request paths to route templates ("GET /ticket/{id:int}") so mismatches can be grouped by route.
/// Templates use the ASP.NET attribute-route syntax: literals, {name}, {name:int}, {name?}, {name=default}.
/// Without a table (or with no match) numeric and GUID segments are replaced heuristically.
/// </summary>
public sealed partial class RouteTable
{
    private readonly List<(string Verb, string Template, Segment[] Segments)> _routes = [];

    private sealed record Segment(string? Literal, bool Optional, bool IntOnly, bool CatchAll);

    public int Count => _routes.Count;

    public void Add(string verb, string template)
    {
        var t = template.Trim().Trim('/');
        var segs = t.Length == 0 ? [] : t.Split('/').Select(ParseSegment).ToArray();
        _routes.Add((verb.ToUpperInvariant(), "/" + t, segs));
    }

    private static Segment ParseSegment(string s)
    {
        if (!s.StartsWith('{') || !s.EndsWith('}')) return new Segment(s.ToLowerInvariant(), false, false, false);
        var inner = s[1..^1];
        var catchAll = inner.StartsWith('*');
        var optional = inner.EndsWith('?') || inner.Contains('=') || catchAll;
        return new Segment(null, optional, inner.Contains(":int", StringComparison.OrdinalIgnoreCase), catchAll);
    }

    /// <summary>Loads a CSV with at least "verb" and "route" columns (docs/migration/routes.csv).</summary>
    public static RouteTable FromCsv(string path)
    {
        var table = new RouteTable();
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) return table;
        var header = Csv.Split(lines[0]);
        int vi = header.FindIndex(h => h.Equals("verb", StringComparison.OrdinalIgnoreCase));
        int ri = header.FindIndex(h => h.Equals("route", StringComparison.OrdinalIgnoreCase));
        if (vi < 0 || ri < 0) throw new InvalidDataException($"{path}: needs 'verb' and 'route' columns");
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = Csv.Split(line);
            if (cols.Count <= Math.Max(vi, ri)) continue;
            var route = cols[ri];
            if (route.StartsWith('(')) continue; // "(child action)" etc.
            foreach (var verb in cols[vi].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                table.Add(verb == "ANY" ? "*" : verb, route);
            }
        }
        return table;
    }

    public string Resolve(string method, string pathAndQuery) =>
        $"{method.ToUpperInvariant()} {Match(method, pathAndQuery) ?? Heuristic(pathAndQuery.Split('?', 2)[0])}";

    /// <summary>The best matching template, or null when no route in the table matches.</summary>
    public string? Match(string method, string pathAndQuery)
    {
        var path = pathAndQuery.Split('?', 2)[0];
        var parts = path.Trim('/').Length == 0 ? [] : path.Trim('/').Split('/');
        string? best = null;
        int bestScore = -1;
        foreach (var (verb, template, segs) in _routes)
        {
            if (verb != "*" && !verb.Equals(method, StringComparison.OrdinalIgnoreCase)) continue;
            var score = Match(segs, parts);
            if (score > bestScore) { bestScore = score; best = template; }
        }
        return best;
    }

    /// <summary>Returns a specificity score (literal matches weigh most) or -1 when the template does not match.</summary>
    private static int Match(Segment[] segs, string[] parts)
    {
        int score = 0, i = 0;
        for (; i < segs.Length; i++)
        {
            var s = segs[i];
            if (s.CatchAll) return score + 1;
            if (i >= parts.Length)
            {
                if (s.Optional) continue;
                return -1;
            }
            var p = Uri.UnescapeDataString(parts[i]);
            if (s.Literal != null)
            {
                if (!s.Literal.Equals(p, StringComparison.OrdinalIgnoreCase)) return -1;
                score += 100;
            }
            else if (s.IntOnly)
            {
                if (!p.All(char.IsAsciiDigit) || p.Length == 0) return -1;
                score += 10;
            }
            else score += 1;
        }
        return i >= parts.Length ? score : -1;
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")]
    private static partial Regex GuidSegment();

    public static string Heuristic(string path)
    {
        var segs = path.Split('/').Select(s =>
            s.Length > 0 && s.All(char.IsAsciiDigit) ? "{id}"
            : GuidSegment().IsMatch(s) ? "{guid}"
            : s.ToLowerInvariant());
        var p = string.Join('/', segs);
        return p.Length == 0 ? "/" : p;
    }
}

/// <summary>Minimal RFC 4180 CSV splitting (quoted fields, doubled quotes).</summary>
public static class Csv
{
    public static List<string> Split(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }

    public static string Escape(string? v)
    {
        v ??= "";
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}
