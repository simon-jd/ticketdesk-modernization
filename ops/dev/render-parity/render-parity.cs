#:project ../../../src/modern/Modules/TicketDesk.Tickets/TicketDesk.Tickets.csproj
// render-parity.cs - the new app's LegacyTextRenderer over the same corpus, compared byte for byte with the legacy output,
// and every difference classified (DOM structure equal? which attributes differ, and are the values the same URL / the same
// style once serialized alike?).
//   dotnet run ops/dev/render-parity/render-parity.cs -- corpus.tsv legacy-out.tsv OUT_DIR
// Writes OUT_DIR/new-out.tsv, OUT_DIR/differences.tsv, OUT_DIR/report.md; exit 1 on any difference outside the known classes.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TicketDesk.Tickets.Rendering;

CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
var (corpus, legacyFile, outDir) = (args[0], args[1], args[2]);
Directory.CreateDirectory(outDir);
var renderer = new LegacyTextRenderer();
var legacy = File.ReadLines(legacyFile).Select(l => l.Split('\t')).ToDictionary(p => p[0], p => (Status: p[1], Value: p[2]));
int same = 0, diff = 0, missing = 0, errSame = 0;
var report = new StringBuilder();
var firstDiffs = new List<string>();
var classes = new Dictionary<string, int>();
var unknown = 0;
using var diffOut = new StreamWriter(Path.Combine(outDir, "differences.tsv"), false, new UTF8Encoding(false)) { NewLine = "\n" };
diffOut.WriteLine("id\tisHtml\tclass\tlegacy\tnew");
var kinds = new Dictionary<string, (int Same, int Diff)>();
using (var w = new StreamWriter(Path.Combine(outDir, "new-out.tsv"), false, new UTF8Encoding(false)) { NewLine = "\n" })
{
    foreach (var line in File.ReadLines(corpus))
    {
        var p = line.Split('\t');
        var text = p[2] == "-" ? null : Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
        string status, value;
        try { status = "OK"; value = renderer.Render(text, p[1] == "1"); }
        catch (Exception e) { status = "ERR"; value = (e.InnerException ?? e).GetType().FullName + ": " + (e.InnerException ?? e).Message; }
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        w.WriteLine($"{p[0]}\t{status}\t{b64}");
        var kind = p[0][0] switch { 't' => "ticket details (baseline data)", 'e' => "event comments (baseline data)", 'g' => "golden journey texts", _ => "tricky inputs" };
        var k = kinds.GetValueOrDefault(kind);
        if (!legacy.TryGetValue(p[0], out var l)) { missing++; continue; }
        if (l.Status == status && l.Value == b64) { same++; kinds[kind] = (k.Same + 1, k.Diff); if (status == "ERR") errSame++; }
        else
        {
            diff++; kinds[kind] = (k.Same, k.Diff + 1);
            var legacyHtml = Encoding.UTF8.GetString(Convert.FromBase64String(l.Value));
            var cls = status == "OK" && l.Status == "OK" ? SanitizerDifference.Classify(legacyHtml, value) : "error";
            classes[cls] = classes.GetValueOrDefault(cls) + 1;
            if (!SanitizerDifference.Known(cls)) unknown++;
            diffOut.WriteLine($"{p[0]}\t{p[1]}\t{cls}\t{Short(legacyHtml)}\t{Short(value)}");
            if (firstDiffs.Count < 20)
                firstDiffs.Add($"- `{p[0]}` (isHtml={p[1]}): legacy {l.Status} `{Short(Encoding.UTF8.GetString(Convert.FromBase64String(l.Value)))}` / new {status} `{Short(value)}`");
        }
    }
}
string Short(string s) => (s.Length > 160 ? s[..160] + "…" : s).Replace("\n", "\\n").Replace("`", "'");
report.AppendLine("# Legacy text rendering parity (Markdown + HtmlSanitizer)").AppendLine();
report.AppendLine($"Corpus `{Path.GetFileName(corpus)}`: {same + diff + missing} cases; byte-equal **{same}**, different **{diff}**, missing in the legacy output {missing}; " +
                  $"{errSame} cases fail identically in both (exception type and message)").AppendLine();
report.AppendLine("| Kind | Byte-equal | Different |").AppendLine("|---|---:|---:|");
foreach (var (kind, (s, d)) in kinds.OrderBy(k => k.Key)) report.AppendLine($"| {kind} | {s} | {d} |");
report.AppendLine().AppendLine("Rendering components of the new app:").AppendLine();
report.AppendLine("- MarkdownSharp 1.13: ported source (`src/modern/Modules/TicketDesk.Tickets/Rendering/MarkdownSharp.cs`)");
var sanitizer = typeof(Ganss.Xss.HtmlSanitizer).Assembly;
report.AppendLine($"- `HtmlSanitizer.dll` {sanitizer.GetName().Version} (package), AngleSharp {typeof(AngleSharp.Dom.INode).Assembly.GetName().Version}");
if (classes.Count > 0)
{
    report.AppendLine().AppendLine("Differences by class (`differences.tsv` lists each):").AppendLine();
    report.AppendLine("| Class | Cases |").AppendLine("|---|---:|");
    foreach (var (c, n) in classes.OrderByDescending(c => c.Value)) report.AppendLine($"| {c} | {n} |");
    report.AppendLine().AppendLine($"Outside the known classes: **{unknown}**");
}
if (firstDiffs.Count > 0) report.AppendLine().AppendLine("First differences:").AppendLine().AppendJoin("\n", firstDiffs).AppendLine();
File.WriteAllText(Path.Combine(outDir, "report.md"), report.ToString());
Console.Write(report);
return unknown == 0 && missing == 0 ? 0 : 1;

