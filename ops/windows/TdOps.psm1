# TdOps.psm1 - shared helpers for the TicketDesk Windows host scripts (Windows PowerShell 5.1+).
# Nothing here assumes KVM, Hyper-V or Azure: the same scripts run on the lab VM and on an Azure VM
# (the Azure helpers at the end run only when a script is given an Azure parameter).
Set-StrictMode -Version 2.0

function Write-TdLog {
    param([Parameter(Mandatory)][string]$Message, [ValidateSet('INFO', 'CHANGE', 'SKIP', 'WARN')][string]$Level = 'INFO')
    Write-Output ('[{0:yyyy-MM-ddTHH:mm:ssZ}] {1,-6} {2}' -f (Get-Date).ToUniversalTime(), $Level, $Message)
}

function Assert-TdAdmin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated (Administrator) session.'
    }
}

function Get-TdFile {
    # Download once to $Destination (skips when it already exists with the expected size or hash).
    param([Parameter(Mandatory)][string]$Uri, [Parameter(Mandatory)][string]$Destination, [string]$Sha256)
    if (Test-Path $Destination) {
        if (-not $Sha256 -or (Get-FileHash $Destination -Algorithm SHA256).Hash -eq $Sha256.ToUpperInvariant()) {
            Write-TdLog "have $Destination" SKIP; return
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $tmp = "$Destination.partial"
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $Uri -OutFile $tmp -UseBasicParsing
    if ($Sha256 -and (Get-FileHash $tmp -Algorithm SHA256).Hash -ne $Sha256.ToUpperInvariant()) {
        Remove-Item $tmp -Force; throw "SHA256 mismatch for $Uri"
    }
    Move-Item $tmp $Destination -Force
    Write-TdLog ("downloaded {0} ({1:N0} bytes, sha256 {2})" -f $Destination, (Get-Item $Destination).Length, (Get-FileHash $Destination -Algorithm SHA256).Hash) CHANGE
}

function Invoke-TdProcessAsSystem {
    # Runs a program as LocalSystem through a one-shot scheduled task and returns its exit code.
    # Needed for installers that use DPAPI (e.g. SQL Server setup with /SAPWD): an OpenSSH key-auth or
    # WinRM network logon has no user credentials, so DPAPI fails with "Access is denied".
    param([Parameter(Mandatory)][string]$FilePath, [string[]]$ArgumentList = @(), [int]$TimeoutMinutes = 120)
    $name = 'TdOps-' + [guid]::NewGuid().ToString('n')
    $action = New-ScheduledTaskAction -Execute $FilePath -Argument ($ArgumentList -join ' ')
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes $TimeoutMinutes) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
    try {
        Start-ScheduledTask -TaskName $name
        $deadline = (Get-Date).AddMinutes($TimeoutMinutes + 2)
        do {
            Start-Sleep -Seconds 5
            $state = (Get-ScheduledTask -TaskName $name).State
            $result = [uint32](Get-ScheduledTaskInfo -TaskName $name).LastTaskResult
        } while (($state -eq 'Running' -or $state -eq 'Queued' -or $result -eq 267011 -or $result -eq 267009) -and (Get-Date) -lt $deadline)
        if ($state -eq 'Running') { throw "$FilePath still running after $TimeoutMinutes minutes" }
        return [BitConverter]::ToInt32([BitConverter]::GetBytes($result), 0)
    } finally {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
    }
}

function Invoke-TdSql {
    # Runs T-SQL through System.Data.SqlClient (in .NET Framework; no sqlcmd or SqlServer module needed).
    # Integrated security by default; with -AccessToken (Azure SQL, Microsoft Entra token of a managed identity) the
    # connection carries the token instead and is encrypted. Azure SQL's transient errors (a serverless database resuming,
    # 40613/40197/40501/49918/4060) are retried for up to three minutes.
    param(
        [Parameter(Mandatory)][string]$Query,
        [string]$ServerInstance = '.\SQLEXPRESS',
        [string]$Database = 'master',
        [int]$TimeoutSeconds = 600,
        [hashtable]$Parameters = @{},
        [string]$AccessToken,
        [switch]$Scalar,
        [switch]$NonQuery,
        [switch]$AllResults
    )
    if ($AccessToken) {
        $cs = "Data Source=$ServerInstance;Initial Catalog=$Database;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;Application Name=TdOps"
    } else {
        $cs = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=SSPI;Connect Timeout=60;Application Name=TdOps"
    }
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    if ($AccessToken) { $conn.AccessToken = $AccessToken }
    for ($i = 1; ; $i++) {
        try { $conn.Open(); break }
        catch [System.Data.SqlClient.SqlException] {
            if ($AccessToken -and $_.Exception.Number -in 40613, 40197, 40501, 49918, 4060 -and $i -lt 18) {
                # host stream, not output: the caller gets only the query result
                Write-Host ('[{0:yyyy-MM-ddTHH:mm:ssZ}] WARN   Azure SQL not available yet (error {1}), retry {2} in 10 s' -f (Get-Date).ToUniversalTime(), $_.Exception.Number, $i)
                Start-Sleep -Seconds 10
                $conn = New-Object System.Data.SqlClient.SqlConnection $cs
                $conn.AccessToken = $AccessToken
                continue
            }
            throw
        }
    }
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Query
        $cmd.CommandTimeout = $TimeoutSeconds
        foreach ($k in $Parameters.Keys) { [void]$cmd.Parameters.AddWithValue("@$k", $Parameters[$k]) }
        if ($Scalar) { return $cmd.ExecuteScalar() }
        if ($NonQuery) { [void]$cmd.ExecuteNonQuery(); return }
        $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
        if ($AllResults) { $ds = New-Object System.Data.DataSet; [void]$da.Fill($ds); return , $ds }
        $dt = New-Object System.Data.DataTable
        [void]$da.Fill($dt)
        return , $dt
    } finally { $conn.Close() }
}

