using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TicketDesk.Replay;

public sealed record ExchangeMismatch
{
    public required string Key { get; init; }
    public required string Journey { get; init; }
    public required string Step { get; init; }
    public required string RouteKey { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public List<string> Kinds { get; init; } = [];
    public List<DiffEntry> Diffs { get; init; } = [];
}

public sealed record RouteSummary(string RouteKey, int Requests, int Mismatches, Dictionary<string, int> Kinds);

public sealed record RunInfo(string File, string Target, string RunId, int Exchanges, int Errors);

public sealed record CompareReport
{
    public DateTimeOffset Generated { get; init; } = DateTimeOffset.UtcNow;
    public required RunInfo Baseline { get; init; }
    public required RunInfo Candidate { get; init; }
    public int Compared { get; init; }
    public int Matched { get; init; }
    public int Mismatched { get; init; }
    public int MissingInCandidate { get; init; }
    public int ExtraInCandidate { get; init; }
    public List<RouteSummary> Routes { get; init; } = [];
    public List<ExchangeMismatch> Mismatches { get; init; } = [];
    public List<string> Errors { get; init; } = [];
}

/// <summary>Pairs exchanges of two runs (journey, step, redirect hop) and compares status, Location, selected headers and normalized bodies.</summary>
public sealed class ExchangeComparer
{
    private readonly TextNormalizer _text;
    private readonly HtmlDomComparer _html;
    private readonly JsonSemanticComparer _json;
    private readonly NormalizerOptions _options;

    /// <summary>
    /// Every diff of an exchange is collected and kept in the report (report.json), so whatever classifies the mismatches
    /// (ops/dev/replay-summary.py, the CI gate) sees all of them. Until Round 5b the HTML and JSON walks stopped at 25 diffs per
    /// exchange, which hid diffs beyond the 25th from the gate; only the Markdown report shortens the list now
    /// (<see cref="ReportWriter.WriteAsync"/>).
    /// </summary>
    public ExchangeComparer(NormalizerOptions? options = null)
    {
        _options = options ?? new NormalizerOptions();
        _text = new TextNormalizer(_options);
        _html = new HtmlDomComparer(_text);
        _json = new JsonSemanticComparer(_text);
    }

    public CompareReport Compare(IReadOnlyList<RecordedExchange> baseline, IReadOnlyList<RecordedExchange> candidate,
        string baselineFile = "", string candidateFile = "")
    {
        var cand = new Dictionary<string, RecordedExchange>();
        foreach (var c in candidate) cand.TryAdd(c.MatchKey, c);
        var seen = new HashSet<string>();
        var mismatches = new List<ExchangeMismatch>();
        var routeStats = new Dictionary<string, RouteStat>();
        int compared = 0, missing = 0, matched = 0;

        foreach (var b in baseline)
        {
            seen.Add(b.MatchKey);
            if (!routeStats.TryGetValue(b.RouteKey, out var stat)) routeStats[b.RouteKey] = stat = new RouteStat();
            stat.Req++;
            List<DiffEntry> diffs;
            if (!cand.TryGetValue(b.MatchKey, out var c))
            {
                missing++;
                diffs = [new DiffEntry("missing", b.MatchKey, $"{b.Request.Method} {b.Request.Path}", "not present in candidate run")];
            }
            else
            {
                compared++;
                diffs = CompareOne(b, c);
                if (diffs.Count == 0) matched++;
            }
            if (diffs.Count > 0)
            {
                var kinds = diffs.Select(d => Category(d.Kind)).Distinct().ToList();
                mismatches.Add(new ExchangeMismatch
                {
                    Key = b.MatchKey, Journey = b.Journey, Step = b.StepName, RouteKey = b.RouteKey,
                    Method = b.Request.Method, Path = b.Request.Path, Kinds = kinds, Diffs = diffs,
                });
                stat.Mis++;
                foreach (var k in kinds) stat.Kinds[k] = stat.Kinds.GetValueOrDefault(k) + 1;
            }
        }

        var extras = candidate.Where(c => !seen.Contains(c.MatchKey)).ToList();
        foreach (var e in extras)
        {
            mismatches.Add(new ExchangeMismatch
            {
                Key = e.MatchKey, Journey = e.Journey, Step = e.StepName, RouteKey = e.RouteKey, Method = e.Request.Method,
                Path = e.Request.Path, Kinds = ["extra"], Diffs = [new DiffEntry("extra", e.MatchKey, null, $"{e.Request.Method} {e.Request.Path}")],
            });
        }

        return new CompareReport
        {
            Baseline = Info(baselineFile, baseline),
            Candidate = Info(candidateFile, candidate),
            Compared = compared,
            Matched = matched,
            Mismatched = mismatches.Count,
            MissingInCandidate = missing,
            ExtraInCandidate = extras.Count,
            Routes = routeStats.Select(kv => new RouteSummary(kv.Key, kv.Value.Req, kv.Value.Mis, kv.Value.Kinds))
                .OrderByDescending(r => r.Mismatches).ThenBy(r => r.RouteKey, StringComparer.Ordinal).ToList(),
            Mismatches = mismatches,
            Errors = baseline.Concat(candidate).Where(x => x.Error != null).Select(x => $"{x.Target} {x.MatchKey}: {x.Error}").ToList(),
        };
    }

