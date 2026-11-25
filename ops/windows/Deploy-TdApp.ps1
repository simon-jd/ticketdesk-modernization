<#
.SYNOPSIS
  Deploys a published TicketDesk build into the IIS site and applies host-specific configuration.
.DESCRIPTION
  Stops the app pool, mirrors -PublishPath into the site folder (App_Data is preserved: it holds
  attachments, the Lucene index and logs), then sets in the deployed Web.config:
    - connectionStrings/TicketDesk  (default: SQL Express on this host, integrated security)
    - appSettings overrides from -AppSettings
    - ticketdesk:SetupEnabled per -SetupMode: Auto (default) turns the first-run wizard off once the
      database has tables. Upstream says to disable it after setup; while it is on, the anonymous
      /first-run-setup/create-database endpoint stays reachable (inventory finding S1).
    - compilation debug off
    - customErrors mode per -CustomErrorsMode (default RemoteOnly: remote clients get the generic error page,
      no stack traces or source lines; inventory finding S9)
    - appSettings/ticketdesk:RemoteAppApiKey from -RemoteAppApiKey (default: env TD_REMOTEAPP_API_KEY): the shared key of
      the System.Web adapters' remote authentication endpoint (/systemweb-adapters/authenticate). Empty: no endpoint.
    - connectionStrings/AzureStorage from -AzureStorageConnectionString (default: env TD_AZURE_STORAGE_CONNECTION):
      attachments go to Azure Blob storage (container ticketdesk-attachments) instead of App_Data\filestore. Empty: disk.
    - appSettings/ticketdesk:SqlAuthentication from -SqlAuthentication: ManagedIdentity makes the app sign its Azure SQL
      connections in with the VM's managed identity (EF6 interceptor in TicketDesk.Domain; the connection string then has
      no credentials). Empty: the setting is removed (SQL Express, integrated security). With it, -SetupMode Auto means off.
  -ConfigOnly changes only the Web.config values above, without stopping the pool or copying files: the file is replaced
  in one step and ASP.NET starts a new app domain on the next request while IIS queues requests (the cutover switch).
    - appSettings/ticketdesk:SharedCookieKeyRing from -SharedCookieKeyRing (default: env TD_SHARED_COOKIE_KEYRING): "database"
      makes the sign-in cookie the one shared with the new app (ASP.NET Core ticket format, Data Protection key ring in
      dbo.DataProtectionKeys, which td-db migrate creates). Empty: the cookie stays protected by the IIS machine key.
    - appSettings/ticketdesk:DataProtectionKeyVaultKeyId from -DataProtectionKeyVaultKeyId (default: env
      TD_DATAPROTECTION_KEYVAULT_KEY_ID; needs the shared key ring): new keys of that ring are stored wrapped with the Key
      Vault key, through the VM's managed identity (the AKS round; the new app wraps with the same key). Empty: removed.
  Secrets come from the environment (sent over stdin by the drivers), never from the command line, and are not logged.
  and starts the pool again. The repository's Web.config is not changed.
  Idempotent: re-running with the same inputs leaves the site in the same state.
.EXAMPLE
  .\Deploy-TdApp.ps1 -PublishPath C:\TdBuild\publish
  .\Deploy-TdApp.ps1 -ConnectionString 'Data Source=tcp:db01,1433;Initial Catalog=TicketDesk;Integrated Security=True'
