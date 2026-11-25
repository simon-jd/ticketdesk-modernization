# Compile LegacyRender.cs with the .NET Framework compiler against the legacy site's DLLs and run it over the corpus.
$ErrorActionPreference = 'Stop'
Set-Location C:\TdRender
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /r:MarkdownSharp.dll /r:HtmlSanitizer.dll /r:AngleSharp.dll LegacyRender.cs
if ($LASTEXITCODE -ne 0) { throw "csc failed" }
.\LegacyRender.exe corpus.tsv legacy-out.tsv
Get-FileHash -Algorithm SHA256 MarkdownSharp.dll, HtmlSanitizer.dll, AngleSharp.dll | ForEach-Object { '{0} {1}' -f $_.Hash.ToLower(), (Split-Path $_.Path -Leaf) }
