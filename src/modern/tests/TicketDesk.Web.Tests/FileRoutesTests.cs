using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketDesk.Data;
using TicketDesk.Testing;

namespace TicketDesk.Web.Tests;

/// <summary>
/// Wave 2 file reads over the Blob store (Azurite, own throw-away container) and the restored legacy data: download, the
/// attachment list (with pending uploads, L1) and the ticket's file panel, with the per-ticket rule (S8). Needs
/// TD_SQL_TEST_CONN (+ seeded DB) and TD_AZURE_STORAGE_CONNECTION_HOST; skipped otherwise.
/// </summary>
public sealed class FileRoutesTests : IAsyncLifetime
{
    private static readonly string? Blob = Environment.GetEnvironmentVariable("TD_AZURE_STORAGE_CONNECTION_HOST") is { Length: > 0 } c ? c : null;
    private static readonly Guid TempId = Guid.Parse("a3d88264-fc4d-494a-8d3e-6ccaf6bdb97b");
    private BlobContainerClient? _container;
    private string? _db;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await SqlTestEnvironment.SeededDatabaseAsync();
        if (Blob == null || _db == null) return;
        _container = new BlobContainerClient(Blob, $"td-routes-files-{Guid.NewGuid():N}");
        await _container.CreateAsync();
        await _container.UploadBlobAsync("1/report one.txt", new BinaryData(Encoding.UTF8.GetBytes("Replay attachment.\n")));
        await _container.UploadBlobAsync($"pending/new (2).png.{TempId}", new BinaryData(new byte[] { 1, 2, 3 }));
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null) await _container.DeleteIfExistsAsync();
    }

    private async Task<(TicketDeskApp App, string Owner, string Stranger)> StartAsync()
    {
        if (_container == null) Assert.Skip("needs TD_SQL_TEST_CONN (seeded DB) and TD_AZURE_STORAGE_CONNECTION_HOST (Azurite)");
        var s = WaveParityTests.Migrated(_db, "file.get", "file.attachments-info", "ticket.files");
        s["Attachments:Store"] = "Blob";
        s["Attachments:BlobConnectionString"] = Blob;
        s["Attachments:BlobContainer"] = _container!.Name;
        var app = new TicketDeskApp(s, testAuth: true);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDeskDbContext>();
        var t = await db.Tickets.Where(x => x.TicketId == 1).Select(x => new { x.Owner, x.AssignedTo, x.CreatedBy }).SingleAsync(Ct);
        var subscribers = await db.TicketSubscribers.Where(x => x.TicketId == 1).Select(x => x.SubscriberId).ToListAsync(Ct);
        var on = new[] { t.Owner, t.AssignedTo, t.CreatedBy }.Concat(subscribers).OfType<string>().ToHashSet();
        var stranger = await db.UserRoles.Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "TdInternalUsers") && !on.Contains(ur.UserId))
            .Select(ur => ur.UserId).FirstAsync(Ct);
        return (app, t.Owner, stranger);
    }

    private static async Task<HttpResponseMessage> Get(HttpClient c, string path, string principal)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, path);
        r.Headers.Add(TestAuthHandler.PrincipalHeader, principal);
        return await c.SendAsync(r);
    }

    [Fact]
    public async Task Staff_get_the_files_and_pending_uploads_with_the_legacy_urls()
    {
        var (app, _, _) = await StartAsync();
        await using var _ = app;
        using var c = app.RawClient();
        var admin = TestAuthHandler.Principal("Admin", "admin-id", "TdAdministrators");

        using (var r = await Get(c, "/ticket/ticket-files?ticketId=1", admin))
        {
            var html = await r.Content.ReadAsStringAsync(Ct);
            Assert.Contains("href=\"/file/1/report%20one.txt\"", html);
            Assert.Contains("Files:", html);
            Assert.Equal("s-maxage=0, private", r.Headers.CacheControl!.ToString());
        }
        using (var r = await Get(c, "/file/1/report%20one.txt", admin))
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("application/octet-stream", r.Content.Headers.ContentType!.ToString());
            Assert.Equal("Replay attachment.\n", await r.Content.ReadAsStringAsync(Ct));
        }
        using (var r = await Get(c, $"/file/{TempId}/new%20(2).png", admin))
            Assert.Equal([1, 2, 3], await r.Content.ReadAsByteArrayAsync(Ct));
        using (var r = await Get(c, $"/file/get-attachments-info?id=1&tempId={TempId}", admin))
        {
            Assert.Equal("application/json; charset=utf-8", r.Content.Headers.ContentType!.ToString());
            var files = JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement.EnumerateArray().ToList();
            Assert.Equal(2, files.Count);
            // L1: pending uploads come first, with a real URL (the legacy action threw here)
            Assert.Equal("new (2).png", files[0].GetProperty("name").GetString());
            Assert.False(files[0].GetProperty("isAttached").GetBoolean());
            Assert.Equal("image/png", files[0].GetProperty("type").GetString());
            Assert.Equal($"/file/{TempId}/new%20(2).png", files[0].GetProperty("url").GetString());
            Assert.Equal("report one.txt", files[1].GetProperty("name").GetString());
            Assert.Equal(19, files[1].GetProperty("size").GetInt32());
            Assert.True(files[1].GetProperty("isAttached").GetBoolean());
            Assert.Equal("text/plain", files[1].GetProperty("type").GetString());
            Assert.Equal("/file/1/report%20one.txt", files[1].GetProperty("url").GetString());
        }
        using (var r = await Get(c, "/file/1/missing.txt", admin)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using (var r = await Get(c, "/file/1/..%2F2%2Fx.txt", admin)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Internal_users_see_files_only_on_their_own_tickets()
    {
        var (app, owner, stranger) = await StartAsync();
        await using var _ = app;
        using var c = app.RawClient();
        var mine = TestAuthHandler.Principal("Owner", owner, "TdInternalUsers");
        var notMine = TestAuthHandler.Principal("Stranger", stranger, "TdInternalUsers");

        using (var r = await Get(c, "/file/1/report%20one.txt", mine)) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        // S8: a ticket the user is not on: no file panel, no list, and a 404 for the download (existence is not revealed)
        using (var r = await Get(c, "/file/1/report%20one.txt", notMine)) Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        using (var r = await Get(c, "/ticket/ticket-files?ticketId=1", notMine)) Assert.DoesNotContain("Files:", await r.Content.ReadAsStringAsync(Ct));
        using (var r = await Get(c, "/file/get-attachments-info?id=1", notMine)) Assert.Equal("[]", await r.Content.ReadAsStringAsync(Ct));
    }
}
