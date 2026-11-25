using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Replay.Tests;

/// <summary>
/// AJAX posts without a form (the subscription toggle, Dropzone uploads) send the latest page's antiforgery token as a
/// header, as the new app's page scripts do in a browser; form submissions and posts that carry the field do not.
/// </summary>
public sealed class AntiforgeryHeaderTests
{
    [Fact]
    public async Task Plain_ajax_posts_carry_the_token_of_the_latest_page_that_had_one()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapGet("/page", () => Results.Content("<html><body><input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"tok&amp;1\" /></body></html>", "text/html"));
        app.MapGet("/partial", () => Results.Content("<div>no token here</div>", "text/html"));
        app.MapPost("/ajax", (HttpRequest r) => Results.Text(r.Headers[JourneyRunner.AntiforgeryHeader].ToString()));
        await app.StartAsync(TestContext.Current.CancellationToken);
        var runner = new JourneyRunner(new RunnerOptions { Target = new Uri("http://localhost/"), HandlerFactory = () => app.GetTestServer().CreateHandler() });
        var journey = new JourneySpec
        {
            Name = "t",
            Steps =
            [
                Steps.Post("before any page", "/ajax", [], ajax: true),
                Steps.Get("page", "/page"),
                Steps.Get("partial", "/partial", ajax: true),
                Steps.Post("after the page", "/ajax", [], ajax: true),
                Steps.Post("with its own field", "/ajax", [Steps.F("__RequestVerificationToken", "x")], ajax: true),
                Steps.Post("not ajax", "/ajax", []),
            ],
        };
        var rec = await runner.RunJourneyAsync(journey, TestContext.Current.CancellationToken);
        Assert.Equal(["", "tok&1", "", ""], rec.Where(e => e.Request.Method == "POST").Select(e => e.Response!.Body ?? ""));
    }
}
