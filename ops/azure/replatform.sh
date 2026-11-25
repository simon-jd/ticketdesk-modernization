#!/usr/bin/env bash
# replatform.sh - Azure SQL re-platform of the legacy app on the Azure VM (stage replatform), driven from lab-host.
# The legacy app moves from SQL Express on the VM to the Azure SQL free-offer database behind its private endpoint,
# Entra-only, with the VM's managed identity (EF6 interceptor). Uses ops/azure/lift.sh's helpers (sourced).
#
#   replatform.sh tools                 td-sqltools-<rev>.zip: td-db (portable) + SqlPackage 170.5.96 (net10.0)
#   replatform.sh bacpac                the shared baseline .bak -> BACPAC on td-sql (lab-host, no Azure), for rebuild:
#                                       restore, schemas of Windows users to dbo, drop them, SqlPackage export
#   replatform.sh stage [--seed] [--bacpac FILE]
#                                       ops/windows bundle + publish package + sql tools to `artifacts`; --seed: the
#                                       shared baseline .bak (backups/seed/baseline-s-shared/); --bacpac: backups/bacpac/
#   replatform.sh provision STEPS [Name=Value...]
#                                       Invoke-TdAzureProvision.ps1 through Run Command with the Azure SQL parameters
#   replatform.sh smoke [TAG]           legacy smoke test (sign in, create a ticket, comment) against https://<vm fqdn>/
#   replatform.sh files-reset           empty the attachments container (fresh state for the golden replay)
#   replatform.sh db-reset              replace the free-offer database with an empty one (terraform -replace)
#   replatform.sh fresh express|azure TAG
#                                       the shared baseline on SQL Express (azure: + export, import, contained user,
#                                       td-db migrate) with the app on that database, attachments emptied, index rebuilt
#   replatform.sh golden TAG            golden replay against https://<vm fqdn>/ + compare (run on a fresh state)
#   replatform.sh latency TAG           the k6 mix (tests/perf/ticketdesk-mix.js, short) against https://<vm fqdn>/
#   replatform.sh cutover [--rehearsal] SQL Express -> Azure SQL: probe (reads + writes, 1/s) from lab-host while one
#                                       Run Command does freeze, export, import, contained user, reconcile, switch
#   replatform.sh plan-tag              usp_TicketList's tag filter on Azure SQL: actual plans, forced plans (none)
#   replatform.sh rotate-cert           new certificate version in Key Vault -> IIS binding through the identity, timed
#   replatform.sh rebuild               from nothing: apply + stage (BACPAC) + provision without SQL Express + smoke
#   replatform.sh down                  back to stage none (Entra app only), timed
#   replatform.sh session [--after-rebuild | --rebuild-rotate]
#                                       one unattended cycle (run it in tmux): rebuild from zero, add SQL Express, latency
#                                       on SQL Express, cutover rehearsal + measured cutover, td-db migrate + plan-tag,
#                                       golden + latency on Azure SQL, certificate rotation; destroys at the end whatever
#                                       happened (trap), so a stopped operator session cannot leave it running
#
# Evidence: $TD_EVIDENCE (default docs/evidence/azure-sql-<UTC date>/). Secrets stay in Key Vault and $TD_W/.secrets.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
export TD_EVIDENCE="${TD_EVIDENCE:-$REPO/docs/evidence/azure-sql-$(date -u +%Y%m%d)}"
export TD_LIFT_ARTIFACTS="${TD_LIFT_ARTIFACTS:-$TD_W/vm/artifacts/azure-sql}"
# shellcheck source=ops/azure/lift.sh
. "$HERE/lift.sh"
AZ_OWNER_TAG="agent-azure-sql"
SEED_NAME="baseline-s-shared"
SEED_BAK="$EXPORTS/ticketdesk-baseline-s-shared.bak"
SQLPACKAGE_VERSION="170.5.96"
SQLPACKAGE_DIR="$TD_W/.tools/sqlpackage/.store/microsoft.sqlpackage/$SQLPACKAGE_VERSION/microsoft.sqlpackage/$SQLPACKAGE_VERSION/tools/net10.0/any"
DOTNET_VERSION="$(ls "$TD_W/.tools/dotnet/shared/Microsoft.NETCore.App" | sort -V | tail -1)"
GOLDEN="$REPO/tests/characterization/golden/legacy-golden.jsonl.xz"
PROBE_TICKET="${TD_PROBE_TICKET:-1}"

