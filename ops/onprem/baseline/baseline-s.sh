#!/usr/bin/env bash
# baseline-s.sh - the fixed baseline data set: legacy TicketDesk on seeded scale S (10k tickets, seed 42).
# Every recording and measurement of the legacy app starts from this state.
#
# Since round 2 (legacy-adapters) snapshot baseline-s is the SHARED state: the net48 build with the System.Web adapters
# ($TD_BASELINE_PACKAGE, remote-auth key from .secrets) on the expanded schema (td-db migrate: Tickets.AssignedDate + trigger,
# indexes, procedures), and, when $EXPORTS/baseline-s.state says FILESTORE=blob, attachments in the lab blob store
# (Azurite, ops/onprem/filestore/azurite.sh). The round-1 state (4.6.2 build, pure EF6 schema) is snapshot baseline-s-legacy.
#
#   baseline-s.sh build-db        restore the clean legacy backup into td-sql as TicketDesk, seed S (seed 42), back it up
#                                 to $TD_W/vm/exports/ticketdesk-baseline-s.bak (+ .manifest.json)     [no VM]
#   baseline-s.sh install-legacy  VM: reset to installed-clean, restore that .bak with Restore-TdSite.ps1, re-run this
#                                 checkout's deploy step on the round-1 build (S9), rebuild the Lucene index, check, stop,
#                                 save snapshot baseline-s-legacy, start                                [needs the VM lock]
#   baseline-s.sh install         VM: reset to installed-clean, reset the lab blob store, deploy $TD_BASELINE_PACKAGE (key +
#                                 blob store), restore that .bak, td-db migrate, restart the pool, rebuild the index, export
#                                 ticketdesk-baseline-s-shared.bak (+ manifest), save snapshot baseline-s, start   [needs the lock]
#   baseline-s.sh upgrade-shared  VM: the same from snapshot baseline-s-legacy instead of installed-clean      [needs the lock]
#                                 (TD_BASELINE_FILESTORE=disk for either: attachments on the VM disk; default blob)
#   baseline-s.sh reset           VM: stop, revert to snapshot $TD_BASELINE_SNAPSHOT (default baseline-s-search since round 3: shared cookie + full-text; earlier states: baseline-s-identity, baseline-s, baseline-s-legacy), start, wait for the
#                                 app; for baseline-s with FILESTORE=blob also reset Azurite                [needs the VM lock]
#   baseline-s.sh shared-cookie   VM: from snapshot baseline-s, deploy $TD_BASELINE_PACKAGE with the sign-in cookie shared with
#                                 the new app (TD_SHARED_COOKIE_KEYRING=database), td-db migrate (V0010 key ring table),
#                                 restart the pool, sign in once (creates the first key), save snapshot baseline-s-identity
#                                 (round 2b identity; baseline-s itself is not changed)                    [needs the VM lock]
#   baseline-s.sh fulltext        VM: from snapshot baseline-s-identity, add the Full-Text Search feature to SQL Express
#                                 (provision-legacy.sh sql-fulltext), td-db migrate (V0011 full-text catalog + indexes),
#                                 wait for the population, save snapshot baseline-s-search (round 3 search; earlier
#                                 snapshots unchanged)                                                     [needs the VM lock]
#   baseline-s.sh save-as NAME    VM: stop, save the current state as snapshot NAME (not baseline-s/-legacy), start, wait
#                                 for the app (a derived state, e.g. baseline-s-identity)                  [needs the VM lock]
#   baseline-s.sh wait-app        wait until https://localhost:8443/ serves the home page and sign-in works
#
# VM-changing commands refuse to run unless TD_LOCK_OWNER holds plan/locks/vm.lock (ops/onprem/vm/vmlock.sh).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
V="$REPO/ops/onprem/vm/vmctl.sh"
LOCK="$REPO/ops/onprem/vm/vmlock.sh"
TDSQL="$REPO/ops/dev/td-sql.sh"
SMOKE=(python3 -I "$REPO/ops/onprem/smoke/legacy_smoke.py")
EXPORTS="$TD_W/vm/exports"
BAK="$EXPORTS/ticketdesk-baseline-s.bak"
BAK_SHARED="$EXPORTS/ticketdesk-baseline-s-shared.bak"
STATE="$EXPORTS/baseline-s.state"
SNAP="${TD_BASELINE_SNAPSHOT:-baseline-s-search}"
SNAP_LEGACY=baseline-s-legacy
PACKAGE="${TD_BASELINE_PACKAGE:-$TD_W/vm/artifacts/legacy-adapters/td-legacy-publish-b4edcc3.zip}"
DISK="$TD_W/vm/ws2025-legacy.qcow2"
SEED="${TD_BASELINE_SEED:-42}"
OWNER="${TD_LOCK_OWNER:-}"

