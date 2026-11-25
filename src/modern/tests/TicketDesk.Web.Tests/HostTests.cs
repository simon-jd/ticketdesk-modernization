using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

public sealed class HealthTests(ScratchDatabase db)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Ready(TicketDeskApp app, string path = "/health/ready")
    {
        using var response = await app.RawClient().GetAsync(path, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken));
    }

    private static string Check(JsonElement body, string name) =>
        body.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name).GetProperty("status").GetString()!;

    [Fact]
    public async Task Ready_is_healthy_when_the_database_answers_and_checks_nothing_else()
    {
        Assert.SkipUnless(db.Available, SqlTestEnvironment.SkipReason);
        await using var app = new TicketDeskApp(TicketDeskApp.Settings(db.ConnectionString));
        var (status, body) = await Ready(app);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", Check(body, "database"));
        // the legacy reachability check went with the legacy app (Round 4): the database is the only dependency of readiness
        Assert.Equal(["database"], body.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Unreachable_database_fails_readiness_but_not_liveness()
    {
        await using var app = new TicketDeskApp(TicketDeskApp.Settings());
        var (status, body) = await Ready(app);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", Check(body, "database"));
        var (liveStatus, live) = await Ready(app, "/health/live");
        Assert.Equal(HttpStatusCode.OK, liveStatus);
        Assert.Equal("Healthy", live.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Missing_connection_string_still_starts_the_host()
    {
        var settings = TicketDeskApp.Settings();
        settings["ConnectionStrings:TicketDesk"] = "";
        await using var app = new TicketDeskApp(settings);
        var (status, body) = await Ready(app);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", Check(body, "database"));
    }
}

/// <summary>docs/api/openapi.json is the checked-in OpenAPI document; this test regenerates it with TD_UPDATE_OPENAPI=1.</summary>
public sealed class OpenApiTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(File.Exists(Path.Combine(dir.FullName, "global.json")) && Directory.Exists(Path.Combine(dir.FullName, "src", "modern"))))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private async Task<string> CurrentDocumentAsync()
    {
        await using var app = new TicketDeskApp(TicketDeskApp.Settings());
        var json = await app.RawClient().GetStringAsync("/openapi/v1.json", Ct);
        return JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).ReplaceLineEndings("\n") + "\n";
    }

    [Fact]
    public async Task Checked_in_document_is_current()
    {
        var current = await CurrentDocumentAsync();
        var path = Path.Combine(RepoRoot(), "docs", "api", "openapi.json");
        if (Environment.GetEnvironmentVariable("TD_UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, current, Ct);
        }
        Assert.True(File.Exists(path), $"{path} is missing; run the tests with TD_UPDATE_OPENAPI=1");
        Assert.True((await File.ReadAllTextAsync(path, Ct)).ReplaceLineEndings("\n") == current,
            "docs/api/openapi.json is out of date; run the tests with TD_UPDATE_OPENAPI=1 and commit the file");
    }

    [Fact]
    public async Task Document_describes_the_endpoints()
    {
        var doc = JsonNode.Parse(await CurrentDocumentAsync())!;
        var paths = doc["paths"]!.AsObject();
        Assert.Equal(["/api/migration/routes", "/api/projects", "/api/reports/assignee-workload", "/api/reports/open-tickets", "/api/reports/time-to-close",
                "/api/session", "/api/tickets", "/api/tickets/options", "/api/tickets/{id}", "/api/tickets/{id}/activities"],
            paths.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.All(paths, p => Assert.Equal(p.Key switch { "/api/tickets" => new[] { "get", "post" }, "/api/tickets/{id}/activities" => new[] { "post" }, _ => new[] { "get" } },
            p.Value!.AsObject().Select(o => o.Key).Order(StringComparer.Ordinal)));
        Assert.NotNull(paths["/api/tickets"]!["post"]!["responses"]!["201"]);
        Assert.NotNull(paths["/api/tickets"]!["post"]!["responses"]!["400"]);
        // strict JSON numbers: integers are integers (no "integer | string" pattern unions in the generated client)
        Assert.Equal("integer", doc["components"]!["schemas"]!["TicketListItem"]!["properties"]!["ticketId"]!["type"]!.GetValue<string>());
        Assert.NotNull(paths["/api/tickets"]!["get"]!["responses"]!["400"]);
        Assert.NotNull(paths["/api/tickets/{id}"]!["get"]!["responses"]!["404"]);
        Assert.NotNull(paths["/api/reports/time-to-close"]!["get"]!["responses"]!["400"]);
        var statusEnum = doc["components"]!["schemas"]!["TicketStatus"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>());
        Assert.Equal(["Active", "MoreInfo", "Resolved", "Closed"], statusEnum);
        Assert.Null(doc["servers"]);
    }
}
