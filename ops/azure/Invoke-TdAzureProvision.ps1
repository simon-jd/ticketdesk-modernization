<#
.SYNOPSIS
  Run Command entry point on the Azure VM: fetches the ops/windows scripts and the legacy publish package
  from blob storage and runs the same provisioning steps as the local VM (ops/onprem/provision-legacy.sh).
.DESCRIPTION
  Runs as LocalSystem through `az vm run-command invoke` (ops/azure/lift.sh); no inbound port is used.
  Authentication is the VM's system-assigned managed identity only:
    - artifacts container: scripts bundle and publish package, each checked against the SHA-256 that
      lift.sh computed when it packaged them;
    - Key Vault: td-sql-sa-password, td-app-admin-password and the td-iis-legacy certificate (its secret is
      a passwordless PFX with the lab CA chain); values go to environment variables or a temporary 0600-like
      file only for the step that needs them, and are never written to the log;
    - backups / logs containers: Backup-TdSite.ps1 copies and this script's transcript.
  Steps (joined with '+' or ',', run in order):
    fetch        scripts bundle -> C:\TdOps\windows, publish package -> C:\TdBuild\publish
    prereqs      Install-TdPrerequisites.ps1
    sql          Install-TdSqlExpress.ps1 (sa from Key Vault; no Windows Firewall rule: SQL stays local)
    sqlupdate    Update-TdSqlServer.ps1 (cumulative update, @@VERSION before/after)
    site         Set-TdIisSite.ps1 with the certificate from Key Vault (DNS names: -Fqdn, localhost)
    restore      Get-TdBlobBackup.ps1 (-RestorePrefix/-RestoreName) + Restore-TdSite.ps1 (files too when the
                 folder has site.zip), then per-table row counts
    deploy       Deploy-TdApp.ps1 (after restore: the first-run wizard is switched off once tables exist)
    backup-task  Register-TdBackupTask.ps1 with the blob copy
    backup-now   Backup-TdSite.ps1 with the blob copy (drills)
    drill-loss   drill only: drop the TicketDesk database and delete the local backup folders
    db-readonly / db-readwrite   freeze / unfreeze writes (cutover tests)
    verify       facts as one TDVERIFY JSON line: OS, SQL version, Defender, TLS binding, HTTPS + sign-in
                 check on https://localhost/ (-AdminEmail, password from Key Vault), row counts
  Azure SQL re-platform steps (ops/azure/replatform.sh; -SqlServer, -SqlDatabase, -DeployClientId). Azure SQL is
  Entra-only and reachable only through the private endpoint, so everything that administers it runs here, as the
  deploy identity (user-assigned, the server's Entra admin; IMDS token by client id). The app is the VM's
  system-assigned identity, a contained database user.
    sqltools     SqlPackage + td-db bundle from `artifacts` (SHA-256) -> C:\TdTools, .NET runtime by the dot.net
                 install script (pinned -DotnetVersion)
    azsql-wake   first query against Azure SQL, timed (resumes a paused serverless database before the window)
    freeze       write freeze without killing reads: DENY INSERT/UPDATE/DELETE to the app's database user on SQL
                 Express (permission checks apply at once, open connections stay), then wait for open app transactions
    unfreeze     REVOKE the freeze (rollback, or giving the lab source back)
    export       COPY_ONLY backup -> restore as TicketDeskExport -> drop its Windows users (a BACPAC cannot carry them)
                 -> SqlPackage export -> C:\TdMedia\ticketdesk.bacpac (-BacpacUpload: also to backups/bacpac/)
    import       SqlPackage import of that BACPAC (or -BacpacBlob) into the empty free-offer database
    azsql-users  contained user for the app identity (WITH SID = its client id, TYPE = E): db_datareader, db_datawriter
    aks-sql-user (AKS round) the new app's user-assigned identity (-AksAppIdentityName, -AksAppClientId; workload identity of
                 the web and worker pods) as a contained user WITH SID, member of td_modern_app_role only (ops/db script)
    migrate      td-db migrate against Azure SQL, then td-db status
    reconcile    td-db reconcile SQL Express (source) vs Azure SQL (target); a mismatch fails the step (no-go)
    switch       Deploy-TdApp.ps1 -ConfigOnly: connection string -> Azure SQL + SqlAuthentication=ManagedIdentity,
                 one atomic Web.config replace, warm-up
    plan-tag     usp_TicketList with a tag filter on Azure SQL: actual plans (STATISTICS XML) -> logs, forced plans
    files-check  Test-TdBlobStore.ps1 against the attachments account (account SAS from Key Vault)
  -Database express|azure selects the database for deploy and verify.
  The transcript goes to C:\TdLogs and to <logs>/<computer>/provision-<RunId>.log.
