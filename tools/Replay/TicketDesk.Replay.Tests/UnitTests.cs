using System.Text.RegularExpressions;

namespace TicketDesk.Replay.Tests;

public class FormReaderTests
{
    private const string Page = """
        <html><body>
        <form action="/search" method="get"><input name="term"></form>
        <form id="f" action="new?x=1" method="post" class="form-horizontal">
          <input name="__RequestVerificationToken" type="hidden" value="tok">
          <input name="Title" value="old">
          <input name="RememberMe" type="checkbox" value="true"><input name="RememberMe" type="hidden" value="false">
          <input name="Agree" type="checkbox" value="yes" checked>
          <input name="Off" value="x" disabled>
          <input type="submit" name="go" value="Go">
          <select name="TicketType"><option value="">-- pick --</option><option value="Problem">Problem</option></select>
          <select name="Category"><option value="Hardware">Hardware</option><option value="Network" selected>Network</option></select>
          <textarea name="Details">line1
        line2</textarea>
        </form></body></html>
        """;

    [Fact]
    public void Reads_successful_controls_like_a_browser()
    {
        var f = FormReader.Read(Page, new Uri("https://h.test/ticket/edit"), "#f");
        Assert.Equal("https://h.test/ticket/new?x=1", f.Action.ToString());
        Assert.Equal("POST", f.Method);
        Assert.Equal(
            ["__RequestVerificationToken=tok", "Title=old", "RememberMe=false", "Agree=yes", "TicketType=", "Category=Network", "Details=line1\nline2"],
            f.Fields.Select(kv => $"{kv.Key}={kv.Value.ReplaceLineEndings("\n")}"));
    }

    [Fact]
    public void Overrides_replace_existing_values_and_first_option_skips_placeholders()
    {
        var f = FormReader.Read(Page, new Uri("https://h.test/"), "#f");
        var fields = FormReader.ApplyOverrides(f, [new("title", "new"), new("TicketType", FormReader.FirstOption), new("Extra", "1")]);
        Assert.Contains(fields, kv => kv.Key == "title" && kv.Value == "new");
        Assert.DoesNotContain(fields, kv => kv.Key == "Title");
        Assert.Contains(fields, kv => kv.Key == "TicketType" && kv.Value == "Problem");
        Assert.Equal("Extra", fields[^1].Key);
        Assert.Equal(fields, FormReader.ApplyOverrides(f, [new("title", "new"), new("TicketType", FormReader.FirstOption), new("Extra", "1"), new("Priority", FormReader.FirstOption)]));
    }

    [Fact]
    public void Option_tokens_pick_first_last_or_nth_non_empty_option()
    {
        string[] opts = ["", "Problem", "Question", "Request"];
        Assert.Equal("Problem", FormReader.PickOption(opts, FormReader.FirstOption));
        Assert.Equal("Request", FormReader.PickOption(opts, FormReader.LastOption));
        Assert.Equal("Question", FormReader.PickOption(opts, "{option:2}"));
        Assert.Null(FormReader.PickOption(opts, "{option:9}"));
        Assert.Throws<ArgumentException>(() => FormReader.PickOption(opts, "{option:middle}"));
        var f = FormReader.Read(Page, new Uri("https://h.test/"), "#f");
        Assert.Contains(FormReader.ApplyOverrides(f, [new("Category", FormReader.FirstOption)]), kv => kv.Key == "Category" && kv.Value == "Hardware");
        Assert.Contains(FormReader.ApplyOverrides(f, [new("Category", FormReader.LastOption)]), kv => kv.Key == "Category" && kv.Value == "Network");
        Assert.Throws<InvalidOperationException>(() => FormReader.ApplyOverrides(f, [new("TicketType", "{option:9}")]));
    }

    [Fact]
    public void Missing_form_throws_with_the_selector() =>
        Assert.Contains("form#none", Assert.Throws<InvalidOperationException>(() => FormReader.Read(Page, new Uri("https://h.test/"), "form#none")).Message);
}

public class RouteTableTests
{
    private static RouteTable Table()
    {
        var t = new RouteTable();
        t.Add("GET", "ticket/{id:int}");
        t.Add("GET", "ticket/new");
        t.Add("GET", "tickets/{listName?}/{page:int?}");
        t.Add("GET", "tickets/reset-user-lists");
        t.Add("GET", "file/{id}/{fileName}");
        t.Add("GET", "admin/users/{page:int?}");
        t.Add("*", "tickets/filterList/{listName=opentickets}/{page:int?}");
        return t;
    }

    [Theory]
    [InlineData("GET", "/ticket/42", "GET /ticket/{id:int}")]
    [InlineData("GET", "/ticket/new", "GET /ticket/new")]
    [InlineData("GET", "/tickets", "GET /tickets/{listName?}/{page:int?}")]
    [InlineData("GET", "/tickets/opentickets/3", "GET /tickets/{listName?}/{page:int?}")]
    [InlineData("GET", "/tickets/reset-user-lists", "GET /tickets/reset-user-lists")]
    [InlineData("GET", "/file/12/report%20q3.pdf", "GET /file/{id}/{fileName}")]
    [InlineData("POST", "/tickets/filterList/opentickets", "POST /tickets/filterList/{listName=opentickets}/{page:int?}")]
    [InlineData("GET", "/admin/users?x=1", "GET /admin/users/{page:int?}")]
    [InlineData("GET", "/ticket/3f2504e0-4f89-11d3-9a0c-0305e82c3301/x", "GET /ticket/{guid}/x")]
    [InlineData("POST", "/ticket/42", "POST /ticket/{id}")]
    public void Resolves_most_specific_template_or_falls_back_to_heuristics(string method, string path, string expected) =>
        Assert.Equal(expected, Table().Resolve(method, path));