function Set-TdAcl {
    # Grants $Identity the given rights on $Path (inherited by children); no-op when already granted.
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Identity,
          [System.Security.AccessControl.FileSystemRights]$Rights = 'Modify')
    $acl = Get-Acl $Path
    $have = $acl.Access | Where-Object {
        $_.IdentityReference.Value -eq $Identity -and $_.AccessControlType -eq 'Allow' -and
        ($_.FileSystemRights -band $Rights) -eq $Rights -and $_.InheritanceFlags -ne 'None' }
    if ($have) { Write-TdLog "$Identity already has $Rights on $Path" SKIP; return }
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($Identity, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $Path -AclObject $acl
    Write-TdLog "granted $Identity $Rights on $Path" CHANGE
}

function Read-TdSecret {
    # Secret from a parameter, else from an environment variable; never echoed.
    param([string]$Value, [Parameter(Mandatory)][string]$EnvName)
    if ($Value) { return $Value }
    $v = [Environment]::GetEnvironmentVariable($EnvName)
    if ($v) { return $v }
    return $null
}

# ---------- Azure helpers (used only when a script gets an Azure parameter such as -BlobContainerUrl;
# on any other host nothing calls them). Authentication is the VM's system-assigned managed identity
# through the instance metadata service: no keys, SAS tokens or passwords on the host.

function Get-TdManagedIdentityToken {
    # Access token for $Resource (e.g. https://vault.azure.net, https://storage.azure.com/, https://database.windows.net/).
    # Without -ClientId: the system-assigned identity; with it: that user-assigned identity (the Azure SQL deploy identity).
    param([Parameter(Mandatory)][string]$Resource, [string]$ClientId)
    $uri = 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=' + [uri]::EscapeDataString($Resource)
    if ($ClientId) { $uri += '&client_id=' + [uri]::EscapeDataString($ClientId) }
    for ($i = 1; ; $i++) {
        try { return (Invoke-RestMethod -Uri $uri -Headers @{ Metadata = 'true' } -UseBasicParsing -TimeoutSec 30).access_token }
        catch {
            if ($i -ge 10) { throw "managed identity token for $Resource failed: $($_.Exception.Message)" }
            Start-Sleep -Seconds (5 * $i)
        }
    }
}

function Invoke-TdAzureRequest {
    # REST call with a managed identity token. Retries 403 (a new identity's role assignments take a few
    # minutes to apply), throttling, 5xx and network errors; other errors throw at once.
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Resource,
        [string]$Method = 'GET',
        [hashtable]$Headers = @{},
        [string]$InFile,
        [string]$OutFile,
        [int]$MaxAttempts = 30
    )
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'   # Windows PowerShell's progress bar slows large transfers badly
    $short = $Uri.Split('?')[0]
    for ($i = 1; ; $i++) {
        $h = @{ Authorization = 'Bearer ' + (Get-TdManagedIdentityToken -Resource $Resource) }
        foreach ($k in $Headers.Keys) { $h[$k] = $Headers[$k] }
        $p = @{ Uri = $Uri; Method = $Method; Headers = $h; UseBasicParsing = $true; TimeoutSec = 1800 }
        if ($InFile) { $p.InFile = $InFile; $p.ContentType = 'application/octet-stream' }
        if ($OutFile) { $p.OutFile = $OutFile }
        try { return Invoke-WebRequest @p }
        catch {
            $code = 0
            if ($_.Exception -is [System.Net.WebException] -and $_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            if ($code -notin 0, 403, 408, 429, 500, 502, 503, 504 -or $i -ge $MaxAttempts) {
                throw "$Method $short failed: HTTP $code $($_.Exception.Message)"
            }
            Write-TdLog "$Method $short -> HTTP $code, retry $i/$MaxAttempts in 15 s" WARN
            Start-Sleep -Seconds 15
        }
    }
}

