<#
.SYNOPSIS
  Registers (or updates) the nightly TicketDesk backup as a Windows scheduled task running as SYSTEM.
.DESCRIPTION
  Idempotent: the task is re-registered only when its command line or time differs.
  Output of each run is appended to <BackupRoot>\backup-task.log.
.EXAMPLE
  .\Register-TdBackupTask.ps1 -At 02:00 -BackupRoot C:\TdBackups -RetentionDays 14
  .\Register-TdBackupTask.ps1 -BlobContainerUrl https://<account>.blob.core.windows.net/backups   # Azure VM
  Start-ScheduledTask -TaskName 'TicketDesk nightly backup'   # run it now
#>
[CmdletBinding()]
param(
    [string]$TaskName = 'TicketDesk nightly backup',
    [string]$At = '02:00',
    [string]$BackupRoot = 'C:\TdBackups',
    [int]$RetentionDays = 14,
    [string]$ScriptsPath,                 # default: this script's folder
    [string]$SqlServerInstance = '.\SQLEXPRESS',
    [string]$DatabaseName = 'TicketDesk',
    [string]$SiteName = 'TicketDesk',
    # Azure VM: each run also copies its folder to this blob container (Backup-TdSite.ps1 -BlobContainerUrl).
    [string]$BlobContainerUrl
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
# (Windows PowerShell 5.1 leaves $PSScriptRoot empty in param defaults)
if (-not $ScriptsPath) { $ScriptsPath = $PSScriptRoot }

$script = Join-Path $ScriptsPath 'Backup-TdSite.ps1'
if (-not (Test-Path $script)) { throw "missing $script" }
if (-not (Test-Path $BackupRoot)) { New-Item -ItemType Directory -Path $BackupRoot | Out-Null }
$log = Join-Path $BackupRoot 'backup-task.log'
$blobArg = if ($BlobContainerUrl) { " -BlobContainerUrl '$BlobContainerUrl'" } else { '' }
$cmd = "& '$script' -BackupRoot '$BackupRoot' -RetentionDays $RetentionDays -SqlServerInstance '$SqlServerInstance' -DatabaseName '$DatabaseName' -SiteName '$SiteName'$blobArg *>> '$log'; exit `$LASTEXITCODE"
$argLine = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command `"$cmd`""
$time = [datetime]::ParseExact($At, 'HH:mm', $null)

$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
$same = $false
if ($existing) {
    $a = $existing.Actions | Select-Object -First 1
    $t = $existing.Triggers | Select-Object -First 1
    $same = ($a.Arguments -eq $argLine) -and $t -and ([datetime]$t.StartBoundary).ToString('HH:mm') -eq $time.ToString('HH:mm')
}
if ($same) {
    Write-TdLog "task '$TaskName' already registered for $At" SKIP
} else {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argLine
    $trigger = New-ScheduledTaskTrigger -Daily -At $time
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
        -Description 'TicketDesk: database .bak + site files + config, see Backup-TdSite.ps1' -Force | Out-Null
    Write-TdLog "registered task '$TaskName' daily at $At (log $log)" CHANGE
}
Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, LastRunTime, LastTaskResult, NextRunTime | Format-List | Out-String
