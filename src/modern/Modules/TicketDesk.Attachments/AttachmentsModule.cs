using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TicketDesk.Attachments;

public sealed record AttachmentInfo(string Name, long Size, DateTimeOffset LastModified);

/// <summary>
/// Attachments as the legacy app stores them (files, not database rows, since Td2.5.007): committed files per ticket, and
/// pending uploads (<c>pending/{fileName}.{tempId}</c>) until the ticket is saved. Names are checked (S8: safe file names).
/// </summary>
public interface IAttachmentStore
{
    Task<IReadOnlyList<AttachmentInfo>> ListAsync(int ticketId, CancellationToken ct);

    /// <summary>Pending uploads of one upload session; names without the <c>.{tempId}</c> suffix.</summary>
    Task<IReadOnlyList<AttachmentInfo>> ListPendingAsync(Guid tempId, CancellationToken ct);

    /// <summary>The content of a committed file, or null when the name is unsafe or there is no such file.</summary>
    Task<Stream?> OpenAsync(int ticketId, string fileName, CancellationToken ct);

    /// <summary>The content of a pending upload, or null.</summary>
    Task<Stream?> OpenPendingAsync(Guid tempId, string fileName, CancellationToken ct);

    /// <summary>Stores a pending upload (<c>pending/{fileName}.{tempId}</c>, replacing one of the same name). False for an unsafe name.</summary>
    Task<bool> SavePendingAsync(Guid tempId, string fileName, Stream content, CancellationToken ct);

    /// <summary>Removes a pending upload if it exists.</summary>
    Task DeletePendingAsync(Guid tempId, string fileName, CancellationToken ct);

    /// <summary>Removes a committed file if it exists.</summary>
    Task DeleteAsync(int ticketId, string fileName, CancellationToken ct);

    /// <summary>
    /// Legacy <c>CommitPendingAttachments</c>: moves every pending upload of <paramref name="tempId"/> into the ticket's folder
    /// (a clash becomes <c>name(1).ext</c>, <c>name(2).ext</c>, ...) and returns the uploaded names in listing order.
    /// </summary>
    Task<IReadOnlyList<string>> CommitPendingAsync(Guid tempId, int ticketId, CancellationToken ct);
}

/// <summary>The legacy clash rule of <c>TicketDeskFileStore.MoveFile</c>.</summary>
public static class AttachmentNameClash
{
    public static string Candidate(string fileName, int count) =>
        count == 0 ? fileName : $"{Path.GetFileNameWithoutExtension(fileName)}({count}){Path.GetExtension(fileName)}";
}

public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    /// <summary>
    /// Root of the legacy layout <c>{root}/{ticketId}/{fileName}</c> (legacy default
    /// <c>App_Data/filestore/ticketdesk-attachments</c>). Relative paths resolve against the content root. During
    /// coexistence both apps must point at one shared store.
    /// </summary>
    public string RootPath { get; set; } = "App_Data/filestore/ticketdesk-attachments";

    /// <summary>"Local" (folder under <see cref="RootPath"/>) or "Blob" (the store shared with the legacy app).</summary>
    public string Store { get; set; } = "Local";

    /// <summary>Blob connection string (Key Vault / secrets file; never in the repository). Azurite in the lab.</summary>
    public string? BlobConnectionString { get; set; }

    /// <summary>The legacy app's container: blobs <c>{ticketId}/{fileName}</c> and <c>pending/{fileName}.{tempId}</c>.</summary>
    public string BlobContainer { get; set; } = "ticketdesk-attachments";
}

/// <summary>
/// File-name rules for attachments. Names come from uploads and URLs, and files move between the Windows VM and Linux
/// containers, so the rules are the union of both platforms' restrictions.
/// </summary>
public static class AttachmentFileNames
{
    private static readonly HashSet<string> WindowsReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] Forbidden = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static bool IsSafe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255) return false;
        if (name is "." or "..") return false;
        if (name != name.Trim() || name.EndsWith('.')) return false;
        if (name.IndexOfAny(Forbidden) >= 0 || name.Any(char.IsControl)) return false;
        var stem = name.Split('.')[0];
        return !WindowsReserved.Contains(stem);
    }
}

public sealed class LocalAttachmentStore : IAttachmentStore
{
    private readonly string _root;

