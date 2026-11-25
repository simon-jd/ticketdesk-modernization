<#
.SYNOPSIS
  Backs up legacy TicketDesk: database (.bak), site files (incl. App_Data attachments and Lucene index)
  and configuration (Web.config, IIS site/app pool definition, TLS binding metadata).
.DESCRIPTION
  Writes one folder per run: <BackupRoot>\<yyyyMMddTHHmmssZ>\ with
    <DatabaseName>.bak        full backup WITH CHECKSUM, COPY_ONLY unless -NotCopyOnly
    site.zip                  everything under the site's physical path
    web.config                the live Web.config (also inside site.zip)
    iis-site.xml, iis-apppool.xml   appcmd exports (re-importable)
    manifest.json             sizes, SHA256, versions, cert thumbprint
  Old run folders beyond -RetentionDays are removed. Safe to run any time (online backup).
  With -BlobContainerUrl (Azure VM) the run folder is also copied to blob storage with the VM's managed
  identity, manifest.json last, so a folder with a manifest in blob is complete.
.EXAMPLE
  .\Backup-TdSite.ps1 -BackupRoot D:\TdBackups -RetentionDays 14
#>
[CmdletBinding()]
param(
    [string]$SiteName = 'TicketDesk',
    [string]$AppPoolName = 'TicketDesk',
    [string]$SqlServerInstance = '.\SQLEXPRESS',
    [string]$DatabaseName = 'TicketDesk',
    [string]$BackupRoot = 'C:\TdBackups',
    [int]$RetentionDays = 14,
    [switch]$NotCopyOnly,
    # Fixed output folder name instead of a timestamp (used for exports such as the clean baseline).
    [string]$Label,
    # Azure VM: also copy the run folder to <BlobContainerUrl>/<BlobPrefix>/<run>/ with the VM's managed
    # identity (Put Blob over HTTPS; no keys or SAS). Empty = local backup only.
    [string]$BlobContainerUrl,
    [string]$BlobPrefix = $env:COMPUTERNAME.ToLowerInvariant()
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
Import-Module WebAdministration

$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$name = if ($Label) { $Label } else { $stamp }
if (-not (Test-Path $BackupRoot)) { New-Item -ItemType Directory -Path $BackupRoot | Out-Null }
# The SQL Server service account writes the .bak itself.
$instId = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').($SqlServerInstance.Split('\')[-1])
$svc = Get-CimInstance Win32_Service -Filter "Name='MSSQL`$$($SqlServerInstance.Split('\')[-1])'"
Set-TdAcl -Path $BackupRoot -Identity $svc.StartName -Rights 'Modify' | Out-Null
$dir = Join-Path $BackupRoot $name
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Path $dir | Out-Null

# ---------- database
$bak = Join-Path $dir "$DatabaseName.bak"
$copyOnly = if ($NotCopyOnly) { '' } else { 'COPY_ONLY, ' }
$sw = [Diagnostics.Stopwatch]::StartNew()
Invoke-TdSql -ServerInstance $SqlServerInstance -NonQuery -TimeoutSeconds 3600 -Query @"
DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = @path WITH $copyOnly INIT, FORMAT, CHECKSUM, NAME = @n';
EXEC sys.sp_executesql @sql, N'@path nvarchar(400), @n nvarchar(128)', @path = @path, @n = @n;
"@ -Parameters @{ db = $DatabaseName; path = $bak; n = "TicketDesk $name" }
Invoke-TdSql -ServerInstance $SqlServerInstance -NonQuery -Query 'RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM' -Parameters @{ path = $bak }
Write-TdLog ("database {0} -> {1} ({2:N0} bytes, {3:N1}s, verified)" -f $DatabaseName, $bak, (Get-Item $bak).Length, $sw.Elapsed.TotalSeconds) CHANGE

# ---------- site files + config
$sitePath = [Environment]::ExpandEnvironmentVariables((Get-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dir 'site.zip'
# Lucene keeps files open; copy to a staging folder first so the zip is consistent and not locked.
$stage = Join-Path $env:TEMP "td-backup-$stamp"
robocopy.exe $sitePath $stage /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, 'Optimal', $false)
Remove-Item $stage -Recurse -Force
Copy-Item (Join-Path $sitePath 'Web.config') (Join-Path $dir 'web.config')
$appcmd = Join-Path $env:windir 'System32\inetsrv\appcmd.exe'
& $appcmd list site $SiteName /config /xml | Out-File (Join-Path $dir 'iis-site.xml') -Encoding utf8
& $appcmd list apppool $AppPoolName /config /xml | Out-File (Join-Path $dir 'iis-apppool.xml') -Encoding utf8
Write-TdLog ("site files {0} -> {1} ({2:N0} bytes)" -f $sitePath, $zip, (Get-Item $zip).Length) CHANGE

# ---------- manifest
$ssl = Get-ChildItem IIS:\SslBindings | Where-Object { $_.Port -eq ((Get-WebBinding -Name $SiteName -Protocol https | Select-Object -First 1).bindingInformation.Split(':')[1]) } | Select-Object -First 1
$manifest = [ordered]@{
    created_utc   = $stamp
    host          = $env:COMPUTERNAME
    site          = $SiteName; app_pool = $AppPoolName; site_path = $sitePath
    sql_instance  = $SqlServerInstance; sql_instance_id = $instId; database = $DatabaseName
    sql_version   = (Invoke-TdSql -ServerInstance $SqlServerInstance -Scalar -Query "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32))")
    copy_only     = (-not $NotCopyOnly)
    tls_thumbprint = if ($ssl) { $ssl.Thumbprint } else { $null }
    files         = @(Get-ChildItem $dir -File | ForEach-Object { [ordered]@{ name = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash } })
}
$manifest | ConvertTo-Json -Depth 5 | Out-File (Join-Path $dir 'manifest.json') -Encoding utf8

# ---------- off-host copy (Azure: blob storage through the managed identity)
if ($BlobContainerUrl) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $files = @(Get-ChildItem $dir -File | Sort-Object { $_.Name -eq 'manifest.json' })   # manifest last = complete
    foreach ($f in $files) { Send-TdBlob -Url "$($BlobContainerUrl.TrimEnd('/'))/$BlobPrefix/$name/$($f.Name)" -Path $f.FullName }
    Write-TdLog ("copied {0} files ({1:N0} bytes) to {2}/{3}/{4}/ in {5:N1}s" -f $files.Count, ($files | Measure-Object Length -Sum).Sum,
        $BlobContainerUrl.TrimEnd('/'), $BlobPrefix, $name, $sw.Elapsed.TotalSeconds) CHANGE
}

# ---------- retention (timestamped folders only; labelled exports are kept)
if (-not $Label -and $RetentionDays -gt 0) {
    Get-ChildItem $BackupRoot -Directory | Where-Object { $_.Name -match '^\d{8}T\d{6}Z$' -and $_.CreationTimeUtc -lt (Get-Date).ToUniversalTime().AddDays(-$RetentionDays) } |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Write-TdLog "pruned $($_.Name)" CHANGE }
}
Write-TdLog "backup complete: $dir"
Write-Output $dir
