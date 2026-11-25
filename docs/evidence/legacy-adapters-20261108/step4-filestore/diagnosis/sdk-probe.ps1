$ErrorActionPreference = 'Stop'
foreach ($d in "Newtonsoft.Json","Microsoft.Azure.KeyVault.Core","Microsoft.WindowsAzure.Storage") { [void][Reflection.Assembly]::LoadFrom("C:\inetpub\ticketdesk\bin\$d.dll") }
[xml]$c = Get-Content C:\inetpub\ticketdesk\Web.config -Raw
$cs = ($c.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AzureStorage' }).connectionString
$acct = [Microsoft.WindowsAzure.Storage.CloudStorageAccount]::Parse($cs)
"blob endpoint: " + $acct.BlobEndpoint
$client = $acct.CreateCloudBlobClient()
$cont = $client.GetContainerReference('probe-container')
try { "exists: " + $cont.Exists() } catch { "ERR " + $_.Exception.InnerException.Message; if ($_.Exception.InnerException.RequestInformation) { $_.Exception.InnerException.RequestInformation.ExtendedErrorInformation | Format-List | Out-String } }
function Try-Step($name, [scriptblock]$b) { try { $r = & $b; "$name -> $r" } catch { $e = $_.Exception; while ($e.InnerException) { $e = $e.InnerException }; "$name -> ERR $($e.Message)" } }
Try-Step 'CreateIfNotExists' { $cont.CreateIfNotExists() }
$blob = $cont.GetBlockBlobReference('dir/a.txt')
Try-Step 'UploadFromByteArray' { $bytes = [Text.Encoding]::UTF8.GetBytes('hello'); $blob.UploadFromByteArray($bytes, 0, $bytes.Length); 'ok' }
Try-Step 'ListBlobs(prefix)' { ($client.ListBlobs('probe-container/dir/') | Measure-Object).Count }
