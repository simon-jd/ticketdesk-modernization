<#
.SYNOPSIS
  IIS app pool + site for legacy TicketDesk, HTTPS with a lab certificate, and database access.
.DESCRIPTION
  Idempotent; every run converges to the parameters:
   - certificate: import -CertificatePfxPath into LocalMachine\My (password from -CertificatePassword or
     TD_CERT_PFX_PASSWORD; none for a PFX exported from Key Vault), or create a self-signed lab
     certificate for -DnsNames when no PFX is given; a new PFX (rotation) rebinds HTTPS to it;
     the newest valid certificate for the first DNS name is reused on later runs;
   - app pool (.NET CLR v4.0, integrated, ApplicationPoolIdentity, on-demand start) and site with
     http/https bindings; the Default Web Site is stopped when it holds the same port;
   - App_Data write access for the pool identity;
   - SQL: empty database (created if missing) and a Windows login for the pool identity as db_owner,
     so TicketDesk's first-run setup can apply its EF6 migrations without sysadmin rights;
   - with a local SQL instance, W3SVC depends on the SQL service: TicketDesk checks the database once
     when its app domain starts and stays in first-run-setup mode until the next recycle if SQL is not
     up yet (seen right after a reboot).
.EXAMPLE
  .\Set-TdIisSite.ps1 -CertificatePfxPath C:\TdMedia\tdlegacy-lab.pfx -DnsNames localhost,tdlegacy01
#>
[CmdletBinding()]
param(
    [string]$SiteName = 'TicketDesk',
    [string]$AppPoolName = 'TicketDesk',
    [string]$PhysicalPath = 'C:\inetpub\ticketdesk',
    [int]$HttpPort = 80,
    [int]$HttpsPort = 443,
    [string]$HostHeader = '',
    [string[]]$DnsNames = @('localhost', $env:COMPUTERNAME.ToLowerInvariant()),
    [string]$CertificatePfxPath,
    [string]$CertificatePassword,
    [string]$SqlServerInstance = '.\SQLEXPRESS',
    [string]$DatabaseName = 'TicketDesk',
    [switch]$SkipDatabase
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
Import-Module WebAdministration
$CertificatePassword = Read-TdSecret -Value $CertificatePassword -EnvName 'TD_CERT_PFX_PASSWORD'

# ---------- certificate
$cert = $null
if ($CertificatePfxPath) {
    # Read the PFX with EphemeralKeySet: the key stays in memory. With DefaultKeySet .NET writes a temporary key
    # file and deletes it when the object is finalized; a Key Vault PFX names its key container, so that deleted
    # the key Import-PfxCertificate had just stored under the same name, and hours later (credential cache
    # expiry) Schannel failed with 0x8009030D and IIS reset every TLS connection (Azure VM, 2026-11-08).
    $pfx = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
    $pfx.Import($CertificatePfxPath, $CertificatePassword, 'EphemeralKeySet')
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Thumbprint -eq $pfx.Thumbprint
    if ($cert) {
        $keyOk = $false
        try { $keyOk = $null -ne [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert) } catch { $keyOk = $false }
        if (-not $keyOk) {
            Remove-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" -Force
            $cert = $null
            Write-TdLog "certificate $($pfx.Thumbprint) was in LocalMachine\My without a usable private key: removed, importing again" CHANGE
        }
    }
    if ($cert) {
        Write-TdLog "certificate $($pfx.Thumbprint) already in LocalMachine\My (private key usable)" SKIP
    } else {
        # A PFX read from Key Vault (the certificate's secret) has no password; a PFX file from the lab CA has one.
        if ($CertificatePassword) {
            $sec = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
            Import-PfxCertificate -FilePath $CertificatePfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $sec | Out-Null
        } else {
            Import-PfxCertificate -FilePath $CertificatePfxPath -CertStoreLocation Cert:\LocalMachine\My | Out-Null
        }
        $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Thumbprint -eq $pfx.Thumbprint
        if (-not $cert) { throw "certificate $($pfx.Thumbprint) not found in LocalMachine\My after the import" }
        Write-TdLog "imported certificate $($cert.Thumbprint) ($($cert.Subject))" CHANGE
    }
    # Trust the issuing lab CA when the PFX carries the chain.
    $chain = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
    $chain.Import($CertificatePfxPath, $CertificatePassword, 'EphemeralKeySet')
    foreach ($c in $chain) {
        if ($c.Subject -eq $c.Issuer -and $c.Thumbprint -ne $cert.Thumbprint -and
            -not (Get-ChildItem Cert:\LocalMachine\Root | Where-Object Thumbprint -eq $c.Thumbprint)) {
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
            $store.Open('ReadWrite'); $store.Add($c); $store.Close()
            Write-TdLog "trusted lab CA $($c.Subject)" CHANGE
        }
    }
} else {
    $cert = Get-ChildItem Cert:\LocalMachine\My |
        Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) -and
                       ($_.DnsNameList.Unicode -contains $DnsNames[0]) -and $_.FriendlyName -eq 'TicketDesk lab' } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($cert) {
        Write-TdLog "reusing self-signed certificate $($cert.Thumbprint)" SKIP
    } else {
        $cert = New-SelfSignedCertificate -DnsName $DnsNames -CertStoreLocation Cert:\LocalMachine\My `
            -FriendlyName 'TicketDesk lab' -NotAfter (Get-Date).AddYears(2) -KeyAlgorithm RSA -KeyLength 2048 `
            -KeyExportPolicy Exportable
        Write-TdLog "created self-signed certificate $($cert.Thumbprint) for $($DnsNames -join ', ')" CHANGE
    }
}

