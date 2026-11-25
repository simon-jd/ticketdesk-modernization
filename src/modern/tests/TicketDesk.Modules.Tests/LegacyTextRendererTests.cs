using System.Globalization;
using System.Text;
using TicketDesk.Tickets.Rendering;

namespace TicketDesk.Modules.Tests;

/// <summary>
/// Ticket text rendering against the legacy app's output: 180 cases (tricky Markdown/HTML/XSS inputs and the golden
/// journeys' texts, each as Markdown and as HTML) with the output of the legacy DLLs on the lab VM (ops/dev/render-parity).
/// MarkdownSharp is a byte-equal port; the sanitizer is a current HtmlSanitizer with the legacy allow-lists, so a few
/// outputs differ in URL and style serialization only (expected difference R1, classified over 55,829 texts in
/// docs/evidence/tickets-20261110/sanitizer/). Those are pinned here; any other change of output fails.
/// </summary>
public sealed class LegacyTextRendererTests
{
    private sealed record Case(string Id, bool IsHtml, string? Input, string Legacy, string? New);

    private static readonly Lazy<List<Case>> All = new(() => File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Rendering", "legacy-render-cases.tsv"))
        .Where(l => !l.StartsWith('#')).Select(l => l.Split('\t'))
        .Select(p => new Case(p[0], p[1] == "1", p[2] == "-" ? null : Text(p[2]), Text(p[3]), p[4] == "=" ? null : Text(p[4]))).ToList());

    private static string Text(string b64) => Encoding.UTF8.GetString(Convert.FromBase64String(b64));

    public static TheoryData<string> Ids() => new(All.Value.Select(c => c.Id));

    private static string Render(Case c)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        try { return new LegacyTextRenderer().Render(c.Input, c.IsHtml); }
        finally { CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture; }
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Renders_like_the_legacy_app_or_the_pinned_R1_output(string id)
    {
        var c = All.Value.Single(x => x.Id == id);
        Assert.True((c.New ?? c.Legacy) == Render(c), id);
    }

    [Fact]
    public void Only_the_recorded_cases_differ_from_the_legacy_output_and_only_in_url_and_style_attributes()
    {
        var differing = All.Value.Where(c => c.New != null).ToList();
        Assert.Equal(19, differing.Count);
        foreach (var c in differing)
        {
            // same markup once URL and style attribute values are blanked out: the difference is inside those values only
            static string Shape(string html) => System.Text.RegularExpressions.Regex.Replace(html, "(href|src|action|style)=\"[^\"]*\"", "$1=\"\"");
            Assert.True(Shape(c.Legacy) == Shape(c.New!), c.Id);
        }
    }

    [Fact]
    public void Script_event_handlers_and_script_urls_never_survive()
    {
        foreach (var c in All.Value)
        {
            var html = Render(c);
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(@"<[^>]+\son[a-z]+\s*=", html);
        }
    }

    [Fact]
    public void The_sanitizer_is_configured_with_the_legacy_allow_lists()
    {
        Assert.Equal(99, LegacyTextRenderer.LegacyAllowList.Tags.Length);
        Assert.Equal(95, LegacyTextRenderer.LegacyAllowList.Attributes.Length);
        Assert.Equal(95, LegacyTextRenderer.LegacyAllowList.StyleProperties.Length);
        Assert.Equal(["http", "https"], LegacyTextRenderer.LegacyAllowList.Schemes);
        Assert.StartsWith("11.", typeof(Ganss.Xss.HtmlSanitizer).Assembly.GetName().Version!.ToString());
    }
}
