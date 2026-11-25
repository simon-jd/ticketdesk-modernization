<#
.SYNOPSIS
  Tests TicketDesk's Blob file store provider (TicketDesk.IO TicketDeskBlobStorageProvider) against a real blob endpoint.
.DESCRIPTION
  The legacy solution has no test projects, so this runs the deployed assemblies (C:\inetpub\ticketdesk\bin) on the host:
  save a file, list it, rename it within a folder and across folders (the attachment commit: pending/<name>.<tempId> ->
  <ticketId>/<name>), refuse a rename onto an existing file and from a missing file, rename a folder, read every file back
  (bytes compared), delete. Uses a throwaway container (td-store-test-<random>) that is deleted at the end.
  Connection string from TD_AZURE_STORAGE_CONNECTION (never printed). One PASS/FAIL line per check; exit 1 on a failure.
#>
[CmdletBinding()]
param(
    [string]$SitePath = 'C:\inetpub\ticketdesk',
    [string]$ConnectionString = $env:TD_AZURE_STORAGE_CONNECTION
)
$ErrorActionPreference = 'Stop'
if (-not $ConnectionString) { throw 'no connection string (TD_AZURE_STORAGE_CONNECTION)' }
# The site's binding redirects do not apply here (AzureBlobFileSystem asks for WindowsAzure.Storage 3.1.0.1): resolve any
# assembly by name from the site's bin, as the redirects would.
$bin = Join-Path $SitePath 'bin'
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $e)
    $f = Join-Path $bin ((New-Object Reflection.AssemblyName $e.Name).Name + '.dll')
    if (Test-Path $f) { [Reflection.Assembly]::LoadFrom($f) } else { $null }
})
foreach ($d in 'Newtonsoft.Json', 'Microsoft.Azure.KeyVault.Core', 'Microsoft.WindowsAzure.Storage', 'AzureBlobFileSystem') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $bin "$d.dll"))
}
$io = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'TicketDesk.IO.dll'))
$account = [Microsoft.WindowsAzure.Storage.CloudStorageAccount]::Parse($ConnectionString)
$type = $io.GetType('TicketDesk.IO.TicketDeskBlobStorageProvider', $true)
$p = [Activator]::CreateInstance($type, [Reflection.BindingFlags]'Instance,Public,NonPublic', $null, @(, $account), $null)
$c = 'td-store-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$failures = 0
function Check([string]$name, [scriptblock]$test) {
    try { $r = & $test; if ($r -eq $false) { throw 'returned false' }; Write-Output "PASS  $name" }
    catch { $script:failures++; $e = $_.Exception; while ($e.InnerException) { $e = $e.InnerException }; Write-Output "FAIL  $name : $($e.GetType().Name): $($e.Message)" }
}
function Save([string]$path, [string]$text) { $ms = New-Object IO.MemoryStream (,[Text.Encoding]::UTF8.GetBytes($text)); $p.SaveStream($path, $ms) }
function Read([string]$path) { $s = $p.GetFile($path).OpenRead(); try { (New-Object IO.StreamReader $s).ReadToEnd() } finally { $s.Dispose() } }
function Throws([scriptblock]$b) { try { & $b; $false } catch { $true } }

$pending = "$c/pending/note.txt.11111111-2222-3333-4444-555555555555"
Check 'save a pending file' { Save $pending 'hello attachment'; $p.FileExists($pending) }
Check 'list pending files' { @($p.ListFiles("$c/pending")).Count -eq 1 }
Check 'commit: rename pending -> ticket folder' { [void]$p.TryCreateFolder("$c/1001"); $p.RenameFile($pending, "$c/1001/note.txt"); (-not $p.FileExists($pending)) -and $p.FileExists("$c/1001/note.txt") }
Check 'content survives the rename' { (Read "$c/1001/note.txt") -eq 'hello attachment' }
Check 'size after the rename' { (@($p.ListFiles("$c/1001")) | Where-Object { $_.GetName() -eq 'note.txt' }).GetSize() -eq 16 }
Check 'rename onto an existing file is refused' { Save "$c/1001/other.txt" 'x'; Throws { $p.RenameFile("$c/1001/other.txt", "$c/1001/note.txt") } }
Check 'existing target unchanged' { (Read "$c/1001/note.txt") -eq 'hello attachment' }
Check 'rename of a missing file is refused' { Throws { $p.RenameFile("$c/1001/missing.txt", "$c/1001/x.txt") } }
Check 'rename within a folder' { $p.RenameFile("$c/1001/other.txt", "$c/1001/note(1).txt"); $p.FileExists("$c/1001/note(1).txt") -and -not $p.FileExists("$c/1001/other.txt") }
Check 'rename a folder' { $p.RenameFolder("$c/1001", "$c/2002"); $p.FileExists("$c/2002/note.txt") -and $p.FileExists("$c/2002/note(1).txt") -and -not $p.FileExists("$c/1001/note.txt") }
Check 'read after the folder rename' { (Read "$c/2002/note.txt") -eq 'hello attachment' }
Check 'delete' { $p.DeleteFile("$c/2002/note(1).txt"); -not $p.FileExists("$c/2002/note(1).txt") }
try { $account.CreateCloudBlobClient().GetContainerReference($c).DeleteIfExists() | Out-Null } catch { }
Write-Output "$failures failure(s)"
exit ([int]($failures -gt 0))