log() { echo "$(date -u +%FT%TZ) $*"; }
vm_sql() {  # query the VM's SQL Express through the 127.0.0.1:11433 forward (sa password from legacy-lab.env, never echoed)
  SQLCMDPASSWORD="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")" \
    "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d TicketDesk -h -1 -W -Q "$1"
}
need_lock() {
  [[ -n "$OWNER" ]] || { echo "set TD_LOCK_OWNER to the lock holder's name" >&2; exit 1; }
  "$LOCK" check "$OWNER" || { echo "VM lock not held by $OWNER ($("$LOCK" status)); take it first: $LOCK acquire $OWNER PURPOSE MINUTES" >&2; exit 1; }
}

cmd_build_db() {
  . "$TD_W/.tools/env.sh"
  local clean="$EXPORTS/ticketdesk-clean.bak" t0=$SECONDS
  log "restore $clean into td-sql as TicketDesk"
  "$TDSQL" restore "$clean" TicketDesk | tail -1
  log "seed scale S, seed $SEED"
  TD_SEED_DB=TicketDesk "$REPO/ops/dev/seed.sh" verify | tail -1
  TD_SEED_DB=TicketDesk "$REPO/ops/dev/seed.sh" run --scale S --seed "$SEED" --json "$EXPORTS/ticketdesk-baseline-s.seed.json" | tail -2
  log "back up"
  "$TDSQL" sql -Q "BACKUP DATABASE [TicketDesk] TO DISK = N'/var/opt/mssql/backup/ticketdesk-baseline-s.bak' WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, NAME = N'TicketDesk baseline-s (scale S, seed $SEED)'" | tail -1
  docker cp td-sql:/var/opt/mssql/backup/ticketdesk-baseline-s.bak "$BAK"
  local counts
  counts="$("$TDSQL" sql -d TicketDesk -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT s.name + '.' + t.name, SUM(p.rows) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1" \
    | awk -F'|' 'NF==2 {printf "%s    \"%s\": %s", (n++ ? ",\n" : ""), $1, $2}')"
  cat > "$EXPORTS/ticketdesk-baseline-s.manifest.json" <<JSON
{
  "created_utc": "$(date -u +%FT%TZ)",
  "source": "ticketdesk-clean.bak restored into td-sql ($("$TDSQL" status 2>/dev/null | sed -n 2p)), td-seed scale S seed $SEED",
  "git": "$(git -C "$REPO" rev-parse --short HEAD)",
  "file": "$(basename "$BAK")",
  "bytes": $(stat -c %s "$BAK"),
  "sha256": "$(sha256sum "$BAK" | cut -c1-64)",
  "rows": {
$counts
  }
}
JSON
  log "wrote $BAK ($(stat -c %s "$BAK") bytes) in $((SECONDS - t0)) s"
}

cmd_wait_app() {
  local limit="${1:-900}" t0=$SECONDS
  until "${SMOKE[@]}" wait --timeout 30 >/dev/null 2>&1; do
    (( SECONDS - t0 < limit )) || { echo "app not ready after ${limit}s" >&2; "${SMOKE[@]}" wait --timeout 30 | tail -5; return 1; }
    sleep 5
  done
  log "app ready ($((SECONDS - t0)) s)"
}

has_snapshot() { qemu-img snapshot -U -l "$DISK" | awk '{print $2}' | grep -qx "$1"; }
save_snapshot() {  # VM must be stopped
  if has_snapshot "$1"; then qemu-img snapshot -d "$1" "$DISK"; fi
  "$V" snapshot "$1" | tail -1
}

