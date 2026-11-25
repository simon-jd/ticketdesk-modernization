using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace TicketDesk.Replay.Tests;

/// <summary>
/// A tiny stand-in for TicketDesk used by the unit tests: random antiforgery tokens, generated IDs, timestamps,
/// cache-busting bundle URLs, cookie auth, AJAX partials, JSON and file downloads. The "changed" variant has
/// deliberate behavioural differences that the comparer must find.
/// </summary>
public sealed class FakeTicketApp : IAsyncDisposable
{
    public const string User = "admin@example.test";
    public const string Password = "fake-lab-password-1";

    private readonly WebApplication _app;
    private int _nextTicketId;
    private readonly Dictionary<int, string> _titles = new();

    public bool Changed { get; }
    public TestServer Server => _app.GetTestServer();
    public Uri BaseAddress => new("https://fake.test/");

    private FakeTicketApp(bool changed, int firstTicketId, TimeSpan clockSkew)
    {
        Changed = changed;
        _nextTicketId = firstTicketId;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _app = builder.Build();
        var now = () => DateTimeOffset.UtcNow + clockSkew;
        var rnd = new Random();

        string Token() => Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=');
        bool Authed(HttpContext c) => c.Request.Cookies.TryGetValue("auth", out var v) && v.StartsWith("ok-", StringComparison.Ordinal);
        IResult Html(string body) => Results.Content($"<!DOCTYPE html><html><head><title>Fake</title>" +
            $"<link href=\"/content/styles?v={Token()}\" rel=\"stylesheet\"/></head><body>{body}</body></html>", "text/html; charset=utf-8");
        IResult RequireAuth(HttpContext c) => Results.Redirect("/user/sign-in?ReturnUrl=" + Uri.EscapeDataString(c.Request.Path + c.Request.QueryString));
        string Antiforgery(HttpContext c)
        {
            var t = Token();
            c.Response.Cookies.Append("__RequestVerificationToken", t, new CookieOptions { HttpOnly = true, Path = "/" });
            return $"<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"{t}\" />";
        }
        bool ValidToken(HttpContext c, IFormCollection f) =>
            c.Request.Cookies.TryGetValue("__RequestVerificationToken", out var ck) && f["__RequestVerificationToken"] == ck;

        _app.MapGet("/", (HttpContext c) => Html(Authed(c)
            ? $"<h1>Welcome</h1><p class=\"lead muted\">Last sign-in {now():g} UTC ({rnd.Next(2, 50)} minutes ago)</p><a href=\"/tickets\">Tickets</a>"
            : "<h1>TicketDesk</h1><a href=\"/user/sign-in\">Sign in</a>"));

        _app.MapGet("/about", () => changed ? Results.NotFound() : Html("<h2>About</h2><p>Version 2.5</p>"));

        _app.MapGet("/user/sign-in", (HttpContext c, string? returnUrl) => Html(
            $"<form action=\"/user/sign-in?ReturnUrl={Uri.EscapeDataString(returnUrl ?? "")}\" method=\"post\">{Antiforgery(c)}" +
            "<input name=\"UserNameOrEmail\" type=\"text\" value=\"\"/><input name=\"Password\" type=\"password\"/>" +
            "<input name=\"RememberMe\" type=\"checkbox\" value=\"true\"/><input name=\"RememberMe\" type=\"hidden\" value=\"false\"/>" +
            "<button type=\"submit\">Sign in</button></form>"));

        _app.MapPost("/user/sign-in", async (HttpContext c, string? returnUrl) =>
        {
            var f = await c.Request.ReadFormAsync();
            if (!ValidToken(c, f)) return Results.BadRequest("antiforgery");
            if (f["UserNameOrEmail"] == User && f["Password"] == Password)
            {
                c.Response.Cookies.Append("auth", "ok-" + Token(), new CookieOptions { HttpOnly = true, Path = "/" });
                return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            }
            return Html($"<form action=\"/user/sign-in\" method=\"post\">{Antiforgery(c)}<div class=\"text-danger\">Invalid login attempt.</div>" +
                        "<input name=\"UserNameOrEmail\" type=\"text\"/><input name=\"Password\" type=\"password\"/></form>");
        });

        _app.MapGet("/user/sign-out", (HttpContext c) =>
        {
            c.Response.Cookies.Delete("auth");
            return Results.Redirect(changed ? "/user/sign-in" : "/");
        });

        _app.MapGet("/tickets", (HttpContext c) =>
        {
            if (!Authed(c)) return RequireAuth(c);
            var rows = string.Join("", _titles.OrderBy(t => t.Key).Select(t =>
                $"<tr><td><a href=\"/ticket/{t.Key}\">{WebUtility.HtmlEncode(t.Value)}</a></td><td>{now():yyyy-MM-ddTHH:mm:ss.fffZ}</td></tr>"));
            return Html($"<table class=\"table\">{rows}</table>");
        });

        _app.MapGet("/ticket/new", (HttpContext c) => !Authed(c) ? RequireAuth(c) : Html(
            $"<form action=\"/ticket/new\" method=\"post\">{Antiforgery(c)}<input type=\"hidden\" name=\"tempId\" value=\"{Guid.NewGuid()}\"/>" +
            "<input name=\"Title\" value=\"\"/><textarea name=\"Details\">\n</textarea>" +
            "<select name=\"TicketType\"><option value=\"\">-- choose --</option><option value=\"Problem\">Problem</option><option value=\"Question\">Question</option></select>" +
            "<select name=\"Category\"><option value=\"Hardware\" selected=\"selected\">Hardware</option><option value=\"Software\">Software</option></select>" +
            "</form><form action=\"/search\" method=\"get\"><input name=\"term\"/></form>"));

        _app.MapPost("/ticket/new", async (HttpContext c) =>
        {
            if (!Authed(c)) return Results.Unauthorized();
            var f = await c.Request.ReadFormAsync();
            if (!ValidToken(c, f)) return Results.BadRequest("antiforgery");
            if (string.IsNullOrEmpty(f["TicketType"]) || string.IsNullOrEmpty(f["Title"])) return Html("<div class=\"validation\">required</div>");
            var id = _nextTicketId++;
            _titles[id] = f["Title"]!;
            return Results.Redirect($"/ticket/{id}");
        });

        _app.MapGet("/ticket/{id:int}", (HttpContext c, int id) =>
        {
            if (!Authed(c)) return RequireAuth(c);
            if (!_titles.TryGetValue(id, out var title)) return Results.Redirect("/tickets");
            var heading = changed ? $"Ticket No. {id}" : $"Ticket #{id}";
            // attribute order and whitespace differ between variants on purpose; only the heading text is a real change
            var panel = changed
                ? $"<div   id=\"details\"  data-ticket=\"{id}\" class=\"panel  panel-default\">\n  <h2>{heading}</h2>\n  <p>{WebUtility.HtmlEncode(title)}</p>\n</div>"
                : $"<div class=\"panel-default panel\" data-ticket=\"{id}\" id=\"details\"><h2>{heading}</h2><p>{WebUtility.HtmlEncode(title)}</p></div>";
            return Html(panel + $"<!-- rendered {now():O} --><span class=\"created\">Created {now():M/d/yyyy h:mm:ss tt}</span>" +
                        $"<a href=\"/ticket/ticket-events?ticketId={id}\">events</a>");
        });

        _app.MapGet("/ticket/ticket-events", (HttpContext c, int ticketId) => !Authed(c) ? RequireAuth(c)
            : Results.Content($"<ul><li>created the ticket <span>{now():MMM d, yyyy}</span></li></ul>", "text/html; charset=utf-8"));

        _app.MapGet("/auto-complete/tag-list", (string? term) =>
        {
            var json = changed
                ? """[{"text":"printer","id":"printer"},{"text":"network","id":"network"},{"text":"extra","id":"extra"}]"""
                : """[{"id":"printer","text":"printer"},{"id":"network","text":"network"}]""";
            return Results.Content(json, "application/json; charset=utf-8");
        });

        _app.MapPost("/file/upload", async (HttpContext c, Guid tempId) =>
        {
            var f = await c.Request.ReadFormAsync();
            return f.Files.Count == 1 ? Results.Json(new { message = "" }) : Results.BadRequest();
        });

        _app.MapGet("/file/{id:int}/{name}", (int id, string name) =>
            Results.File(Encoding.UTF8.GetBytes(changed ? "different bytes" : "attachment bytes"), "application/octet-stream"));
    }

