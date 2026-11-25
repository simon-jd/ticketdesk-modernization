<#
.SYNOPSIS
  IIS with ASP.NET 4.x and the modules legacy TicketDesk (MVC 5 on .NET Framework 4.6.2) needs.
.DESCRIPTION
  Idempotent: installs only missing Windows features. Works on Server Core and Desktop Experience,
  on-prem or on an Azure VM. Exits 0 when nothing was missing.
.EXAMPLE
  .\Install-TdPrerequisites.ps1
#>
[CmdletBinding()]
param(
    # Extra Windows features to add on top of the baseline list.
    [string[]]$AdditionalFeatures = @()
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin

$features = @(
    'Web-Server', 'Web-WebServer',
    'Web-Default-Doc', 'Web-Static-Content', 'Web-Http-Errors', 'Web-Http-Redirect',
    'Web-Http-Logging', 'Web-Request-Monitor',
    'Web-Stat-Compression', 'Web-Dyn-Compression',
    'Web-Filtering',
    'Web-Net-Ext45', 'Web-Asp-Net45', 'Web-ISAPI-Ext', 'Web-ISAPI-Filter', 'NET-Framework-45-ASPNET',
    'Web-Mgmt-Tools', 'Web-Scripting-Tools'
) + $AdditionalFeatures

$missing = @(Get-WindowsFeature -Name $features | Where-Object { -not $_.Installed } | ForEach-Object Name)
if ($missing.Count -eq 0) {
    Write-TdLog "all $($features.Count) features already installed" SKIP
} else {
    Write-TdLog ("installing: {0}" -f ($missing -join ', ')) CHANGE
    $r = Install-WindowsFeature -Name $missing
    if (-not $r.Success) { throw "Install-WindowsFeature failed: $($r.ExitCode)" }
    if ($r.RestartNeeded -eq 'Yes') { Write-TdLog 'a restart is required to finish the feature install' WARN }
}

# .NET Framework 4.6.2 or later must be present (Server 2025 ships 4.8.1).
$release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
if ($release -lt 394802) { throw ".NET Framework 4.6.2+ required (Release=$release)" }
Write-TdLog ".NET Framework 4.x release key $release"

# ASP.NET 4.x must be registered with IIS (the features above do this; verify).
Import-Module WebAdministration
$isapi = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter 'system.webServer/security/isapiCgiRestriction/add' |
    Where-Object { $_.path -like '*Framework64\v4.0.30319\aspnet_isapi.dll' }
if (-not $isapi -or -not $isapi.allowed) { throw 'ASP.NET v4.0 (64-bit) is not allowed in ISAPI/CGI restrictions' }
Write-TdLog 'ASP.NET v4.0 registered with IIS'
Get-WindowsFeature -Name $features | Select-Object Name, InstallState | Format-Table -AutoSize | Out-String -Width 120