rp_outputs() {
  load_outputs
  local j; j="$(terraform -chdir="$TF_DIR" output -json)"
  eval "$(python3 -I -c '
import json, shlex, sys
o = json.load(sys.stdin)
for k, n in (("sql_server_fqdn", "SQLSRV"), ("sql_database", "SQLDB"), ("sql_deploy_identity_client_id", "DEPLOY_ID"), ("files_account", "FILES")):
    v = (o.get(k) or {}).get("value") or ""
    print(n + "=" + shlex.quote(str(v)))' <<<"$j")"
  [[ -n "$SQLSRV" && -n "$DEPLOY_ID" && -n "$FILES" ]] || die "replatform outputs missing (is the replatform stage applied?)"
}
ttl_hours() {  # whole hours until the resource group's current ttl (at least 1), so a re-apply keeps the ttl
  local ttl; ttl="$(az group show -n "$RG" --query tags.ttl -o tsv 2>/dev/null || true)"
  [[ -n "$ttl" ]] || { echo "${TTL_HOURS:-3}"; return; }
  python3 -I -c 'import sys, math, datetime as d
t = d.datetime.strptime(sys.argv[1], "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=d.timezone.utc)
print(max(1, math.ceil((t - d.datetime.now(d.timezone.utc)).total_seconds() / 3600)))' "$ttl"
}
marks_from_log() {  # TDMARK lines of a provisioning transcript -> "name time" lines
  grep -ao 'TDMARK [a-z_]* [0-9TZ:.-]*' "$1" | awk '{print $2, $3}' || true
}

# ---------------------------------------------------------------------------------------------------
rp_tools() {
  local rev tmp out
  rev="$(git -C "$REPO" rev-parse --short HEAD)"
  [[ -z "$(git -C "$REPO" status --porcelain -- src/modern/TicketDesk.Database src/modern/Directory.Packages.props)" ]] || rev="$rev-dirty"
  out="$ART/td-sqltools-$rev.zip"; tmp="$(mktemp -d)"
  dotnet publish "$REPO/src/modern/TicketDesk.Database" -c Release -o "$tmp/td-db" -v quiet -nologo >/dev/null
  cp -r "$SQLPACKAGE_DIR" "$tmp/sqlpackage"
  python3 -I - "$tmp" "$out" <<'PY'
import os, sys, zipfile
src, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for root, dirs, files in os.walk(src):
        dirs.sort()
        for f in sorted(files):
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, src))
PY
  rm -rf "$tmp"
  ( cd "$ART" && sha256sum "$(basename "$out")" > "$(basename "$out").sha256" )
  log "sql tools $(basename "$out"): $(stat -c %s "$out") bytes, td-db + SqlPackage $SQLPACKAGE_VERSION, .NET runtime on the VM $DOTNET_VERSION"
}

BACPAC_FILE="$EXPORTS/azure-sql/ticketdesk-baseline-s-shared.bacpac"
rp_bacpac() {
  local db="TicketDeskBacpac" ev="$EVID/bacpac-local" work t0=$SECONDS
  mkdir -p "$ev" "$(dirname "$BACPAC_FILE")"; work="$(mktemp -d)"; chmod 700 "$work"
  "$REPO/ops/dev/td-sql.sh" restore "$SEED_BAK" "$db" > "$ev/restore.txt" 2>&1
  "$REPO/ops/dev/td-sql.sh" sql -d "$db" -b -h -1 -W -Q "SET NOCOUNT ON;
DECLARE @s nvarchar(max) = N'', @n nvarchar(max) = N'';
SELECT @s += N'ALTER AUTHORIZATION ON SCHEMA::' + QUOTENAME(sc.name) + N' TO dbo;' FROM sys.schemas AS sc JOIN sys.database_principals AS p ON p.principal_id = sc.principal_id WHERE p.type IN ('U', 'G') AND p.principal_id > 4;
SELECT @s += N'DROP USER ' + QUOTENAME(name) + N';', @n += name + N' ' FROM sys.database_principals WHERE type IN ('U', 'G') AND principal_id > 4;
EXEC (@s); SELECT N'dropped Windows users: ' + @n;" > "$ev/strip-users.txt" 2>&1
  local pw; pw="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)"
  [[ -n "$pw" ]] || die "no MSSQL_SA_PASSWORD in $TD_W/.secrets/td-sql.env"
  # 0600 response file, one argument per line (a connection string with spaces would be split)
  printf '%s\n' "/Action:Export" "/SourceServerName:127.0.0.1,14333" "/SourceDatabaseName:$db" "/SourceUser:sa" "/SourcePassword:$pw" \
    "/SourceTrustServerCertificate:True" "/TargetFile:$BACPAC_FILE" "/p:VerifyFullTextDocumentTypesSupported=false" > "$work/export.rsp"
  rm -f "$BACPAC_FILE"
  sqlpackage "@$work/export.rsp" 2>&1 | sed "s/$pw/***/g" > "$ev/export.txt" || true
  rm -rf "$work"
  "$REPO/ops/dev/td-sql.sh" sql -b -Q "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db];" >> "$ev/restore.txt" 2>&1
  [[ -s "$BACPAC_FILE" ]] || die "no BACPAC written (see $ev/export.txt)"
  { echo "# $(now) $(basename "$SEED_BAK") (sha256 $(sha256sum "$SEED_BAK" | cut -c1-16)) -> $(basename "$BACPAC_FILE") on td-sql, SqlPackage $SQLPACKAGE_VERSION"
    echo "bytes: $(stat -c %s "$BACPAC_FILE")  sha256: $(sha256sum "$BACPAC_FILE" | cut -c1-64)  seconds: $((SECONDS - t0))"; cat "$ev/strip-users.txt"; } | tee "$ev/bacpac.txt"
}

