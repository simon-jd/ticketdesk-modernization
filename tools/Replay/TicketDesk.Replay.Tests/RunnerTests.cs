using System.Text.Json;

namespace TicketDesk.Replay.Tests;

public class JourneyRunnerTests
{
    [Fact]
    public async Task Records_a_cookie_session_with_antiforgery_and_generated_ids()
    {
        await using var app = await FakeTicketApp.StartAsync(firstTicketId: 4711);
        var runner = new JourneyRunner(app.RunnerOptions());
        var rec = await runner.RunJourneyAsync(FakeTicketApp.FullJourney(), TestContext.Current.CancellationToken);

        Assert.All(rec, r => Assert.Null(r.Error));
        var create = rec.Single(r => r.StepName == "create" && r.Hop == 0);
        Assert.Equal(302, create.Response!.Status);
        Assert.Equal("4711", create.Variables["ticketId"]);
        Assert.Contains(create.Request.Form!, f => f.Key == "__RequestVerificationToken" && f.Value.Length > 10);
        Assert.Contains(create.Request.Form!, f => f.Key == "TicketType" && f.Value == "Problem"); // {option:first} skipped the empty option
        Assert.Contains(create.Request.Form!, f => f.Key == "Category" && f.Value == "Hardware"); // selected option kept

        var followed = rec.Single(r => r.StepName == "create" && r.Hop == 1);
        Assert.Equal("/ticket/4711", followed.Request.Path);
        Assert.Equal(200, followed.Response!.Status);

        var view = rec.Single(r => r.StepName == "view");
        Assert.Contains("Ticket #4711", view.Response!.Body);
        Assert.Contains("auth=…", view.Request.Headers["Cookie"]); // cookie names recorded, values not

        var events = rec.Single(r => r.StepName == "events partial");
        Assert.Equal("XMLHttpRequest", events.Request.Headers["X-Requested-With"]);

        var download = rec.Single(r => r.StepName == "download");
        Assert.True(download.Response!.BodyIsBase64);
    }

