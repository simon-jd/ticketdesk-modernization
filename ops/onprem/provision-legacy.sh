#!/usr/bin/env bash
# provision-legacy.sh - drive ops/windows/*.ps1 on the lab VM over ssh, from lab-host.
#
#   provision-legacy.sh STEP [STEP...]     steps run in the order given; "all" = the full sequence
#
#   copy-ops     copy ops/windows to C:\TdOps\windows and the lab PFX to C:\TdMedia
#   prereqs      Install-TdPrerequisites.ps1   (IIS + ASP.NET 4.x)
#   sql          Install-TdSqlExpress.ps1      (SQL Server 2022 Express, TCP 1433, mixed auth)
#   sql-fulltext Install-TdSqlExpress.ps1 -FullText (adds Full-Text Search from the Express Advanced media; round 3 search)
#   site         Set-TdIisSite.ps1             (app pool, site, HTTPS with the lab cert, DB + login)
#   buildtools   Install-TdBuildTools.ps1      (VS 2022 Build Tools, 4.8 targeting pack, nuget.exe)
#   source       git archive of the repo HEAD -> C:\TdSrc\ticketdesk
#   build        Build-TdLegacy.ps1            (nuget restore, msbuild, file-system publish)
#   deploy       Deploy-TdApp.ps1              (mirror into the site, host config in Web.config)
#   backup-task  Register-TdBackupTask.ps1     (nightly backup at 02:00)
#
# The same .ps1 files run unchanged on any Windows Server 2025 host (e.g. the Azure VM in round 1);
# only this driver knows about the lab VM. Secrets are read from $TD_W/.secrets and sent on stdin.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
VMCTL="$HERE/vm/vmctl.sh"
SECRETS="$TD_W/.secrets"
OPS='C:\TdOps\windows'

vm() { "$VMCTL" ssh "$@"; }
ps_file() {  # ps_file Script.ps1 [args...]  (no secrets)
  local s="$1"; shift
  vm "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $OPS\\$s $*"
}
ps_secret() {  # ps_secret 'ENVNAME=value' ... -- Script.ps1 [args...]   (env vars go over stdin)
  local lines=() s
  while [[ "$1" != "--" ]]; do lines+=("\$env:${1%%=*} = '${1#*=}'"); shift; done; shift
  s="$1"; shift
  lines+=("try { & '$OPS\\$s' $*; if (-not \$?) { exit 1 } } catch { Write-Host (\$_ | Out-String); exit 1 }" "exit 0")
  printf '%s\n' "${lines[@]}" | vm 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -'
}
envval() { [[ -f "$1" ]] || return 0; sed -n "s/^$2=//p" "$1" | head -1; }

step() {
  echo "==== $(date -u +%FT%TZ) step $1"
  case "$1" in
    copy-ops)
      vm 'New-Item -ItemType Directory -Force C:\TdOps\windows, C:\TdMedia | Out-Null'
      "$VMCTL" scp "$REPO"/ops/windows/*.ps1 "$REPO"/ops/windows/*.psm1 "vm:C:/TdOps/windows/"
      "$VMCTL" scp "$SECRETS/legacy-lab-ca/server.pfx" "vm:C:/TdMedia/tdlegacy-lab.pfx"
      vm 'Get-ChildItem C:\TdOps\windows | Select-Object Name, Length | Format-Table -AutoSize | Out-String -Width 100' ;;
    prereqs)    ps_file Install-TdPrerequisites.ps1 ;;
    sql)        ps_secret "TD_SQL_SA_PASSWORD=$(envval "$SECRETS/legacy-lab.env" TD_SQL_SA_PASSWORD)" -- Install-TdSqlExpress.ps1 -TcpPort 1433 ;;
    sql-fulltext) ps_secret "TD_SQL_SA_PASSWORD=$(envval "$SECRETS/legacy-lab.env" TD_SQL_SA_PASSWORD)" -- Install-TdSqlExpress.ps1 -TcpPort 1433 -FullText ;;
    site)       ps_secret "TD_CERT_PFX_PASSWORD=$(envval "$SECRETS/legacy-lab-cert.env" TD_CERT_PFX_PASSWORD)" -- \
                  Set-TdIisSite.ps1 -CertificatePfxPath 'C:\TdMedia\tdlegacy-lab.pfx' ;;
    buildtools) ps_file Install-TdBuildTools.ps1 ;;
    source)
      local rev tarball; rev="$(git -C "$REPO" rev-parse --short HEAD)"
      tarball="$(mktemp --suffix=.tar)"
      git -C "$REPO" archive --format=tar -o "$tarball" HEAD
      vm 'if (Test-Path C:\TdSrc\ticketdesk) { Remove-Item C:\TdSrc\ticketdesk -Recurse -Force }; New-Item -ItemType Directory -Force C:\TdSrc\ticketdesk | Out-Null'
      "$VMCTL" scp "$tarball" "vm:C:/TdSrc/ticketdesk.tar"
      rm -f "$tarball"
      vm "tar -xf C:\\TdSrc\\ticketdesk.tar -C C:\\TdSrc\\ticketdesk; Remove-Item C:\\TdSrc\\ticketdesk.tar; Set-Content C:\\TdSrc\\ticketdesk\\REVISION '$rev'; 'source at revision $rev'" ;;
    build)
      local srcrev; srcrev="$(vm 'Get-Content C:\TdSrc\ticketdesk\REVISION' | tr -d '\r\n')"
      ps_file Build-TdLegacy.ps1 -SourcePath 'C:\TdSrc\ticketdesk' -PublishPath 'C:\TdBuild\publish' -SourceRevision "$srcrev" ;;
    deploy)     # deploy secrets (optional files): remote-auth API key, Azure Blob/Azurite connection for attachments
                ps_secret "TD_REMOTEAPP_API_KEY=$(envval "$SECRETS/legacy-remoteapp.env" TD_REMOTEAPP_API_KEY 2>/dev/null)" \
                  "TD_AZURE_STORAGE_CONNECTION=$(envval "$SECRETS/legacy-filestore.env" TD_AZURE_STORAGE_CONNECTION 2>/dev/null)" -- \
                  Deploy-TdApp.ps1 -PublishPath 'C:\TdBuild\publish' ;;
    backup-task) ps_file Register-TdBackupTask.ps1 ;;
    all) for s in copy-ops prereqs sql site buildtools source build deploy backup-task; do step "$s"; done ;;
    *) sed -n '2,20p' "$0"; exit 2 ;;
  esac
}
[[ $# -gt 0 ]] || { sed -n '2,20p' "$0"; exit 2; }
for s in "$@"; do step "$s"; done
echo "==== $(date -u +%FT%TZ) done: $*"
