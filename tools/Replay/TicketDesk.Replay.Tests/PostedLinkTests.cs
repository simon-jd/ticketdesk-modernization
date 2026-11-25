using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Replay.Tests;

/// <summary>
/// S3, Round 5: a recorded GET of a URL that the latest pages offer as a post now (a one-button form with that action, or an
/// AJAX link of the route marked data-ajax-method="POST") is sent as that post; every other GET stays a GET.
/// </summary>
public sealed class PostedLinkTests
{
    private const string Page = """
        <html><body>
        <form action="/language?name=pt-BR" method="post"><input name="__RequestVerificationToken" type="hidden" value="tok-1" /><button type="submit">PT</button></form>
        <form action="/account/edit" method="post"><input name="__RequestVerificationToken" type="hidden" value="tok-1" /><input name="DisplayName" value="x" /><button>Save</button></form>
        <a href="/plain">plain</a>
        <a data-ajax="true" data-ajax-method="POST" href="/tickets/sortList/opentickets/1?ColumnName=Title">Title</a>
        </body></html>
        """;

    [Fact]
    public async Task Gets_of_urls_the_page_posts_are_sent_as_that_post_others_stay_gets()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapGet("/page", () => Results.Content(Page, "text/html"));
        app.Map("/{**path}", async (HttpRequest r) =>
        {
            var body = r.HasFormContentType ? string.Join("&", (await r.ReadFormAsync()).Select(f => $"{f.Key}={f.Value}")) : "";
            return Results.Text($"{r.Method} {r.Path}{r.QueryString} [{body}] header=[{r.Headers[JourneyRunner.AntiforgeryHeader]}]");
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        var routes = new RouteTable();
        routes.Add("*", "tickets/sortList/{listName=opentickets}/{page:int?}");
        var runner = new JourneyRunner(new RunnerOptions { Target = new Uri("http://localhost/"), Routes = routes, HandlerFactory = () => app.GetTestServer().CreateHandler() });
        var journey = new JourneySpec
        {
            Name = "t",
            Steps =
            [
                Steps.Get("before any page", "/language?name=pt-BR"),
                Steps.Get("page", "/page"),
                Steps.Get("the form's action", "/language?name=pt-BR"),
                Steps.Get("another query", "/language?name=de-DE"),
                Steps.Get("a form to fill in", "/account/edit"),
                Steps.Get("a plain link", "/plain"),
                Steps.Get("the ajax link's route", "/tickets/sortList/opentickets?columnName=Priority", ajax: true),
                Steps.Get("the ajax link's route, not ajax", "/tickets/sortList/opentickets?columnName=Priority"),
            ],
        };
        var rec = await runner.RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.Equal(
        [
            "GET /language?name=pt-BR [] header=[]",
            "POST /language?name=pt-BR [__RequestVerificationToken=tok-1] header=[]",
            "GET /language?name=de-DE [] header=[]",
            "GET /account/edit [] header=[]",
            "GET /plain [] header=[]",
            "POST /tickets/sortList/opentickets?columnName=Priority [] header=[tok-1]",
            "GET /tickets/sortList/opentickets?columnName=Priority [] header=[]",
        ], rec.Where(e => e.StepName != "page").Select(e => e.Response!.Body!));
        Assert.Equal("POST", rec.Single(e => e.StepName == "the form's action").Request.Method);
        Assert.Equal(2, runner.PostedLinkSteps);
    }
}
