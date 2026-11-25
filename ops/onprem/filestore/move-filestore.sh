#!/usr/bin/env bash
# move-filestore.sh - copy the lab VM's disk attachments into the lab blob store with ops/windows/Move-TdFileStore.ps1
# (idempotent, counts + MD5 before and after) and fetch its CSV manifest.
#
#   TD_LOCK_OWNER=<you> ops/onprem/filestore/move-filestore.sh MANIFEST_OUT.csv
#
# The connection string (TD_AZURE_STORAGE_CONNECTION, VM view) comes from $TD_W/.secrets/legacy-filestore.env and goes to
# the VM over stdin. Run ops/onprem/filestore/check-attachments.py --manifest MANIFEST_OUT.csv once the app uses the store.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
V="$REPO/ops/onprem/vm/vmctl.sh"
OUT="${1:?manifest output path}"
"$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "VM lock not held by TD_LOCK_OWNER" >&2; exit 1; }
conn="$(sed -n 's/^TD_AZURE_STORAGE_CONNECTION=//p' "$TD_W/.secrets/legacy-filestore.env")"
"$V" ssh 'New-Item -ItemType Directory -Force C:\TdOps\windows, C:\TdBackups | Out-Null' < /dev/null
"$V" scp "$REPO/ops/windows/Move-TdFileStore.ps1" "$REPO/ops/windows/TdOps.psm1" "vm:C:/TdOps/windows/" < /dev/null
printf '%s\n' "\$env:TD_AZURE_STORAGE_CONNECTION = '$conn'" \
  "try { & 'C:\\TdOps\\windows\\Move-TdFileStore.ps1' -ManifestPath C:\\TdBackups\\filestore-move.csv; exit \$LASTEXITCODE } catch { Write-Host (\$_ | Out-String); exit 1 }" \
  | "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -'
"$V" scp "vm:C:/TdBackups/filestore-move.csv" "$OUT" < /dev/null
echo "manifest: $OUT ($(($(wc -l < "$OUT") - 1)) files)"