#>
param(
    [string]$Steps = 'fetch,prereqs,sql,sqlupdate,site,restore,deploy,backup-task,verify',
    [Parameter(Mandatory)][string]$StorageAccount,
    [Parameter(Mandatory)][string]$KeyVault,
    [string]$Fqdn = '',
    [string]$BundleBlob = '',
    [string]$BundleSha256 = '',
    [string]$PublishBlob = '',
    [string]$PublishSha256 = '',
    [string]$RestorePrefix = '',
    [string]$RestoreName = 'latest',
    [string]$AdminEmail = '',
    [string]$RunId = '',
    [ValidateSet('express', 'azure')][string]$Database = 'express',
    [string]$SqlServer = '',
    [string]$SqlDatabase = 'sqldb-ticketdesk',
    [string]$DeployClientId = '',
    [string]$AppIdentityName = 'vm-tdlab-legacy',
    [string]$SqlToolsBlob = '',
    [string]$SqlToolsSha256 = '',
    [string]$DotnetVersion = '10.0.14',
    [string]$BacpacBlob = '',
    [string]$BacpacSha256 = '',
    [string]$BacpacUpload = '',
    [string]$PlanTag = 'printer',
    # AKS round: sign-in shared with the new app on AKS (key ring in the database, wrapped with this Key Vault key)
    [ValidateSet('', 'database')][string]$SharedCookieKeyRing = '',
    [string]$DataProtectionKeyId = '',
    [string]$AksAppIdentityName = '',
    [string]$AksAppClientId = ''
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (-not $RunId) { $RunId = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') }
$Ops = 'C:\TdOps\windows'
$Blob = "https://$StorageAccount.blob.core.windows.net"
New-Item -ItemType Directory -Force -Path C:\TdLogs, C:\TdMedia | Out-Null
$log = "C:\TdLogs\provision-$RunId.log"
Start-Transcript -Path $log -Force | Out-Null
$total = [Diagnostics.Stopwatch]::StartNew()
$timings = [ordered]@{}
$failed = $null

function Say([string]$m) { Write-Output ('[{0:yyyy-MM-ddTHH:mm:ssZ}] {1}' -f (Get-Date).ToUniversalTime(), $m) }

# Bootstrap download (before TdOps.psm1 exists on the host): managed identity token + one GET, retried
# while the identity's new role assignment propagates.
function Get-BootstrapBlob([string]$Url, [string]$Dest, [string]$Sha) {
    $tokUri = 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=' + [uri]::EscapeDataString('https://storage.azure.com/')
    for ($i = 1; ; $i++) {
        try {
            $tok = (Invoke-RestMethod -Uri $tokUri -Headers @{ Metadata = 'true' } -UseBasicParsing -TimeoutSec 30).access_token
            Invoke-WebRequest -Uri $Url -Headers @{ Authorization = "Bearer $tok"; 'x-ms-version' = '2023-11-03' } -UseBasicParsing -OutFile "$Dest.partial" -TimeoutSec 1800
            break
        } catch {
            if ($i -ge 30) { throw "download $Url failed: $($_.Exception.Message)" }
            Say "download $($Url.Split('?')[0]) not yet possible ($($_.Exception.Message)); retry $i in 15 s"
            Start-Sleep -Seconds 15
        }
    }
    $h = (Get-FileHash "$Dest.partial" -Algorithm SHA256).Hash
    if ($Sha -and $h -ne $Sha.ToUpperInvariant()) { Remove-Item "$Dest.partial" -Force; throw "SHA256 mismatch for $Url ($h)" }
    Move-Item "$Dest.partial" $Dest -Force
    Say ("fetched {0} ({1:N0} bytes, sha256 {2})" -f $Url.Split('?')[0], (Get-Item $Dest).Length, $h)
}

function Expand-Zip([string]$Zip, [string]$To) {
    if (Test-Path $To) { Remove-Item $To -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($Zip, $To)
}

function Invoke-Ops([string]$Script, [hashtable]$Arguments = @{}) {
    & (Join-Path $Ops $Script) @Arguments
}

$RowCountSql = "SELECT s.name + '.' + t.name AS tbl, SUM(p.rows) AS n FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1"
function Get-RowCounts([string]$Db = $Database) {
    $t = if ($Db -eq 'azure') { Invoke-AzSql -Query $RowCountSql } else { Invoke-TdSql -Database TicketDesk -Query $RowCountSql }
    $o = [ordered]@{}; foreach ($r in $t.Rows) { $o[[string]$r.tbl] = [long]$r.n }; return $o
}

# ---- Azure SQL helpers (deploy identity) ------------------------------------------------------------
$Tools = 'C:\TdTools'
$Dotnet = "$Tools\dotnet\dotnet.exe"
$ExpressConn = 'Server=tcp:localhost,1433;Database=TicketDesk;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;Connect Timeout=60'
function Assert-AzSqlParams { if (-not $SqlServer -or -not $DeployClientId) { throw 'this step needs -SqlServer and -DeployClientId' } }
function Get-AzSqlToken { Assert-AzSqlParams; Get-TdManagedIdentityToken -Resource 'https://database.windows.net/' -ClientId $DeployClientId }
function Invoke-AzSql([string]$Query, [switch]$Scalar, [switch]$NonQuery, [switch]$AllResults, [int]$TimeoutSeconds = 600) {
    Invoke-TdSql -ServerInstance "tcp:$SqlServer,1433" -Database $SqlDatabase -AccessToken (Get-AzSqlToken) -Query $Query `
        -Scalar:$Scalar -NonQuery:$NonQuery -AllResults:$AllResults -TimeoutSeconds $TimeoutSeconds
}
# Connection string for SqlPackage and td-db (Microsoft.Data.SqlClient): the deploy identity, no secret inside.
function Get-AzSqlConn { Assert-AzSqlParams; "Server=tcp:$SqlServer,1433;Database=$SqlDatabase;Authentication=Active Directory Managed Identity;User Id=$DeployClientId;Encrypt=True;Connect Timeout=60" }
function Invoke-Native([string]$Exe, [string[]]$Arguments) {
    # Native tools write progress to stderr; with ErrorActionPreference Stop, Windows PowerShell would turn the first
    # stderr line into a terminating error. Output goes to the transcript; the exit code decides.
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & $Exe @Arguments 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $eap }
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $Exe -Leaf) $($Arguments[0]) $($Arguments[1]) exited with $LASTEXITCODE" }
}
function Invoke-SqlPackage([string[]]$Arguments) { Invoke-Native $Dotnet (@("$Tools\sqlpackage\sqlpackage.dll") + $Arguments) }
function Invoke-TdDb([string[]]$Arguments, [hashtable]$Environment) {
    foreach ($k in $Environment.Keys) { [Environment]::SetEnvironmentVariable($k, $Environment[$k]) }
    try { Invoke-Native $Dotnet (@("$Tools\td-db\td-db.dll") + $Arguments) }
    finally { foreach ($k in $Environment.Keys) { [Environment]::SetEnvironmentVariable($k, $null) } }
}
function Mark([string]$Name) { Say ("TDMARK {0} {1:yyyy-MM-ddTHH:mm:ss.fffZ}" -f $Name, (Get-Date).ToUniversalTime()) }
function Get-DeploySecrets {
    # Secrets the current legacy build needs, from Key Vault through the system-assigned identity (env only, never logged).
    $e = @{}
    if (Test-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-remoteapp-api-key') { $e.TD_REMOTEAPP_API_KEY = Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-remoteapp-api-key' }
    if (Test-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-attachments-connection') { $e.TD_AZURE_STORAGE_CONNECTION = Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-attachments-connection' }
    return $e
}
function Get-AppDeployArgs {
    $a = @{ PublishPath = 'C:\TdBuild\publish' }
    if ($Database -eq 'azure') {
        Assert-AzSqlParams
        $a.ConnectionString = "Data Source=tcp:$SqlServer,1433;Initial Catalog=$SqlDatabase;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;MultipleActiveResultSets=True;Application Name=TicketDesk"
        $a.SqlAuthentication = 'ManagedIdentity'
    }
    if ($SharedCookieKeyRing) { $a.SharedCookieKeyRing = $SharedCookieKeyRing }
    if ($DataProtectionKeyId) { $a.DataProtectionKeyVaultKeyId = $DataProtectionKeyId }
    return $a
}
function Invoke-AppDeploy([hashtable]$Extra = @{}) {
    $secrets = Get-DeploySecrets
    foreach ($k in $secrets.Keys) { [Environment]::SetEnvironmentVariable($k, $secrets[$k]) }
    try {
        $a = Get-AppDeployArgs; foreach ($k in $Extra.Keys) { $a[$k] = $Extra[$k] }
        Invoke-Ops 'Deploy-TdApp.ps1' $a
    } finally { foreach ($k in $secrets.Keys) { [Environment]::SetEnvironmentVariable($k, $null) } }
    Say ("deploy: database {0}, remote-app key {1}, attachments {2}" -f $Database, [bool]$secrets.TD_REMOTEAPP_API_KEY, $(if ($secrets.TD_AZURE_STORAGE_CONNECTION) { 'Azure Blob (account SAS from Key Vault)' } else { 'App_Data' }))
}

function Test-SignIn([string]$Base, [string]$Email, [string]$Password) {
    # GET the sign-in form, POST the credentials with the antiforgery token, expect a signed-in page.
    $s = $null
    $r = Invoke-WebRequest -Uri "$Base/user/sign-in" -UseBasicParsing -SessionVariable s -TimeoutSec 300
    $tok = [regex]::Match($r.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    $body = @{ __RequestVerificationToken = $tok; UserNameOrEmail = $Email; Password = $Password; RememberMe = 'false' }
    $r2 = Invoke-WebRequest -Uri "$Base/user/sign-in" -Method Post -Body $body -WebSession $s -UseBasicParsing -TimeoutSec 300
    return ($r2.StatusCode -eq 200 -and $r2.Content -match 'sign-out')
}

function Invoke-Step([string]$Name) {
    Say "==== step $Name"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    switch ($Name) {
        'fetch' {
            if (-not $BundleBlob) { throw 'fetch needs -BundleBlob' }
            Get-BootstrapBlob "$Blob/artifacts/$BundleBlob" 'C:\TdMedia\td-ops-bundle.zip' $BundleSha256
            Expand-Zip 'C:\TdMedia\td-ops-bundle.zip' $Ops
            Say "scripts: $((Get-ChildItem $Ops -File | ForEach-Object Name) -join ', ')"
            if ($PublishBlob) {
                $marker = 'C:\TdBuild\publish.sha256'
                if ((Test-Path $marker) -and (Get-Content $marker -Raw).Trim() -eq $PublishSha256.ToUpperInvariant() -and (Test-Path 'C:\TdBuild\publish\Web.config')) {
                    Say 'publish package already extracted (same SHA-256)'
                } else {
                    New-Item -ItemType Directory -Force -Path C:\TdBuild | Out-Null
                    Get-BootstrapBlob "$Blob/artifacts/$PublishBlob" 'C:\TdMedia\td-legacy-publish.zip' $PublishSha256
                    Expand-Zip 'C:\TdMedia\td-legacy-publish.zip' 'C:\TdBuild\publish'
                    Set-Content $marker $PublishSha256.ToUpperInvariant()
                    Say "publish package: $((Get-ChildItem C:\TdBuild\publish -Recurse -File).Count) files"
                }
            }
        }
        'prereqs' { Invoke-Ops 'Install-TdPrerequisites.ps1' }
        'sql' {
            $env:TD_SQL_SA_PASSWORD = Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-sql-sa-password'
            try { Invoke-Ops 'Install-TdSqlExpress.ps1' @{ TcpPort = 1433; FirewallRemoteAddress = @('') } }
            finally { Remove-Item Env:\TD_SQL_SA_PASSWORD -ErrorAction SilentlyContinue }
        }
        'sqlupdate' { Invoke-Ops 'Update-TdSqlServer.ps1' }
        'site' {
            if (-not $Fqdn) { throw 'site needs -Fqdn' }
            $pfx = 'C:\TdMedia\td-iis-legacy.pfx'
            [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String((Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-iis-legacy')))
            try {
                icacls.exe $pfx /inheritance:r /grant:r 'SYSTEM:F' 'BUILTIN\Administrators:F' | Out-Null
                Remove-Item Env:\TD_CERT_PFX_PASSWORD -ErrorAction SilentlyContinue
                $siteArgs = @{ CertificatePfxPath = $pfx; DnsNames = @($Fqdn, 'localhost') }
                # re-platformed host without SQL Express (replatform.sh rebuild): no local database to prepare
                if (-not (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue)) { $siteArgs.SkipDatabase = $true; Say 'no SQL Express on this host: site without a local database' }
                Invoke-Ops 'Set-TdIisSite.ps1' $siteArgs
            } finally { Remove-Item $pfx -Force -ErrorAction SilentlyContinue }
        }
        'restore' {
            if (-not $RestorePrefix) { throw 'restore needs -RestorePrefix' }
            $out = @(Invoke-Ops 'Get-TdBlobBackup.ps1' @{ BlobContainerUrl = "$Blob/backups"; Prefix = $RestorePrefix; Name = $RestoreName })
            if ($out.Count -gt 1) { $out[0..($out.Count - 2)] | ForEach-Object { Write-Output $_ } }
            $path = [string]$out[-1]
            $a = @{ BackupPath = $path }
            if (-not (Test-Path (Join-Path $path 'site.zip'))) { $a.SkipFiles = $true; Say 'no site.zip: database only' }
            Invoke-Ops 'Restore-TdSite.ps1' $a
            Write-Output ('ROWCOUNTS ' + (Get-RowCounts | ConvertTo-Json -Compress))
        }
        'deploy' { Invoke-AppDeploy }
        'backup-task' { Invoke-Ops 'Register-TdBackupTask.ps1' @{ BlobContainerUrl = "$Blob/backups" } }
        'backup-now' { Invoke-Ops 'Backup-TdSite.ps1' @{ BlobContainerUrl = "$Blob/backups" } }
        'drill-loss' {
            Invoke-TdSql -NonQuery -Query "IF DB_ID('TicketDesk') IS NOT NULL BEGIN ALTER DATABASE [TicketDesk] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TicketDesk]; END"
            Get-ChildItem C:\TdBackups -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
            Say 'drill: TicketDesk database dropped, local backup folders deleted'
        }
        'db-readonly' { Invoke-TdSql -NonQuery -Query 'ALTER DATABASE [TicketDesk] SET READ_ONLY WITH ROLLBACK IMMEDIATE'; Say 'TicketDesk READ_ONLY' }
        'db-readwrite' { Invoke-TdSql -NonQuery -Query 'ALTER DATABASE [TicketDesk] SET READ_WRITE WITH ROLLBACK IMMEDIATE'; Say 'TicketDesk READ_WRITE' }
        'sqltools' {
            if (-not $SqlToolsBlob) { throw 'sqltools needs -SqlToolsBlob' }
            $marker = "$Tools\sqltools.sha256"
            if ((Test-Path $marker) -and (Get-Content $marker -Raw).Trim() -eq $SqlToolsSha256.ToUpperInvariant()) {
                Say 'sql tools bundle already extracted (same SHA-256)'
            } else {
                Get-BootstrapBlob "$Blob/artifacts/$SqlToolsBlob" 'C:\TdMedia\td-sqltools.zip' $SqlToolsSha256
                foreach ($d in 'sqlpackage', 'td-db') { if (Test-Path "$Tools\$d") { Remove-Item "$Tools\$d" -Recurse -Force } }
                New-Item -ItemType Directory -Force -Path $Tools | Out-Null
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                [IO.Compression.ZipFile]::ExtractToDirectory('C:\TdMedia\td-sqltools.zip', $Tools)
                Set-Content $marker $SqlToolsSha256.ToUpperInvariant()
            }
            if (-not (Test-Path "$Tools\dotnet\shared\Microsoft.NETCore.App\$DotnetVersion")) {
                # the official dot.net install script, runtime only, pinned version (same as lab-host's)
                Get-TdFile -Uri 'https://dot.net/v1/dotnet-install.ps1' -Destination 'C:\TdMedia\dotnet-install.ps1'
                Invoke-Native 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'C:\TdMedia\dotnet-install.ps1',
                    '-Runtime', 'dotnet', '-Version', $DotnetVersion, '-InstallDir', "$Tools\dotnet", '-NoPath')
            }
            Invoke-Native $Dotnet @('--list-runtimes')
            Invoke-SqlPackage @('/Version:true')
            Invoke-TdDb @('scripts') @{}
        }
        'azsql-wake' {
            # The free-offer database is serverless and pauses after an hour without connections; the first connection
            # resumes it (error 40613 until it is online). Done before the freeze, and timed: the cold start.
            $sw2 = [Diagnostics.Stopwatch]::StartNew()
            $v = Invoke-AzSql -Scalar -Query "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Status') AS nvarchar(20))"
            Mark 'azsql_online'
            Say ("Azure SQL {0} answered after {1:N1} s ({2})" -f $SqlDatabase, $sw2.Elapsed.TotalSeconds, $v)
        }
        'freeze' {
            Mark 'freeze_start'
            Invoke-TdSql -Database TicketDesk -NonQuery -Query 'DENY INSERT, UPDATE, DELETE TO [IIS AppPool\TicketDesk];'
            Mark 'write_denied'
            # Transactions the app opened before the DENY may still commit: wait until none is open, so the export sees
            # everything that was acknowledged. New writes fail with error 229; reads go on over the same connections.
            $open = -1; $deadline = (Get-Date).AddSeconds(30)
            do {
                $open = [int](Invoke-TdSql -Scalar -Query "SELECT COUNT(*) FROM sys.dm_tran_session_transactions AS t JOIN sys.dm_exec_sessions AS s ON s.session_id = t.session_id WHERE s.original_login_name = N'IIS APPPOOL\TicketDesk' AND s.database_id = DB_ID(N'TicketDesk')")
                if ($open -eq 0) { break }
                Start-Sleep -Milliseconds 200
            } while ((Get-Date) -lt $deadline)
            if ($open -ne 0) { throw "freeze: $open app transaction(s) still open after 30 s" }
            Mark 'frozen'
            Say 'writes frozen: DENY INSERT, UPDATE, DELETE to [IIS AppPool\TicketDesk]; no app transaction open'
        }
        'unfreeze' {
            Invoke-TdSql -Database TicketDesk -NonQuery -Query 'REVOKE INSERT, UPDATE, DELETE TO [IIS AppPool\TicketDesk];'
            Mark 'unfrozen'
        }
        'export' {
            Mark 'export_start'
            $bak = 'C:\TdMedia\td-export.bak'; $bacpac = 'C:\TdMedia\ticketdesk.bacpac'
            Remove-Item $bak, $bacpac -Force -ErrorAction SilentlyContinue
            Invoke-TdSql -NonQuery -TimeoutSeconds 3600 -Query 'BACKUP DATABASE [TicketDesk] TO DISK = @p WITH COPY_ONLY, INIT, CHECKSUM' -Parameters @{ p = $bak }
            $files = Invoke-TdSql -Query 'RESTORE FILELISTONLY FROM DISK = @p' -Parameters @{ p = $bak }
            $dataPath = Invoke-TdSql -Scalar -Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))"
            $logPath = Invoke-TdSql -Scalar -Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))"
            $moves = foreach ($f in $files.Rows) {
                $target = if ($f.Type -eq 'L') { Join-Path $logPath 'TicketDeskExport_log.ldf' } else { Join-Path $dataPath ("TicketDeskExport{0}" -f $(if ($f.FileId -eq 1) { '.mdf' } else { "_$($f.FileId).ndf" })) }
                "MOVE N'{0}' TO N'{1}'" -f ($f.LogicalName -replace "'", "''"), ($target -replace "'", "''")
            }
            Invoke-TdSql -NonQuery -TimeoutSeconds 3600 -Parameters @{ p = $bak } -Query ("IF DB_ID(N'TicketDeskExport') IS NOT NULL BEGIN ALTER DATABASE [TicketDeskExport] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TicketDeskExport]; END; RESTORE DATABASE [TicketDeskExport] FROM DISK = @p WITH RECOVERY, " + ($moves -join ', '))
            Mark 'copy_restored'
            # A BACPAC is for Azure SQL Database: Windows users cannot be in it. The copy loses them; the source keeps its own.
            $dropped = Invoke-TdSql -Database TicketDeskExport -Query @"
DECLARE @s nvarchar(max) = N'', @n nvarchar(max) = N'';
-- schemas the app created while it was db_owner (EF migrations) belong to its Windows user: give them to dbo first
SELECT @s += N'ALTER AUTHORIZATION ON SCHEMA::' + QUOTENAME(sc.name) + N' TO dbo;' FROM sys.schemas AS sc JOIN sys.database_principals AS p ON p.principal_id = sc.principal_id WHERE p.type IN ('U', 'G') AND p.principal_id > 4;
SELECT @s += N'ALTER AUTHORIZATION ON OBJECT::' + QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name) + N' TO SCHEMA OWNER;' FROM sys.objects AS o JOIN sys.database_principals AS p ON p.principal_id = o.principal_id WHERE p.type IN ('U', 'G') AND p.principal_id > 4;
SELECT @s += N'DROP USER ' + QUOTENAME(name) + N';', @n += name + N' ' FROM sys.database_principals WHERE type IN ('U', 'G') AND principal_id > 4;
EXEC (@s);
SELECT @n AS dropped;
"@
            Say "export copy: dropped Windows users: $($dropped.Rows[0].dropped)"
            Invoke-SqlPackage @('/Action:Export', '/SourceConnectionString:Server=tcp:localhost,1433;Database=TicketDeskExport;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True',
                "/TargetFile:$bacpac", '/p:VerifyFullTextDocumentTypesSupported=false')
            Invoke-TdSql -NonQuery -Query 'ALTER DATABASE [TicketDeskExport] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TicketDeskExport];'
            Remove-Item $bak -Force
            Mark 'exported'
            Say ("BACPAC {0:N0} bytes, sha256 {1}" -f (Get-Item $bacpac).Length, (Get-FileHash $bacpac -Algorithm SHA256).Hash)
            if ($BacpacUpload) {
                Send-TdBlob -Url "$Blob/backups/bacpac/$BacpacUpload" -Path $bacpac
                Say "uploaded to backups/bacpac/$BacpacUpload"
            }
        }
        'import' {
            $bacpac = 'C:\TdMedia\ticketdesk.bacpac'
            if ($BacpacBlob) {
                $bacpac = 'C:\TdMedia\import.bacpac'
                Save-TdBlob -Url "$Blob/backups/$BacpacBlob" -Destination $bacpac -Sha256 $BacpacSha256 | Out-Null
                Say "BACPAC from backups/$BacpacBlob"
            }
            if (-not (Test-Path $bacpac)) { throw "no BACPAC at $bacpac (run export, or pass -BacpacBlob)" }
            # SqlPackage would create a (paid) database if the target did not exist: import only into the free-offer
            # database Terraform created, and only while it is empty.
            $objects = [int](Invoke-AzSql -Scalar -Query 'SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0')
            if ($objects -gt 0) { throw "target $SqlDatabase has $objects user objects: replace the database first (replatform.sh db-reset)" }
            Say ("target {0}: {1}" -f $SqlDatabase, (Invoke-AzSql -Scalar -Query "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Edition') AS nvarchar(40)) + ' ' + CAST(DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS nvarchar(40))"))
            Mark 'import_start'
            Invoke-SqlPackage @('/Action:Import', "/SourceFile:$bacpac", "/TargetConnectionString:$(Get-AzSqlConn)")
            Mark 'imported'
        }
        'azsql-users' {
            # The app is the VM's system-assigned identity: its client id is the appid claim of its own token. A
            # service-principal admin cannot look names up in Entra (no Directory Readers), so the user is created by SID.
            $claims = Get-TdTokenClaims (Get-TdManagedIdentityToken -Resource 'https://database.windows.net/')
            $appId = [string]$claims.appid
            if (-not ($appId -as [guid])) { throw 'no appid in the system-assigned identity token' }
            Invoke-AzSql -NonQuery -Query @"
DECLARE @name sysname = N'$AppIdentityName', @sid varbinary(16) = CAST(CAST(N'$appId' AS uniqueidentifier) AS varbinary(16));
IF USER_ID(@name) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE USER ' + QUOTENAME(@name) + N' WITH SID = ' + CONVERT(nvarchar(100), @sid, 1) + N', TYPE = E;';
    EXEC (@sql);
END;
IF IS_ROLEMEMBER(N'db_datareader', @name) = 0 EXEC (N'ALTER ROLE db_datareader ADD MEMBER ' + N'[$AppIdentityName]');
IF IS_ROLEMEMBER(N'db_datawriter', @name) = 0 EXEC (N'ALTER ROLE db_datawriter ADD MEMBER ' + N'[$AppIdentityName]');
"@
            $roles = Invoke-AzSql -Query "SELECT r.name FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id JOIN sys.database_principals u ON u.principal_id = m.member_principal_id WHERE u.name = N'$AppIdentityName' ORDER BY 1"
            Say ("contained user [{0}] (app id {1}, TYPE E): {2}" -f $AppIdentityName, $appId, (($roles.Rows | ForEach-Object { $_.name }) -join ', '))
        }
        'aks-sql-user' {
            if (-not $AksAppIdentityName -or -not ($AksAppClientId -as [guid])) { throw 'aks-sql-user needs -AksAppIdentityName and -AksAppClientId' }
            if ($AksAppIdentityName -notmatch '^[A-Za-z0-9_-]{1,128}$') { throw 'AksAppIdentityName: letters, digits, - and _ only' }
            # the role (idempotent; database-level grants only), then the identity as its only member
            Invoke-AzSql -NonQuery -Query (Get-Content -Raw (Join-Path $Ops 'td_modern_app_role.sql'))
            Invoke-AzSql -NonQuery -Query @"
DECLARE @name sysname = N'$AksAppIdentityName', @sid varbinary(16) = CAST(CAST(N'$AksAppClientId' AS uniqueidentifier) AS varbinary(16));
IF USER_ID(@name) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE USER ' + QUOTENAME(@name) + N' WITH SID = ' + CONVERT(nvarchar(100), @sid, 1) + N', TYPE = E;';
    EXEC (@sql);
END;
IF IS_ROLEMEMBER(N'td_modern_app_role', @name) = 0 EXEC (N'ALTER ROLE td_modern_app_role ADD MEMBER ' + N'[$AksAppIdentityName]');
"@
            $roles = Invoke-AzSql -Query "SELECT r.name FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id JOIN sys.database_principals u ON u.principal_id = m.member_principal_id WHERE u.name = N'$AksAppIdentityName' ORDER BY 1"
            $grants = Invoke-AzSql -Scalar -Query "SELECT COUNT(*) FROM sys.database_permissions p JOIN sys.database_principals r ON r.principal_id = p.grantee_principal_id WHERE r.name = N'td_modern_app_role'"
            Say ("contained user [{0}] (client id {1}, TYPE E): roles {2}; td_modern_app_role grants {3}" -f $AksAppIdentityName, $AksAppClientId, (($roles.Rows | ForEach-Object { $_.name }) -join ', '), $grants)
        }
        'migrate' {
            Invoke-TdDb @('migrate') @{ TD_DB_CONN = (Get-AzSqlConn) }
            Invoke-TdDb @('status') @{ TD_DB_CONN = (Get-AzSqlConn) }
        }
        'reconcile' {
            Mark 'reconcile_start'
            $json = "C:\TdLogs\reconcile-$RunId.json"
            try { Invoke-TdDb @('reconcile', '--json', $json) @{ TD_DB_SOURCE = $ExpressConn; TD_DB_TARGET = (Get-AzSqlConn) } }
            finally { if (Test-Path $json) { Send-TdBlob -Url "$Blob/logs/$($env:COMPUTERNAME.ToLowerInvariant())/reconcile-$RunId.json" -Path $json } }
            Mark 'reconciled'
        }
        'switch' {
            Mark 'switch_start'
            if ($Database -ne 'azure') { throw 'switch needs -Database azure' }
            Invoke-AppDeploy @{ ConfigOnly = $true }
            Mark 'switched'
        }
        'plan-tag' {
            $forced = [int](Invoke-AzSql -Scalar -Query 'SELECT COUNT(*) FROM sys.query_store_plan WHERE is_forced_plan = 1')
            $hints = [int](Invoke-AzSql -Scalar -Query 'SELECT COUNT(*) FROM sys.query_store_query_hints')
            $qs = Invoke-AzSql -Scalar -Query 'SELECT actual_state_desc FROM sys.database_query_store_options'
            $out = @()
            foreach ($run in 1..2) {
                $ds = Invoke-AzSql -AllResults -Query "SET STATISTICS XML ON; DECLARE @c int; EXEC dbo.usp_TicketList @Tag = N'$PlanTag', @TotalCount = @c OUTPUT; SET STATISTICS XML OFF; SELECT @c AS total;"
                $i = 0
                foreach ($t in $ds.Tables) {
                    if ($t.Columns.Count -eq 1 -and $t.Columns[0].ColumnName -like '*Showplan*') {
                        $xml = [string]$t.Rows[0][0]; $i++
                        $path = "C:\TdLogs\plan-tag-$RunId-run$run-$i.sqlplan"
                        [IO.File]::WriteAllText($path, $xml)
                        Send-TdBlob -Url "$Blob/logs/$($env:COMPUTERNAME.ToLowerInvariant())/$(Split-Path $path -Leaf)" -Path $path
                        [xml]$p = $xml
                        $ns = New-Object Xml.XmlNamespaceManager $p.NameTable; $ns.AddNamespace('s', 'http://schemas.microsoft.com/sqlserver/2004/07/showplan')
                        $st = $p.SelectSingleNode('//s:StmtSimple', $ns); $qp = $p.SelectSingleNode('//s:QueryPlan', $ns)
                        $ops = @($p.SelectNodes('//s:RelOp', $ns) | ForEach-Object { $_.PhysicalOp } | Select-Object -Unique)
                        $out += [ordered]@{ run = $run; plan = $i; statement = ([string]$st.StatementText).Trim().Substring(0, [Math]::Min(90, ([string]$st.StatementText).Trim().Length))
                            dop = $qp.DegreeOfParallelism; non_parallel_reason = $qp.NonParallelPlanReason; elapsed_ms = $p.SelectSingleNode('//s:QueryTimeStats', $ns).ElapsedTime
                            cpu_ms = $p.SelectSingleNode('//s:QueryTimeStats', $ns).CpuTime; operators = ($ops -join ',') }
                    } elseif ($t.Columns.Contains('total')) { $total = $t.Rows[0].total }
                }
            }
            Write-Output ('TDPLAN ' + ([ordered]@{ tag = $PlanTag; total = $total; query_store = $qs; forced_plans = $forced; query_store_hints = $hints; plans = $out } | ConvertTo-Json -Depth 4 -Compress))
        }
        'files-check' {
            $env:TD_AZURE_STORAGE_CONNECTION = Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-attachments-connection'
            try { Invoke-Ops 'Test-TdBlobStore.ps1' } finally { Remove-Item Env:\TD_AZURE_STORAGE_CONNECTION -ErrorAction SilentlyContinue }
            if ($LASTEXITCODE -ne 0) { throw 'Test-TdBlobStore.ps1 reported failures' }
        }
        'verify' {
            $os = Get-CimInstance Win32_OperatingSystem
            $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
            $mp = Get-MpComputerStatus
            Import-Module WebAdministration
            $ssl = Get-Item 'IIS:\SslBindings\0.0.0.0!443' -ErrorAction SilentlyContinue
            $cert = if ($ssl) { Get-Item "Cert:\LocalMachine\My\$($ssl.Thumbprint)" } else { $null }
            $sw2 = [Diagnostics.Stopwatch]::StartNew()
            $homePage = Invoke-WebRequest -Uri 'https://localhost/' -UseBasicParsing -TimeoutSec 300
            $homeMs = $sw2.ElapsedMilliseconds
            # The admin's e-mail is not a secret (parameter); the password is read from Key Vault for this check only.
            $signIn = if ($AdminEmail) { Test-SignIn 'https://localhost' $AdminEmail (Get-TdKeyVaultSecret -VaultName $KeyVault -Name 'td-app-admin-password') } else { $null }
            $facts = [ordered]@{
                computer = $env:COMPUTERNAME; os = $os.Caption; os_build = "$($cv.CurrentBuild).$($cv.UBR)"; display_version = $cv.DisplayVersion
                last_boot_utc = $os.LastBootUpTime.ToUniversalTime().ToString('o')
                database = $Database
                sql = $(if ($Database -eq 'azure') { Invoke-AzSql -Scalar -Query "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(64)) + ' ' + CAST(DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS nvarchar(40))" }
                        else { Invoke-TdSql -Scalar -Query "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(SERVERPROPERTY('ProductUpdateLevel') AS nvarchar(16)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(64))" })
                app_db = $(try { [xml]$wc = Get-Content 'C:\inetpub\ticketdesk\Web.config' -Raw; ($wc.configuration.connectionStrings.add | Where-Object { $_.name -eq 'TicketDesk' }).connectionString -replace '(?i)^.*?(Data Source|Server)=([^;]+).*$', '$2' } catch { $null })
                defender = [ordered]@{ am_service = $mp.AMServiceEnabled; antivirus = $mp.AntivirusEnabled; realtime = $mp.RealTimeProtectionEnabled
                                       signature_version = $mp.AntivirusSignatureVersion; signature_updated_utc = $mp.AntivirusSignatureLastUpdated.ToUniversalTime().ToString('o') }
                tls = if ($cert) { [ordered]@{ thumbprint = $cert.Thumbprint; subject = $cert.Subject; issuer = $cert.Issuer; dns = @($cert.DnsNameList.Unicode); not_after = $cert.NotAfter.ToString('yyyy-MM-dd')
                                               private_key_usable = $(try { $null -ne [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert) } catch { $false }) } } else { $null }
                https_home = [ordered]@{ status = $homePage.StatusCode; ms = $homeMs; ticketdesk = ($homePage.Content -match 'TicketDesk') }
                admin_sign_in = $signIn
                site = (Get-Website -Name TicketDesk).State; app_pool = (Get-WebAppPoolState -Name TicketDesk).Value
                backup_task = (Get-ScheduledTask -TaskName 'TicketDesk nightly backup' -ErrorAction SilentlyContinue | ForEach-Object { $_.State.ToString() })
                rows = Get-RowCounts
            }
            $json = $facts | ConvertTo-Json -Depth 4 -Compress
            $json | Out-File "C:\TdLogs\verify-$RunId.json" -Encoding utf8
            Write-Output "TDVERIFY $json"
            if ($signIn -eq $false -or $homePage.StatusCode -ne 200) { throw 'verify: HTTPS home page or admin sign-in failed' }
        }
        default { throw "unknown step '$Name'" }
    }
    $timings[$Name] = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    Say ("step {0} done in {1}s" -f $Name, $timings[$Name])
}

try {
    Say "provision run $RunId on $env:COMPUTERNAME, steps: $Steps"
    foreach ($s in ($Steps -split '[,+\s]+' | Where-Object { $_ })) {
        if ($s -ne 'fetch' -and -not (Get-Module TdOps)) {
            if (-not (Test-Path "$Ops\TdOps.psm1")) { throw "no $Ops\TdOps.psm1: run the fetch step first" }
            Import-Module "$Ops\TdOps.psm1" -Force -DisableNameChecking
        }
        Invoke-Step $s
        if ($s -eq 'fetch') { Import-Module "$Ops\TdOps.psm1" -Force -DisableNameChecking }
    }
} catch {
    $failed = $_
    Say "FAILED: $($_.Exception.Message)"
    Say ($_.ScriptStackTrace)
} finally {
    Say ("TDTIMINGS " + ($timings | ConvertTo-Json -Compress) + (" total {0:N1}s" -f $total.Elapsed.TotalSeconds))
    Stop-Transcript | Out-Null
    try {
        if (Get-Command Send-TdBlob -ErrorAction SilentlyContinue) {
            Send-TdBlob -Url "$Blob/logs/$($env:COMPUTERNAME.ToLowerInvariant())/provision-$RunId.log" -Path $log
            Write-Output "transcript: $Blob/logs/$($env:COMPUTERNAME.ToLowerInvariant())/provision-$RunId.log"
        }
    } catch { Write-Output "transcript upload failed: $($_.Exception.Message)" }
}
if ($failed) { exit 1 }
exit 0