# ---------- app pool
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-TdLog "created app pool $AppPoolName" CHANGE
}
$poolWanted = [ordered]@{
    managedRuntimeVersion = 'v4.0'; managedPipelineMode = 'Integrated'; startMode = 'OnDemand'
    'processModel.identityType' = 'ApplicationPoolIdentity'; 'processModel.loadUserProfile' = $true
    'processModel.idleTimeout' = [TimeSpan]::Zero; enable32BitAppOnWin64 = $false
}
foreach ($k in $poolWanted.Keys) {
    $cur = (Get-ItemProperty "IIS:\AppPools\$AppPoolName" -Name $k)
    if ($cur -is [Microsoft.IIs.PowerShell.Framework.ConfigurationAttribute]) { $cur = $cur.Value }
    if ("$cur" -ne "$($poolWanted[$k])") {
        Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name $k -Value $poolWanted[$k]
        Write-TdLog "app pool $AppPoolName $k = $($poolWanted[$k])" CHANGE
    }
}

# ---------- content folder and ACLs
foreach ($d in @($PhysicalPath, (Join-Path $PhysicalPath 'App_Data'))) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null; Write-TdLog "created $d" CHANGE }
}
$poolIdentity = "IIS AppPool\$AppPoolName"
Set-TdAcl -Path $PhysicalPath -Identity $poolIdentity -Rights 'ReadAndExecute'
Set-TdAcl -Path (Join-Path $PhysicalPath 'App_Data') -Identity $poolIdentity -Rights 'Modify'

# ---------- free the ports from the Default Web Site
$dws = Get-Website -Name 'Default Web Site' -ErrorAction SilentlyContinue
if ($dws -and $dws.State -eq 'Started' -and ($dws.bindings.Collection | Where-Object { $_.bindingInformation -like "*:$($HttpPort):*" })) {
    Stop-Website -Name 'Default Web Site'
    Set-ItemProperty 'IIS:\Sites\Default Web Site' -Name serverAutoStart -Value $false
    Write-TdLog 'stopped Default Web Site (port conflict)' CHANGE
}

# ---------- site and bindings
if (-not (Get-Website -Name $SiteName)) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port $HttpPort -HostHeader $HostHeader | Out-Null
    Write-TdLog "created site $SiteName" CHANGE
}
$site = "IIS:\Sites\$SiteName"
if ((Get-ItemProperty $site -Name physicalPath) -ne $PhysicalPath) { Set-ItemProperty $site -Name physicalPath -Value $PhysicalPath; Write-TdLog "site path $PhysicalPath" CHANGE }
if ((Get-ItemProperty $site -Name applicationPool) -ne $AppPoolName) { Set-ItemProperty $site -Name applicationPool -Value $AppPoolName; Write-TdLog "site pool $AppPoolName" CHANGE }
$httpInfo = "*:$($HttpPort):$HostHeader"; $httpsInfo = "*:$($HttpsPort):$HostHeader"
$bindings = (Get-ItemProperty $site -Name bindings).Collection
foreach ($b in @($bindings)) {
    $keep = ($b.protocol -eq 'http' -and $b.bindingInformation -eq $httpInfo) -or ($b.protocol -eq 'https' -and $b.bindingInformation -eq $httpsInfo)
    if (-not $keep) {
        Remove-WebBinding -Name $SiteName -Protocol $b.protocol -BindingInformation $b.bindingInformation
        Write-TdLog "removed binding $($b.protocol) $($b.bindingInformation)" CHANGE
    }
}
if (-not (Get-WebBinding -Name $SiteName -Protocol http -Port $HttpPort)) {
    New-WebBinding -Name $SiteName -Protocol http -Port $HttpPort -HostHeader $HostHeader; Write-TdLog "binding http $httpInfo" CHANGE
}
if (-not (Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort)) {
    New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostHeader; Write-TdLog "binding https $httpsInfo" CHANGE
}
# http.sys SSL binding for 0.0.0.0:<port> -> certificate
$sslPath = "IIS:\SslBindings\0.0.0.0!$HttpsPort"
$ssl = Get-Item $sslPath -ErrorAction SilentlyContinue
if (-not $ssl -or $ssl.Thumbprint -ne $cert.Thumbprint) {
    if ($ssl) { Remove-Item $sslPath }
    Get-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" | New-Item $sslPath | Out-Null
    Write-TdLog "TLS binding 0.0.0.0:$HttpsPort -> $($cert.Thumbprint)" CHANGE
} else { Write-TdLog "TLS binding 0.0.0.0:$HttpsPort present" SKIP }