    private sealed class RouteStat { public int Req; public int Mis; public Dictionary<string, int> Kinds { get; } = new(); }

    private static RunInfo Info(string file, IReadOnlyList<RecordedExchange> run) =>
        new(file, run.FirstOrDefault()?.Target ?? "", run.FirstOrDefault()?.RunId ?? "", run.Count, run.Count(x => x.Error != null));

    private static string Category(string kind) => kind switch
    {
        "element" or "attribute" or "text" or "removed" or "added" or "node" => "body-html",
        _ when kind.StartsWith("json", StringComparison.Ordinal) => "body-json",
        _ => kind,
    };

    public List<DiffEntry> CompareOne(RecordedExchange expected, RecordedExchange actual)
    {
        var diffs = new List<DiffEntry>();
        if (expected.Error != null || actual.Error != null)
        {
            if (expected.Error != actual.Error) diffs.Add(new DiffEntry("error", expected.MatchKey, expected.Error, actual.Error));
        }
        var ra = expected.Response;
        var rb = actual.Response;
        if (ra == null || rb == null)
        {
            if (ra != rb) diffs.Add(new DiffEntry("missing-response", expected.MatchKey, ra == null ? null : "response", rb == null ? null : "response"));
            return diffs;
        }
        if (ra.Status != rb.Status) diffs.Add(new DiffEntry("status", "status", ra.Status.ToString(), rb.Status.ToString()));

        foreach (var h in _options.ComparedHeaders)
        {
            var va = Header(ra, h);
            var vb = Header(rb, h);
            if (h.Equals("Location", StringComparison.OrdinalIgnoreCase))
            {
                va = _text.NormalizeLocation(va, expected.Target, expected.Variables);
                vb = _text.NormalizeLocation(vb, actual.Target, actual.Variables);
                if (va != vb) diffs.Add(new DiffEntry("location", "Location", va, vb));
                continue;
            }
            if (h.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                va = va?.Replace(" ", "").ToLowerInvariant();
                vb = vb?.Replace(" ", "").ToLowerInvariant();
            }
            if (va != vb) diffs.Add(new DiffEntry("header", h, va, vb));
        }
        if (_options.CompareSetCookie)
        {
            var ca = SetCookies(ra);
            var cb = SetCookies(rb);
            if (ca != cb) diffs.Add(new DiffEntry("set-cookie", "Set-Cookie", ca, cb));
        }

        diffs.AddRange(CompareBodies(expected, actual, ra, rb));
        return diffs;
    }

    private IEnumerable<DiffEntry> CompareBodies(RecordedExchange ea, RecordedExchange eb, RecordedResponse ra, RecordedResponse rb)
    {
        var ma = MediaType(ra.ContentType);
        var mb = MediaType(rb.ContentType);
        if (ra.BodyIsBase64 || rb.BodyIsBase64)
        {
            var ha = Hash(ra);
            var hb = Hash(rb);
            return ha == hb ? [] : [new DiffEntry("body-binary", "body", $"{ra.BodyLength} B sha256={ha}", $"{rb.BodyLength} B sha256={hb}")];
        }
        var a = ra.Body ?? "";
        var b = rb.Body ?? "";
        if (a.Length == 0 && b.Length == 0) return [];
        if (ma is "text/html" or "application/xhtml+xml" && mb == ma)
            return _html.Compare(a, b, ea.Variables, eb.Variables);
        // A JSON content type with a body that is not JSON (e.g. legacy MVC rendering its HTML error view after the action
        // had set application/json) is compared as text, so two identical bodies still match.
        if (ma != null && ma.Contains("json") && mb != null && mb.Contains("json") && IsJson(a) && IsJson(b))
            return _json.Compare(a, b, ea.Variables, eb.Variables);
        var na = _text.Normalize(a, ea.Variables).ReplaceLineEndings("\n");
        var nb = _text.Normalize(b, eb.Variables).ReplaceLineEndings("\n");
        if (na == nb) return [];
        var la = na.Split('\n');
        var lb = nb.Split('\n');
        int i = 0;
        while (i < la.Length && i < lb.Length && la[i] == lb[i]) i++;
        return [new DiffEntry("body-text", $"line {i + 1}", i < la.Length ? Trunc(la[i]) : "(end)", i < lb.Length ? Trunc(lb[i]) : "(end)")];
    }

