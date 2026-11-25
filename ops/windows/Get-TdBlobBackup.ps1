<#
.SYNOPSIS
  Downloads a Backup-TdSite.ps1 run folder from blob storage (Azure VM, managed identity) and checks it
  against its manifest, so Restore-TdSite.ps1 can restore it.
.DESCRIPTION
  Folders live at <BlobContainerUrl>/<Prefix>/<run>/ (Backup-TdSite.ps1 -BlobContainerUrl writes them,
  manifest.json last). -Name latest picks the last folder name (UTC timestamps sort) that has a
  manifest.json; pass -Name for labelled folders. Every file listed in the manifest must match its
  SHA256. A folder without a manifest (e.g. an exported .bak staged by hand) is downloaded as is.
  Writes the local folder path as the last output line.
.EXAMPLE
  .\Get-TdBlobBackup.ps1 -BlobContainerUrl https://sttdlab.blob.core.windows.net/backups -Prefix tdlab-legacy
  .\Get-TdBlobBackup.ps1 -BlobContainerUrl ... -Prefix cutover -Name 20261108T170000Z -Destination D:\Restore
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BlobContainerUrl,
    [Parameter(Mandatory)][string]$Prefix,
    [string]$Name = 'latest',
    [string]$Destination = 'C:\TdBackups\from-blob'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
$container = $BlobContainerUrl.TrimEnd('/')
$Prefix = $Prefix.Trim('/')
$sw = [Diagnostics.Stopwatch]::StartNew()

$blobs = Get-TdBlobList -ContainerUrl $container -Prefix "$Prefix/"
if ($blobs.Count -eq 0) { throw "no blobs under $container/$Prefix/" }
if ($Name -eq 'latest') {
    # Newest complete run: the last folder name (UTC timestamps sort) among folders with a manifest.
    $re = "^$([regex]::Escape($Prefix))/([^/]+)/manifest\.json$"
    $runs = @($blobs | ForEach-Object { if ($_ -match $re) { $Matches[1] } })
    if ($runs.Count -eq 0) {
        $runs = @($blobs | ForEach-Object { ($_.Substring($Prefix.Length + 1) -split '/')[0] } | Sort-Object -Unique)
    }
    $Name = $runs | Sort-Object | Select-Object -Last 1
}
$files = @($blobs | Where-Object { $_.StartsWith("$Prefix/$Name/") -and $_.Length -gt "$Prefix/$Name/".Length })
if ($files.Count -eq 0) { throw "no files in $container/$Prefix/$Name/" }

$local = Join-Path $Destination $Name
if (Test-Path $local) { Remove-Item $local -Recurse -Force }
New-Item -ItemType Directory -Force -Path $local | Out-Null
$bytes = 0
foreach ($b in $files) {
    $target = Join-Path $local ($b.Substring("$Prefix/$Name/".Length) -replace '/', '\')
    [void](Save-TdBlob -Url "$container/$b" -Destination $target)
    $bytes += (Get-Item $target).Length
}
Write-TdLog ("downloaded {0} files ({1:N0} bytes) from {2}/{3}/{4}/ in {5:N1}s" -f $files.Count, $bytes, $container, $Prefix, $Name, $sw.Elapsed.TotalSeconds) CHANGE

$manifestPath = Join-Path $local 'manifest.json'
if (Test-Path $manifestPath) {
    $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
    foreach ($f in $m.files) {
        $p = Join-Path $local $f.name
        if (-not (Test-Path $p)) { throw "manifest lists $($f.name) but it is not in the download" }
        $h = (Get-FileHash $p -Algorithm SHA256).Hash
        if ($h -ne $f.sha256) { throw "SHA256 mismatch for $($f.name): $h, manifest $($f.sha256)" }
    }
    Write-TdLog ("manifest OK: {0} files, created {1} on {2}, SQL {3}" -f @($m.files).Count, $m.created_utc, $m.host, $m.sql_version)
} else {
    Write-TdLog "no manifest.json in $Prefix/$Name (staged export): files not checked against a manifest" WARN
}
Write-Output $local
