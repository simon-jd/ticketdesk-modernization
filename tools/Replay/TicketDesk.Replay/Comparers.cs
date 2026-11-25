using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace TicketDesk.Replay;

public sealed record DiffEntry(string Kind, string Path, string? Expected, string? Actual);

/// <summary>
/// DOM-level HTML comparison: both documents are parsed (AngleSharp), canonicalized (attributes sorted, class tokens sorted,
/// whitespace collapsed, comments dropped, values normalized) and diffed tree-wise with LCS alignment of child lists.
/// </summary>
public sealed class HtmlDomComparer(TextNormalizer normalizer)
{
    // Hash = hash of the whole canonical subtree; used to align child lists (exact matches first, leftovers paired).
    private abstract record Node(int Hash);
    private sealed record Element(string Name, SortedDictionary<string, string> Attrs, List<Node> Children, int Hash) : Node(Hash);
    private sealed record Text(string Value, int Hash) : Node(Hash);

    public List<DiffEntry> Compare(string expectedHtml, string actualHtml,
        IReadOnlyDictionary<string, string>? expectedVars = null, IReadOnlyDictionary<string, string>? actualVars = null, int maxDiffs = int.MaxValue)
    {
        var a = Canonicalize(expectedHtml, expectedVars);
        var b = Canonicalize(actualHtml, actualVars);
        var diffs = new List<DiffEntry>();
        CompareNodes(a, b, a.Name, diffs, maxDiffs);
        return diffs;
    }

    /// <summary>Canonical text form of a document (handy for debugging and for snapshot files).</summary>
    public string CanonicalText(string html, IReadOnlyDictionary<string, string>? vars = null)
    {
        var sb = new System.Text.StringBuilder();
        Write(Canonicalize(html, vars), 0, sb);
        return sb.ToString();
    }

    private static void Write(Node n, int depth, System.Text.StringBuilder sb)
    {
        var pad = new string(' ', depth * 2);
        switch (n)
        {
            case Text t: sb.Append(pad).Append('"').Append(t.Value).Append("\"\n"); break;
            case Element e:
                sb.Append(pad).Append('<').Append(e.Name);
                foreach (var (k, v) in e.Attrs) sb.Append(' ').Append(k).Append("=\"").Append(v).Append('"');
                sb.Append(">\n");
                foreach (var c in e.Children) Write(c, depth + 1, sb);
                break;
        }
    }