#>
[CmdletBinding()]
param(
    [string]$PublishPath = 'C:\TdBuild\publish',
    [string]$SiteName = 'TicketDesk',
    [string]$AppPoolName = 'TicketDesk',
    [string]$ConnectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=TicketDesk;Integrated Security=True;MultipleActiveResultSets=True',
    [hashtable]$AppSettings = @{},
    [ValidateSet('Auto', 'Enabled', 'Disabled')][string]$SetupMode = 'Auto',
    [string]$WarmupUrl = 'http://localhost/',
    [ValidateSet('RemoteOnly', 'On', 'Off')][string]$CustomErrorsMode = 'RemoteOnly',
    [string]$RemoteAppApiKey = $env:TD_REMOTEAPP_API_KEY,
    [string]$AzureStorageConnectionString = $env:TD_AZURE_STORAGE_CONNECTION,
    [ValidateSet('', 'ManagedIdentity')][string]$SqlAuthentication = '',
    [switch]$ConfigOnly,
    [string]$SharedCookieKeyRing = $env:TD_SHARED_COOKIE_KEYRING,
    [string]$DataProtectionKeyVaultKeyId = $env:TD_DATAPROTECTION_KEYVAULT_KEY_ID
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TdOps.psm1') -Force
Assert-TdAdmin
Import-Module WebAdministration
if (-not (Test-Path (Join-Path $PublishPath 'Web.config'))) { throw "no published site at $PublishPath" }
$sitePath = [Environment]::ExpandEnvironmentVariables((Get-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath))

# First-run wizard on or off (see DESCRIPTION). Azure SQL: the database is imported before the app points at it.
if ($SetupMode -eq 'Auto' -and $SqlAuthentication) {
    $SetupMode = 'Disabled'
    Write-TdLog 'Azure SQL with the managed identity: first-run setup disabled'
}
if ($SetupMode -eq 'Auto') {
    try {
        $tables = [int](Invoke-TdSql -Query "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" -Scalar `
            -ServerInstance ($ConnectionString -replace '(?i)^.*?(Data Source|Server)=([^;]+).*$', '$2') `
            -Database ($ConnectionString -replace '(?i)^.*?(Initial Catalog|Database)=([^;]+).*$', '$2'))
        $SetupMode = if ($tables -gt 0) { 'Disabled' } else { 'Enabled' }
        Write-TdLog "database has $tables tables -> first-run setup $($SetupMode.ToLowerInvariant())"
    } catch {
        $SetupMode = 'Enabled'
        Write-TdLog "could not inspect the database ($($_.Exception.Message)); leaving first-run setup enabled" WARN
    }
}
if (-not $AppSettings.ContainsKey('ticketdesk:SetupEnabled')) {
    $AppSettings['ticketdesk:SetupEnabled'] = if ($SetupMode -eq 'Disabled') { 'false' } else { 'true' }
}

if (-not $ConfigOnly -and (Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped') {
    Stop-WebAppPool -Name $AppPoolName
    $t = 0; while ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Stopped' -and $t -lt 60) { Start-Sleep 1; $t++ }
}
try {
  if (-not $ConfigOnly) {
    # /XD by name. With the site's full App_Data path here, /MIR still mirrored the publish folder's App_Data and purged
    # the runtime content (App_Data\ticketdesk-searchindex deleted on the lab VM, 2026-11-08; attachments live there too).
    robocopy.exe $PublishPath $sitePath /MIR /XD App_Data /R:3 /W:2 /NP /NJH /NDL /NS /NC | Out-Host
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    Write-TdLog ("mirrored {0} -> {1} (robocopy rc {2}, App_Data kept)" -f $PublishPath, $sitePath, $LASTEXITCODE) CHANGE
    # App_Data content from the build (e.g. WebJob binaries) is added without overwriting runtime data.
    $pubData = Join-Path $PublishPath 'App_Data'
    if (Test-Path $pubData) {
        robocopy.exe $pubData (Join-Path $sitePath 'App_Data') /E /XO /XN /XC /R:3 /W:2 /NP /NJH /NJS /NDL /NFL | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy App_Data failed ($LASTEXITCODE)" }
    }
    $appData = Join-Path $sitePath 'App_Data'
    if (-not (Test-Path $appData)) { New-Item -ItemType Directory -Path $appData | Out-Null }
    Set-TdAcl -Path $appData -Identity "IIS AppPool\$AppPoolName" -Rights 'Modify'
  }

    # ---------- host-specific Web.config values
    $cfgPath = Join-Path $sitePath 'Web.config'
    [xml]$cfg = Get-Content $cfgPath -Raw
    $changed = @()
    $cs = $cfg.configuration.connectionStrings.add | Where-Object { $_.name -eq 'TicketDesk' }
    if (-not $cs) { throw 'Web.config has no TicketDesk connection string' }
    if ($cs.connectionString -ne $ConnectionString) { $cs.SetAttribute('connectionString', $ConnectionString); $changed += 'connectionStrings/TicketDesk' }
    if ($cs.providerName -ne 'System.Data.SqlClient') { $cs.SetAttribute('providerName', 'System.Data.SqlClient'); $changed += 'providerName' }
    # Shared file store: Azure Blob (Azurite in the lab, a storage account on Azure) when a connection string is given.
    $csNode = $cfg.configuration.SelectSingleNode('connectionStrings')
    $az = $csNode.SelectSingleNode("add[@name='AzureStorage']")
    if ($AzureStorageConnectionString) {
        if (-not $az) { $az = $cfg.CreateElement('add'); $az.SetAttribute('name', 'AzureStorage'); [void]$csNode.AppendChild($az) }
        if ($az.GetAttribute('connectionString') -ne $AzureStorageConnectionString) { $az.SetAttribute('connectionString', $AzureStorageConnectionString); $changed += 'connectionStrings/AzureStorage' }
        $endpoint = if ($AzureStorageConnectionString -match '(?i)BlobEndpoint=([^;]+)') { $Matches[1] } elseif ($AzureStorageConnectionString -match '(?i)AccountName=([^;]+)') { "account $($Matches[1])" } else { '?' }
        Write-TdLog "file store: Azure Blob ($endpoint)"
        # Application Insights' dependency collector adds x-ms-request-id/-root-id headers to outgoing HTTP calls after the
        # storage SDK has signed them; storage then rejects the SharedKey signature (403 AuthorizationFailure). Its config
        # excludes *.core.windows.net, localhost and 127.0.0.1 only, so a lab endpoint (Azurite at 10.0.2.2) is added.
        $blobHost = if ($AzureStorageConnectionString -match '(?i)BlobEndpoint=([^;]+)') { ([Uri]$Matches[1]).Host } else { $null }
        $aiPath = Join-Path $sitePath 'ApplicationInsights.config'
        if ($blobHost -and (Test-Path $aiPath)) {
            [xml]$ai = Get-Content $aiPath -Raw
            $aiNs = New-Object Xml.XmlNamespaceManager $ai.NameTable; $aiNs.AddNamespace('a', $ai.DocumentElement.NamespaceURI)
            $excl = $ai.SelectSingleNode('//a:ExcludeComponentCorrelationHttpHeadersOnDomains', $aiNs)
            $listed = @($excl.SelectNodes('a:Add', $aiNs) | ForEach-Object { $_.InnerText.Trim() })
            if ($excl -and -not ($listed | Where-Object { $blobHost -eq $_ -or $blobHost.EndsWith(".$_") })) {
                $n = $ai.CreateElement('Add', $ai.DocumentElement.NamespaceURI); $n.InnerText = $blobHost; [void]$excl.AppendChild($n)
                $ai.Save($aiPath)
                Write-TdLog "ApplicationInsights.config: no correlation headers to $blobHost" CHANGE
            }
        }
    } else {
        if ($az) { [void]$csNode.RemoveChild($az); $changed += 'connectionStrings/AzureStorage removed' }
        Write-TdLog 'file store: App_Data\filestore (no AzureStorage connection string)'
    }
    # System.Web adapters: remote authentication endpoint only with a key (the app checks it is a non-empty GUID).
    $keyNode = $cfg.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:RemoteAppApiKey' }
    if ($RemoteAppApiKey) {
        $g = [guid]::Empty
        if (-not [guid]::TryParse($RemoteAppApiKey, [ref]$g) -or $g -eq [guid]::Empty) { throw 'RemoteAppApiKey must be a non-empty GUID' }
        if (-not $keyNode) { $keyNode = $cfg.CreateElement('add'); $keyNode.SetAttribute('key', 'ticketdesk:RemoteAppApiKey'); [void]$cfg.configuration.appSettings.AppendChild($keyNode) }
        if ($keyNode.GetAttribute('value') -ne $RemoteAppApiKey) { $keyNode.SetAttribute('value', $RemoteAppApiKey); $changed += 'appSettings/ticketdesk:RemoteAppApiKey' }
        Write-TdLog 'remote authentication: on (/systemweb-adapters/authenticate)'
    } else {
        if ($keyNode) { [void]$keyNode.ParentNode.RemoveChild($keyNode); $changed += 'appSettings/ticketdesk:RemoteAppApiKey removed' }
        Write-TdLog 'remote authentication: off (no RemoteAppApiKey)'
    }
    # Azure SQL: the EF6 interceptor adds the managed identity's token (TicketDesk.Domain ManagedIdentitySqlTokenInterceptor).
    $sqlAuthNode = $cfg.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:SqlAuthentication' }
    if ($SqlAuthentication) {
        if (-not $sqlAuthNode) { $sqlAuthNode = $cfg.CreateElement('add'); $sqlAuthNode.SetAttribute('key', 'ticketdesk:SqlAuthentication'); [void]$cfg.configuration.appSettings.AppendChild($sqlAuthNode) }
        if ($sqlAuthNode.GetAttribute('value') -ne $SqlAuthentication) { $sqlAuthNode.SetAttribute('value', $SqlAuthentication); $changed += "appSettings/ticketdesk:SqlAuthentication=$SqlAuthentication" }
        Write-TdLog "database: Azure SQL, $SqlAuthentication ($($ConnectionString -replace '(?i)^.*?(Data Source|Server)=([^;]+).*$', '$2'))"
    } else {
        if ($sqlAuthNode) { [void]$sqlAuthNode.ParentNode.RemoveChild($sqlAuthNode); $changed += 'appSettings/ticketdesk:SqlAuthentication removed' }
    }
    # Shared sign-in with the new app (Microsoft.Owin.Security.Interop + the database key ring).
    $scNode = $cfg.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:SharedCookieKeyRing' }
    if ($SharedCookieKeyRing -and $SharedCookieKeyRing -ne 'database') { throw "SharedCookieKeyRing must be empty or 'database'" }
    if ($SharedCookieKeyRing) {
        if (-not $scNode) { $scNode = $cfg.CreateElement('add'); $scNode.SetAttribute('key', 'ticketdesk:SharedCookieKeyRing'); [void]$cfg.configuration.appSettings.AppendChild($scNode) }
        if ($scNode.GetAttribute('value') -ne $SharedCookieKeyRing) { $scNode.SetAttribute('value', $SharedCookieKeyRing); $changed += 'appSettings/ticketdesk:SharedCookieKeyRing' }
        Write-TdLog "shared sign-in cookie: on (key ring: $SharedCookieKeyRing)"
    } else {
        if ($scNode) { [void]$scNode.ParentNode.RemoveChild($scNode); $changed += 'appSettings/ticketdesk:SharedCookieKeyRing removed' }
        Write-TdLog 'shared sign-in cookie: off (machine key)'
    }
    # AKS round: the shared key ring wrapped with a Key Vault key through the VM's managed identity (same key as the new app).
    $kvNode = $cfg.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:DataProtectionKeyVaultKeyId' }
    if ($DataProtectionKeyVaultKeyId) {
        if (-not $SharedCookieKeyRing) { throw 'DataProtectionKeyVaultKeyId needs SharedCookieKeyRing=database (it wraps that key ring)' }
        if ($DataProtectionKeyVaultKeyId -notmatch '^https://[a-z0-9-]+\.vault\.azure\.net/keys/[A-Za-z0-9-]+(/[0-9a-f]{32})?$') { throw 'DataProtectionKeyVaultKeyId must be https://<vault>.vault.azure.net/keys/<name>[/<version>]' }
        if (-not $kvNode) { $kvNode = $cfg.CreateElement('add'); $kvNode.SetAttribute('key', 'ticketdesk:DataProtectionKeyVaultKeyId'); [void]$cfg.configuration.appSettings.AppendChild($kvNode) }
        if ($kvNode.GetAttribute('value') -ne $DataProtectionKeyVaultKeyId) { $kvNode.SetAttribute('value', $DataProtectionKeyVaultKeyId); $changed += 'appSettings/ticketdesk:DataProtectionKeyVaultKeyId' }
        Write-TdLog "key ring wrapped with Key Vault key $DataProtectionKeyVaultKeyId (managed identity)"
    } else {
        if ($kvNode) { [void]$kvNode.ParentNode.RemoveChild($kvNode); $changed += 'appSettings/ticketdesk:DataProtectionKeyVaultKeyId removed' }
    }
    foreach ($k in $AppSettings.Keys) {
        $node = $cfg.configuration.appSettings.add | Where-Object { $_.key -eq $k }
        if (-not $node) {
            $node = $cfg.CreateElement('add'); $node.SetAttribute('key', $k)
            [void]$cfg.configuration.appSettings.AppendChild($node)
        }
        if ($node.GetAttribute('value') -ne [string]$AppSettings[$k]) { $node.SetAttribute('value', [string]$AppSettings[$k]); $changed += "appSettings/$k" }
    }
    $comp = $cfg.configuration.'system.web'.compilation
    if ($comp -and $comp.HasAttribute('debug') -and $comp.GetAttribute('debug') -ne 'false') { $comp.SetAttribute('debug', 'false'); $changed += 'compilation/debug' }
    $sysWeb = $cfg.configuration.SelectSingleNode('system.web')
    $ce = $sysWeb.SelectSingleNode('customErrors')
    if (-not $ce) { $ce = $cfg.CreateElement('customErrors'); [void]$sysWeb.AppendChild($ce) }
    if ($ce.GetAttribute('mode') -ne $CustomErrorsMode) { $ce.SetAttribute('mode', $CustomErrorsMode); $changed += "customErrors/mode=$CustomErrorsMode" }
    if ($changed) {
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true; $settings.Encoding = New-Object System.Text.UTF8Encoding($true)
        if ($ConfigOnly) {
            # one atomic replace: ASP.NET never sees a half-written file while the app keeps serving
            $tmp = "$cfgPath.td-new"
            $w = [System.Xml.XmlWriter]::Create($tmp, $settings); $cfg.Save($w); $w.Close()
            [IO.File]::Replace($tmp, $cfgPath, [NullString]::Value)  # $null would arrive as ""
        } else {
            $w = [System.Xml.XmlWriter]::Create($cfgPath, $settings); $cfg.Save($w); $w.Close()
        }
        Write-TdLog ("Web.config: {0}" -f ($changed -join ', ')) CHANGE
    } else { Write-TdLog 'Web.config already configured for this host' SKIP }
} finally {
    if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne 'Started') { Start-WebAppPool -Name $AppPoolName }
}

if ($WarmupUrl) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    # HttpWebRequest without redirects: the first response tells whether the app is up (200) or wants
    # the first-run setup (302 -> /first-run-setup).
    $req = [System.Net.HttpWebRequest]::Create($WarmupUrl)
    $req.AllowAutoRedirect = $false; $req.Timeout = 600000
    try { $resp = $req.GetResponse() } catch [System.Net.WebException] { $resp = $_.Exception.Response }
    if ($resp) {
        $code = [int]$resp.StatusCode; $loc = $resp.Headers['Location']; $resp.Close()
        if ($loc) { $code = "$code -> $loc" }
    } else { $code = 'no response' }
    Write-TdLog ("warm-up {0} -> {1} in {2:N1}s" -f $WarmupUrl, $code, $sw.Elapsed.TotalSeconds)
    if ("$code" -notmatch '^(200|302)') {
        # A failed start: the exception from ASP.NET's event log (health monitoring logs it whatever customErrors says).
        $ev = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'ASP.NET 4.0.30319.0'; StartTime = (Get-Date).AddMinutes(-15) } -MaxEvents 3 -ErrorAction SilentlyContinue
        foreach ($e in @($ev)) {
            $lines = @(($e.Message -split "`r?`n") | Where-Object { $_ -match 'Exception (type|message)|Event message|Stack trace|^\s+at ' } | Select-Object -First 25)
            Write-TdLog ("ASP.NET event {0} at {1:u}:`n  {2}" -f $e.Id, $e.TimeCreated.ToUniversalTime(), ($lines -join "`n  ")) WARN
        }
    }
}
