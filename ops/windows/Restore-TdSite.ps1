<#
.SYNOPSIS
  Restores a Backup-TdSite.ps1 folder: database (WITH REPLACE, files moved to the instance's default
  paths, so it also works on a different server) and site files; re-maps the app pool login.
.DESCRIPTION
  Stops the app pool, restores, starts it again. Re-running with the same folder gives the same result.
  The IIS site/app pool must exist (run Set-TdIisSite.ps1 first on a fresh server).
.EXAMPLE
  .\Restore-TdSite.ps1 -BackupPath C:\TdBackups\20261107T020000Z
  .\Restore-TdSite.ps1 -BackupPath C:\TdBackups\clean -SkipFiles
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BackupPath,
    [string]$SiteName = 'TicketDesk',
    [string]$AppPoolName = 'TicketDesk',
    [string]$SqlServerInstance = '.\SQLEXPRESS',
    [string]$DatabaseName = 'TicketDesk',
    [switch]$SkipDatabase,
    [switch]$SkipFiles
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
Import-Module WebAdministration
if (-not (Test-Path $BackupPath)) { throw "no backup at $BackupPath" }
$sw = [Diagnostics.Stopwatch]::StartNew()

if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped') {
    Stop-WebAppPool -Name $AppPoolName
    $t = 0; while ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped' -and $t -lt 60) { Start-Sleep 1; $t++ }
    Write-TdLog "stopped app pool $AppPoolName" CHANGE
}
try {
    if (-not $SkipDatabase) {
        $bak = Get-ChildItem $BackupPath -Filter '*.bak' | Select-Object -First 1
        if (-not $bak) { throw "no .bak in $BackupPath" }
        # RESTORE ... WITH MOVE for every file in the backup, to the instance default data/log paths.
        $files = Invoke-TdSql -ServerInstance $SqlServerInstance -Query 'RESTORE FILELISTONLY FROM DISK = @p' -Parameters @{ p = $bak.FullName }
        $dataPath = Invoke-TdSql -ServerInstance $SqlServerInstance -Scalar -Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))"
        $logPath = Invoke-TdSql -ServerInstance $SqlServerInstance -Scalar -Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))"
        $moves = foreach ($f in $files.Rows) {
            $target = if ($f.Type -eq 'L') { Join-Path $logPath "$DatabaseName`_log.ldf" } else { Join-Path $dataPath ("{0}{1}" -f $DatabaseName, $(if ($f.FileId -eq 1) { '.mdf' } else { "_$($f.FileId).ndf" })) }
            "MOVE N'{0}' TO N'{1}'" -f ($f.LogicalName -replace "'", "''"), ($target -replace "'", "''")
        }
        $db = $DatabaseName -replace ']', ']]'
        $sql = @"
IF DB_ID(@db) IS NOT NULL EXEC (N'ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE');
RESTORE DATABASE [$db] FROM DISK = @p WITH REPLACE, CHECKSUM, RECOVERY, $($moves -join ', ');
ALTER DATABASE [$db] SET MULTI_USER;
"@
        Invoke-TdSql -ServerInstance $SqlServerInstance -NonQuery -TimeoutSeconds 3600 -Query $sql -Parameters @{ db = $DatabaseName; p = $bak.FullName }
        Write-TdLog "restored $DatabaseName from $($bak.FullName)" CHANGE
        # A backup taken under a write freeze (cutover: source database READ_ONLY) restores read-only: the
        # restored copy is the new primary, so open it for writes.
        $upd = Invoke-TdSql -ServerInstance $SqlServerInstance -Scalar -Query "SELECT CAST(DATABASEPROPERTYEX(@db, 'Updateability') AS nvarchar(20))" -Parameters @{ db = $DatabaseName }
        if ($upd -eq 'READ_ONLY') {
            Invoke-TdSql -ServerInstance $SqlServerInstance -NonQuery -Query "ALTER DATABASE [$db] SET READ_WRITE WITH ROLLBACK IMMEDIATE"
            Write-TdLog "$DatabaseName was READ_ONLY in the backup (write freeze): set READ_WRITE" CHANGE
        }

        # The pool identity's SID is derived from the pool name, so the user maps on any server once the login exists.
        $login = "IIS AppPool\$AppPoolName"
        Invoke-TdSql -ServerInstance $SqlServerInstance -NonQuery -Query @"
IF SUSER_ID(@login) IS NULL BEGIN DECLARE @l nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@login) + N' FROM WINDOWS'; EXEC sys.sp_executesql @l; END
DECLARE @u nvarchar(max) = N'USE ' + QUOTENAME(@db) + N';
IF USER_ID(@login) IS NULL CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';
ELSE ALTER USER ' + QUOTENAME(@login) + N' WITH LOGIN = ' + QUOTENAME(@login) + N';
IF IS_ROLEMEMBER(''db_owner'', @login) = 0 ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@login) + N';';
EXEC sys.sp_executesql @u, N'@login sysname', @login = @login;
"@ -Parameters @{ db = $DatabaseName; login = $login }
        Write-TdLog "mapped $login as db_owner in $DatabaseName"
    }

    if (-not $SkipFiles) {
        $zip = Join-Path $BackupPath 'site.zip'
        if (-not (Test-Path $zip)) { throw "no site.zip in $BackupPath" }
        $sitePath = [Environment]::ExpandEnvironmentVariables((Get-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath))
        $stage = Join-Path $env:TEMP ("td-restore-" + [guid]::NewGuid().ToString('n'))
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $stage)
        robocopy.exe $stage $sitePath /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
        $rc = $LASTEXITCODE
        Remove-Item $stage -Recurse -Force
        if ($rc -ge 8) { throw "robocopy failed ($rc)" }
        Write-TdLog "restored site files to $sitePath" CHANGE
    }
} finally {
    Start-WebAppPool -Name $AppPoolName
    Write-TdLog "started app pool $AppPoolName"
}
Write-TdLog ("restore complete in {0:N1}s" -f $sw.Elapsed.TotalSeconds)