    public static async Task<FakeTicketApp> StartAsync(bool changed = false, int firstTicketId = 100, TimeSpan clockSkew = default)
    {
        var app = new FakeTicketApp(changed, firstTicketId, clockSkew);
        await app._app.StartAsync();
        return app;
    }

    public RunnerOptions RunnerOptions(string runId = "test", RouteTable? routes = null) => new()
    {
        Target = BaseAddress,
        RunId = runId,
        HandlerFactory = () => Server.CreateHandler(),
        Secrets = SecretStore.FromSources([], [new("TD_ADMIN_USER", User), new("TD_ADMIN_PASSWORD", Password)]),
        Routes = routes ?? new RouteTable(),
    };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>A journey covering the features the fake app emulates.</summary>
    public static JourneySpec FullJourney() => new()
    {
        Name = "fake-full",
        Steps =
        [
            Steps.Get("home anonymous", "/"),
            Steps.Get("about", "/about"),
            Steps.Get("tickets require sign-in", "/tickets"),
            Steps.Get("sign-in page", "/user/sign-in?ReturnUrl=%2Ftickets", expect: 200),
            Steps.Submit("sign-in", "form[action*='sign-in']", [Steps.F("UserNameOrEmail", "{secret:TD_ADMIN_USER}"), Steps.F("Password", "{secret:TD_ADMIN_PASSWORD}")], expect: 302),
            Steps.Get("new ticket", "/ticket/new", expect: 200, extract: new ExtractSpec("tempId", "html", "input[name='tempId']", "value")),
            Steps.Submit("create", "form[action$='/ticket/new']",
                [Steps.F("Title", "Printer offline"), Steps.F("TicketType", FormReader.FirstOption)], fromStep: "new ticket", expect: 302,
                extract: new ExtractSpec("ticketId", "location", @"/ticket/(\d+)")),
            Steps.Get("view", "/ticket/{ticketId}", expect: 200),
            Steps.Get("events partial", "/ticket/ticket-events?ticketId={ticketId}", ajax: true),
            Steps.Get("tags json", "/auto-complete/tag-list?term=p", ajax: true),
            Steps.Post("upload", "/file/upload?tempId={tempId}", [], ajax: true, file: new FileSpec("file", "note.txt", "text/plain", "hello")),
            Steps.Get("download", "/file/{ticketId}/note.txt"),
            Steps.Get("home signed in", "/"),
            Steps.Get("sign out", "/user/sign-out"),
        ],
    };
}