    [Fact]
    public void Loads_routes_csv_with_quoted_fields_and_multiple_verbs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"routes-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(path,
        [
            "controller,action,verb,route,notes",
            "Ticket,Index,GET,ticket/{id:int},\"view, with comma\"",
            "Account,ChangePassword,GET|POST,account/change-password,",
            "Navigation,ProjectMenu,GET,(child action only),",
        ]);
        try
        {
            var t = RouteTable.FromCsv(path);
            Assert.Equal(3, t.Count);
            Assert.Equal("POST /account/change-password", t.Resolve("POST", "/account/change-password"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Csv_split_handles_quotes() =>
        Assert.Equal(["a", "b, c", "say \"hi\"", ""], Csv.Split("a,\"b, c\",\"say \"\"hi\"\"\","));
}

public class TemplateTests
{
    [Fact]
    public void Expands_variables_and_secrets()
    {
        var secrets = SecretStore.FromSources([], [new("PW", "s3cret")]);
        var vars = new Dictionary<string, string> { ["ticketId"] = "12" };
        Assert.Equal("/ticket/12?p=s3cret", Template.Expand("/ticket/{ticketId}?p={secret:PW}", vars, secrets));
        Assert.Equal("/ticket/12?p={secret:PW}", Template.ExpandForRecording("/ticket/{ticketId}?p={secret:PW}", vars));
        Assert.Equal(FormReader.FirstOption, Template.Expand(FormReader.FirstOption, vars, secrets));
    }

    [Fact]
    public void Unknown_variable_or_secret_fails_loudly()
    {
        var secrets = new SecretStore();
        Assert.Throws<KeyNotFoundException>(() => Template.Expand("/ticket/{ticketId}", new Dictionary<string, string>(), secrets));
        Assert.Throws<KeyNotFoundException>(() => Template.Expand("{secret:TD_NO_SUCH_SECRET_9F3}", new Dictionary<string, string>(), secrets));
    }

    [Fact]
    public void Secrets_file_parsing_handles_export_quotes_and_comments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"secrets-{Guid.NewGuid():N}.env");
        File.WriteAllLines(path, ["# lab", "export A=1", "B=\"two words\"", "C='x=y'", "", "bogus"]);
        try
        {
            var s = SecretStore.FromSources([path]);
            Assert.Equal("1", s.Get("A"));
            Assert.Equal("two words", s.Get("B"));
            Assert.Equal("x=y", s.Get("C"));
        }
        finally { File.Delete(path); }
    }
}

/// <summary>Static checks of the TicketDesk journeys against the route inventory (docs/migration/routes.csv).</summary>
public class TicketDeskJourneyTests
{
    private static readonly string RoutesCsv = Path.Combine(AppContext.BaseDirectory, "routes.csv");

    public static TheoryData<string> JourneyNames() => [.. TicketDeskJourneys.All.Select(j => j.Name)];

    [Fact]
    public void Covers_the_journeys_named_in_the_brief() =>
        Assert.Equal(
            ["anonymous", "sign-in", "search", "ticket-list", "ticket-create-view", "ticket-edit", "ticket-comment", "ticket-assign", "ticket-close",
             "ticket-workflow", "attachments", "admin-settings", "admin-changes", "internal-user", "register-disabled"],
            TicketDeskJourneys.All.Select(j => j.Name));

    [Theory]
    [MemberData(nameof(JourneyNames))]
    public void Every_placeholder_is_produced_by_an_earlier_step(string name)
    {
        var journey = TicketDeskJourneys.All.Single(j => j.Name == name);
        var known = new HashSet<string>();
        var stepNames = new HashSet<string>();
        foreach (var step in journey.Steps)
        {
            var templates = new[] { step.Path ?? "" }.Concat(step.Form.Select(f => f.Value)).Concat(step.Query.Select(q => q.Value));
            foreach (Match m in Regex.Matches(string.Join(" ", templates), @"\{(?!secret:|option:)([A-Za-z_][A-Za-z0-9_]*)\}"))
                Assert.True(known.Contains(m.Groups[1].Value), $"{name}/{step.Name}: '{{{m.Groups[1].Value}}}' used before it is extracted");
            if (step.FromStep != null) Assert.Contains(step.FromStep, stepNames);
            Assert.True(stepNames.Add(step.Name), $"{name}: duplicate step name '{step.Name}'");
            foreach (var x in step.Extract) known.Add(x.Var);
        }
    }

    [Theory]
    [MemberData(nameof(JourneyNames))]
    public void Every_request_path_maps_to_an_inventoried_route(string name)
    {
        var table = RouteTable.FromCsv(RoutesCsv);
        var journey = TicketDeskJourneys.All.Single(j => j.Name == name);
        var sample = new Dictionary<string, string>
        {
            ["ticketId"] = "12", ["tempId"] = Guid.Empty.ToString(), ["modifyTempId"] = Guid.Empty.ToString(), ["userHref"] = "/admin/user/" + Guid.Empty,
            ["newUserHref"] = "/admin/user/" + Guid.Empty, ["projectId"] = "7", ["af"] = "token",
        };
        foreach (var step in journey.Steps.Where(s => s.Kind == StepKinds.Request))
        {
            var path = Template.ExpandForRecording(step.Path!, sample);
            if (path.StartsWith("/content/", StringComparison.Ordinal) || path.StartsWith("/bundles/", StringComparison.Ordinal)) continue; // bundles are not MVC routes
            Assert.True(table.Match(step.Method, path) != null, $"{name}/{step.Name}: {step.Method} {path} does not match any route in routes.csv");
        }
    }
}
