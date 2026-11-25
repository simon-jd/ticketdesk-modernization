namespace TicketDesk.Replay.Tests;

public class NormalizerTests
{
    private static readonly TextNormalizer N = new(new NormalizerOptions());

    [Theory]
    [InlineData("id 3f2504e0-4f89-11d3-9a0c-0305e82c3301 end", "id {guid} end")]
    [InlineData("at 2026-11-07T20:09:00.1234567+00:00 ok", "at {datetime} ok")]
    [InlineData("at 2026-11-07T20:09:00Z", "at {datetime}")]
    [InlineData("on 11/7/2026 3:04:05 PM", "on {datetime}")]
    [InlineData("on 11/7/2026", "on {date}")]
    [InlineData("on 07.11.2026", "on {date}")]
    [InlineData("on Nov 7, 2026", "on {date}")]
    [InlineData("on Saturday, November 7, 2026 at 3:04 PM", "on {datetime}")]
    [InlineData("on 2026-11-07", "on {date}")]
    [InlineData("Date: Sat, 07 Nov 2026 20:09:00 GMT", "Date: {httpdate}")]
    [InlineData("\"/Date(1762546140000)/\"", "\"{datetime}\"")]
    [InlineData("updated 3 minutes ago", "updated {relative}")]
    [InlineData("updated an hour ago", "updated {relative}")]
    [InlineData("updated just now", "updated {relative}")]
    [InlineData("<script src=\"/bundles/common?v=AbC_123-xyz\">", "<script src=\"/bundles/common?v={v}\">")]
    [InlineData("/tickets?_=1762546140000&x=1", "/tickets?_={v}&x=1")]
    [InlineData("at 15:04:05 sharp", "at {time} sharp")]
    [InlineData("Bootstrap 3.3.6 and jQuery 2.2.2", "Bootstrap 3.3.6 and jQuery 2.2.2")]
    public void Replaces_run_specific_values_with_placeholders(string input, string expected) =>
        Assert.Equal(expected, N.Normalize(input));

    [Fact]
    public void Replaces_captured_variables_on_word_boundaries_only()
    {
        var vars = new Dictionary<string, string> { ["ticketId"] = "101" };
        Assert.Equal("/ticket/{var:ticketId} #{var:ticketId} 1010 x101y", N.Normalize("/ticket/101 #101 1010 x101y", vars));
    }

    [Fact]
    public void Skips_variables_shorter_than_the_minimum_length()
    {
        var vars = new Dictionary<string, string> { ["ticketId"] = "7" };
        Assert.Equal("/ticket/7", N.Normalize("/ticket/7", vars));
    }

    [Fact]
    public void Set_cookie_keeps_name_and_attributes_but_not_value_or_expiry()
    {
        var a = TextNormalizer.NormalizeSetCookie(".AspNet.ApplicationCookie=abc123; path=/; expires=Sat, 07 Nov 2026 20:09:00 GMT; HttpOnly");
        var b = TextNormalizer.NormalizeSetCookie(".AspNet.ApplicationCookie=zzz; HttpOnly; path=/");
        Assert.Equal(a, b);
        Assert.Equal(".AspNet.ApplicationCookie; httponly; path=/", a);
    }

    [Fact]
    public void Location_on_the_target_host_becomes_relative()
    {
        Assert.Equal("/ticket/{var:id}", N.NormalizeLocation("https://localhost:8443/ticket/42", "https://localhost:8443/", new Dictionary<string, string> { ["id"] = "42" }));
        Assert.Equal("https://elsewhere.test/x", N.NormalizeLocation("https://elsewhere.test/x", "https://localhost:8443/", null));
    }

    [Fact]
    public void Normalizers_can_be_switched_off()
    {
        var n = new TextNormalizer(new NormalizerOptions { Guids = false, Timestamps = false });
        Assert.Equal("3f2504e0-4f89-11d3-9a0c-0305e82c3301 2026-11-07", n.Normalize("3f2504e0-4f89-11d3-9a0c-0305e82c3301 2026-11-07"));
    }
}

public class HtmlDomComparerTests
{
    private static HtmlDomComparer Comparer(NormalizerOptions? o = null) => new(new TextNormalizer(o ?? new NormalizerOptions()));

    [Fact]
    public void Ignores_attribute_order_whitespace_comments_and_class_token_order()
    {
        var a = "<html><body><div class=\"a b\" id=\"x\" data-k=\"1\"><p>Hello   world</p></div><!-- c1 --></body></html>";
        var b = "<html>\n<body>\n  <div data-k=\"1\" id=\"x\"   class=\"b  a\">\n    <p>Hello world</p>\n  </div>\n</body></html>";
        Assert.Empty(Comparer().Compare(a, b));
    }

    [Fact]
    public void Antiforgery_token_values_are_ignored()
    {
        var a = "<form><input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"AAA\"></form>";
        var b = "<form><input type=\"hidden\" value=\"BBB\" name=\"__RequestVerificationToken\"></form>";
        Assert.Empty(Comparer().Compare(a, b));
    }