    [Fact]
    public async Task Never_writes_passwords_or_secret_values_to_the_recording()
    {
        await using var app = await FakeTicketApp.StartAsync();
        var rec = await new JourneyRunner(app.RunnerOptions()).RunJourneyAsync(FakeTicketApp.FullJourney(), TestContext.Current.CancellationToken);
        var path = Path.Combine(Path.GetTempPath(), $"td-replay-{Guid.NewGuid():N}.jsonl");
        try
        {
            await ReplayJson.WriteJsonlAsync(path, rec, TestContext.Current.CancellationToken);
            var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(FakeTicketApp.Password, text);
            Assert.DoesNotContain(FakeTicketApp.User, text);
            Assert.Contains("{secret:TD_ADMIN_PASSWORD}", text);

            var back = await ReplayJson.ReadJsonlAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(rec.Count, back.Count);
            Assert.Equal(JsonSerializer.Serialize(rec, ReplayJson.Options), JsonSerializer.Serialize(back, ReplayJson.Options));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Secret_values_echoed_in_responses_are_redacted()
    {
        await using var app = await FakeTicketApp.StartAsync();
        var options = app.RunnerOptions();
        options.Secrets.Set("TD_ADMIN_EMAIL", "TicketDesk"); // appears in every page title and heading of the fake app
        var journey = new JourneySpec { Name = "echo", Steps = [Steps.Get("home", "/")] };
        var rec = await new JourneyRunner(options).RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<h1>TicketDesk</h1>", rec[0].Response!.Body);
        Assert.Contains("<h1>{secret:TD_ADMIN_EMAIL}</h1>", rec[0].Response!.Body);
    }

    [Fact]
    public async Task Stops_a_journey_when_an_expected_status_is_not_met()
    {
        await using var app = await FakeTicketApp.StartAsync();
        var journey = new JourneySpec
        {
            Name = "expect-fail",
            Steps = [Steps.Get("tickets", "/tickets", expect: 200), Steps.Get("never", "/")],
        };
        var rec = await new JourneyRunner(app.RunnerOptions()).RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(rec, r => r.StepName == "never");
        Assert.Contains("expected HTTP 200, got 302", rec[^1].Error);
    }

    [Fact]
    public async Task Missing_form_is_recorded_as_a_journey_error()
    {
        await using var app = await FakeTicketApp.StartAsync();
        var journey = new JourneySpec
        {
            Name = "no-form",
            Steps = [Steps.Get("home", "/"), Steps.Submit("submit", "form#nope", [])],
        };
        var rec = await new JourneyRunner(app.RunnerOptions()).RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.Equal("(not sent)", rec[^1].RouteKey);
        Assert.Contains("form#nope", rec[^1].Error);
    }

    [Fact]
    public async Task A_failed_extractor_keeps_the_response_that_caused_it()
    {
        await using var app = await FakeTicketApp.StartAsync();
        var journey = new JourneySpec
        {
            Name = "extract-fail",
            Steps = [Steps.Get("home", "/", extract: new ExtractSpec("nothing", "regex", "no-such-text-([0-9]+)")), Steps.Get("never", "/about")],
        };
        var rec = await new JourneyRunner(app.RunnerOptions()).RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        var home = Assert.Single(rec);
        Assert.Equal(200, home.Response!.Status); // the real response is recorded, not a "(not sent)" placeholder
        Assert.Equal("GET /", home.RouteKey.Split(' ', 2)[0] + " " + home.Request.Path);
        Assert.Contains("extractor 'nothing'", home.Error);
    }

    [Fact]
    public async Task Groups_exchanges_by_route_template_when_a_route_table_is_given()
    {
        var routes = new RouteTable();
        routes.Add("GET", "ticket/{id:int}");
        routes.Add("POST", "ticket/new");
        await using var app = await FakeTicketApp.StartAsync();
        var rec = await new JourneyRunner(app.RunnerOptions(routes: routes)).RunJourneyAsync(FakeTicketApp.FullJourney(), TestContext.Current.CancellationToken);
        Assert.Equal("GET /ticket/{id:int}", rec.Single(r => r.StepName == "view").RouteKey);
        Assert.Equal("POST /ticket/new", rec.Single(r => r.StepName == "create" && r.Hop == 0).RouteKey);
        Assert.Equal("GET /auto-complete/tag-list", rec.Single(r => r.StepName == "tags json").RouteKey); // heuristic fallback
    }
}

public class ReplayCompareTests
{
    private static async Task<List<RecordedExchange>> Record(FakeTicketApp app, string runId) =>
        await new JourneyRunner(app.RunnerOptions(runId)).RunJourneyAsync(FakeTicketApp.FullJourney(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Two_runs_of_the_same_app_match_despite_tokens_ids_and_timestamps()
    {
        await using var a = await FakeTicketApp.StartAsync(firstTicketId: 101);
        await using var b = await FakeTicketApp.StartAsync(firstTicketId: 98765, clockSkew: TimeSpan.FromDays(400));
        var ra = await Record(a, "a");
        var rb = await Record(b, "b");
        Assert.NotEqual(ra.Single(r => r.StepName == "view").Response!.Body, rb.Single(r => r.StepName == "view").Response!.Body);

        var report = new ExchangeComparer().Compare(ra, rb, "a.jsonl", "b.jsonl");
        Assert.True(report.Mismatched == 0, ReportWriter.ToMarkdown(report));
        Assert.Equal(ra.Count, report.Compared);
        Assert.Equal(ra.Count, report.Matched);
    }

    [Fact]
    public async Task Finds_behaviour_changes_and_groups_them_by_route()
    {
        await using var a = await FakeTicketApp.StartAsync();
        await using var b = await FakeTicketApp.StartAsync(changed: true, firstTicketId: 300);
        var report = new ExchangeComparer().Compare(await Record(a, "a"), await Record(b, "b"));

        var about = report.Mismatches.Single(m => m.Step == "about");
        Assert.Contains("status", about.Kinds);
        Assert.Contains(about.Diffs, d => d.Kind == "status" && d.Expected == "200" && d.Actual == "404");

        var view = report.Mismatches.Single(m => m.Step == "view");
        Assert.Equal(["body-html"], view.Kinds);
        var text = Assert.Single(view.Diffs); // attribute order / whitespace / class order are not reported
        Assert.Equal("Ticket #{var:ticketId}", text.Expected);
        Assert.Equal("Ticket No. {var:ticketId}", text.Actual);

        var tags = report.Mismatches.Single(m => m.Step == "tags json");
        Assert.Equal(["body-json"], tags.Kinds); // key order ignored, the extra element is reported
        Assert.Contains(tags.Diffs, d => d.Kind == "json-added" && d.Path == "$[2]");

        Assert.Contains(report.Mismatches, m => m.Step == "download" && m.Kinds.Contains("body-binary"));
        Assert.Contains(report.Mismatches, m => m.Step == "sign out" && m.Kinds.Contains("location"));
        // the POST itself matches (its followed GET /ticket/{id} hop carries the heading change)
        Assert.DoesNotContain(report.Mismatches, m => m.Key.EndsWith(".0") && m.Step is "create" or "sign-in" or "events partial" or "home signed in");
        Assert.Contains(report.Mismatches, m => m.Step == "create" && m.Key.EndsWith(".1"));

        var aboutRoute = report.Routes.Single(r => r.RouteKey == "GET /about");
        Assert.Equal(1, aboutRoute.Mismatches);
        Assert.Equal(report.Routes.OrderByDescending(r => r.Mismatches).First().Mismatches, report.Routes[0].Mismatches);
    }

    [Fact]
    public async Task Writes_json_and_markdown_reports()
    {
        await using var a = await FakeTicketApp.StartAsync();
        await using var b = await FakeTicketApp.StartAsync(changed: true);
        var report = new ExchangeComparer().Compare(await Record(a, "a"), await Record(b, "b"), "base.jsonl", "cand.jsonl");
        var dir = Path.Combine(Path.GetTempPath(), $"td-replay-report-{Guid.NewGuid():N}");
        try
        {
            await ReportWriter.WriteAsync(report, dir);
            var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "report.json"), TestContext.Current.CancellationToken));
            Assert.Equal(report.Mismatched, json.RootElement.GetProperty("mismatched").GetInt32());
            Assert.True(json.RootElement.GetProperty("routes").GetArrayLength() > 0);
            var md = await File.ReadAllTextAsync(Path.Combine(dir, "report.md"), TestContext.Current.CancellationToken);
            Assert.Contains("## By route", md);
            Assert.Contains("`GET /about`", md);
            Assert.Contains("| status | `status` | 200 | 404 |", md);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task A_recording_can_be_replayed_against_another_target_without_journey_code()
    {
        await using var a = await FakeTicketApp.StartAsync(firstTicketId: 10);
        var baseline = await Record(a, "a");
        var journeys = Recording.ToJourneys(baseline);
        Assert.Equal(FakeTicketApp.FullJourney().Steps.Count, Assert.Single(journeys).Steps.Count);

        await using var b = await FakeTicketApp.StartAsync(firstTicketId: 777);
        var replayed = await new JourneyRunner(b.RunnerOptions("replay")).RunAsync(journeys, TestContext.Current.CancellationToken);
        Assert.Equal(baseline.Select(x => x.MatchKey), replayed.Select(x => x.MatchKey));
        Assert.Equal("777", replayed.Single(r => r.StepName == "create" && r.Hop == 0).Variables["ticketId"]);
        Assert.Equal(0, new ExchangeComparer().Compare(baseline, replayed).Mismatched);
    }

    [Fact]
    public void A_json_content_type_with_a_non_json_body_is_compared_as_text()
    {
        static RecordedExchange X(string body) => new()
        {
            RunId = "r", Target = "https://h.test/", Journey = "j", StepName = "s", RouteKey = "GET /x",
            Request = new RecordedRequest { Method = "GET", Path = "/x" },
            Response = new RecordedResponse { Status = 500, ContentType = "application/json; charset=utf-8", Body = body, BodyLength = body.Length },
        };
        const string errorPage = "\n<!DOCTYPE html>\n<html><head><title>Error</title></head><body><h1>Error.</h1></body></html>\n";
        Assert.Equal(0, new ExchangeComparer().Compare([X(errorPage)], [X(errorPage)]).Mismatched);
        var report = new ExchangeComparer().Compare([X(errorPage)], [X("[]")]);
        Assert.Equal(["body-text"], Assert.Single(report.Mismatches).Kinds);
    }

    [Fact]
    public async Task Missing_and_extra_exchanges_are_reported()
    {
        await using var a = await FakeTicketApp.StartAsync();
        var full = await Record(a, "a");
        var shorter = full.Where(x => x.StepName != "download").ToList();
        var report = new ExchangeComparer().Compare(full, shorter);
        Assert.Equal(1, report.MissingInCandidate);
        var reverse = new ExchangeComparer().Compare(shorter, full);
        Assert.Equal(1, reverse.ExtraInCandidate);
    }
}
