<#
.SYNOPSIS
  SQL Server 2022 Express for legacy TicketDesk: install (once) and configure (every run).
.DESCRIPTION
  Idempotent. Install is skipped when the instance service exists. Configuration enforced each run:
  TCP on a static port, named pipes off, optional mixed-mode auth with an sa password, firewall rule.
  -FullText (round 3, ticket search on SQL Server full-text): adds the Full-Text Search feature to the instance when it is
  missing. Express has it only in the "Express with Advanced Services" media, which the same bootstrapper downloads
  (/MEDIATYPE=Advanced); the feature is added with setup /ACTION=Install /FEATURES=FullText to the existing instance.
  The sa password comes from -SaPassword or the TD_SQL_SA_PASSWORD environment variable and is never logged.
.EXAMPLE
  $env:TD_SQL_SA_PASSWORD = '...'; .\Install-TdSqlExpress.ps1 -TcpPort 1433
#>
[CmdletBinding()]
param(
    [string]$InstanceName = 'SQLEXPRESS',
    [string]$MediaRoot = 'C:\TdMedia\sql2022-express',
    # Official SQL Server 2022 Express bootstrapper (Microsoft Download Center id 108838).
    [string]$BootstrapperUri = 'https://download.microsoft.com/download/29654887-7cde-4397-bba3-d7f087970845/SQL2022-SSEI-Expr.exe',
    [string[]]$SysAdminAccounts = @('BUILTIN\Administrators'),
    [string]$SaPassword,
    [int]$TcpPort = 1433,
    # Who may reach the TCP port through Windows Firewall ('LocalSubnet', 'Any', or CIDRs); '' = no rule.
    [string[]]$FirewallRemoteAddress = @('LocalSubnet'),
    [switch]$FullText
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
$SaPassword = Read-TdSecret -Value $SaPassword -EnvName 'TD_SQL_SA_PASSWORD'
$svcName = "MSSQL`$$InstanceName"
$server = ".\$InstanceName"

# ---------- install (once)
if (Get-Service $svcName -ErrorAction SilentlyContinue) {
    Write-TdLog "$svcName already installed" SKIP
} else {
    $boot = Join-Path $MediaRoot 'SQL2022-SSEI-Expr.exe'
    Get-TdFile -Uri $BootstrapperUri -Destination $boot
    $pkg = Get-ChildItem $MediaRoot -Filter 'SQLEXPR*_x64_ENU.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $pkg) {
        Write-TdLog 'downloading Express media (Core)' CHANGE
        $p = Start-Process $boot -ArgumentList '/ACTION=Download', "/MEDIAPATH=$MediaRoot", '/MEDIATYPE=Core', '/QUIET' -Wait -PassThru
        if ($p.ExitCode -ne 0) { throw "SSEI media download failed: exit $($p.ExitCode)" }
        $pkg = Get-ChildItem $MediaRoot -Filter 'SQLEXPR*_x64_ENU.exe' | Select-Object -First 1
    }
    $setupDir = Join-Path $MediaRoot 'setup'
    if (-not (Test-Path (Join-Path $setupDir 'setup.exe'))) {
        Write-TdLog "extracting $($pkg.Name)" CHANGE
        $p = Start-Process $pkg.FullName -ArgumentList '/q', "/x:$setupDir" -Wait -PassThru
        if ($p.ExitCode -ne 0) { throw "extract failed: exit $($p.ExitCode)" }
    }
    $setupArgs = @('/Q', '/ACTION=Install', '/FEATURES=SQLEngine', "/INSTANCENAME=$InstanceName",
              "/SQLSVCACCOUNT=`"NT Service\$svcName`"", '/SQLSVCSTARTUPTYPE=Automatic',
              ('/SQLSYSADMINACCOUNTS=' + (($SysAdminAccounts | ForEach-Object { "`"$_`"" }) -join ' ')),
              '/TCPENABLED=1', '/NPENABLED=0', '/UPDATEENABLED=0', '/IACCEPTSQLSERVERLICENSETERMS',
              '/SUPPRESSPRIVACYSTATEMENTNOTICE')
    if ($SaPassword) { $setupArgs += @('/SECURITYMODE=SQL', "/SAPWD=`"$SaPassword`"") }
    Write-TdLog "running SQL Server setup for instance $InstanceName (as SYSTEM)" CHANGE
    # Setup protects /SAPWD with DPAPI, which fails in a network logon (ssh key auth, WinRM): run as SYSTEM.
    $exit = Invoke-TdProcessAsSystem -FilePath (Join-Path $setupDir 'setup.exe') -ArgumentList $setupArgs -TimeoutMinutes 60
    if ($exit -notin 0, 3010) {
        $summary = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\Setup Bootstrap\Log\Summary.txt' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($summary) { Get-Content $summary.FullName -TotalCount 40 }
        throw "SQL Server setup failed: exit $exit"
    }
    Write-TdLog "setup exit $exit" CHANGE
}

# ---------- configure (every run)
$instId = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').$InstanceName
$net = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instId\MSSQLServer\SuperSocketNetLib"
$restart = $false
if ((Get-ItemProperty "$net\Tcp").Enabled -ne 1) { Set-ItemProperty "$net\Tcp" -Name Enabled -Value 1; $restart = $true; Write-TdLog 'TCP enabled' CHANGE }
if ((Get-ItemProperty "$net\Np").Enabled -ne 0) { Set-ItemProperty "$net\Np" -Name Enabled -Value 0; $restart = $true; Write-TdLog 'named pipes disabled' CHANGE }
$ipAll = Get-ItemProperty "$net\Tcp\IPAll"
if ($ipAll.TcpPort -ne "$TcpPort" -or $ipAll.TcpDynamicPorts -ne '') {
    Set-ItemProperty "$net\Tcp\IPAll" -Name TcpPort -Value "$TcpPort"
    Set-ItemProperty "$net\Tcp\IPAll" -Name TcpDynamicPorts -Value ''
    $restart = $true; Write-TdLog "static TCP port $TcpPort" CHANGE
}
$mssql = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instId\MSSQLServer"
if ($SaPassword -and (Get-ItemProperty $mssql).LoginMode -ne 2) {
    Set-ItemProperty $mssql -Name LoginMode -Value 2; $restart = $true; Write-TdLog 'mixed-mode authentication' CHANGE
}
if ($restart) { Restart-Service $svcName -Force; Write-TdLog "restarted $svcName" CHANGE }
if ((Get-Service $svcName).Status -ne 'Running') { Start-Service $svcName }

if ($SaPassword) {
    # Keep sa enabled with the supplied password (idempotent; ALTER LOGIN is cheap).
    Invoke-TdSql -ServerInstance $server -NonQuery -Query @"
DECLARE @sql nvarchar(max) = N'ALTER LOGIN [sa] WITH PASSWORD = ' + QUOTENAME(@pw, '''') + N', CHECK_POLICY = OFF; ALTER LOGIN [sa] ENABLE;';
EXEC sys.sp_executesql @sql;
"@ -Parameters @{ pw = $SaPassword }
    Write-TdLog 'sa login enabled, password set'
}

if ($FirewallRemoteAddress -and $FirewallRemoteAddress[0]) {
    $ruleName = "TicketDesk-SQL-$InstanceName-TCP"
    $rule = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
    if (-not $rule) {
        New-NetFirewallRule -Name $ruleName -DisplayName "TicketDesk SQL Server ($InstanceName) TCP $TcpPort" -Direction Inbound `
            -Protocol TCP -LocalPort $TcpPort -RemoteAddress $FirewallRemoteAddress -Action Allow | Out-Null
        Write-TdLog "firewall rule $ruleName" CHANGE
    } else {
        Set-NetFirewallRule -Name $ruleName -LocalPort $TcpPort -RemoteAddress $FirewallRemoteAddress
        Write-TdLog "firewall rule $ruleName present" SKIP
    }
}

# ---------- Full-Text Search feature (once, when asked)
if ($FullText) {
    $ft = Invoke-TdSql -ServerInstance $server -Scalar -Query "SELECT CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS int)"
    if ($ft -eq 1) {
        Write-TdLog 'Full-Text Search already installed' SKIP
    } else {
        $boot = Join-Path $MediaRoot 'SQL2022-SSEI-Expr.exe'
        Get-TdFile -Uri $BootstrapperUri -Destination $boot
        $adv = Get-ChildItem $MediaRoot -Filter 'SQLEXPRADV*_x64_ENU.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $adv) {
            Write-TdLog 'downloading Express media (Advanced: includes Full-Text Search)' CHANGE
            $p = Start-Process $boot -ArgumentList '/ACTION=Download', "/MEDIAPATH=$MediaRoot", '/MEDIATYPE=Advanced', '/QUIET' -Wait -PassThru
            if ($p.ExitCode -ne 0) { throw "SSEI Advanced media download failed: exit $($p.ExitCode)" }
            $adv = Get-ChildItem $MediaRoot -Filter 'SQLEXPRADV*_x64_ENU.exe' | Select-Object -First 1
        }
        $advDir = Join-Path $MediaRoot 'setup-advanced'
        if (-not (Test-Path (Join-Path $advDir 'setup.exe'))) {
            Write-TdLog "extracting $($adv.Name)" CHANGE
            $p = Start-Process $adv.FullName -ArgumentList '/q', "/x:$advDir" -Wait -PassThru
            if ($p.ExitCode -ne 0) { throw "extract failed: exit $($p.ExitCode)" }
        }
        $ftArgs = @('/Q', '/ACTION=Install', '/FEATURES=FullText', "/INSTANCENAME=$InstanceName", '/UPDATEENABLED=0',
                    '/IACCEPTSQLSERVERLICENSETERMS', '/SUPPRESSPRIVACYSTATEMENTNOTICE')
        Write-TdLog "adding feature FullText to instance $InstanceName (as SYSTEM)" CHANGE
        # SQL Server setup stops IIS (W3SVC) while it installs and leaves it stopped (seen on the lab VM, 2026-11-10): the
        # site then refuses connections until the service is started again
        $iisWasRunning = (Get-Service W3SVC -ErrorAction SilentlyContinue).Status -eq 'Running'
        $exit = Invoke-TdProcessAsSystem -FilePath (Join-Path $advDir 'setup.exe') -ArgumentList $ftArgs -TimeoutMinutes 60
        if ($iisWasRunning -and (Get-Service W3SVC).Status -ne 'Running') { Start-Service W3SVC; Write-TdLog 'W3SVC was stopped by setup: started again' CHANGE }
        if ($exit -notin 0, 3010) {
            $summary = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\Setup Bootstrap\Log\Summary.txt' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($summary) { Get-Content $summary.FullName -TotalCount 40 }
            throw "Full-Text setup failed: exit $exit"
        }
        $ft = Invoke-TdSql -ServerInstance $server -Scalar -Query "SELECT CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS int)"
        if ($ft -ne 1) { throw 'setup finished but FULLTEXTSERVICEPROPERTY(IsFullTextInstalled) is not 1' }
        Write-TdLog "Full-Text Search installed (setup exit $exit)" CHANGE
    }
}

$v = Invoke-TdSql -ServerInstance $server -Scalar -Query "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(64)) + ', full-text ' + CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS nvarchar(1))"
Write-TdLog "SQL Server $v on $server, TCP $TcpPort"