function Get-TdKeyVaultSecret {
    # Secret value from Key Vault (never logged). A certificate's secret is its PFX, base64.
    param([Parameter(Mandatory)][string]$VaultName, [Parameter(Mandatory)][string]$Name)
    $r = Invoke-TdAzureRequest -Uri "https://$VaultName.vault.azure.net/secrets/$($Name)?api-version=7.4" -Resource 'https://vault.azure.net'
    return ($r.Content | ConvertFrom-Json).value
}

function Test-TdKeyVaultSecret {
    # True when the secret exists (stages differ in which secrets they create); other errors throw.
    param([Parameter(Mandatory)][string]$VaultName, [Parameter(Mandatory)][string]$Name)
    $uri = "https://$VaultName.vault.azure.net/secrets/$($Name)?api-version=7.4"
    try {
        Invoke-WebRequest -Uri $uri -Headers @{ Authorization = 'Bearer ' + (Get-TdManagedIdentityToken -Resource 'https://vault.azure.net') } -UseBasicParsing -TimeoutSec 60 | Out-Null  # Key Vault has no HEAD (405)
        return $true
    } catch {
        if ($_.Exception -is [System.Net.WebException] -and $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $false }
        throw
    }
}

function Get-TdTokenClaims {
    # Claims of a JWT (payload only, no signature check): used to read a managed identity's own appid/oid. Never logs the token.
    param([Parameter(Mandatory)][string]$Token)
    $p = $Token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    switch ($p.Length % 4) { 2 { $p += '==' } 3 { $p += '=' } }
    return ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json)
}

$script:TdBlobHeaders = @{ 'x-ms-version' = '2023-11-03' }

function Save-TdBlob {
    # Downloads a blob to $Destination; with -Sha256 the file must match or the call throws.
    param([Parameter(Mandatory)][string]$Url, [Parameter(Mandatory)][string]$Destination, [string]$Sha256)
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    $tmp = "$Destination.partial"
    Invoke-TdAzureRequest -Uri $Url -Resource 'https://storage.azure.com/' -Headers $script:TdBlobHeaders -OutFile $tmp | Out-Null
    $hash = (Get-FileHash $tmp -Algorithm SHA256).Hash
    if ($Sha256 -and $hash -ne $Sha256.ToUpperInvariant()) { Remove-Item $tmp -Force; throw "SHA256 mismatch for $Url ($hash)" }
    Move-Item $tmp $Destination -Force
    return $hash
}

function Send-TdBlob {
    # Uploads one file as a block blob (single Put Blob: files up to 5000 MiB).
    param([Parameter(Mandatory)][string]$Url, [Parameter(Mandatory)][string]$Path)
    $h = $script:TdBlobHeaders.Clone(); $h['x-ms-blob-type'] = 'BlockBlob'
    Invoke-TdAzureRequest -Uri $Url -Resource 'https://storage.azure.com/' -Method Put -Headers $h -InFile $Path | Out-Null
}

function Get-TdBlobList {
    # Blob names under $Prefix in a container (all pages).
    param([Parameter(Mandatory)][string]$ContainerUrl, [string]$Prefix = '')
    $names = @(); $marker = ''
    do {
        $u = "$($ContainerUrl)?restype=container&comp=list&prefix=$([uri]::EscapeDataString($Prefix))"
        if ($marker) { $u += "&marker=$([uri]::EscapeDataString($marker))" }
        $r = Invoke-TdAzureRequest -Uri $u -Resource 'https://storage.azure.com/' -Headers $script:TdBlobHeaders
        # The listing is UTF-8 with a BOM and no charset header: decode the bytes, not .Content (Latin-1).
        $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()).TrimStart([char]0xFEFF)
        [xml]$x = $text
        $names += @($x.SelectNodes('/EnumerationResults/Blobs/Blob/Name') | ForEach-Object { $_.InnerText })
        $m = $x.SelectSingleNode('/EnumerationResults/NextMarker')
        $marker = if ($m) { $m.InnerText } else { '' }
    } while ($marker)
    return , $names
}

Export-ModuleMember -Function Write-TdLog, Assert-TdAdmin, Get-TdFile, Invoke-TdProcessAsSystem, Invoke-TdSql, Set-TdAcl, Read-TdSecret,
    Get-TdManagedIdentityToken, Invoke-TdAzureRequest, Get-TdKeyVaultSecret, Test-TdKeyVaultSecret, Get-TdTokenClaims, Save-TdBlob, Send-TdBlob, Get-TdBlobList
