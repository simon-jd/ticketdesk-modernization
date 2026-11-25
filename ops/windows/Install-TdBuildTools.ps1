<#
.SYNOPSIS
  Build prerequisites for legacy TicketDesk on a Windows build host: Visual Studio 2022 Build Tools
  (MSBuild, web build targets, .NET Framework 4.8 targeting pack) and nuget.exe for packages.config.
  The legacy projects target 4.8 since round 2 (legacy-adapters); they were 4.6.2 before.
.DESCRIPTION
  Idempotent: the VS installer runs only when vswhere finds no Build Tools instance with the required
  components; nuget.exe is downloaded once. Prints the MSBuild path at the end.
.EXAMPLE
  .\Install-TdBuildTools.ps1 -InstallPath C:\BuildTools
#>
[CmdletBinding()]
param(
    [string]$InstallPath = 'C:\BuildTools',
    [string]$MediaRoot = 'C:\TdMedia\buildtools',
    [string]$BootstrapperUri = 'https://aka.ms/vs/17/release/vs_BuildTools.exe',
    [string]$NuGetUri = 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe',
    [string[]]$Components = @(
        'Microsoft.VisualStudio.Workload.MSBuildTools',
        'Microsoft.VisualStudio.Workload.WebBuildTools',
        'Microsoft.Net.Component.4.8.TargetingPack',
        'Microsoft.Net.Component.4.8.SDK'
    )
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
function Find-BuildTools {
    if (-not (Test-Path $vswhere)) { return $null }
    $req = @(); foreach ($c in $Components) { $req += @('-requires', $c) }
    $p = & $vswhere -products 'Microsoft.VisualStudio.Product.BuildTools' @req -property installationPath -format value
    if ($p) { return ($p | Select-Object -First 1) } else { return $null }
}

$found = Find-BuildTools
if ($found) {
    Write-TdLog "Build Tools with required components at $found" SKIP
} else {
    $boot = Join-Path $MediaRoot 'vs_BuildTools.exe'
    Get-TdFile -Uri $BootstrapperUri -Destination $boot
    $vsArgs = @('--quiet', '--wait', '--norestart', '--nocache', '--installPath', "`"$InstallPath`"")
    foreach ($c in $Components) { $vsArgs += @('--add', $c) }
    $vsArgs += '--includeRecommended'
    Write-TdLog ("installing VS Build Tools: {0}" -f ($Components -join ', ')) CHANGE
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $boot -ArgumentList $vsArgs -Wait -PassThru
    Write-TdLog ("vs_BuildTools exit {0} after {1:N0}s" -f $p.ExitCode, $sw.Elapsed.TotalSeconds) CHANGE
    if ($p.ExitCode -notin 0, 3010) {
        Get-ChildItem $env:TEMP -Filter 'dd_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 3 |
            ForEach-Object { "---- $($_.Name)"; Get-Content $_.FullName -Tail 30 }
        throw "Build Tools install failed: exit $($p.ExitCode)"
    }
    $found = Find-BuildTools
    if (-not $found) { throw 'Build Tools installed but vswhere does not report the required components' }
}

$nuget = Join-Path $InstallPath 'nuget\nuget.exe'
Get-TdFile -Uri $NuGetUri -Destination $nuget

$msbuild = Join-Path $found 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) { $msbuild = Join-Path $found 'MSBuild\Current\Bin\MSBuild.exe' }
$tp = "${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
if (-not (Test-Path $tp)) { throw ".NET Framework 4.8 reference assemblies missing at $tp" }
Write-TdLog ("MSBuild {0} at {1}" -f ((& $msbuild -version -nologo) | Select-Object -Last 1), $msbuild)
Write-TdLog ("nuget.exe {0}" -f (Get-Item $nuget).VersionInfo.FileVersion)
Write-TdLog "targeting pack: $tp"
