<#
.SYNOPSIS
  Builds legacy TicketDesk (TicketDesk-2.5.sln, .NET Framework 4.8) and publishes the web app to a folder.
.DESCRIPTION
  1. nuget.exe restore (packages.config) into TicketDesk\packages
  2. MSBuild the whole solution (Release)
  3. File-system publish of TicketDesk.Web.Client (Web.Release.config transform applied) to -PublishPath
  Writes build-info.json (revision, tool versions, warnings/errors, duration) next to the publish output
  and an MSBuild binary log per step under -LogPath. Exit code is non-zero on any failure.
.EXAMPLE
  .\Build-TdLegacy.ps1 -SourcePath C:\TdSrc\ticketdesk -PublishPath C:\TdBuild\publish -SourceRevision abc1234
#>
[CmdletBinding()]
param(
    [string]$SourcePath = 'C:\TdSrc\ticketdesk',
    [string]$PublishPath = 'C:\TdBuild\publish',
    [string]$LogPath = 'C:\TdBuild\logs',
    [string]$Configuration = 'Release',
    [string]$BuildToolsPath = 'C:\BuildTools',
    [string]$SourceRevision = 'unknown',
    [int]$MaxCpuCount = 4
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force

$sln = Join-Path $SourcePath 'TicketDesk\TicketDesk-2.5.sln'
$web = Join-Path $SourcePath 'TicketDesk\TicketDesk.Web.Client\TicketDesk.Web.Client.csproj'
$msbuild = Join-Path $BuildToolsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$nuget = Join-Path $BuildToolsPath 'nuget\nuget.exe'
foreach ($f in $sln, $web, $msbuild, $nuget) { if (-not (Test-Path $f)) { throw "missing $f" } }
New-Item -ItemType Directory -Force -Path $LogPath | Out-Null
$total = [Diagnostics.Stopwatch]::StartNew()
$info = [ordered]@{ revision = $SourceRevision; configuration = $Configuration; started_utc = (Get-Date).ToUniversalTime().ToString('o'); steps = @() }

function Invoke-Step([string]$Name, [string]$Exe, [string[]]$StepArgs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $log = Join-Path $LogPath "$Name.log"
    Write-TdLog "$Name : $Exe $($StepArgs -join ' ')"
    $ErrorActionPreference = 'Continue'   # native stderr must not abort the step (function scope only)
    & $Exe @StepArgs 2>&1 | ForEach-Object { "$_" } | Tee-Object -Variable lines | Out-Host
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $lines | Set-Content -Path $log -Encoding UTF8   # (Tee-Object -FilePath writes UTF-16 in PS 5.1)
    $text = Get-Content $log -Raw
    $warn = ([regex]::Matches($text, '(\d+) Warning\(s\)') | Select-Object -Last 1)
    $err = ([regex]::Matches($text, '(\d+) Error\(s\)') | Select-Object -Last 1)
    $step = [ordered]@{ name = $Name; exit = $code; seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1)
                        warnings = if ($warn) { [int]$warn.Groups[1].Value } else { $null }
                        errors = if ($err) { [int]$err.Groups[1].Value } else { $null }; log = $log }
    $script:info.steps += $step
    Write-TdLog ("{0} exit {1} in {2}s (warnings {3}, errors {4})" -f $Name, $code, $step.seconds, $step.warnings, $step.errors)
    if ($code -ne 0) { throw "$Name failed (exit $code), see $log" }
}

Invoke-Step 'restore' $nuget @('restore', $sln, '-NonInteractive', '-PackagesDirectory', (Join-Path $SourcePath 'TicketDesk\packages'))
Invoke-Step 'build' $msbuild @($sln, "/p:Configuration=$Configuration", "/m:$MaxCpuCount", '/nr:false', '/v:minimal',
    '/clp:Summary', "/bl:$(Join-Path $LogPath 'build.binlog')")
if (Test-Path $PublishPath) { Remove-Item $PublishPath -Recurse -Force }
Invoke-Step 'publish' $msbuild @($web, "/p:Configuration=$Configuration", '/p:DeployOnBuild=true', '/p:DeployDefaultTarget=WebPublish',
    '/p:WebPublishMethod=FileSystem', "/p:PublishUrl=$PublishPath", '/p:DeleteExistingFiles=true', '/p:BuildProjectReferences=false',
    '/nr:false', '/v:minimal', '/clp:Summary', "/bl:$(Join-Path $LogPath 'publish.binlog')")

$info.msbuild = ((& $msbuild -version -nologo) | Select-Object -Last 1)
$info.nuget = (Get-Item $nuget).VersionInfo.FileVersion
$info.publish_files = (Get-ChildItem $PublishPath -Recurse -File).Count
$info.publish_bytes = (Get-ChildItem $PublishPath -Recurse -File | Measure-Object Length -Sum).Sum
$info.seconds = [math]::Round($total.Elapsed.TotalSeconds, 1)
$info | ConvertTo-Json -Depth 4 | Out-File (Join-Path (Split-Path $PublishPath) 'build-info.json') -Encoding utf8
Write-TdLog ("published {0} files to {1} in {2}s" -f $info.publish_files, $PublishPath, $info.seconds)