# Firewall: IIS feature adds "World Wide Web Services (HTTP/HTTPS Traffic-In)"; make sure they are enabled.
Get-NetFirewallRule -Name 'IIS-WebServerRole-HTTP-In-TCP', 'IIS-WebServerRole-HTTPS-In-TCP' -ErrorAction SilentlyContinue |
    Where-Object { $_.Enabled -ne 'True' } | ForEach-Object { Enable-NetFirewallRule -Name $_.Name; Write-TdLog "enabled firewall $($_.Name)" CHANGE }
if ($HttpsPort -ne 443 -or $HttpPort -ne 80) {
    $fw = "TicketDesk-Web-$HttpPort-$HttpsPort"
    if (-not (Get-NetFirewallRule -Name $fw -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name $fw -DisplayName "TicketDesk web $HttpPort/$HttpsPort" -Direction Inbound -Protocol TCP -LocalPort $HttpPort, $HttpsPort -Action Allow | Out-Null
        Write-TdLog "firewall rule $fw" CHANGE
    }
}

if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Started') { Start-WebAppPool -Name $AppPoolName }
if ((Get-Website -Name $SiteName).State -ne 'Started') { Start-Website -Name $SiteName; Write-TdLog "started site $SiteName" CHANGE }

# ---------- start IIS after a local SQL Server (see DESCRIPTION)
$localSql = '^(\.|localhost|\(local\)|127\.0\.0\.1|' + [regex]::Escape($env:COMPUTERNAME) + ')(\\|,|$)'
if ($SqlServerInstance -match $localSql) {
    $sqlSvc = if ($SqlServerInstance -match '\\(.+)$') { "MSSQL`$$($Matches[1])" } else { 'MSSQLSERVER' }
    if (Get-Service $sqlSvc -ErrorAction SilentlyContinue) {
        $deps = @((Get-Service W3SVC).ServicesDependedOn | ForEach-Object Name)
        if ($deps -notcontains $sqlSvc) {
            $newDeps = ($deps + $sqlSvc) -join '/'
            sc.exe config W3SVC depend= $newDeps | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "sc.exe config W3SVC failed ($LASTEXITCODE)" }
            Write-TdLog "W3SVC now depends on $newDeps" CHANGE
        } else { Write-TdLog "W3SVC already depends on $sqlSvc" SKIP }
    }
}

# ---------- database access for the pool identity
if (-not $SkipDatabase) {
    $q = @"
IF DB_ID(@db) IS NULL BEGIN
    DECLARE @c nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@db); EXEC sys.sp_executesql @c; SELECT 'created database' AS r;
END
IF SUSER_ID(@login) IS NULL BEGIN
    DECLARE @l nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@login) + N' FROM WINDOWS'; EXEC sys.sp_executesql @l; SELECT 'created login' AS r;
END
DECLARE @u nvarchar(max) = N'USE ' + QUOTENAME(@db) + N';
IF USER_ID(@login) IS NULL BEGIN CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N'; SELECT ''created user'' AS r; END
IF IS_ROLEMEMBER(''db_owner'', @login) = 0 BEGIN ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@login) + N'; SELECT ''granted db_owner'' AS r; END';
EXEC sys.sp_executesql @u, N'@login sysname', @login = @login;
"@
    $conn = New-Object System.Data.SqlClient.SqlConnection "Data Source=$SqlServerInstance;Initial Catalog=master;Integrated Security=SSPI;Connect Timeout=60"
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $q
        [void]$cmd.Parameters.AddWithValue('@db', $DatabaseName)
        [void]$cmd.Parameters.AddWithValue('@login', $poolIdentity)
        $rd = $cmd.ExecuteReader(); $changes = @()
        do { while ($rd.Read()) { $changes += $rd.GetString(0) } } while ($rd.NextResult())
        $rd.Close()
    } finally { $conn.Close() }
    if ($changes) { $changes | ForEach-Object { Write-TdLog "SQL: $_ ($DatabaseName / $poolIdentity)" CHANGE } }
    else { Write-TdLog "SQL: database $DatabaseName and db_owner login $poolIdentity present" SKIP }
}

Write-TdLog "site $SiteName -> $PhysicalPath, http $HttpPort, https $HttpsPort, cert $($cert.Thumbprint) (expires $($cert.NotAfter.ToString('yyyy-MM-dd')))"