cmd_install_legacy() {
  need_lock
  local t0=$SECONDS
  [[ -f "$BAK" ]] || { echo "no $BAK; run build-db first" >&2; exit 1; }
  log "reset VM to installed-clean"
  "$V" stop
  "$V" reset installed-clean
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  log "copy and restore $(basename "$BAK") with Restore-TdSite.ps1 -SkipFiles"
  "$V" ssh 'New-Item -ItemType Directory -Force C:\TdBackups\baseline-s | Out-Null'
  "$V" scp "$BAK" "vm:C:/TdBackups/baseline-s/TicketDesk.bak"
  "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\TdOps\windows\Restore-TdSite.ps1 -BackupPath C:\TdBackups\baseline-s -SkipFiles'
  cmd_wait_app
  log "deploy step of this checkout on the round-1 build (customErrors RemoteOnly, S9; no adapters key, disk store)"
  "$V" scp "$REPO"/ops/windows/*.ps1 "$REPO"/ops/windows/*.psm1 "vm:C:/TdOps/windows/" < /dev/null
  "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\TdOps\windows\Deploy-TdApp.ps1 -PublishPath C:\TdBuild\publish' < /dev/null | tail -3
  cmd_wait_app
  log "rebuild the Lucene index (admin/search-manager)"
  "${SMOKE[@]}" reindex
  vm_sql "SET NOCOUNT ON; SELECT 'tickets=' + CAST(COUNT(*) AS varchar(12)) + ' users=' + CAST((SELECT COUNT(*) FROM dbo.IdentityUsers) AS varchar(12)) + ' version=' + CAST(SERVERPROPERTY('ProductVersion') AS varchar(20)) FROM dbo.Tickets"
  log "stop and save snapshot $SNAP_LEGACY"
  "$V" stop
  save_snapshot "$SNAP_LEGACY"
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  log "install-legacy done in $((SECONDS - t0)) s"
}

# ---- shared-state steps (used by install and upgrade-shared)
deploy_shared() {  # deploy_shared FILESTORE: lab blob store reset (blob) + package with the deploy secrets
  log "deploy $(basename "$PACKAGE") (file store: $1)"
  if [[ "$1" == blob ]]; then "$REPO/ops/onprem/filestore/azurite.sh" reset; else export TD_DEPLOY_FILESTORE=0; fi
  "$REPO/ops/onprem/legacy-artifact.sh" deploy "$PACKAGE" | grep -E 'file store|remote authentication|ApplicationInsights|deployed ' || true
}
migrate_shared() {  # td-db migrate on the VM database, then restart the pool so EF6 re-runs its startup check
  log "td-db migrate on the VM database"
  ( . "$TD_W/.tools/env.sh"
    dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
    TD_DB_CONN="Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env");TrustServerCertificate=True;Encrypt=True" \
      "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate | tail -1 )
  "$V" ssh 'Import-Module WebAdministration; Restart-WebAppPool TicketDesk; "app pool restarted"' < /dev/null
  cmd_wait_app
}
export_shared() {  # export_shared SOURCE-DESCRIPTION FILESTORE: ticketdesk-baseline-s-shared.bak + manifest + state file
  log "export $(basename "$BAK_SHARED")"
  # C:\TdBackups: the folder Backup-TdSite.ps1 / Restore-TdSite.ps1 use, so the SQL Server service can write there
  "$V" ssh 'New-Item -ItemType Directory -Force C:\TdBackups | Out-Null' < /dev/null
  vm_sql "BACKUP DATABASE [TicketDesk] TO DISK = N'C:\\TdBackups\\ticketdesk-baseline-s-shared.bak' WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, NAME = N'TicketDesk baseline-s shared (scale S seed $SEED, expanded schema)'" | tail -1
  "$V" scp "vm:C:/TdBackups/ticketdesk-baseline-s-shared.bak" "$BAK_SHARED" < /dev/null
  "$V" ssh 'Remove-Item C:\TdBackups\ticketdesk-baseline-s-shared.bak' < /dev/null
  local counts
  counts="$(vm_sql "SET NOCOUNT ON; SELECT s.name + '.' + t.name + '|' + CAST(SUM(p.rows) AS varchar(20)) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1" \
    | tr -d '\r' | awk -F'|' 'NF==2 {printf "%s    \"%s\": %s", (n++ ? ",\n" : ""), $1, $2}')"
  cat > "${BAK_SHARED%.bak}.manifest.json" <<JSON
{
  "created_utc": "$(date -u +%FT%TZ)",
  "source": "$1 + $(basename "$PACKAGE") + td-db migrate, backed up on the VM (SQL Server 2022 Express)",
  "git": "$(git -C "$REPO" rev-parse --short HEAD)",
  "package_sha256": "$(sha256sum "$PACKAGE" | cut -c1-64)",
  "file_store": "$2",
  "file": "$(basename "$BAK_SHARED")",
  "bytes": $(stat -c %s "$BAK_SHARED"),
  "sha256": "$(sha256sum "$BAK_SHARED" | cut -c1-64)",
  "rows": {
$counts
  }
}
JSON
  printf 'PACKAGE=%s\nFILESTORE=%s\nSAVED=%s\n' "$(basename "$PACKAGE")" "$2" "$(date -u +%FT%TZ)" > "$STATE"
}
save_shared() {  # stop, replace snapshot baseline-s, start (the blob store is already in its baseline state: empty)
  log "stop and save snapshot $SNAP"
  "$V" stop
  save_snapshot "$SNAP"
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
}

cmd_install() {  # shared baseline-s from installed-clean; baseline-s-legacy is not touched
  need_lock
  local t0=$SECONDS filestore="${TD_BASELINE_FILESTORE:-blob}"
  [[ -f "$BAK" ]] || { echo "no $BAK; run build-db first" >&2; exit 1; }
  [[ -f "$PACKAGE" ]] || { echo "no package $PACKAGE (ops/onprem/legacy-artifact.sh build)" >&2; exit 1; }
  log "reset VM to installed-clean"
  "$V" stop
  "$V" reset installed-clean
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  deploy_shared "$filestore"
  log "copy and restore $(basename "$BAK") with Restore-TdSite.ps1 -SkipFiles"
  "$V" ssh 'New-Item -ItemType Directory -Force C:\TdBackups\baseline-s | Out-Null' < /dev/null
  "$V" scp "$BAK" "vm:C:/TdBackups/baseline-s/TicketDesk.bak" < /dev/null
  "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\TdOps\windows\Restore-TdSite.ps1 -BackupPath C:\TdBackups\baseline-s -SkipFiles' < /dev/null
  cmd_wait_app
  migrate_shared
  log "rebuild the Lucene index (admin/search-manager)"
  "${SMOKE[@]}" reindex | tail -2
  vm_sql "SET NOCOUNT ON; SELECT 'tickets=' + CAST(COUNT(*) AS varchar(12)) + ' users=' + CAST((SELECT COUNT(*) FROM dbo.IdentityUsers) AS varchar(12)) + ' schema_scripts=' + CAST((SELECT COUNT(*) FROM dbo.__ModernSchemaHistory) AS varchar(12)) + ' version=' + CAST(SERVERPROPERTY('ProductVersion') AS varchar(20)) FROM dbo.Tickets"
  export_shared "installed-clean + $(basename "$BAK") restored" "$filestore"
  [[ "$filestore" == blob ]] && "$REPO/ops/onprem/filestore/azurite.sh" reset | tail -1   # reindex may not write files, but be sure
  save_shared
  log "install done in $((SECONDS - t0)) s"
}

cmd_upgrade_shared() {  # shared baseline-s from snapshot baseline-s-legacy (created from the current baseline-s if missing)
  need_lock
  local t0=$SECONDS filestore="${TD_BASELINE_FILESTORE:-blob}"
  [[ -f "$PACKAGE" ]] || { echo "no package $PACKAGE (ops/onprem/legacy-artifact.sh build)" >&2; exit 1; }
  "$V" stop
  if ! has_snapshot "$SNAP_LEGACY"; then
    log "no $SNAP_LEGACY yet: the current $SNAP (round-1 state) becomes $SNAP_LEGACY"
    "$V" reset "$SNAP"; "$V" snapshot "$SNAP_LEGACY" | tail -1
  fi
  "$V" reset "$SNAP_LEGACY"
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  deploy_shared "$filestore"
  migrate_shared
  export_shared "VM snapshot $SNAP_LEGACY ($(basename "$BAK") restored)" "$filestore"
  save_shared
  log "upgrade-shared done in $((SECONDS - t0)) s"
}

cmd_reset() {
  need_lock
  local t0=$SECONDS
  "$V" stop
  "$V" reset "$SNAP"
  # baseline-s and the states derived from it (baseline-s-*) keep attachments in the lab blob store; baseline-s-legacy does not
  if [[ "$SNAP" == baseline-s* && "$SNAP" != "$SNAP_LEGACY" && -f "$STATE" ]] && grep -qx 'FILESTORE=blob' "$STATE"; then
    "$REPO/ops/onprem/filestore/azurite.sh" reset | tail -1
  fi
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  log "reset to $SNAP done in $((SECONDS - t0)) s"
}

cmd_save_as() {
  need_lock
  local name="${1:?snapshot name}" t0=$SECONDS
  [[ "$name" != baseline-s && "$name" != "$SNAP_LEGACY" && "$name" != installed-clean ]] || { echo "save-as does not replace $name" >&2; exit 1; }
  log "stop and save snapshot $name"
  "$V" stop
  save_snapshot "$name"
  "$V" start
  "$V" wait-ssh 900
  cmd_wait_app
  log "save-as $name done in $((SECONDS - t0)) s"
}

cmd_shared_cookie() {
  need_lock
  local t0=$SECONDS name="${TD_SHARED_COOKIE_SNAPSHOT:-baseline-s-identity}"
  [[ -f "$PACKAGE" ]] || { echo "no package $PACKAGE (ops/onprem/legacy-artifact.sh build)" >&2; exit 1; }
  SNAP=baseline-s cmd_reset
  log "deploy $(basename "$PACKAGE") with the shared sign-in cookie"
  TD_SHARED_COOKIE_KEYRING=database "$REPO/ops/onprem/legacy-artifact.sh" deploy "$PACKAGE" | grep -E 'shared sign-in|file store|remote authentication|deployed ' || true
  migrate_shared   # V0010 creates dbo.DataProtectionKeys; restarts the pool; wait-app signs in (the legacy app writes the first key)
  vm_sql "SET NOCOUNT ON; SELECT 'keys=' + CAST(COUNT(*) AS varchar(12)) FROM dbo.DataProtectionKeys"
  printf 'PACKAGE=%s\nSHARED_COOKIE=database\nSAVED=%s\n' "$(basename "$PACKAGE")" "$(date -u +%FT%TZ)" > "$EXPORTS/$name.state"
  cmd_save_as "$name"
  log "shared-cookie ($name) done in $((SECONDS - t0)) s"
}

cmd_fulltext() {
  need_lock
  local t0=$SECONDS name="${TD_FULLTEXT_SNAPSHOT:-baseline-s-search}"
  SNAP="${TD_FULLTEXT_FROM:-baseline-s-identity}" cmd_reset
  log "add the Full-Text Search feature to SQL Express"
  "$REPO/ops/onprem/provision-legacy.sh" copy-ops sql-fulltext < /dev/null | grep -E 'Full-Text|FullText|full-text|SQL Server 2022|==== ' || true
  migrate_shared   # V0011: full-text catalog and indexes (expand-only), restarts the pool, waits for the app
  ( . "$TD_W/.tools/env.sh"
    TD_DB_CONN="Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env");TrustServerCertificate=True;Encrypt=True" \
      "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" fulltext --wait 900 )
  printf 'FROM=%s\nFULLTEXT=V0011\nSAVED=%s\n' "${TD_FULLTEXT_FROM:-baseline-s-identity}" "$(date -u +%FT%TZ)" > "$EXPORTS/$name.state"
  cmd_save_as "$name"
  log "fulltext ($name) done in $((SECONDS - t0)) s"
}

case "${1:-}" in
  build-db) cmd_build_db ;;
  fulltext) cmd_fulltext ;;
  shared-cookie) cmd_shared_cookie ;;
  save-as) shift; cmd_save_as "$@" ;;
  install-legacy) cmd_install_legacy ;;
  upgrade-shared) cmd_upgrade_shared ;;
  install) cmd_install ;;
  reset) cmd_reset ;;
  wait-app) shift; cmd_wait_app "$@" ;;
  *) sed -n '2,26p' "$0"; exit 2 ;;
esac