rp_stage() {
  local seed=0 bacpac="" tools t0=$SECONDS
  while [[ $# -gt 0 ]]; do case "$1" in --seed) seed=1;; --bacpac) bacpac="$2"; shift;; *) die "stage: unknown $1";; esac; shift; done
  rp_outputs
  cmd_stage   # lift.sh: scripts bundle + newest publish package in $ART -> artifacts, staged.env
  tools="$(ls -t "$ART"/td-sqltools-*.zip 2>/dev/null | head -1 || true)"
  [[ -n "$tools" ]] || die "no sql tools bundle (replatform.sh tools)"
  blob_up artifacts "$(basename "$tools")" "$tools"
  printf 'SQLTOOLS_BLOB=%s\nSQLTOOLS_SHA=%s\n' "$(basename "$tools")" "$(cut -c1-64 "$tools.sha256")" >> "$ART/staged.env"
  if ((seed)); then blob_up backups "seed/$SEED_NAME/TicketDesk.bak" "$SEED_BAK"; log "staged seed $SEED_NAME ($(stat -c %s "$SEED_BAK") B)"; fi
  if [[ -n "$bacpac" ]]; then
    blob_up backups "bacpac/$(basename "$bacpac")" "$bacpac"
    printf 'BACPAC_BLOB=%s\nBACPAC_SHA=%s\n' "bacpac/$(basename "$bacpac")" "$(sha256sum "$bacpac" | cut -c1-64)" >> "$ART/staged.env"
    log "staged BACPAC $(basename "$bacpac") ($(stat -c %s "$bacpac") B)"
  fi
  log "stage done in $((SECONDS - t0)) s"; cat "$ART/staged.env"
}

rp_provision() {  # rp_provision STEPS [Name=Value...]: lift.sh cmd_provision + the Azure SQL parameters
  rp_outputs
  # shellcheck disable=SC1091
  . "$ART/staged.env"
  local steps="$1"; shift
  # Scripts on the VM come from the staged bundle: fetch first, always (0.5 s when nothing changed).
  [[ "$steps" == fetch* ]] || steps="fetch+$steps"
  cmd_provision "$steps" "SqlServer=$SQLSRV" "SqlDatabase=$SQLDB" "DeployClientId=$DEPLOY_ID" "AppIdentityName=$VM" \
    "SqlToolsBlob=${SQLTOOLS_BLOB:-}" "SqlToolsSha256=${SQLTOOLS_SHA:-}" "DotnetVersion=$DOTNET_VERSION" "$@"
}

rp_files_reset() {
  rp_outputs
  local n i
  for i in $(seq 1 20); do
    if az storage blob delete-batch --auth-mode login --account-name "$FILES" -s ticketdesk-attachments --only-show-errors -o none 2>/dev/null; then break; fi
    log "attachments container not writable yet (role propagation), retry $i in 15 s"; sleep 15
  done
  n="$(az storage blob list --auth-mode login --account-name "$FILES" -c ticketdesk-attachments --query 'length(@)' -o tsv)"
  log "attachments container ticketdesk-attachments on $FILES: $n blobs"
  [[ "$n" == 0 ]]
}

rp_db_reset() {
  local t0=$SECONDS out="$EVID/db-reset-$(date -u +%Y%m%dT%H%M%SZ).txt"
  make -C "$REPO" --no-print-directory azure-up-replatform AZ_OWNER="$AZ_OWNER_TAG" TTL_HOURS="$(ttl_hours)" \
    AZ_EXTRA_ARGS='-replace=module.sql[0].azapi_resource.database' > "$out" 2>&1 || { tail -20 "$out"; die "db-reset failed"; }
  log "free-offer database replaced in $((SECONDS - t0)) s ($(grep -ao 'Apply complete![^.]*' "$out" | tail -1))"
  "$REPO/scripts/azure_ttl_sync.sh" >> "$out" 2>&1 || true   # the re-apply may move the ttl: Azure-created children follow
}

rp_reindex() {  # rebuild the legacy Lucene index (a restored database comes without it)
  rp_outputs
  "${SMOKE[@]}" reindex --base "https://$FQDN" --json "$1.json" > "$1.txt" 2>&1 || { tail -5 "$1.txt"; return 1; }
  tail -1 "$1.txt"
}

rp_fresh() {
  local db="${1:?express|azure}" tag="${2:?tag}" ev rc=0
  ev="$EVID/fresh-$tag"; mkdir -p "$ev"
  if [[ "$db" == express ]]; then
    rp_provision "restore+deploy+verify" "RestorePrefix=seed" "RestoreName=$SEED_NAME" "Database=express" > "$ev/provision.txt" 2>&1 || rc=$?
  else
    rp_db_reset > "$ev/db-reset.txt" 2>&1 || rc=$?
    # shellcheck disable=SC1091
    . "$ART/staged.env"
    if [[ -n "${BACPAC_BLOB:-}" ]]; then   # the staged baseline BACPAC (replatform.sh bacpac / stage --bacpac)
      ((rc)) || rp_provision "import+azsql-users+migrate+deploy+verify" "Database=azure" "BacpacBlob=$BACPAC_BLOB" "BacpacSha256=$BACPAC_SHA" \
        > "$ev/provision.txt" 2>&1 || rc=$?
    else                                   # through SQL Express: restore the seed, export, import
      ((rc)) || rp_provision "restore+export+import+azsql-users+migrate+deploy+verify" "RestorePrefix=seed" "RestoreName=$SEED_NAME" \
        "Database=azure" > "$ev/provision.txt" 2>&1 || rc=$?
    fi
  fi
  ((rc == 0)) || { tail -30 "$ev/provision.txt"; die "fresh $db failed"; }
  rp_files_reset > "$ev/files-reset.txt" 2>&1 || { cat "$ev/files-reset.txt"; die "attachments reset failed"; }
  rp_reindex "$ev/reindex"
  grep -h 'TDVERIFY' "$ev/provision.txt" | tail -1 | cut -c1-400
  log "fresh $db ($tag) ready"
}

rp_golden() {
  rp_outputs
  local tag="${1:?tag}" ev bin base
  ev="$EVID/golden-$tag"; mkdir -p "$ev"
  dotnet build "$REPO/tools/Replay/TicketDesk.Replay.Cli" -c Release -v quiet -nologo >/dev/null
  bin="$REPO/tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay"
  base="$(mktemp --suffix=.jsonl)"; xz -dc "$GOLDEN" > "$base"
  {
    echo "# $(now) td-replay replay against https://$FQDN/ (Azure VM, $tag; fresh baseline-s-shared, attachments emptied, index rebuilt)"
    echo "# golden: $(sha256sum "$GOLDEN" | cut -c1-16)  git: $(git -C "$REPO" rev-parse --short HEAD)"
  } > "$ev/replay.txt"
  "$bin" replay --from "$base" --target "https://$FQDN/" --insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" \
    --secrets-file "$TD_W/.secrets/td-seed.env" --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes "$REPO/docs/migration/routes.csv" \
    --out "$ev/candidate.jsonl" --run-id "azure-sql-$tag" >> "$ev/replay.txt" 2>&1 || true
  "$bin" compare --baseline "$base" --candidate "$ev/candidate.jsonl" --out-dir "$ev/compare" > "$ev/compare.txt" 2>&1 || true
  rm -f "$base"
  xz -9f "$ev/candidate.jsonl"
  # no secrets in the evidence
  ! grep -rlF "$(envval "$TD_W/.secrets/legacy-lab.env" TD_ADMIN_PASSWORD)" "$ev" || die "admin password found in $ev"
  tail -1 "$ev/compare.txt"
}

rp_latency() {
  rp_outputs
  local tag="${1:?tag}" ev
  ev="$EVID/latency-$tag"; mkdir -p "$ev"
  {
    echo "# $(now) k6 $(k6 version | head -1 | cut -c1-40) against https://$FQDN (Azure VM, $tag) from lab-host"
    echo "# mix tests/perf/ticketdesk-mix.js, rate ${TD_RATE:-5} ops/s, warm-up ${TD_WARMUP_S:-20} s, measure ${TD_MEASURE_S:-60} s; host load $(cut -d' ' -f1-3 /proc/loadavg)"
    echo "# network: TCP connect to the VM from lab-host (5 samples, s):"
    for _ in 1 2 3 4 5; do curl -s -o /dev/null -w '%{time_connect} ' --cacert "$TD_W/.secrets/legacy-lab-ca/ca.crt" "https://$FQDN/about"; done; echo
  } > "$ev/environment.txt"
  cp "$REPO/tests/perf/users-helpdesk.json" "$ev/users-helpdesk.json"
  TD_PASSWORD="$(envval "$TD_W/.secrets/td-seed.env" TD_SEED_USER_PASSWORD)" k6 run --no-color -e TD_TARGET="https://$FQDN" \
    -e TD_USERS_FILE="$ev/users-helpdesk.json" -e TD_SUMMARY="$ev/summary.json" -e TD_RATE="${TD_RATE:-5}" \
    -e TD_WARMUP_S="${TD_WARMUP_S:-20}" -e TD_MEASURE_S="${TD_MEASURE_S:-60}" "$REPO/tests/perf/ticketdesk-mix.js" > "$ev/k6.txt" 2>&1 || true
  rm -f "$ev/users-helpdesk.json"
  python3 -I -c "import json,sys; s=json.load(open(sys.argv[1]))['all']; print('p50 %.1f p95 %.1f p99 %.1f ms, %d requests, errors %.2f%%' % (s['duration_ms']['med'], s['duration_ms']['p(95)'], s['duration_ms']['p(99)'], s['duration_ms']['count'], 100*s['failed']['rate']))" "$ev/summary.json" | tee "$ev/result.txt"
}

rp_cutover() {
  rp_outputs
  local kind="measured" stamp ev tf stop pp rc=0 result=switched runid
  [[ "${1:-}" == --rehearsal ]] && kind="rehearsal"
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"; ev="$EVID/cutover-$kind-$stamp"; mkdir -p "$ev"
  tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  declare -A M; mark() { M[$1]="$(now)"; log "MARK $1 ${M[$1]}"; }
  log "cutover ($kind) $stamp on https://$FQDN: SQL Express on the VM -> $SQLSRV/$SQLDB" | tee "$ev/cutover.log"
  echo "https://$FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" --write-ticket "$PROBE_TICKET" > "$ev/probe-live.json" 2>&1 &
  pp=$!
  mark probe_start; sleep 30
  mark runcommand_start
  # one Run Command for the whole window, so its start-up latency is not inside the freeze
  az sql db show -g "$RG" -s "${SQLSRV%%.*}" -n "$SQLDB" --query '{status: status, serviceObjective: currentServiceObjectiveName, useFreeLimit: useFreeLimit}' -o json > "$ev/target-before.json" 2>&1 || true
  rp_provision "azsql-wake+freeze+export+import+azsql-users+reconcile+switch" "Database=azure" > "$ev/window.txt" 2>&1 || rc=$?
  runid="$PROV_RUNID"; mark runcommand_end
  if ((rc)); then
    # The switch is the window's last step, so a failure leaves the app on SQL Express: only the freeze is undone.
    result="rolled-back"; log "window FAILED (see window.txt): rolling back (REVOKE on the source)" | tee -a "$ev/cutover.log"
    rp_provision "unfreeze" "Database=express" > "$ev/rollback.txt" 2>&1 || log "rollback provisioning failed" | tee -a "$ev/cutover.log"
    mark rolled_back
  else
    if cmd_smoke "cutover-$kind-$stamp" > "$ev/smoke.txt" 2>&1; then mark smoke_passed; else result="smoke-failed"; fi
  fi
  sleep 60; touch "$stop"; wait "$pp" || true; mark probe_end
  cp "$EVID/provision-$runid.log" "$ev/window-transcript.log" 2>/dev/null || true
  az storage blob download --auth-mode login --account-name "$SA" -c logs -n "$VM_HOST/reconcile-$runid.json" -f "$ev/reconcile.json" --only-show-errors -o none 2>/dev/null || true
  {
    printf '{'
    local first=1 k
    while read -r k v; do [[ -n "$k" ]] || continue; ((first)) || printf ','; first=0; printf '"vm_%s":"%s"' "$k" "$v"; done < <(marks_from_log "$ev/window-transcript.log")
    for k in probe_start runcommand_start runcommand_end smoke_passed rolled_back probe_end; do
      [[ -n "${M[$k]:-}" ]] || continue; ((first)) || printf ','; first=0; printf '"%s":"%s"' "$k" "${M[$k]}"
    done
    printf ',"result":"%s","kind":"%s","runid":"%s","source":"SQL Express on vm-tdlab-legacy","target":"%s/%s"}\n' "$result" "$kind" "$runid" "$SQLSRV" "$SQLDB"
  } > "$ev/marks.json"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" --marks "$ev/marks.json" > "$ev/probe-summary.json"
  xz -9f "$ev/probe.jsonl"; rm -f "$tf" "$stop" "$ev/probe-live.json"
  python3 -I - "$ev" <<'PY' | tee "$ev/summary.txt"
import json, sys, datetime as d
ev = sys.argv[1]
m = json.load(open(f"{ev}/marks.json")); s = json.load(open(f"{ev}/probe-summary.json"))
t = lambda k: d.datetime.strptime(m[k], "%Y-%m-%dT%H:%M:%S.%fZ" if "." in m[k] else "%Y-%m-%dT%H:%M:%SZ")
def span(a, b):
    return round((t(b) - t(a)).total_seconds(), 1) if a in m and b in m else None
print(f"result: {m['result']} ({m['kind']})")
print(f"target before the window: {open(f'{ev}/target-before.json').read().strip()!s:.200}")
print(f"write freeze on the VM (DENY -> switched, warm): {span('vm_write_denied', 'vm_switched')} s")
for a, b in (("vm_freeze_start", "vm_frozen"), ("vm_export_start", "vm_copy_restored"), ("vm_copy_restored", "vm_exported"),
             ("vm_import_start", "vm_imported"), ("vm_imported", "vm_reconcile_start"), ("vm_reconcile_start", "vm_reconciled"),
             ("vm_switch_start", "vm_switched")):
    print(f"  {a[3:]} -> {b[3:]}: {span(a, b)} s")
wd = s.get("write_downtime") or {}
print(f"probe: reads {s['read_requests']} ({s['failed_reads']} failed), writes {s['write_requests']} ({s['failed_writes']} failed), "
      f"sign-ins {s['signins']} ({s['failed_signins']} failed)")
print(f"probe write downtime (last ok -> first ok): {wd.get('seconds')} s ({wd.get('last_ok_before')} -> {wd.get('first_ok_after')})")
print("read failure windows: " + json.dumps(s["failure_windows"]))
PY
  return $([[ "$result" == switched ]] && echo 0 || echo 1)
}

rp_plan_tag() {
  rp_outputs
  local ev="$EVID/plan-tag" f
  mkdir -p "$ev"
  rp_provision "plan-tag" "Database=azure" > "$ev/provision.txt" 2>&1 || { tail -20 "$ev/provision.txt"; die "plan-tag failed"; }
  grep -ao 'TDPLAN .*' "$ev/provision.txt" | tail -1 | sed 's/^TDPLAN //' | python3 -I -m json.tool > "$ev/plan-tag.json"
  for f in $(az storage blob list --auth-mode login --account-name "$SA" -c logs --prefix "$VM_HOST/plan-tag-$PROV_RUNID" --query '[].name' -o tsv); do
    az storage blob download --auth-mode login --account-name "$SA" -c logs -n "$f" -f "$ev/$(basename "$f")" --only-show-errors -o none
  done
  cat "$ev/plan-tag.json"
}

rp_rotate_cert() {
  rp_outputs
  local ev="$EVID/cert-rotation" d="$TD_W/.secrets/legacy-lab-ca" t0 t1 t2 t3 nopass rc=0
  mkdir -p "$ev"
  served() { echo | openssl s_client -connect "$FQDN:443" -servername "$FQDN" 2>/dev/null | openssl x509 -noout -serial -fingerprint -sha1 -enddate; }
  { echo "# before"; served; } > "$ev/served.txt"
  TD_CERT_NAME=azure-sql-rotated "$REPO/ops/onprem/new-lab-cert.sh" "$FQDN" localhost "$VM_HOST" > "$ev/new-cert.txt" 2>&1
  nopass="$(mktemp --suffix=.pfx)"; chmod 600 "$nopass"
  # passwordless PFX straight from the new key + certificate + lab CA (az then needs no --password on its command line)
  openssl pkcs12 -export -inkey "$d/azure-sql-rotated.key" -in "$d/azure-sql-rotated.crt" -certfile "$d/ca.crt" \
    -name "TicketDesk lab" -passout pass: -out "$nopass"
  [[ -s "$nopass" ]] || die "passwordless PFX not written"
  t0=$SECONDS; echo "rotation start $(now)" > "$ev/rotation.txt"
  az keyvault certificate import --vault-name "$KV" -n td-iis-legacy -f "$nopass" --query '{version: id, thumbprint: x509ThumbprintHex, notBefore: attributes.notBefore, expires: attributes.expires}' -o json >> "$ev/rotation.txt" 2>&1 || rc=$?
  rm -f "$nopass"; ((rc == 0)) || die "certificate import failed (see $ev/rotation.txt)"
  t1=$SECONDS; echo "new version in Key Vault after $((t1 - t0)) s ($(now))" >> "$ev/rotation.txt"
  rp_provision "site+verify" "Database=azure" > "$ev/provision.txt" 2>&1 || rc=$?
  t2=$SECONDS; echo "IIS binding updated through the managed identity after $((t2 - t0)) s ($(now)), provision rc=$rc" >> "$ev/rotation.txt"
  cmd_smoke "after-cert-rotation" > "$ev/smoke.txt" 2>&1 || rc=$?
  t3=$SECONDS; echo "smoke after $((t3 - t0)) s ($(now)), rc=$rc: $(tail -1 "$ev/smoke.txt")" >> "$ev/rotation.txt"
  { echo "# after"; served; echo "# new certificate"; openssl x509 -in "$d/azure-sql-rotated.crt" -noout -serial -fingerprint -sha1 -enddate; } >> "$ev/served.txt"
  cat "$ev/rotation.txt" "$ev/served.txt"
  return $rc
}

rp_rebuild() {
  local ev t0 rc=0 bacpac="${1:-$BACPAC_FILE}"
  [[ "$(az group exists -n "$RG")" == false ]] || die "$RG exists: a rebuild starts from nothing (replatform.sh down first)"
  ev="$EVID/rebuild-$(date -u +%Y%m%dT%H%M%SZ)"; mkdir -p "$ev"
  t0=$SECONDS; echo "rebuild start $(now)" | tee "$ev/rebuild.txt"
  make -C "$REPO" --no-print-directory azure-up-replatform AZ_OWNER="$AZ_OWNER_TAG" TTL_HOURS="${TTL_HOURS:-2}" > "$ev/apply.txt" 2>&1 || rc=$?
  local t_apply=$((SECONDS - t0)); tail -n 2 "$ev/apply.txt" | tee -a "$ev/rebuild.txt"
  "$REPO/scripts/azure_ttl_sync.sh" >> "$ev/apply.txt" 2>&1 || true
  ((rc == 0)) || die "apply failed (see $ev/apply.txt)"
  rp_stage --bacpac "$bacpac" > "$ev/stage.txt" 2>&1 || die "stage failed"
  local t_stage=$((SECONDS - t0))
  # shellcheck disable=SC1091
  . "$ART/staged.env"
  rp_provision "fetch+prereqs+site+sqltools+import+azsql-users+migrate+deploy+verify" "Database=azure" \
    "BacpacBlob=$BACPAC_BLOB" "BacpacSha256=$BACPAC_SHA" > "$ev/provision.txt" 2>&1 || rc=$?
  local t_prov=$((SECONDS - t0))
  ((rc == 0)) || { tail -n 30 "$ev/provision.txt"; die "provision failed"; }
  cp "$EVID/provision-$PROV_RUNID.log" "$ev/provision-transcript.log" 2>/dev/null || true
  cmd_smoke "rebuild" > "$ev/smoke.txt" 2>&1 || rc=$?
  local t_smoke=$((SECONDS - t0))
  printf 'apply_end_s=%s stage_end_s=%s provision_end_s=%s smoke_end_s=%s smoke_rc=%s end=%s\n' \
    "$t_apply" "$t_stage" "$t_prov" "$t_smoke" "$rc" "$(now)" | tee -a "$ev/rebuild.txt"
  grep -ao 'TDTIMINGS .*' "$ev/provision.txt" | tail -1 | tee -a "$ev/rebuild.txt"
  return $rc
}

rp_down() {
  local out="$EVID/destroy-$(date -u +%Y%m%dT%H%M%SZ).txt"
  make -C "$REPO" --no-print-directory azure-down-foundation AZ_OWNER="$AZ_OWNER_TAG" > "$out" 2>&1 || { tail -20 "$out"; die "down failed"; }
  tail -2 "$out"
}

rp_session() {
  # globals, not locals: the EXIT trap runs after this function has returned (a local here left the trap with an
  # unbound variable on 2026-11-10 and skipped the destroy)
  SESSION_EV="$EVID/session-$(date -u +%Y%m%dT%H%M%SZ)"; mkdir -p "$SESSION_EV"
  sl="$SESSION_EV/session.log"; local ev="$SESSION_EV" ok=1 from="${1:-}"
  step() { echo "$(now) SESSION $*" | tee -a "$sl"; }
  # each step in a subshell: a die (exit) inside it fails that step only; the trap still destroys at the end
  run() { local name="$1"; shift; step "start $name"; if ( set -e; "$@" ) >> "$sl" 2>&1; then step "ok $name"; else step "FAILED $name"; return 1; fi; }
  finish() {
    set +eu; step "destroy"
    rp_down >> "$sl" 2>&1
    # a half-created resource (2026-11-10: a VM whose allocation failed holds its NIC) can block the stage-none apply:
    # then delete the group itself and let Terraform drop what is gone
    if [[ "$(az group exists -n "$RG")" == true ]]; then
      step "stage none left the group: make azure-down"
      make -C "$REPO" --no-print-directory azure-down AZ_OWNER="$AZ_OWNER_TAG" >> "$sl" 2>&1
      make -C "$REPO" --no-print-directory azure-down-foundation AZ_OWNER="$AZ_OWNER_TAG" >> "$sl" 2>&1
    fi
    step "destroyed: RG exists=$(az group exists -n "$RG")"
  }
  trap finish EXIT
  set +e
  python3 -I "$REPO/scripts/azure_ledger.py" check | tee -a "$sl" || return 1
  if [[ "$from" == --rebuild-rotate ]]; then    # a clean rebuild-from-zero sample and the certificate rotation only
    run rebuild rp_rebuild || return 1
    run cert-rotation rp_rotate_cert || ok=0
    ((ok)) && step "session complete" || step "session complete with failures"
    return 0
  fi
  if [[ "$from" == --after-rebuild ]]; then     # the RG is up and the app runs on Azure SQL (rebuild done before)
    step "continuing after the rebuild"
  else
    run rebuild rp_rebuild || return 1          # the app must work on Azure SQL, or nothing else makes sense
  fi
  run stage-seed rp_stage --seed --bacpac "$BACPAC_FILE"
  run sql-express rp_provision "sql+sqlupdate+restore+deploy+verify" "RestorePrefix=seed" "RestoreName=$SEED_NAME" "Database=express"
  run files-reset rp_files_reset
  run reindex rp_reindex "$ev/reindex-express"
  run latency-express rp_latency express
  run rehearsal-db-reset rp_db_reset
  run rehearsal-fresh rp_fresh express rehearsal-source
  if run rehearsal rp_cutover --rehearsal; then
    run measured-db-reset rp_db_reset
    run measured-fresh rp_fresh express measured-source
    run measured-cutover rp_cutover || ok=0
    run migrate rp_provision "migrate" "Database=azure"
    run plan-tag rp_plan_tag
  else
    ok=0; step "rehearsal failed: no measured cutover in this session"
  fi
  run fresh-azure-golden rp_fresh azure azure-golden && run golden-azure rp_golden azure
  run fresh-azure-latency rp_fresh azure azure-latency && run latency-azure rp_latency azure
  run cert-rotation rp_rotate_cert
  ((ok)) && step "session complete" || step "session complete with failures"
}

# Sourced by ops/azure/aks.sh for the helpers above: no dispatch then.
[[ "${BASH_SOURCE[0]}" == "$0" ]] || return 0
[[ $# -gt 0 ]] || { sed -n '2,30p' "$0"; exit 2; }
c="$1"; shift
case "$c" in
  tools) rp_tools ;;
  bacpac) rp_bacpac ;;
  stage) rp_stage "$@" ;;
  provision) rp_provision "$@" ;;
  smoke) rp_outputs; cmd_smoke "$@" ;;
  files-reset) rp_files_reset ;;
  db-reset) rp_db_reset ;;
  fresh) rp_fresh "$@" ;;
  golden) rp_golden "$@" ;;
  latency) rp_latency "$@" ;;
  cutover) rp_cutover "$@" ;;
  plan-tag) rp_plan_tag ;;
  rotate-cert) rp_rotate_cert ;;
  rebuild) rp_rebuild "$@" ;;
  down) rp_down ;;
  session) rp_session "$@" ;;
  *) sed -n '2,30p' "$0"; exit 2 ;;
esac
