<#
.SYNOPSIS
  Copies TicketDesk attachments from the disk store (App_Data\filestore\ticketdesk-attachments) into Azure Blob storage
  (container ticketdesk-attachments), the layout TicketDesk's Blob file store reads, and verifies the copy.
.DESCRIPTION
  Blob name = path below ticketdesk-attachments with '/' (e.g. 1234/report.pdf, pending/x.txt.<tempId>).
  Idempotent: a file whose blob already exists with the same size and Content-MD5 is skipped; a missing or different blob is
  (re)uploaded with its Content-MD5. The disk files are never changed or deleted.
  Before: file count, bytes and MD5 per file on disk. After: the container is listed and every disk file must have a blob
  with the same size and MD5 (blobs without a disk file, e.g. uploads made after the switch, are counted, not touched).
  Writes a CSV manifest (path, bytes, md5, action) and prints one summary line; exit code 1 when a file does not match.
  Uses the WindowsAzure.Storage assembly deployed with TicketDesk (no extra download). The connection string comes from
  the environment (TD_AZURE_STORAGE_CONNECTION) and is never printed.
  Cutover: run it while the app still uses disk (bulk copy), stop the app pool, run it again (delta), deploy with
  TD_AZURE_STORAGE_CONNECTION (Deploy-TdApp.ps1), start.
.EXAMPLE
  $env:TD_AZURE_STORAGE_CONNECTION = '...'; .\Move-TdFileStore.ps1 -ManifestPath C:\TdBackups\filestore-move.csv
#>
[CmdletBinding()]
param(
    [string]$SitePath = 'C:\inetpub\ticketdesk',
    [string]$ConnectionString = $env:TD_AZURE_STORAGE_CONNECTION,
    [string]$Container = 'ticketdesk-attachments',
    [string]$ManifestPath = ''
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
if (-not $ConnectionString) { throw 'no connection string (TD_AZURE_STORAGE_CONNECTION)' }
foreach ($d in 'Newtonsoft.Json', 'Microsoft.Azure.KeyVault.Core', 'Microsoft.WindowsAzure.Storage') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $SitePath "bin\$d.dll"))
}
$sw = [Diagnostics.Stopwatch]::StartNew()
$account = [Microsoft.WindowsAzure.Storage.CloudStorageAccount]::Parse($ConnectionString)
$cont = $account.CreateCloudBlobClient().GetContainerReference($Container)
[void]$cont.CreateIfNotExists()
$root = Join-Path $SitePath 'App_Data\filestore\ticketdesk-attachments'
$md5 = [Security.Cryptography.MD5]::Create()
function Get-Md5([string]$path) { $s = [IO.File]::OpenRead($path); try { [Convert]::ToBase64String($md5.ComputeHash($s)) } finally { $s.Dispose() } }

# ---- before: what is on disk
$files = @()
if (Test-Path $root) {
    $files = @(Get-ChildItem $root -Recurse -File | ForEach-Object {
        [pscustomobject]@{ Name = $_.FullName.Substring($root.Length + 1).Replace('\', '/'); Path = $_.FullName; Bytes = $_.Length; Md5 = (Get-Md5 $_.FullName); Action = '' }
    })
}
$diskBytes = ($files | Measure-Object Bytes -Sum).Sum; if (-not $diskBytes) { $diskBytes = 0 }

# ---- copy
$opts = New-Object Microsoft.WindowsAzure.Storage.Blob.BlobRequestOptions
$opts.StoreBlobContentMD5 = $true
foreach ($f in $files) {
    $blob = $cont.GetBlockBlobReference($f.Name)
    if ($blob.Exists()) {
        $blob.FetchAttributes()
        if ($blob.Properties.Length -eq $f.Bytes -and $blob.Properties.ContentMD5 -eq $f.Md5) { $f.Action = 'skipped (same)'; continue }
        $f.Action = 'replaced'
    } else { $f.Action = 'uploaded' }
    $blob.UploadFromFile($f.Path, $null, $opts, $null)
}

# ---- after: list the container and compare
$blobs = @{}
foreach ($b in $cont.ListBlobs($null, $true, [Microsoft.WindowsAzure.Storage.Blob.BlobListingDetails]::None, $null, $null)) {
    if ($b.Name.EndsWith('/')) { continue }   # folder marker blobs of the Blob store
    $blobs[$b.Name] = $b
}
$mismatch = 0
foreach ($f in $files) {
    $b = $blobs[$f.Name]
    if (-not $b) { $f.Action += '; MISSING after copy'; $mismatch++; continue }
    $b.FetchAttributes()
    if ($b.Properties.Length -ne $f.Bytes -or $b.Properties.ContentMD5 -ne $f.Md5) { $f.Action += '; MISMATCH after copy'; $mismatch++ }
}
$onDisk = @{}; foreach ($f in $files) { $onDisk[$f.Name] = $true }
$extra = @($blobs.Keys | Where-Object { -not $onDisk.ContainsKey($_) }).Count
if ($ManifestPath) {
    New-Item -ItemType Directory -Force -Path (Split-Path $ManifestPath) | Out-Null
    $files | Select-Object Name, Bytes, Md5, Action | Export-Csv -Path $ManifestPath -NoTypeInformation -Encoding UTF8
}
$endpoint = if ($ConnectionString -match '(?i)BlobEndpoint=([^;]+)') { $Matches[1] } else { $account.BlobEndpoint.ToString() }
$up = @($files | Where-Object { $_.Action -like 'uploaded*' -or $_.Action -like 'replaced*' }).Count
$skip = @($files | Where-Object { $_.Action -like 'skipped*' }).Count
Write-TdLog ("filestore move -> {0}/{1}: disk {2} files {3} bytes; uploaded {4}, skipped {5}; after: {6} of {2} match (size + MD5), {7} blob(s) without a disk file; {8:N1} s" -f `
    $endpoint, $Container, $files.Count, $diskBytes, $up, $skip, ($files.Count - $mismatch), $extra, $sw.Elapsed.TotalSeconds)
if ($mismatch -gt 0) { Write-TdLog "$mismatch file(s) do not match" WARN; exit 1 }
