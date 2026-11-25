#!/usr/bin/env bash
# legacy-artifact.sh - build the legacy app once into a publish package, and deploy a package, on the lab VM.
#
#   legacy-artifact.sh build [REV]      git archive REV (default HEAD) -> C:\TdSrc\ticketdesk, Build-TdLegacy.ps1, zip
#                                       C:\TdBuild\publish -> $TD_ARTIFACTS/td-legacy-publish-<rev>.zip (+ .sha256,
#                                       .build-info.json)                                              [needs the VM lock]
#   legacy-artifact.sh deploy ZIP       copy ZIP to the VM, check its SHA-256, unpack to C:\TdBuild\publish, run
#                                       Deploy-TdApp.ps1 with the deploy secrets (below)                [needs the VM lock]
#   legacy-artifact.sh info             what is deployed now (web.config adapters/file-store settings, build-info)
#
# Deploy secrets (environment of Deploy-TdApp.ps1, sent over stdin, never printed):
#   TD_REMOTEAPP_API_KEY          from $TD_W/.secrets/legacy-remoteapp.env   (System.Web adapters remote authentication)
#   TD_AZURE_STORAGE_CONNECTION   from $TD_W/.secrets/legacy-filestore.env   (attachments in Azure Blob / Azurite)
# Set TD_DEPLOY_REMOTEAPP=0 or TD_DEPLOY_FILESTORE=0 to deploy without one of them (the setting is then removed).
# TD_SHARED_COOKIE_KEYRING=database (not a secret) turns on the sign-in cookie shared with the new app (needs td-db V0010).
# Package naming matches ops/azure/lift.sh (td-legacy-publish-<rev>.zip), so the same package can be staged for Azure.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
V="$HERE/vm/vmctl.sh"
LOCK="$HERE/vm/vmlock.sh"
ART="${TD_ARTIFACTS:-$TD_W/vm/artifacts/legacy-adapters}"
SECRETS="$TD_W/.secrets"
OWNER="${TD_LOCK_OWNER:-}"
log() { echo "$(date -u +%FT%TZ) $*"; }
die() { echo "legacy-artifact: $*" >&2; exit 1; }
envval() { [[ -f "$1" ]] && sed -n "s/^$2=//p" "$1" | head -1 || true; }
need_lock() {
  [[ -n "$OWNER" ]] || die "set TD_LOCK_OWNER to the VM lock holder's name"
  "$LOCK" check "$OWNER" || die "VM lock not held by $OWNER ($("$LOCK" status))"
}

cmd_build() {
  need_lock
  local rev="${1:-HEAD}" short tarball out t0=$SECONDS
  short="$(git -C "$REPO" rev-parse --short "$rev")"
  tarball="$(mktemp --suffix=.tar)"
  git -C "$REPO" archive --format=tar -o "$tarball" "$rev"
  "$V" ssh 'if (Test-Path C:\TdSrc\ticketdesk) { Remove-Item C:\TdSrc\ticketdesk -Recurse -Force }; New-Item -ItemType Directory -Force C:\TdSrc\ticketdesk, C:\TdOps\windows | Out-Null'
  "$V" scp "$tarball" "vm:C:/TdSrc/ticketdesk.tar"; rm -f "$tarball"
  "$V" ssh "tar -xf C:\\TdSrc\\ticketdesk.tar -C C:\\TdSrc\\ticketdesk; Remove-Item C:\\TdSrc\\ticketdesk.tar; Set-Content C:\\TdSrc\\ticketdesk\\REVISION '$short'"
  # the build/deploy scripts of the same revision
  "$V" ssh 'Copy-Item C:\TdSrc\ticketdesk\ops\windows\* C:\TdOps\windows\ -Force'
  log "build $short on the VM"
  "$V" ssh "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\\TdOps\\windows\\Build-TdLegacy.ps1 -SourcePath C:\\TdSrc\\ticketdesk -PublishPath C:\\TdBuild\\publish -SourceRevision $short"
  out="$("$V" ps <<'PS'
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = 'C:\TdMedia\td-legacy-publish.zip'
New-Item -ItemType Directory -Force C:\TdMedia | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory('C:\TdBuild\publish', $zip, 'Optimal', $false)
"ZIPSHA=" + (Get-FileHash $zip -Algorithm SHA256).Hash
PS
)"
  mkdir -p "$ART"
  local zip="$ART/td-legacy-publish-$short.zip" sha
  "$V" scp "vm:C:/TdMedia/td-legacy-publish.zip" "$zip"
  "$V" scp "vm:C:/TdBuild/build-info.json" "$ART/td-legacy-publish-$short.build-info.json"
  "$V" ssh 'Remove-Item C:\TdMedia\td-legacy-publish.zip -Force' || true
  ( cd "$ART" && sha256sum "$(basename "$zip")" > "$(basename "$zip").sha256" )
  sha="$(cut -c1-64 "$zip.sha256")"
  [[ "${sha^^}" == "$(sed -n 's/^ZIPSHA=//p' <<<"$out" | tr -d '\r')" ]] || die "SHA-256 after copy differs from the VM's"
  log "package $zip ($(stat -c %s "$zip") bytes, sha256 $sha) in $((SECONDS - t0)) s"
}