    private Element Canonicalize(string html, IReadOnlyDictionary<string, string>? vars)
    {
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var sel in normalizer.Options.IgnoreSelectors)
        {
            foreach (var el in doc.QuerySelectorAll(sel).ToList()) el.Remove();
        }
        return (Element)Build(doc.DocumentElement, vars)!;
    }

    private Node? Build(INode node, IReadOnlyDictionary<string, string>? vars)
    {
        switch (node.NodeType)
        {
            case NodeType.Element:
                var el = (IElement)node;
                var name = el.LocalName.ToLowerInvariant();
                var attrs = new SortedDictionary<string, string>(StringComparer.Ordinal);
                var isToken = name == "input" && normalizer.Options.Antiforgery &&
                              normalizer.Options.AntiforgeryFieldNames.Contains(el.GetAttribute("name") ?? "", StringComparer.OrdinalIgnoreCase);
                foreach (var attr in el.Attributes)
                {
                    var an = attr.Name.ToLowerInvariant();
                    var v = attr.Value;
                    if (isToken && an == "value") v = "{antiforgery}";
                    else if (an == "class") v = string.Join(' ', v.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
                    else v = normalizer.CollapseWhitespace(normalizer.Normalize(v, vars));
                    attrs[an] = v;
                }
                var children = new List<Node>();
                IEnumerable<INode> kids = el is AngleSharp.Html.Dom.IHtmlTemplateElement t ? t.Content.ChildNodes : el.ChildNodes;
                foreach (var c in kids)
                {
                    var built = Build(c, vars);
                    if (built != null) children.Add(built);
                }
                var h = new HashCode();
                h.Add(name);
                foreach (var (k, v) in attrs) { h.Add(k); h.Add(v); }
                foreach (var c in children) h.Add(c.Hash);
                return new Element(name, attrs, children, h.ToHashCode());
            case NodeType.Text:
                var text = normalizer.CollapseWhitespace(normalizer.Normalize(node.TextContent, vars));
                return text.Length == 0 ? null : new Text(text, HashCode.Combine("#text", text));
            default:
                return null; // comments, processing instructions, doctype
        }
    }

    private static void CompareNodes(Element a, Element b, string path, List<DiffEntry> diffs, int max)
    {
        if (diffs.Count >= max) return;
        if (a.Name != b.Name)
        {
            diffs.Add(new DiffEntry("element", path, Describe(a), Describe(b)));
            return;
        }
        foreach (var key in a.Attrs.Keys.Union(b.Attrs.Keys).Order(StringComparer.Ordinal))
        {
            a.Attrs.TryGetValue(key, out var av);
            b.Attrs.TryGetValue(key, out var bv);
            if (av != bv) { diffs.Add(new DiffEntry("attribute", $"{path}/@{key}", av, bv)); if (diffs.Count >= max) return; }
        }

        var align = Align(a.Children, b.Children);
        var counters = new Dictionary<string, int>();
        foreach (var (x, y) in align)
        {
            if (diffs.Count >= max) return;
            var basis = x ?? y!;
            var label = basis is Element be ? be.Name : "text()";
            counters[label] = counters.GetValueOrDefault(label) + 1;
            var childPath = $"{path}/{label}[{counters[label]}]";
            switch (x, y)
            {
                case (Element ex, Element ey): CompareNodes(ex, ey, childPath, diffs, max); break;
                case (Text tx, Text ty) when tx.Value != ty.Value: diffs.Add(new DiffEntry("text", childPath, tx.Value, ty.Value)); break;
                case (Text, Text): break;
                case (not null, null): diffs.Add(new DiffEntry("removed", childPath, Describe(x), null)); break;
                case (null, not null): diffs.Add(new DiffEntry("added", childPath, null, Describe(y))); break;
                default: diffs.Add(new DiffEntry("node", childPath, Describe(x!), Describe(y!))); break;
            }
        }
    }

    private static int KeyOf(Node n) => n.Hash;

    /// <summary>LCS alignment on subtree hashes; adjacent unmatched runs are paired so a changed text/attribute reads as a change, not remove+add.</summary>
    private static List<(Node? A, Node? B)> Align(List<Node> a, List<Node> b)
    {
        int n = a.Count, m = b.Count;
        var result = new List<(Node?, Node?)>(Math.Max(n, m));
        if ((long)n * m > 4_000_000) // pathological sizes: fall back to index pairing
        {
            for (int i = 0; i < Math.Max(n, m); i++) result.Add((i < n ? a[i] : null, i < m ? b[i] : null));
            return result;
        }
        var lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = KeyOf(a[i]) == KeyOf(b[j]) ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        int p = 0, q = 0;
        var pendA = new List<Node>();
        var pendB = new List<Node>();
        void Flush()
        {
            int k = 0;
            for (; k < Math.Min(pendA.Count, pendB.Count); k++) result.Add((pendA[k], pendB[k]));
            for (int r = k; r < pendA.Count; r++) result.Add((pendA[r], null));
            for (int r = k; r < pendB.Count; r++) result.Add((null, pendB[r]));
            pendA.Clear(); pendB.Clear();
        }
        while (p < n && q < m)
        {
            if (KeyOf(a[p]) == KeyOf(b[q])) { Flush(); result.Add((a[p], b[q])); p++; q++; }
            else if (lcs[p + 1, q] >= lcs[p, q + 1]) pendA.Add(a[p++]);
            else pendB.Add(b[q++]);
        }
        while (p < n) pendA.Add(a[p++]);
        while (q < m) pendB.Add(b[q++]);
        Flush();
        return result;
    }

    private static string Describe(Node n)
    {
        var s = n switch
        {
            Text t => "\"" + t.Value + "\"",
            Element e => "<" + e.Name + string.Concat(e.Attrs.Take(4).Select(kv => $" {kv.Key}=\"{kv.Value}\"")) + ">" + Inner(e),
            _ => "?",
        };
        return s.Length > 300 ? s[..300] + "…" : s;
    }

    private static string Inner(Element e)
    {
        var texts = new List<string>();
        void Walk(Node n) { if (n is Text t) texts.Add(t.Value); else if (n is Element x) foreach (var c in x.Children) Walk(c); }
        Walk(e);
        var joined = string.Join(' ', texts);
        return joined.Length > 120 ? joined[..120] + "…" : joined;
    }
}