    private static bool IsJson(string s)
    {
        try { using var _ = JsonDocument.Parse(s); return true; }
        catch (JsonException) { return false; }
    }

    private static string Trunc(string s) => s.Length > 300 ? s[..300] + "…" : s;

    private static string? MediaType(string? contentType) => contentType?.Split(';')[0].Trim().ToLowerInvariant();

    private static string? Header(RecordedResponse r, string name) =>
        r.Headers.TryGetValue(name, out var v) ? string.Join(", ", v) : null;

    private static string SetCookies(RecordedResponse r) =>
        r.Headers.TryGetValue("Set-Cookie", out var v)
            ? string.Join(" | ", v.Select(TextNormalizer.NormalizeSetCookie).Order(StringComparer.Ordinal))
            : "";

    private static string Hash(RecordedResponse r) =>
        Convert.ToHexStringLower(SHA256.HashData(r.BodyIsBase64 && r.Body != null ? Convert.FromBase64String(r.Body) : Encoding.UTF8.GetBytes(r.Body ?? "")))[..16];
}

public static class ReportWriter
{
    /// <summary>report.json with every diff; report.md with at most <paramref name="maxDetails"/> mismatches and
    /// <paramref name="maxDiffsShown"/> diffs of each (the rest is in report.json and counted all the same).</summary>
    public static async Task WriteAsync(CompareReport report, string outDir, int maxDetails = 200, int maxDiffsShown = 25)
    {
        Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(Path.Combine(outDir, "report.json"), JsonSerializer.Serialize(report, ReplayJson.Indented));
        await File.WriteAllTextAsync(Path.Combine(outDir, "report.md"), ToMarkdown(report, maxDetails, maxDiffsShown));
    }

    public static string ToMarkdown(CompareReport r, int maxDetails = 200, int maxDiffsShown = 25)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Replay comparison report").AppendLine();
        sb.AppendLine($"Generated {r.Generated:u}").AppendLine();
        sb.AppendLine("| | Baseline | Candidate |").AppendLine("|---|---|---|");
        sb.AppendLine($"| File | `{r.Baseline.File}` | `{r.Candidate.File}` |");
        sb.AppendLine($"| Target | {r.Baseline.Target} | {r.Candidate.Target} |");
        sb.AppendLine($"| Run | {r.Baseline.RunId} | {r.Candidate.RunId} |");
        sb.AppendLine($"| Exchanges | {r.Baseline.Exchanges} | {r.Candidate.Exchanges} |");
        sb.AppendLine($"| Journey errors | {r.Baseline.Errors} | {r.Candidate.Errors} |").AppendLine();
        sb.AppendLine($"**Compared {r.Compared}, matched {r.Matched}, mismatched {r.Mismatched}** " +
                      $"(missing in candidate {r.MissingInCandidate}, extra in candidate {r.ExtraInCandidate}).").AppendLine();
        sb.AppendLine("## By route").AppendLine();
        sb.AppendLine("| Route | Requests | Mismatches | Kinds |").AppendLine("|---|---:|---:|---|");
        foreach (var rt in r.Routes)
            sb.AppendLine($"| `{rt.RouteKey}` | {rt.Requests} | {rt.Mismatches} | {string.Join(", ", rt.Kinds.Select(k => $"{k.Key}×{k.Value}"))} |");
        if (r.Errors.Count > 0)
        {
            sb.AppendLine().AppendLine("## Journey errors").AppendLine();
            foreach (var e in r.Errors) sb.AppendLine($"- {e}");
        }
        if (r.Mismatches.Count > 0)
        {
            sb.AppendLine().AppendLine("## Mismatches").AppendLine();
            foreach (var m in r.Mismatches.Take(maxDetails))
            {
                sb.AppendLine($"### {m.Key} — {m.Method} {m.Path}").AppendLine();
                sb.AppendLine($"Route `{m.RouteKey}`, step `{m.Step}`, kinds: {string.Join(", ", m.Kinds)}").AppendLine();
                sb.AppendLine("| Kind | Path | Baseline | Candidate |").AppendLine("|---|---|---|---|");
                foreach (var d in m.Diffs.Take(maxDiffsShown)) sb.AppendLine($"| {d.Kind} | `{Cell(d.Path)}` | {Cell(d.Expected)} | {Cell(d.Actual)} |");
                if (m.Diffs.Count > maxDiffsShown) sb.AppendLine().AppendLine($"… {m.Diffs.Count - maxDiffsShown} more diffs of this exchange in report.json");
                sb.AppendLine();
            }
            if (r.Mismatches.Count > maxDetails) sb.AppendLine($"… {r.Mismatches.Count - maxDetails} more in report.json");
        }
        return sb.ToString();
    }

    private static string Cell(string? s) => s == null ? "—" : s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