/// <summary>
/// Classes of sanitizer differences: the DOM structure (elements, attribute names, text) is the same and only the values of
/// URL attributes (same URL once both are normalized like System.Uri) or of style attributes (same declarations once colors
/// and zero lengths are written alike) differ. Anything else is "structure" (an element, attribute or text added or dropped).
/// </summary>
static class SanitizerDifference
{
    private static readonly string[] UriAttributes = ["action", "background", "dynsrc", "href", "lowsrc", "src"];

    public static bool Known(string cls) => cls.StartsWith("same DOM", StringComparison.Ordinal);

    public static string Classify(string legacy, string current)
    {
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        var a = parser.ParseDocument("<body>" + legacy + "</body>").Body!;
        var b = parser.ParseDocument("<body>" + current + "</body>").Body!;
        var urls = false; var styles = false; var other = false;
        if (!Same(a, b, ref urls, ref styles, ref other)) return "structure: elements, attributes or text added or dropped";
        if (other) return "other attribute values";
        if (urls && styles) return "same DOM: URL and style serialization";
        if (urls) return "same DOM: URL serialization (System.Uri form: trailing slash on a bare host, percent-encoded non-ASCII and <>)";
        if (styles) return "same DOM: style serialization (colors as rgba(), zero lengths without unit)";
        return "same DOM: markup serialization only";
    }

    private static bool Same(AngleSharp.Dom.INode x, AngleSharp.Dom.INode y, ref bool urls, ref bool styles, ref bool other)
    {
        if (x.NodeType != y.NodeType || x.NodeName != y.NodeName) return false;
        if (x.NodeType == AngleSharp.Dom.NodeType.Text) return x.TextContent == y.TextContent;
        if (x is AngleSharp.Dom.IElement ex && y is AngleSharp.Dom.IElement ey)
        {
            var na = ex.Attributes.Select(t => t.Name).Order().ToList();
            if (!na.SequenceEqual(ey.Attributes.Select(t => t.Name).Order())) return false;
            foreach (var name in na)
            {
                var va = ex.GetAttribute(name)!; var vb = ey.GetAttribute(name)!;
                if (va == vb) continue;
                if (UriAttributes.Contains(name) && UrlForm(va) == UrlForm(vb)) urls = true;
                else if (name == "style" && StyleForm(va) == StyleForm(vb)) styles = true;
                else other = true;
            }
        }
        var cx = x.ChildNodes.Where(n => n.NodeType is AngleSharp.Dom.NodeType.Element or AngleSharp.Dom.NodeType.Text).ToList();
        var cy = y.ChildNodes.Where(n => n.NodeType is AngleSharp.Dom.NodeType.Element or AngleSharp.Dom.NodeType.Text).ToList();
        if (cx.Count != cy.Count) return false;
        for (var i = 0; i < cx.Count; i++) if (!Same(cx[i], cy[i], ref urls, ref styles, ref other)) return false;
        return true;
    }

    private static string UrlForm(string v) => Uri.TryCreate(v, UriKind.Absolute, out var u) ? u.AbsoluteUri : v;

    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "0, 0, 0", ["white"] = "255, 255, 255", ["red"] = "255, 0, 0", ["lime"] = "0, 255, 0", ["blue"] = "0, 0, 255",
        ["yellow"] = "255, 255, 0", ["aqua"] = "0, 255, 255", ["fuchsia"] = "255, 0, 255", ["silver"] = "192, 192, 192",
        ["gray"] = "128, 128, 128", ["maroon"] = "128, 0, 0", ["olive"] = "128, 128, 0", ["green"] = "0, 128, 0",
        ["purple"] = "128, 0, 128", ["teal"] = "0, 128, 128", ["navy"] = "0, 0, 128", ["orange"] = "255, 165, 0",
    };

    public static string StyleForm(string v)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(v, @"\brgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", "rgba($1, $2, $3, 1)");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<=:\s*|\s)([a-z]+)(?=\s*(;|$))", m => Named.TryGetValue(m.Value, out var rgb) ? $"rgba({rgb}, 1)" : m.Value);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<![\d.])0px\b", "0");
        return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim().TrimEnd(';');
    }
}
