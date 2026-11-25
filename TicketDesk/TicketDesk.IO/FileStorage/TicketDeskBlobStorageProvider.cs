// Added for the ASP.NET Core migration (shared attachment store). Distributed under the Ms-PL like the rest of TicketDesk.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AzureBlobFileSystem;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace TicketDesk.IO
{
    /// <summary>
    /// The Azure Blob file store TicketDesk uses when an AzureStorage connection string exists: BlobFileSystem.Azure's
    /// <see cref="AzureBlobStorageProvider"/> for everything except renames.
    /// </summary>
    /// <remarks>
    /// BlobFileSystem.Azure 0.0.7 was built against WindowsAzure.Storage 3.x; its RenameFile calls
    /// <c>CloudBlockBlob.StartCopyFromBlob</c>, which the WindowsAzure.Storage 8.2.0 TicketDesk ships no longer has, so
    /// committing pending attachments threw MissingMethodException and the Blob mode never worked. Renames (and folder
    /// renames, which are renames of every blob below a prefix) are done here with the 8.2.0 API: server-side copy, wait
    /// until the copy has finished, delete the source. Paths are "container/blob/name", as in the wrapped provider.
    /// </remarks>
    internal sealed class TicketDeskBlobStorageProvider : IStorageProvider
    {
        private static readonly TimeSpan CopyTimeout = TimeSpan.FromMinutes(2);
        private readonly AzureBlobStorageProvider _inner;

        public TicketDeskBlobStorageProvider(CloudStorageAccount account)
        {
            _inner = new AzureBlobStorageProvider(account);
        }

        public DateTimeOffset? DefaultSharedAccessExpiration
        {
            get { return _inner.DefaultSharedAccessExpiration; }
            set { _inner.DefaultSharedAccessExpiration = value; }
        }

        public IStorageFile GetFile(string path) { return _inner.GetFile(path); }
        public IEnumerable<IStorageFile> ListFiles(string path) { return _inner.ListFiles(path); }
        public IEnumerable<IStorageFolder> ListFolders(string path) { return _inner.ListFolders(path); }
        public bool TryCreateFolder(string path) { return _inner.TryCreateFolder(path); }
        public void CreateFolder(string path) { _inner.CreateFolder(path); }
        public void DeleteFolder(string path) { _inner.DeleteFolder(path); }
        public void DeleteFile(string path) { _inner.DeleteFile(path); }
        public IStorageFile CreateFile(string path) { return _inner.CreateFile(path); }
        public bool TrySaveStream(string path, Stream inputStream) { return _inner.TrySaveStream(path, inputStream); }
        public void SaveStream(string path, Stream inputStream) { _inner.SaveStream(path, inputStream); }
        public string Combine(string path1, string path2) { return _inner.Combine(path1, path2); }
        public bool FileExists(string path) { return _inner.FileExists(path); }

        public void RenameFile(string oldPath, string newPath)
        {
            // FileExists also creates a missing container, as the wrapped provider does for every path it touches.
            if (!_inner.FileExists(oldPath))
            {
                throw new ArgumentException("File " + oldPath + " does not exist");
            }
            if (_inner.FileExists(newPath))
            {
                throw new ArgumentException("File " + newPath + " already exists");
            }

            var source = GetBlob(oldPath);
            var target = GetBlob(newPath);
            target.StartCopy(source);
            WaitForCopy(target, oldPath);
            source.Delete();
        }

        public void RenameFolder(string oldPath, string newPath)
        {
            var oldPrefix = oldPath.TrimEnd('/') + "/";
            var newPrefix = newPath.TrimEnd('/') + "/";
            var parts = Split(oldPrefix);
            if (parts.Item2.Length == 0)
            {
                throw new ArgumentException("Renaming root folders represented by azure containers is not supported", "oldPath");
            }
            var blobs = _inner.BlobClient.GetContainerReference(parts.Item1)
                .ListBlobs(parts.Item2, true).OfType<CloudBlockBlob>().ToList();
            foreach (var blob in blobs)
            {
                var relative = blob.Name.Substring(parts.Item2.Length);
                RenameFile(oldPrefix + relative, newPrefix + relative);
            }
        }

        private CloudBlockBlob GetBlob(string path)
        {
            var parts = Split(path);
            return _inner.BlobClient.GetContainerReference(parts.Item1).GetBlockBlobReference(parts.Item2);
        }

        private static Tuple<string, string> Split(string path)
        {
            if (path.StartsWith("/") || path.StartsWith("http://") || path.StartsWith("https://"))
            {
                throw new ArgumentException("Path must be relative");
            }
            var segments = path.Split('/');
            return Tuple.Create(segments[0], string.Join("/", segments.Skip(1)));
        }

        private static void WaitForCopy(CloudBlockBlob target, string sourcePath)
        {
            // A copy within one storage account normally completes before StartCopy returns; poll in case it does not.
            var deadline = DateTime.UtcNow + CopyTimeout;
            target.FetchAttributes();
            while (target.CopyState != null && target.CopyState.Status == CopyStatus.Pending)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new IOException("Copy of " + sourcePath + " did not finish within " + CopyTimeout.TotalSeconds + " s");
                }
                Thread.Sleep(200);
                target.FetchAttributes();
            }
            if (target.CopyState != null && target.CopyState.Status != CopyStatus.Success)
            {
                throw new IOException("Copy of " + sourcePath + " ended with " + target.CopyState.Status + ": " + target.CopyState.StatusDescription);
            }
        }
    }
}
