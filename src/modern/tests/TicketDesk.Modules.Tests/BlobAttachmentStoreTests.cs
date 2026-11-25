using System.Text;
using Azure.Storage.Blobs;
using TicketDesk.Attachments;

namespace TicketDesk.Modules.Tests;

/// <summary>
/// The Blob attachment store against Azurite (the lab's shared store), in a throw-away container of its own. Needs
/// TD_AZURE_STORAGE_CONNECTION_HOST (from $W/.secrets/legacy-filestore.env); skipped without it.
/// </summary>
public sealed class BlobAttachmentStoreTests : IAsyncLifetime
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("TD_AZURE_STORAGE_CONNECTION_HOST") is { Length: > 0 } c ? c : null;
    private BlobContainerClient? _container;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid TempId = Guid.Parse("da6a5d4c-8f66-4aac-a60f-361c41d9abe9");

    public async ValueTask InitializeAsync()
    {
        if (Connection == null) return;
        _container = new BlobContainerClient(Connection, $"td-routes-test-{Guid.NewGuid():N}");
        await _container.CreateAsync();
        async Task Put(string name, string text) => await _container.UploadBlobAsync(name, new BinaryData(Encoding.UTF8.GetBytes(text)));
        await Put("7/b.txt", "bee");
        await Put("7/A notes (1).txt", "alpha");
        await Put("7/", "");                           // folder marker of the ticket (legacy BlobFileSystem)
        await Put("7/sub/deeper.txt", "not a file of the ticket");
        await Put("70/other.txt", "other ticket");
        await Put("pending/upload.txt." + TempId, "pending!");
        await Put("pending/other.txt." + Guid.NewGuid(), "someone else's upload");
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null) await _container.DeleteIfExistsAsync();
    }

    private BlobAttachmentStore Store()
    {
        if (_container == null) Assert.Skip("set TD_AZURE_STORAGE_CONNECTION_HOST (Azurite) to run the Blob store tests");
        return new BlobAttachmentStore(_container!);
    }

    [Fact]
    public async Task Lists_a_tickets_files_in_blob_order_without_folders_or_other_tickets()
    {
        var store = Store();
        var files = await store.ListAsync(7, Ct);
        Assert.Equal(["A notes (1).txt", "b.txt"], files.Select(f => f.Name));
        Assert.Equal([5L, 3L], files.Select(f => f.Size));
        Assert.Empty(await store.ListAsync(8, Ct));
        Assert.Empty(await store.ListAsync(0, Ct));
    }

    [Fact]
    public async Task Lists_the_pending_uploads_of_one_upload_session()
    {
        var pending = await Store().ListPendingAsync(TempId, Ct);
        Assert.Equal("upload.txt", Assert.Single(pending).Name);
        Assert.Empty(await Store().ListPendingAsync(Guid.NewGuid(), Ct));
    }

    [Theory]
    [InlineData("b.txt", "bee")]
    [InlineData("A notes (1).txt", "alpha")]
    [InlineData("../70/other.txt", null)]
    [InlineData("sub/deeper.txt", null)]
    [InlineData("missing.txt", null)]
    public async Task Opens_only_safe_names_of_the_ticket(string name, string? content)
    {
        await using var stream = await Store().OpenAsync(7, name, Ct);
        if (content == null) { Assert.Null(stream); return; }
        Assert.Equal(content, await new StreamReader(stream!).ReadToEndAsync(Ct));
    }

    [Fact]
    public async Task Opens_a_pending_upload_by_its_session()
    {
        await using var stream = await Store().OpenPendingAsync(TempId, "upload.txt", Ct);
        Assert.Equal("pending!", await new StreamReader(stream!).ReadToEndAsync(Ct));
        Assert.Null(await Store().OpenPendingAsync(Guid.NewGuid(), "upload.txt", Ct));
    }

    [Fact]
    public async Task A_missing_container_means_no_files()
    {
        if (Connection == null) Assert.Skip("set TD_AZURE_STORAGE_CONNECTION_HOST");
        var store = new BlobAttachmentStore(new BlobContainerClient(Connection, $"td-routes-none-{Guid.NewGuid():N}"));
        Assert.Empty(await store.ListAsync(7, Ct));
        Assert.Null(await store.OpenAsync(7, "b.txt", Ct));
    }
}