    /// <summary>Round 5b: every diff is reported (the walk stopped at 25 before, which hid later diffs from the replay gate).</summary>
    [Fact]
    public void Reports_every_diff_of_a_page_not_only_the_first_25()
    {
        static string Page(string word) => "<ul>" + string.Concat(Enumerable.Range(1, 40).Select(i => $"<li>{word} {i}</li>")) + "</ul>";
        var diffs = Comparer().Compare(Page("old"), Page("new"));
        Assert.Equal(40, diffs.Count);
        Assert.Equal("html/body[1]/ul[1]/li[40]/text()[1]", diffs[^1].Path);
        var report = ReportWriter.ToMarkdown(new CompareReport
        {
            Baseline = new RunInfo("a", "t", "a", 1, 0), Candidate = new RunInfo("b", "t", "b", 1, 0), Compared = 1, Mismatched = 1,
            Mismatches = [new ExchangeMismatch { Key = "j#000.0", Journey = "j", Step = "s", RouteKey = "GET /", Method = "GET", Path = "/", Kinds = ["body-html"], Diffs = diffs }],
        }, maxDiffsShown: 25);
        Assert.Contains("… 15 more diffs of this exchange in report.json", report); // only the Markdown is shortened
    }

    [Fact]
    public void Reports_text_change_with_a_dom_path()
    {
        var diffs = Comparer().Compare("<div id=\"d\"><h2>Ticket #1</h2></div>", "<div id=\"d\"><h2>Ticket No. 1</h2></div>");
        var d = Assert.Single(diffs);
        Assert.Equal("text", d.Kind);
        Assert.Equal("html/body[1]/div[1]/h2[1]/text()[1]", d.Path);
        Assert.Equal("Ticket #1", d.Expected);
        Assert.Equal("Ticket No. 1", d.Actual);
    }

    [Fact]
    public void Reports_attribute_change()
    {
        var d = Assert.Single(Comparer().Compare("<a href=\"/tickets\">x</a>", "<a href=\"/ticket-center\">x</a>"));
        Assert.Equal("attribute", d.Kind);
        Assert.EndsWith("/@href", d.Path);
    }

    [Fact]
    public void Aligns_children_so_one_inserted_row_is_one_diff()
    {
        var a = "<table><tr><td>1</td></tr><tr><td>2</td></tr><tr><td>3</td></tr></table>";
        var b = "<table><tr><td>1</td></tr><tr><td>2</td></tr><tr><td>2b</td></tr><tr><td>3</td></tr></table>";
        var diffs = Comparer().Compare(a, b);
        Assert.Single(diffs);
    }

    [Fact]
    public void Reports_removed_element()
    {
        var diffs = Comparer().Compare("<ul><li>a</li><span>s</span></ul>", "<ul><li>a</li></ul>");
        Assert.Contains(diffs, d => d.Kind == "removed");
    }

    [Fact]
    public void Ignore_selectors_drop_volatile_regions()
    {
        var o = new NormalizerOptions { IgnoreSelectors = ["#footer .build"] };
        Assert.Empty(Comparer(o).Compare("<div id=\"footer\"><span class=\"build\">b1</span></div>", "<div id=\"footer\"><span class=\"build\">b2</span></div>"));
    }

    [Fact]
    public void Run_variables_are_normalized_per_side()
    {
        var diffs = Comparer().Compare("<a href=\"/ticket/101\">#101</a>", "<a href=\"/ticket/5000\">#5000</a>",
            new Dictionary<string, string> { ["ticketId"] = "101" }, new Dictionary<string, string> { ["ticketId"] = "5000" });
        Assert.Empty(diffs);
    }
}

public class JsonSemanticComparerTests
{
    private static readonly JsonSemanticComparer C = new(new TextNormalizer(new NormalizerOptions()));

    [Fact]
    public void Property_order_and_number_format_do_not_matter() =>
        Assert.Empty(C.Compare("""{"a":1,"b":{"x":[1,2]},"c":1.50}""", """{"c":1.5,"b":{"x":[1.0,2]},"a":1}"""));

    [Fact]
    public void Reports_value_changes_with_json_paths()
    {
        var d = Assert.Single(C.Compare("""{"items":[{"id":"a"}]}""", """{"items":[{"id":"b"}]}"""));
        Assert.Equal("json-value", d.Kind);
        Assert.Equal("$.items[0].id", d.Path);
    }

    [Fact]
    public void Reports_added_and_removed_members()
    {
        var diffs = C.Compare("""{"a":1,"gone":true}""", """{"a":1,"new":true}""");
        Assert.Contains(diffs, d => d.Kind == "json-removed" && d.Path == "$.gone");
        Assert.Contains(diffs, d => d.Kind == "json-added" && d.Path == "$.new");
    }

    [Fact]
    public void Normalizes_strings_inside_json() =>
        Assert.Empty(C.Compare("""{"at":"2026-11-07T10:00:00Z","id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301"}""",
                               """{"at":"2027-01-01T00:00:00Z","id":"00000000-4f89-11d3-9a0c-0305e82c3301"}"""));

    [Fact]
    public void Invalid_json_is_a_diff_not_an_exception() =>
        Assert.Equal("json-parse", Assert.Single(C.Compare("{}", "<html>")).Kind);
}