cmd_deploy() {
  need_lock
  local zip="${1:?zip}" sha lines=() key conn t0=$SECONDS
  [[ -f "$zip" ]] || die "no $zip"
  sha="$(sha256sum "$zip" | cut -c1-64)"
  [[ ! -f "$zip.sha256" || "$(cut -c1-64 "$zip.sha256")" == "$sha" ]] || die "$zip does not match $zip.sha256"
  "$V" ssh 'New-Item -ItemType Directory -Force C:\TdMedia, C:\TdOps\windows | Out-Null; if (Test-Path C:\TdBuild\publish.sha256) { Remove-Item C:\TdBuild\publish.sha256 }'
  "$V" scp "$REPO"/ops/windows/*.ps1 "$REPO"/ops/windows/*.psm1 "vm:C:/TdOps/windows/"   # deploy script of this checkout
  "$V" scp "$zip" "vm:C:/TdMedia/td-legacy-publish.zip"
  key=""; conn=""
  [[ "${TD_DEPLOY_REMOTEAPP:-1}" == 1 ]] && key="$(envval "$SECRETS/legacy-remoteapp.env" TD_REMOTEAPP_API_KEY)"
  [[ "${TD_DEPLOY_FILESTORE:-1}" == 1 ]] && conn="$(envval "$SECRETS/legacy-filestore.env" TD_AZURE_STORAGE_CONNECTION)"
  lines+=("\$ErrorActionPreference = 'Stop'")
  lines+=("\$env:TD_REMOTEAPP_API_KEY = '$key'" "\$env:TD_AZURE_STORAGE_CONNECTION = '$conn'")
  [[ "${TD_SHARED_COOKIE_KEYRING:-}" =~ ^(|database)$ ]] || die "TD_SHARED_COOKIE_KEYRING must be empty or 'database'"
  lines+=("\$env:TD_SHARED_COOKIE_KEYRING = '${TD_SHARED_COOKIE_KEYRING:-}'")
  # one line: "powershell -Command -" runs stdin line by line and silently drops an unfinished multi-line block
  local body=(
    "\$h = (Get-FileHash C:\\TdMedia\\td-legacy-publish.zip -Algorithm SHA256).Hash"
    "if (\$h -ne '${sha^^}') { throw \"package SHA-256 \$h differs\" }"
    "Add-Type -AssemblyName System.IO.Compression.FileSystem"
    "if (Test-Path C:\\TdBuild\\publish) { Remove-Item C:\\TdBuild\\publish -Recurse -Force }"
    "[IO.Compression.ZipFile]::ExtractToDirectory('C:\\TdMedia\\td-legacy-publish.zip', 'C:\\TdBuild\\publish')"
    "& 'C:\\TdOps\\windows\\Deploy-TdApp.ps1' -PublishPath C:\\TdBuild\\publish"
    "if (-not \$?) { throw 'Deploy-TdApp.ps1 failed' }"
    "Set-Content C:\\TdBuild\\publish.sha256 \$h")
  local joined; joined="$(printf '%s; ' "${body[@]}")"
  lines+=("try { $joined } catch { Write-Host (\$_ | Out-String); exit 1 }" "exit 0")
  printf '%s\n' "${lines[@]}" | "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -'
  local got; got="$("$V" ssh 'if (Test-Path C:\TdBuild\publish.sha256) { (Get-Content C:\TdBuild\publish.sha256 -Raw).Trim() }' | tr -d '\r\n')"
  [[ "$got" == "${sha^^}" ]] || die "deploy did not complete (deployed package marker: '${got:-none}')"
  log "deployed $(basename "$zip") (sha256 $sha) in $((SECONDS - t0)) s"
}

cmd_info() {
  "$V" ps <<'PS'
[xml]$c = Get-Content C:\inetpub\ticketdesk\Web.config -Raw
$key = $c.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:RemoteAppApiKey' }
$az = $c.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AzureStorage' }
"remote authentication key set: " + [bool]$key
$sc = $c.configuration.appSettings.add | Where-Object { $_.key -eq 'ticketdesk:SharedCookieKeyRing' }
"shared sign-in cookie key ring: " + $(if ($sc) { $sc.value } else { 'none (machine key)' })
"AzureStorage: " + $(if ($az -and $az.connectionString -match '(?i)BlobEndpoint=([^;]+)') { $Matches[1] } elseif ($az) { 'set' } else { 'none (App_Data\filestore)' })
"httpRuntime targetFramework: " + $c.configuration.'system.web'.httpRuntime.targetFramework
"deployed package sha256: " + $(if (Test-Path C:\TdBuild\publish.sha256) { (Get-Content C:\TdBuild\publish.sha256 -Raw).Trim() } else { '?' })
PS
}

case "${1:-}" in
  build) shift; cmd_build "$@" ;;
  deploy) shift; cmd_deploy "$@" ;;
  info) cmd_info ;;
  *) sed -n '2,17p' "$0"; exit 2 ;;
esac
