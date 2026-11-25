using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace TicketDesk.Attachments;

/// <summary>
/// The attachment store shared with the legacy app (Azure Blob; Azurite on lab-host in the lab): container
/// <c>ticketdesk-attachments</c>, blobs <c>{ticketId}/{fileName}</c> (committed) and <c>pending/{fileName}.{tempId}</c>
/// (pending uploads). Folder-marker blobs (names ending in '/') are not files. Listing order is the blob listing order
/// (ordinal by name), as the legacy app lists them.
/// </summary>
public sealed class BlobAttachmentStore : IAttachmentStore
{
    private readonly BlobContainerClient _container;

    public BlobAttachmentStore(IOptions<AttachmentOptions> options) : this(Create(options.Value)) { }

    public BlobAttachmentStore(BlobContainerClient container) => _container = container;

    private static BlobContainerClient Create(AttachmentOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.BlobConnectionString))
            throw new InvalidOperationException("Attachments:Store is Blob but Attachments:BlobConnectionString is not set.");
        return new BlobContainerClient(o.BlobConnectionString, o.BlobContainer);
    }

    public Task<IReadOnlyList<AttachmentInfo>> ListAsync(int ticketId, CancellationToken ct) =>
        ticketId > 0 ? List(ticketId.ToString(CultureInfo.InvariantCulture) + "/", suffix: null, ct) : Task.FromResult<IReadOnlyList<AttachmentInfo>>([]);

    public Task<IReadOnlyList<AttachmentInfo>> ListPendingAsync(Guid tempId, CancellationToken ct) =>
        tempId != Guid.Empty ? List("pending/", "." + tempId.ToString(), ct) : Task.FromResult<IReadOnlyList<AttachmentInfo>>([]);

    private async Task<IReadOnlyList<AttachmentInfo>> List(string prefix, string? suffix, CancellationToken ct)
    {
        var result = new List<AttachmentInfo>();
        try
        {
            await foreach (var item in _container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, "/", prefix, ct))
            {
                if (!item.IsBlob || item.Blob.Name.EndsWith('/')) continue;
                var name = item.Blob.Name[prefix.Length..];
                if (suffix != null)
                {
                    if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name[..^suffix.Length];
                }
                if (!AttachmentFileNames.IsSafe(name)) continue;
                result.Add(new AttachmentInfo(name, item.Blob.Properties.ContentLength ?? 0, item.Blob.Properties.LastModified ?? default));
            }
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            // no container yet: no files
        }
        return result;
    }

    public Task<Stream?> OpenAsync(int ticketId, string fileName, CancellationToken ct) =>
        ticketId > 0 && AttachmentFileNames.IsSafe(fileName) ? Open($"{ticketId.ToString(CultureInfo.InvariantCulture)}/{fileName}", ct) : Task.FromResult<Stream?>(null);

    public Task<Stream?> OpenPendingAsync(Guid tempId, string fileName, CancellationToken ct) =>
        tempId != Guid.Empty && AttachmentFileNames.IsSafe(fileName) ? Open($"pending/{fileName}.{tempId}", ct) : Task.FromResult<Stream?>(null);

    public async Task<bool> SavePendingAsync(Guid tempId, string fileName, Stream content, CancellationToken ct)
    {
        if (tempId == Guid.Empty || !AttachmentFileNames.IsSafe(fileName)) return false;
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        await _container.GetBlobClient($"pending/{fileName}.{tempId}").UploadAsync(content, overwrite: true, ct);
        return true;
    }

    public async Task DeletePendingAsync(Guid tempId, string fileName, CancellationToken ct)
    {
        if (tempId != Guid.Empty && AttachmentFileNames.IsSafe(fileName))
            await _container.GetBlobClient($"pending/{fileName}.{tempId}").DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(int ticketId, string fileName, CancellationToken ct)
    {
        if (ticketId > 0 && AttachmentFileNames.IsSafe(fileName))
            await _container.GetBlobClient($"{ticketId.ToString(CultureInfo.InvariantCulture)}/{fileName}").DeleteIfExistsAsync(cancellationToken: ct);
    }

    /// <summary>Copy then delete, as the legacy store's rename did (WindowsAzure.Storage StartCopy + delete).</summary>
    public async Task<IReadOnlyList<string>> CommitPendingAsync(Guid tempId, int ticketId, CancellationToken ct)
    {
        var pending = await ListPendingAsync(tempId, ct);
        if (pending.Count == 0 || ticketId <= 0) return [];
        var prefix = ticketId.ToString(CultureInfo.InvariantCulture) + "/";
        foreach (var file in pending)
        {
            var source = _container.GetBlobClient($"pending/{file.Name}.{tempId}");
            BlobClient target;
            var n = 0;
            do target = _container.GetBlobClient(prefix + AttachmentNameClash.Candidate(file.Name, n++));
            while (await target.ExistsAsync(ct));
            var content = await source.DownloadContentAsync(ct);
            await target.UploadAsync(content.Value.Content, overwrite: false, ct);
            await source.DeleteIfExistsAsync(cancellationToken: ct);
        }
        return pending.Select(p => p.Name).ToList();
    }

    private async Task<Stream?> Open(string blobName, CancellationToken ct)
    {
        try
        {
            var download = await _container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return download.Value.Content;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }
}
