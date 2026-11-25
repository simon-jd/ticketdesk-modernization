using System.Diagnostics;
using TicketDesk.Replay;

namespace TicketDesk.Web.Tests;

/// <summary>
/// The golden characterization recording (<c>tests/characterization/golden/legacy-golden.jsonl.xz</c>): what the legacy app
/// answered to the 15 journeys. Tests compare the new app's answer for a migrated route with the recorded legacy answer using
/// td-replay's own comparer (same normalizers as a replay through the gateway). Decompressed with the <c>xz</c> tool; the
/// tests skip when it is not installed.
/// </summary>
internal static class Golden
{
    private static readonly Lazy<Dictionary<string, RecordedExchange>?> All = new(Load);

    public static string? RepoRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "docs", "migration", "routes.csv"))) return dir.FullName;
            return null;
        }
    }

    private static Dictionary<string, RecordedExchange>? Load()
    {
        var root = RepoRoot;
        if (root == null) return null;
        var xz = Path.Combine(root, "tests", "characterization", "golden", "legacy-golden.jsonl.xz");
        if (!File.Exists(xz)) return null;
        var jsonl = Path.Combine(Path.GetTempPath(), $"td-golden-{Guid.NewGuid():N}.jsonl");
        try
        {
            var psi = new ProcessStartInfo("xz", ["-dc", xz]) { RedirectStandardOutput = true, UseShellExecute = false };
            using (var p = Process.Start(psi)!)
            using (var f = File.Create(jsonl))
            {
                p.StandardOutput.BaseStream.CopyTo(f);
                p.WaitForExit();
                if (p.ExitCode != 0) return null;
            }
            return ReplayJson.ReadJsonlAsync(jsonl).GetAwaiter().GetResult().ToDictionary(e => e.MatchKey);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // no xz on this machine
        }
        finally
        {
            File.Delete(jsonl);
        }
    }

    /// <summary>Every exchange of the recording in recording order (skips the test without the file or xz).</summary>
    public static IReadOnlyList<RecordedExchange> Exchanges()
    {
        if (All.Value == null) Assert.Skip("golden recording or the xz tool is not available");
        return All.Value!.Values.OrderBy(e => e.Seq).ToList();
    }

    public static RecordedExchange Get(string matchKey)
    {
        if (All.Value == null) Assert.Skip("golden recording or the xz tool is not available");
        return All.Value![matchKey];
    }

    /// <summary>The new app's response as a recorded exchange, paired with the golden one (same key and variables).</summary>
    public static async Task<RecordedExchange> RecordAsync(RecordedExchange golden, HttpResponseMessage response, CancellationToken ct)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Headers.Concat(response.Content.Headers)) headers[h.Key] = h.Value.ToArray();
        var body = await response.Content.ReadAsStringAsync(ct);
        return golden with
        {
            RunId = "new-app-test",
            Target = "http://localhost/",
            Response = new RecordedResponse
            {
                Status = (int)response.StatusCode,
                Headers = headers,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Body = body,
                BodyLength = body.Length,
            },
        };
    }

    /// <summary>
    /// td-replay's comparison of one exchange (status, compared headers, Set-Cookie, normalized body), without the expected
    /// difference every page with the layout has since Round 5 (<see cref="IsMenuPostDifference"/>).
    /// </summary>
    public static List<DiffEntry> Compare(RecordedExchange golden, RecordedExchange candidate) =>
        new ExchangeComparer().CompareOne(golden, candidate).Where(d => !IsMenuPostDifference(d, candidate.Response?.Body)).ToList();

    private static readonly System.Text.RegularExpressions.Regex LinkRx = new("^<a (?:[^>]* )?href=\"([^\"]*)\"[^>]*>(.*)$", System.Text.RegularExpressions.RegexOptions.Singleline);
    private static readonly System.Text.RegularExpressions.Regex PostFormRx = new("^<form action=\"([^\"]*)\"[^>]* method=\"post\"[^>]*>(.*)$", System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>
    /// Expected difference S3 (Round 5), the rules of <c>docs/migration/replay-explanations.json</c>: a link that changed state
    /// (sign-out, the language and project menus, the reset dialog) is a form that posts to the same URL with the same text;
    /// on a page with those forms (class <c>td-menu-post</c>) also the layout's style element that styles them, and the token
    /// they carry (no-cache headers; the antiforgery cookie of either app, with every other cookie unchanged).
    /// </summary>
    public static bool IsMenuPostDifference(DiffEntry d, string? candidateBody)
    {
        if (d.Kind == "element" && d.Expected != null && d.Actual != null)
        {
            var (a, f) = (LinkRx.Match(d.Expected), PostFormRx.Match(d.Actual));
            return a.Success && f.Success && a.Groups[1].Value == f.Groups[1].Value && a.Groups[2].Value.Trim() == f.Groups[2].Value.Trim();
        }
        if (candidateBody == null || !candidateBody.Contains("class=\"td-menu-post\"", StringComparison.Ordinal)) return false;
        return d.Kind switch
        {
            "added" => d.Path == "html/head[1]/style[1]/text()[1]" && d.Actual != null && d.Actual.StartsWith("\"/* S3 (Round 5): menu items that post", StringComparison.Ordinal),
            "header" => d.Path == "Cache-Control" && d.Actual is "no-store, no-cache" or "no-cache, no-store",
            "set-cookie" => WithoutAntiforgeryCookie(d.Expected) == WithoutAntiforgeryCookie(d.Actual),
            _ => false,
        };
    }

    private static string WithoutAntiforgeryCookie(string? cookies) => string.Join(" | ",
        (cookies ?? "").Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !c.StartsWith(".TicketDesk.Antiforgery;", StringComparison.Ordinal) && !c.StartsWith("__RequestVerificationToken;", StringComparison.Ordinal)).Order(StringComparer.Ordinal));

    /// <summary>
    /// Expected difference S3 on pages with a form that posts to the new app: the hidden antiforgery field, the antiforgery
    /// cookie, and the no-cache headers ASP.NET Core sets on every response that issues a token.
    /// </summary>
    public static bool IsAntiforgeryDifference(DiffEntry d) => d.Kind switch
    {
        "added" => d.Expected == null && d.Actual != null && d.Actual.StartsWith("<input name=\"__RequestVerificationToken\" type=\"hidden\"", StringComparison.Ordinal),
        "header" => d.Path == "Cache-Control" && d.Actual is "no-store, no-cache" or "no-cache, no-store",
        "set-cookie" => d.Actual != null && string.Join(" | ", d.Actual.Split(" | ").Where(c => !c.StartsWith(".TicketDesk.Antiforgery;", StringComparison.Ordinal))) == (d.Expected ?? ""),
        _ => false,
    };

    /// <summary>S3: the script with which a page's AJAX posts send its token as a header (<c>_AntiforgeryAjax</c>).</summary>
    public static bool IsAntiforgeryScript(DiffEntry d) =>
        d.Kind == "added" && d.Actual != null && d.Actual.StartsWith("<script type=\"text/javascript\">", StringComparison.Ordinal) && d.Actual.Contains("S3 RequestVerificationToken");

    /// <summary>S3 (Round 5): the ticket list's sort headers post (<c>data-ajax-method="POST"</c>).</summary>
    public static bool IsSortHeaderPost(DiffEntry d) =>
        d.Kind == "attribute" && d.Expected == null && d.Actual == "POST"
        && System.Text.RegularExpressions.Regex.IsMatch(d.Path, @"/thead\[1\]/tr\[1\]/th\[\d+\]/span\[1\]/a\[1\]/@data-ajax-method$");

    /// <summary>Expected difference S5: rows inside the ticket table (only used where the sort has ties).</summary>
    public static bool IsTicketRow(DiffEntry d) => System.Text.RegularExpressions.Regex.IsMatch(d.Path, @"/tbody\[1\]/tr\[\d+\]");

    public static string Describe(IEnumerable<DiffEntry> diffs) =>
        string.Join("\n", diffs.Select(d => $"{d.Kind} {d.Path}: expected [{d.Expected}] actual [{d.Actual}]"));
}
