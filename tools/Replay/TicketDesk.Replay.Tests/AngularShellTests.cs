using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Replay.Tests;

/// <summary>
/// A page answered by the Angular front end has no server-rendered form: the runner records the Angular answer and reads the
/// next step's form, extracted values and token from the same URL's server page (X-TicketDesk-Ui: server), unrecorded.
/// </summary>
public sealed class AngularShellTests
{
    [Fact]
    public async Task Forms_and_extractors_after_an_Angular_page_come_from_its_server_page()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        var serverPageRequests = 0;
        app.MapGet("/ticket/new", (HttpContext c) =>
        {
            if (c.Request.Headers[JourneyRunner.UiHeader] == "server")
            {
                serverPageRequests++;
                return Results.Content("<html><body><form action=\"/ticket/new\" method=\"post\"><input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"tok\" />" +
                                       "<input name=\"tempId\" value=\"T-1\" /><input name=\"Title\" value=\"\" /></form></body></html>", "text/html");
            }
            c.Response.Headers["X-TicketDesk-Backend"] = "modern; route=ticket.new.get; ui=angular";
            return Results.Content("<html><body><td-root></td-root></body></html>", "text/html");
        });
        app.MapPost("/ticket/new", (HttpRequest r) => Results.Text($"{r.Form["__RequestVerificationToken"]}|{r.Form["tempId"]}|{r.Form["Title"]}"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        var runner = new JourneyRunner(new RunnerOptions { Target = new Uri("http://localhost/"), HandlerFactory = () => app.GetTestServer().CreateHandler() });
        var journey = new JourneySpec
        {
            Name = "t",
            Steps =
            [
                Steps.Get("new ticket page", "/ticket/new", extract: new ExtractSpec("tempId", "html", "input[name='tempId']", "value")),
                Steps.Submit("create", "form[action$='/ticket/new']", [Steps.F("Title", "x")], fromStep: "new ticket page"),
            ],
        };
        var rec = await runner.RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.Equal(2, rec.Count);
        Assert.Null(rec[1].Error);
        Assert.Contains("<td-root>", rec[0].Response!.Body); // the Angular answer is what is recorded
        Assert.Equal("T-1", rec[0].Variables!["tempId"]);
        Assert.Equal("tok|T-1|x", rec[1].Response!.Body);
        Assert.Equal(1, serverPageRequests);
        Assert.Equal(1, runner.ServerPageFetches);

        var off = new JourneyRunner(new RunnerOptions { Target = new Uri("http://localhost/"), HandlerFactory = () => app.GetTestServer().CreateHandler(), ServerPageForAngular = false });
        var failed = await off.RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.NotNull(failed[^1].Error); // without it the journey cannot go on: the shell has no tempId field
    }
}
