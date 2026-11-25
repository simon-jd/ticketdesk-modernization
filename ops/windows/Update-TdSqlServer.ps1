<#
.SYNOPSIS
  Brings the SQL Server 2022 instance to a cumulative-update level (default: CU27 + GDR, KB5126934,
  16.0.4305.5, 2026-10-13) and records @@VERSION before and after.
.DESCRIPTION
  Idempotent: does nothing when the instance's ProductVersion is already at or above -TargetVersion.
  Otherwise downloads the package once (Microsoft Update Catalog, SHA256 from the KB article), stops IIS
  if it depends on the instance (Set-TdIisSite.ps1 sets W3SVC -> MSSQL$<instance>), runs the patch as
  LocalSystem (like setup in Install-TdSqlExpress.ps1, so it also works over ssh), starts the services
  again and checks the new version. Writes <LogPath>\sql-update-<UTC>.json (before/after @@VERSION,
  package, hash, exit code, seconds, restart flag) and prints the same as one "SQLUPDATE {...}" line.
  Exit code 3010 (restart required) is reported, not hidden; reboot before measurements in that case.
.EXAMPLE
  .\Update-TdSqlServer.ps1
  .\Update-TdSqlServer.ps1 -TargetVersion 16.0.4295.3 -PackageUri <CU27 url> -PackageSha256 <hash>
#>
[CmdletBinding()]
param(
    [string]$InstanceName = 'SQLEXPRESS',
    [string]$TargetVersion = '16.0.4305.5',
    # SQLServer2022-KB5126934-x64.exe (Security Update for SQL Server 2022 RTM CU, i.e. CU27 + GDR):
    # Microsoft Update Catalog download for update ff6b594c-cb32-4c73-ae6d-4c87e8e036ee; SHA256 from
    # https://support.microsoft.com/servicing/sql/sql-server-2022/cumulative-update/kb5126934-cu27-gdr-october-2026
    [string]$PackageUri = 'https://catalog.s.download.windowsupdate.com/c/msdownload/update/software/secu/2026/10/sqlserver2022-kb5126934-x64_01742bd8accd82ea7314264b0a5608e28bdd8af0.exe',
    [string]$PackageSha256 = '8F1A7F0AC5F47FF98F5825687C8CCD897563AA835915F2271F908273E962E7E2',
    [string]$MediaRoot = 'C:\TdMedia\sql2022-cu',
    [string]$LogPath = 'C:\TdLogs',
    [int]$TimeoutMinutes = 60
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
$svcName = "MSSQL`$$InstanceName"
$server = ".\$InstanceName"
if (-not (Get-Service $svcName -ErrorAction SilentlyContinue)) { throw "$svcName is not installed (run Install-TdSqlExpress.ps1 first)" }
if ((Get-Service $svcName).Status -ne 'Running') { Start-Service $svcName }

function Get-SqlVersion {
    $r = Invoke-TdSql -ServerInstance $server -Query "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) AS v, CAST(SERVERPROPERTY('ProductUpdateLevel') AS nvarchar(16)) AS cu, CAST(SERVERPROPERTY('ProductUpdateReference') AS nvarchar(16)) AS kb, @@VERSION AS full_version"
    $row = $r.Rows[0]
    [ordered]@{ product_version = [string]$row.v; update_level = [string]$row.cu; kb = [string]$row.kb
                version = (([string]$row.full_version) -replace '\s+', ' ').Trim() }
}

$before = Get-SqlVersion
Write-TdLog "before: $($before.version)"
$record = [ordered]@{ host = $env:COMPUTERNAME; instance = $InstanceName; started_utc = (Get-Date).ToUniversalTime().ToString('o')
                      target_version = $TargetVersion; before = $before; package = $PackageUri; package_sha256 = $PackageSha256 }

if ([version]$before.product_version -ge [version]$TargetVersion) {
    Write-TdLog "already at $($before.product_version) (target $TargetVersion)" SKIP
    $record.action = 'none'; $record.after = $before
} else {
    $pkg = Join-Path $MediaRoot (Split-Path ($PackageUri -replace '\?.*$', '') -Leaf)
    Get-TdFile -Uri $PackageUri -Destination $pkg -Sha256 $PackageSha256
    # IIS depends on the instance on TicketDesk hosts: stop it first so the patch can restart SQL Server.
    $w3 = Get-Service W3SVC -ErrorAction SilentlyContinue
    $w3WasRunning = $w3 -and $w3.Status -eq 'Running'
    if ($w3WasRunning) { Stop-Service W3SVC -Force; Write-TdLog 'stopped W3SVC for the patch' CHANGE }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        Write-TdLog "patching $InstanceName with $(Split-Path $pkg -Leaf) (as SYSTEM)" CHANGE
        $exit = Invoke-TdProcessAsSystem -FilePath $pkg -TimeoutMinutes $TimeoutMinutes -ArgumentList @(
            '/quiet', '/IAcceptSQLServerLicenseTerms', '/Action=Patch', "/InstanceName=$InstanceName")
    } finally {
        if ((Get-Service $svcName).Status -ne 'Running') { Start-Service $svcName }
        if ($w3WasRunning) { Start-Service W3SVC; Write-TdLog 'started W3SVC' }
    }
    $record.exit_code = $exit; $record.seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    $record.restart_required = ($exit -eq 3010)
    if ($exit -notin 0, 3010) {
        $summary = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\Setup Bootstrap\Log\Summary.txt' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($summary) { Get-Content $summary.FullName -TotalCount 60 }
        throw "patch failed: exit $exit"
    }
    $record.action = 'patched'
    $record.after = Get-SqlVersion
    Write-TdLog ("after: {0} (exit {1}, {2}s{3})" -f $record.after.version, $exit, $record.seconds, $(if ($exit -eq 3010) { ', RESTART REQUIRED' } else { '' })) CHANGE
    if ([version]$record.after.product_version -lt [version]$TargetVersion) {
        throw "instance reports $($record.after.product_version) after the patch, expected >= $TargetVersion"
    }
}
$record.finished_utc = (Get-Date).ToUniversalTime().ToString('o')
New-Item -ItemType Directory -Force -Path $LogPath | Out-Null
$json = $record | ConvertTo-Json -Depth 4 -Compress
$json | Out-File (Join-Path $LogPath ("sql-update-{0:yyyyMMddTHHmmssZ}.json" -f (Get-Date).ToUniversalTime())) -Encoding utf8
Write-Output "SQLUPDATE $json"
