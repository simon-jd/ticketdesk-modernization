using System.Text;
using Azure.Storage.Blobs;
using TicketDesk.Attachments;

namespace TicketDesk.Modules.Tests;

/// <summary>
/// Wave 4 file writes on both stores: pending uploads, deleting them, committing them into a ticket's folder with the legacy
/// clash rule (<c>name(1).ext</c>), deleting committed files. The Blob half runs against Azurite (TD_AZURE_STORAGE_CONNECTION_HOST).
/// </summary>
public sealed class AttachmentWriteTests : IAsyncLifetime
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("TD_AZURE_STORAGE_CONNECTION_HOST") is { Length: > 0 } c ? c : null;
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("td-attach-write-");
    private BlobContainerClient? _container;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (Connection == null) return;
        _container = new BlobContainerClient(Connection, $"td-tickets-test-{Guid.NewGuid():N}");
        await _container.CreateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _root.Delete(recursive: true);
        if (_container != null) await _container.DeleteIfExistsAsync();
    }

    public static TheoryData<string> Stores => new() { "local", "blob" };

    private IAttachmentStore Store(string kind)
    {
        if (kind == "local") return new LocalAttachmentStore(_root.FullName);
        if (_container == null) Assert.Skip("set TD_AZURE_STORAGE_CONNECTION_HOST (Azurite) to run the Blob store tests");
        return new BlobAttachmentStore(_container!);
    }

    private static MemoryStream Text(string s) => new(Encoding.UTF8.GetBytes(s));

    private async Task<string> Read(IAttachmentStore store, int ticketId, string name)
    {
        await using var s = await store.OpenAsync(ticketId, name, Ct);
        Assert.NotNull(s);
        return await new StreamReader(s!).ReadToEndAsync(Ct);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Pending_uploads_are_listed_deleted_and_committed_with_the_legacy_clash_rule(string kind)
    {
        var store = Store(kind);
        var tempId = Guid.NewGuid();
        Assert.True(await store.SavePendingAsync(tempId, "note.txt", Text("first"), Ct));
        Assert.True(await store.SavePendingAsync(tempId, "extra.txt", Text("mistake"), Ct));
        await store.DeletePendingAsync(tempId, "extra.txt", Ct);
        Assert.Equal(["note.txt"], (await store.ListPendingAsync(tempId, Ct)).Select(f => f.Name));

        Assert.Equal(["note.txt"], await store.CommitPendingAsync(tempId, 42, Ct));
        Assert.Empty(await store.ListPendingAsync(tempId, Ct));
        Assert.Equal("first", await Read(store, 42, "note.txt"));

        // the same name again: legacy MoveFile added (1), (2), ... and the activity comment names the uploaded file
        var second = Guid.NewGuid();
        await store.SavePendingAsync(second, "note.txt", Text("second"), Ct);
        Assert.Equal(["note.txt"], await store.CommitPendingAsync(second, 42, Ct));
        Assert.Equal("second", await Read(store, 42, "note(1).txt"));
        Assert.Equal(["note(1).txt", "note.txt"], (await store.ListAsync(42, Ct)).Select(f => f.Name).Order(StringComparer.Ordinal));

        await store.DeleteAsync(42, "note.txt", Ct);
        Assert.Equal(["note(1).txt"], (await store.ListAsync(42, Ct)).Select(f => f.Name));
        Assert.Empty(await store.CommitPendingAsync(Guid.NewGuid(), 42, Ct)); // nothing pending: nothing moves
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task Unsafe_names_are_not_stored(string kind)
    {
        var store = Store(kind);
        var tempId = Guid.NewGuid();
        Assert.False(await store.SavePendingAsync(tempId, "../escape.txt", Text("x"), Ct));
        Assert.False(await store.SavePendingAsync(tempId, "CON.txt", Text("x"), Ct));
        Assert.False(await store.SavePendingAsync(Guid.Empty, "ok.txt", Text("x"), Ct));
        Assert.Empty(await store.ListPendingAsync(tempId, Ct));
    }
}