    public LocalAttachmentStore(IOptions<AttachmentOptions> options, IHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, options.Value.RootPath))
    {
    }

    public LocalAttachmentStore(string rootPath) => _root = Path.GetFullPath(rootPath);

    public Task<IReadOnlyList<AttachmentInfo>> ListAsync(int ticketId, CancellationToken ct)
    {
        IReadOnlyList<AttachmentInfo> result = [];
        var folder = TicketFolder(ticketId);
        if (folder != null && Directory.Exists(folder) && !IsLink(new DirectoryInfo(folder)))
        {
            result = new DirectoryInfo(folder).EnumerateFiles()
                .Where(f => !IsLink(f) && AttachmentFileNames.IsSafe(f.Name))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new AttachmentInfo(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();
        }
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AttachmentInfo>> ListPendingAsync(Guid tempId, CancellationToken ct)
    {
        IReadOnlyList<AttachmentInfo> result = [];
        var folder = Path.Combine(_root, "pending");
        var suffix = "." + tempId.ToString();
        if (tempId != Guid.Empty && Directory.Exists(folder) && !IsLink(new DirectoryInfo(folder)))
        {
            result = new DirectoryInfo(folder).EnumerateFiles("*" + suffix)
                .Where(f => !IsLink(f) && f.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && AttachmentFileNames.IsSafe(f.Name[..^suffix.Length]))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => new AttachmentInfo(f.Name[..^suffix.Length], f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();
        }
        return Task.FromResult(result);
    }

    public Task<Stream?> OpenAsync(int ticketId, string fileName, CancellationToken ct) =>
        Task.FromResult<Stream?>(TryResolve(ticketId, fileName, out var path) ? File.OpenRead(path) : null);

    public Task<Stream?> OpenPendingAsync(Guid tempId, string fileName, CancellationToken ct)
    {
        var folder = Path.Combine(_root, "pending");
        if (tempId == Guid.Empty || !AttachmentFileNames.IsSafe(fileName)) return Task.FromResult<Stream?>(null);
        var info = new FileInfo(Path.Combine(folder, fileName + "." + tempId));
        return Task.FromResult<Stream?>(info.Exists && !IsLink(info) && !IsLink(new DirectoryInfo(folder)) ? info.OpenRead() : null);
    }

    public async Task<bool> SavePendingAsync(Guid tempId, string fileName, Stream content, CancellationToken ct)
    {
        if (tempId == Guid.Empty || !AttachmentFileNames.IsSafe(fileName)) return false;
        var folder = Path.Combine(_root, "pending");
        Directory.CreateDirectory(folder);
        await using var f = File.Create(Path.Combine(folder, fileName + "." + tempId));
        await content.CopyToAsync(f, ct);
        return true;
    }

    public Task DeletePendingAsync(Guid tempId, string fileName, CancellationToken ct)
    {
        if (tempId != Guid.Empty && AttachmentFileNames.IsSafe(fileName))
        {
            var info = new FileInfo(Path.Combine(_root, "pending", fileName + "." + tempId));
            if (info.Exists && !IsLink(info)) info.Delete();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int ticketId, string fileName, CancellationToken ct)
    {
        if (TryResolve(ticketId, fileName, out var path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> CommitPendingAsync(Guid tempId, int ticketId, CancellationToken ct)
    {
        var pending = await ListPendingAsync(tempId, ct);
        var folder = TicketFolder(ticketId);
        if (pending.Count == 0 || folder == null) return [];
        Directory.CreateDirectory(folder);
        foreach (var file in pending)
        {
            var target = Enumerable.Range(0, int.MaxValue).Select(n => Path.Combine(folder, AttachmentNameClash.Candidate(file.Name, n))).First(p => !File.Exists(p));
            File.Move(Path.Combine(_root, "pending", file.Name + "." + tempId), target);
        }
        return pending.Select(p => p.Name).ToList();
    }

    public bool TryResolve(int ticketId, string fileName, out string fullPath)
    {
        fullPath = "";
        var folder = TicketFolder(ticketId);
        if (folder == null || !AttachmentFileNames.IsSafe(fileName)) return false;
        var candidate = Path.GetFullPath(Path.Combine(folder, fileName));
        if (!candidate.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
        var info = new FileInfo(candidate);
        if (!info.Exists || IsLink(info) || IsLink(new DirectoryInfo(folder))) return false;
        fullPath = candidate;
        return true;
    }

    private string? TicketFolder(int ticketId) =>
        ticketId > 0 ? Path.Combine(_root, ticketId.ToString(CultureInfo.InvariantCulture)) : null;

    // symlinks could point outside the store
    private static bool IsLink(FileSystemInfo info) => info.LinkTarget != null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
}

public static class AttachmentsModule
{
    public static IServiceCollection AddAttachmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AttachmentOptions>().Bind(configuration.GetSection(AttachmentOptions.SectionName));
        var store = configuration.GetSection(AttachmentOptions.SectionName)[nameof(AttachmentOptions.Store)];
        if (string.Equals(store, "Blob", StringComparison.OrdinalIgnoreCase)) services.AddSingleton<IAttachmentStore, BlobAttachmentStore>();
        else services.AddSingleton<IAttachmentStore, LocalAttachmentStore>();
        return services;
    }
}