/// <summary>Semantic JSON comparison: object key order ignored, numbers compared by value, strings normalized.</summary>
public sealed class JsonSemanticComparer(TextNormalizer normalizer)
{
    public List<DiffEntry> Compare(string expected, string actual,
        IReadOnlyDictionary<string, string>? expectedVars = null, IReadOnlyDictionary<string, string>? actualVars = null, int maxDiffs = int.MaxValue)
    {
        var diffs = new List<DiffEntry>();
        JsonNode? a, b;
        try { a = JsonNode.Parse(expected); } catch (JsonException ex) { return [new DiffEntry("json-parse", "$", "invalid baseline JSON: " + ex.Message, null)]; }
        try { b = JsonNode.Parse(actual); } catch (JsonException ex) { return [new DiffEntry("json-parse", "$", null, "invalid JSON: " + ex.Message)]; }
        Walk(a, b, "$", diffs, maxDiffs, expectedVars, actualVars);
        return diffs;
    }

    private void Walk(JsonNode? a, JsonNode? b, string path, List<DiffEntry> diffs, int max,
        IReadOnlyDictionary<string, string>? va, IReadOnlyDictionary<string, string>? vb)
    {
        if (diffs.Count >= max) return;
        switch (a, b)
        {
            case (null, null): return;
            case (JsonObject oa, JsonObject ob):
                foreach (var key in oa.Select(kv => kv.Key).Union(ob.Select(kv => kv.Key)).Order(StringComparer.Ordinal))
                {
                    var hasA = oa.TryGetPropertyValue(key, out var ca);
                    var hasB = ob.TryGetPropertyValue(key, out var cb);
                    if (!hasA) diffs.Add(new DiffEntry("json-added", $"{path}.{key}", null, Short(cb)));
                    else if (!hasB) diffs.Add(new DiffEntry("json-removed", $"{path}.{key}", Short(ca), null));
                    else Walk(ca, cb, $"{path}.{key}", diffs, max, va, vb);
                    if (diffs.Count >= max) return;
                }
                return;
            case (JsonArray xa, JsonArray xb):
                for (int i = 0; i < Math.Max(xa.Count, xb.Count); i++)
                {
                    if (i >= xa.Count) diffs.Add(new DiffEntry("json-added", $"{path}[{i}]", null, Short(xb[i])));
                    else if (i >= xb.Count) diffs.Add(new DiffEntry("json-removed", $"{path}[{i}]", Short(xa[i]), null));
                    else Walk(xa[i], xb[i], $"{path}[{i}]", diffs, max, va, vb);
                    if (diffs.Count >= max) return;
                }
                return;
            case (JsonValue sa, JsonValue sb):
                var (na, nb) = (Scalar(sa, va), Scalar(sb, vb));
                if (na != nb) diffs.Add(new DiffEntry("json-value", path, na, nb));
                return;
            default:
                diffs.Add(new DiffEntry("json-type", path, Short(a), Short(b)));
                return;
        }
    }

    private string Scalar(JsonValue v, IReadOnlyDictionary<string, string>? vars)
    {
        var el = v.GetValue<JsonElement>();
        return el.ValueKind switch
        {
            JsonValueKind.String => "\"" + normalizer.Normalize(el.GetString(), vars) + "\"",
            JsonValueKind.Number => decimal.TryParse(el.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d.ToString("G29", CultureInfo.InvariantCulture) : el.GetRawText(),
            _ => el.GetRawText(),
        };
    }

    private static string? Short(JsonNode? n)
    {
        var s = n?.ToJsonString() ?? "null";
        return s.Length > 300 ? s[..300] + "…" : s;
    }
}
