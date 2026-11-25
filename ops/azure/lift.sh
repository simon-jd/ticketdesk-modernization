#!/usr/bin/env bash
# lift.sh - lift & shift of legacy TicketDesk onto the Azure VM (lift stage), driven from lab-host.
#
#   lift.sh cert                  IIS certificate for the VM's DNS name from the lab CA ($TD_W/.secrets, once)
#   lift.sh fetch-publish         copy the legacy publish output from the local VM and package it once with a
#                                 SHA-256 ($TD_W/vm/artifacts)                              [needs the VM lock]
#   lift.sh stage [--seed] [--backup DIR]
#                                 upload the ops/windows bundle + publish package to the artifacts container;
#                                 --seed also the scale-S .bak (backups/seed/baseline-s/), --backup DIR a local
#                                 Backup-TdSite folder (backups/tdlab-legacy/<name>/)
#   lift.sh provision [STEPS] [Name=Value...]
#                                 run ops/azure/Invoke-TdAzureProvision.ps1 on the VM through Run Command;
#                                 STEPS joined with '+' (default: the full sequence); saves output + transcript
#   lift.sh smoke [TAG]           ops/onprem/smoke/legacy_smoke.py smoke against https://<vm fqdn>/
#   lift.sh backup-drill          backup -> blob, write after it, lose DB + local backups, restore from blob, smoke
#   lift.sh pull-backup           newest blob backup of the VM -> $TD_W/vm/exports/azure-lift/<name>/
#   lift.sh cutover               local VM -> Azure VM with a write freeze and a 1/s probe   [needs the VM lock]
#   lift.sh rebuild [DIR]         from nothing: apply lift, stage (+ backup DIR), provision, smoke (timed)
#   lift.sh local-sqlupdate       cumulative update on the local VM, saved as snapshot baseline-s-cu
#                                 (only after plan/handoffs/r1-baseline.md exists)          [needs the VM lock]
#   lift.sh env                   non-secret Terraform outputs used here
#
# Evidence goes to $TD_EVIDENCE (default docs/evidence/azure-lift-<UTC date>/). Secrets stay in Key Vault and
# $TD_W/.secrets; nothing here prints them. VM-changing commands need TD_LOCK_OWNER to hold plan/locks/vm.lock.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
# shellcheck disable=SC1091
. "$TD_W/.tools/env.sh"
RG="rg-ticketdesk-lab"
TF_DIR="$REPO/infra/terraform/envs/lab"
ART="${TD_LIFT_ARTIFACTS:-$TD_W/vm/artifacts}"   # replatform.sh keeps its own folder
EXPORTS="$TD_W/vm/exports"
EVID="${TD_EVIDENCE:-$REPO/docs/evidence/azure-lift-$(date -u +%Y%m%d)}"
V="$REPO/ops/onprem/vm/vmctl.sh"
LOCK="$REPO/ops/onprem/vm/vmlock.sh"
SMOKE=(python3 -I "$REPO/ops/onprem/smoke/legacy_smoke.py")
PROBE=(python3 -I "$HERE/cutover_probe.py")
OWNER="${TD_LOCK_OWNER:-}"
VM_HOST="tdlab-legacy"                      # computer name: blob prefix of the VM's own backups and logs
FULL_STEPS="fetch+prereqs+sql+sqlupdate+site+restore+deploy+backup-task+verify"
mkdir -p "$EVID" "$ART"

log() { echo "$(date -u +%FT%TZ) $*"; }
die() { echo "lift.sh: $*" >&2; exit 1; }
now() { date -u +%FT%TZ; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
need_lock() {
  [[ -n "$OWNER" ]] || die "set TD_LOCK_OWNER to the VM lock holder's name"
  "$LOCK" check "$OWNER" || die "VM lock not held by $OWNER ($("$LOCK" status)); take it: $LOCK acquire $OWNER PURPOSE MINUTES"
}
vm_sql() {  # vm_sql DB QUERY: T-SQL on the local VM via the 127.0.0.1:11433 forward (sa from legacy-lab.env, never echoed)
  SQLCMDPASSWORD="$(envval "$TD_W/.secrets/legacy-lab.env" TD_SQL_SA_PASSWORD)" \
    "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d "$1" -h -1 -W -b -Q "$2"
}
wait_local_app() {  # until the local VM's home page answers 200 over HTTPS (lab CA)
  local t0=$SECONDS
  until [[ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 60 --cacert "$TD_W/.secrets/legacy-lab-ca/ca.crt" https://localhost:8443/)" == 200 ]]; do
    (( SECONDS - t0 < ${1:-900} )) || return 1; sleep 5
  done
  log "local app answers 200 after $((SECONDS - t0)) s"
}
expected_fqdn() {
  local sub; sub="$(az account show --query id -o tsv)"
  echo "tdlab-legacy-$(printf '%s' "$sub/$RG" | sha1sum | cut -c1-6).swedencentral.cloudapp.azure.com"
}
load_outputs() {
  local j; j="$(terraform -chdir="$TF_DIR" output -json)"
  eval "$(python3 -I -c '
import json, shlex, sys
o = json.load(sys.stdin)
for k, n in (("vm_name", "VM"), ("vm_fqdn", "FQDN"), ("key_vault_name", "KV"), ("storage_account", "SA")):
    v = (o.get(k) or {}).get("value") or ""
    print(n + "=" + shlex.quote(str(v)))' <<<"$j")"
  [[ -n "${VM:-}" && -n "${SA:-}" && -n "${KV:-}" ]] || die "lift stage outputs missing (is the lift stage applied?)"
}
blob_up() {  # blob_up CONTAINER NAME FILE   (retries while the operator's data-plane role propagates)
  local err; err="$(mktemp)"
  for i in $(seq 1 24); do
    if az storage blob upload --auth-mode login --account-name "$SA" -c "$1" -n "$2" -f "$3" --overwrite \
         --only-show-errors -o none 2>"$err"; then rm -f "$err"; return 0; fi
    grep -qiE 'AuthorizationPermissionMismatch|AuthorizationFailure|not authorized|403' "$err" || { cat "$err" >&2; rm -f "$err"; return 1; }
    log "upload $1/$2: not authorized yet (role propagation), retry $i in 15 s"; sleep 15
  done
  cat "$err" >&2; rm -f "$err"; return 1
}
blob_up_dir() {  # blob_up_dir CONTAINER PREFIX DIR   (manifest.json last: a folder with a manifest is complete)
  local f
  for f in "$3"/*; do [[ -f "$f" && "$(basename "$f")" != manifest.json ]] && blob_up "$1" "$2/$(basename "$f")" "$f"; done
  [[ -f "$3/manifest.json" ]] && blob_up "$1" "$2/manifest.json" "$3/manifest.json"
  return 0
}

# ---------------------------------------------------------------------------------------------------
cmd_env() { load_outputs; echo "VM=$VM FQDN=$FQDN KV=$KV SA=$SA"; }

cmd_cert() {
  local fqdn crt="$TD_W/.secrets/legacy-lab-ca/azure-lift.crt"
  fqdn="$(expected_fqdn)"
  if [[ -f "$crt" ]] && openssl x509 -in "$crt" -noout -ext subjectAltName | grep -q "DNS:$fqdn" \
     && openssl x509 -in "$crt" -noout -checkend $((30 * 86400)) >/dev/null; then
    log "certificate for $fqdn present"
  else
    TD_CERT_NAME=azure-lift "$REPO/ops/onprem/new-lab-cert.sh" "$fqdn" localhost "$VM_HOST"
  fi
  { echo "# lift.sh cert $(now): IIS certificate for the Azure VM (public part only)"
    openssl x509 -in "$crt" -noout -subject -issuer -serial -startdate -enddate -fingerprint -sha1 -ext subjectAltName
  } | tee "$EVID/certificate.txt"
}

cmd_fetch_publish() {
  need_lock
  local t0=$SECONDS out rev zip
  log "packaging C:\\TdBuild\\publish on the local VM"
  out="$("$V" ps <<'PS'
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = 'C:\TdMedia\td-legacy-publish.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory('C:\TdBuild\publish', $zip, 'Optimal', $false)
$files = Get-ChildItem C:\TdBuild\publish -Recurse -File
"REVISION=" + (Get-Content C:\TdSrc\ticketdesk\REVISION -Raw).Trim()
"FILES=" + $files.Count
"BYTES=" + ($files | Measure-Object Length -Sum).Sum
"ZIPSHA=" + (Get-FileHash $zip -Algorithm SHA256).Hash
PS
)"
  echo "$out"
  rev="$(sed -n 's/^REVISION=//p' <<<"$out" | tr -d '\r')"
  zip="$ART/td-legacy-publish-$rev.zip"
  "$V" scp "vm:C:/TdMedia/td-legacy-publish.zip" "$zip"
  "$V" scp "vm:C:/TdBuild/build-info.json" "$ART/td-legacy-publish-$rev.build-info.json"
  "$V" ssh 'Remove-Item C:\TdMedia\td-legacy-publish.zip -Force' || true
  ( cd "$ART" && sha256sum "$(basename "$zip")" > "$(basename "$zip").sha256" )
  local sha; sha="$(cut -c1-64 "$zip.sha256")"
  [[ "${sha^^}" == "$(sed -n 's/^ZIPSHA=//p' <<<"$out" | tr -d '\r')" ]] || die "SHA-256 after copy differs from the VM's"
  { echo "# lift.sh fetch-publish $(now): legacy publish output packaged once on the local VM"
    echo "package: $(basename "$zip")"; echo "bytes: $(stat -c %s "$zip")"; echo "sha256: $sha"
    tr -d '\r' <<<"$out" | grep -E '^(REVISION|FILES|BYTES)='
    echo "build-info.json:"; tr -d '\r' < "$ART/td-legacy-publish-$rev.build-info.json"
  } | tee "$EVID/artifact-publish.txt"
  log "fetch-publish done in $((SECONDS - t0)) s"
}

cmd_stage() {
  load_outputs
  local seed=0 backup="" rev bundle pub
  while [[ $# -gt 0 ]]; do case "$1" in --seed) seed=1;; --backup) backup="$2"; shift;; *) die "stage: unknown $1";; esac; shift; done
  pub="$(ls -t "$ART"/td-legacy-publish-*.zip 2>/dev/null | head -1 || true)"
  [[ -n "$pub" ]] || log "WARN no publish package in $ART yet (lift.sh fetch-publish): staging the scripts only"
  rev="$(git -C "$REPO" rev-parse --short HEAD)"
  [[ -z "$(git -C "$REPO" status --porcelain -- ops/windows ops/db)" ]] || rev="$rev-dirty"
  bundle="$ART/td-ops-windows-$rev.zip"
  # ops/windows scripts + the new app's database role (aks-sql-user step), flat
  python3 -I - "$REPO/ops/windows" "$bundle" "$REPO/ops/db/td_modern_app_role.sql" <<'PY'
import sys, zipfile, pathlib
src, out = pathlib.Path(sys.argv[1]), sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for p in sorted(src.iterdir()):
        if p.suffix in (".ps1", ".psm1"):
            z.write(p, p.name)
    for extra in sys.argv[3:]:
        z.write(extra, pathlib.Path(extra).name)
PY
  BUNDLE_BLOB="$(basename "$bundle")"; BUNDLE_SHA="$(sha256sum "$bundle" | cut -c1-64)"
  PUBLISH_BLOB=""; PUBLISH_SHA=""
  local t0=$SECONDS
  blob_up artifacts "$BUNDLE_BLOB" "$bundle"
  if [[ -n "$pub" ]]; then
    PUBLISH_BLOB="$(basename "$pub")"; PUBLISH_SHA="$(cut -c1-64 "$pub.sha256")"
    blob_up artifacts "$PUBLISH_BLOB" "$pub"
  fi
  log "staged $BUNDLE_BLOB ($(stat -c %s "$bundle") B)${pub:+ and $PUBLISH_BLOB ($(stat -c %s "$pub") B)} in $((SECONDS - t0)) s"
  if ((seed)); then
    t0=$SECONDS
    blob_up backups "seed/baseline-s/TicketDesk.bak" "$EXPORTS/ticketdesk-baseline-s.bak"
    log "staged seed baseline-s ($(stat -c %s "$EXPORTS/ticketdesk-baseline-s.bak") B) in $((SECONDS - t0)) s"
  fi
  if [[ -n "$backup" ]]; then
    t0=$SECONDS
    blob_up_dir backups "$VM_HOST/$(basename "$backup")" "$backup"
    log "staged backup $(basename "$backup") ($(du -sb "$backup" | cut -f1) B) in $((SECONDS - t0)) s"
  fi
  printf 'BUNDLE_BLOB=%s\nBUNDLE_SHA=%s\nPUBLISH_BLOB=%s\nPUBLISH_SHA=%s\n' "$BUNDLE_BLOB" "$BUNDLE_SHA" "$PUBLISH_BLOB" "$PUBLISH_SHA" > "$ART/staged.env"
  cat "$ART/staged.env"
}

# provision STEPS [Name=Value...]: sets PROV_RUNID, PROV_RC, PROV_SECONDS; output in $EVID/provision-<runid>.*
cmd_provision() {
  load_outputs
  # shellcheck disable=SC1091
  . "$ART/staged.env"
  local steps="${1:-$FULL_STEPS}"; shift || true
  local runid t0 json txt
  runid="$(date -u +%Y%m%dT%H%M%SZ)"; json="$EVID/provision-$runid.json"; txt="$EVID/provision-$runid.txt"
  log "provision $runid on $VM: $steps $*" | tee "$txt"
  t0=$SECONDS
  set +e
  az vm run-command invoke -g "$RG" -n "$VM" --command-id RunPowerShellScript \
    --scripts @"$HERE/Invoke-TdAzureProvision.ps1" \
    --parameters "Steps=$steps" "StorageAccount=$SA" "KeyVault=$KV" "Fqdn=$FQDN" \
      "BundleBlob=$BUNDLE_BLOB" "BundleSha256=$BUNDLE_SHA" "PublishBlob=$PUBLISH_BLOB" "PublishSha256=$PUBLISH_SHA" \
      "AdminEmail=$(envval "$TD_W/.secrets/legacy-lab.env" TD_ADMIN_EMAIL)" "RunId=$runid" "$@" \
    -o json > "$json" 2>>"$txt"
  local azrc=$?
  set -e
  PROV_SECONDS=$((SECONDS - t0)); PROV_RUNID="$runid"
  python3 -I - "$json" >>"$txt" <<'PY' || true
import json, sys
try:
    d = json.load(open(sys.argv[1]))
except Exception as e:
    print(f"(no JSON output: {e})"); sys.exit()
for v in d.get("value", []):
    print(f"---- {v.get('code')}"); print(v.get("message", ""))
PY
  az storage blob download --auth-mode login --account-name "$SA" -c logs -n "$VM_HOST/provision-$runid.log" \
    -f "$EVID/provision-$runid.log" --only-show-errors -o none 2>>"$txt" || echo "(transcript not downloaded)" >>"$txt"
  # The transcript header repeats the Run Command parameters: keep the admin's e-mail out of the evidence.
  python3 -I - "$(envval "$TD_W/.secrets/legacy-lab.env" TD_ADMIN_EMAIL)" "$json" "$txt" "$EVID/provision-$runid.log" <<'PY2' || true
import sys, pathlib
v = sys.argv[1]
for f in sys.argv[2:]:
    p = pathlib.Path(f)
    if v and p.exists():
        b = p.read_bytes()
        if v.encode() in b:
            p.write_bytes(b.replace(v.encode(), b"<admin-email>"))
PY2
  PROV_RC=0
  if ((azrc)) || grep -q 'FAILED:' "$txt" || ! grep -q 'TDTIMINGS' "$txt"; then PROV_RC=1; fi
  echo "provision $runid steps=$steps elapsed_s=$PROV_SECONDS rc=$PROV_RC" | tee -a "$txt"
  tail -n 25 "$txt"
  return $PROV_RC
}

cmd_smoke() {
  load_outputs
  local tag="${1:-$(date -u +%Y%m%dT%H%M%SZ)}" rc=0
  "${SMOKE[@]}" smoke --base "https://$FQDN" --json "$EVID/smoke-$tag.json" > "$EVID/smoke-$tag.txt" 2>&1 || rc=$?
  tail -n 3 "$EVID/smoke-$tag.txt"
  return $rc
}

cmd_pull_backup() {
  load_outputs
  local name dest
  name="$(az storage blob list --auth-mode login --account-name "$SA" -c backups --prefix "$VM_HOST/" \
          --query "[?ends_with(name, 'manifest.json')].name" -o tsv | sort | tail -1 | cut -d/ -f2)"
  [[ -n "$name" ]] || die "no complete backup under backups/$VM_HOST/"
  dest="$EXPORTS/azure-lift/$name"; mkdir -p "$dest"
  az storage blob download-batch --auth-mode login --account-name "$SA" -s backups --pattern "$VM_HOST/$name/*" \
    -d "$EXPORTS/azure-lift/.dl" --only-show-errors -o none
  mv "$EXPORTS/azure-lift/.dl/$VM_HOST/$name/"* "$dest/" && rm -rf "$EXPORTS/azure-lift/.dl"
  ( cd "$dest" && python3 -I - <<'PY'
import hashlib, json
m = json.load(open("manifest.json", encoding="utf-8-sig"))
for f in m["files"]:
    h = hashlib.sha256(open(f["name"], "rb").read()).hexdigest().upper()
    assert h == f["sha256"], f["name"]
print(f"manifest OK: {len(m['files'])} files, created {m['created_utc']}, SQL {m['sql_version']}")
PY
  )
  echo "$dest"
}

cmd_backup_drill() {
  load_outputs
  local ev="$EVID/backup-drill" m
  mkdir -p "$ev"; m="$ev/marks.json"
  local t_backup t_write t_loss t_restore_start t_restore_end t_smoke
  t_backup="$(now)"; cmd_provision "backup-now+verify" | tee "$ev/1-backup.txt"
  t_write="$(now)"; cmd_smoke drill-write-after-backup | tee "$ev/2-write-after-backup.txt"
  t_loss="$(now)"; cmd_provision "drill-loss" | tee "$ev/3-loss.txt"
  t_restore_start="$(now)"
  cmd_provision "restore+verify" "RestorePrefix=$VM_HOST" "RestoreName=latest" | tee "$ev/4-restore.txt"
  t_restore_end="$(now)"
  cmd_smoke drill-after-restore | tee "$ev/5-smoke.txt"
  t_smoke="$(now)"
  printf '{"backup_start":"%s","write_after_backup":"%s","loss":"%s","restore_start":"%s","restore_end":"%s","smoke_passed":"%s"}\n' \
    "$t_backup" "$t_write" "$t_loss" "$t_restore_start" "$t_restore_end" "$t_smoke" > "$m"
  cat "$m"
}

cmd_cutover() {
  need_lock
  load_outputs
  local stamp ev tf stop pp src local_dir t
  stamp="$(date -u +%Y%m%dT%H%M%SZ)"; ev="$EVID/cutover-$stamp"; mkdir -p "$ev"
  tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  declare -A M
  mark() { M[$1]="$(now)"; log "MARK $1 ${M[$1]}"; }
  log "cutover $stamp: local VM https://localhost:8443 -> https://$FQDN" | tee "$ev/cutover.log"
  {
    echo "== source before"; vm_sql master "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(20)) + ' ' + CAST(DATABASEPROPERTYEX('TicketDesk','Updateability') AS varchar(20))"
    vm_sql TicketDesk "SET NOCOUNT ON; SELECT s.name + '.' + t.name + '=' + CAST(SUM(p.rows) AS varchar(20)) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1" > "$ev/source-rowcounts.txt"
    cat "$ev/source-rowcounts.txt"
  } >> "$ev/cutover.log" 2>&1
  echo "https://localhost:8443" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" > "$ev/probe-summary-live.json" 2>&1 &
  pp=$!
  mark probe_start; sleep 30
  # 1. freeze writes on the source (reads keep working)
  mark freeze_start
  vm_sql master "ALTER DATABASE [TicketDesk] SET READ_ONLY WITH ROLLBACK IMMEDIATE" >> "$ev/cutover.log" 2>&1
  mark frozen
  # 2. back up on the source (Backup-TdSite.ps1: .bak + site.zip incl. App_Data + config + manifest)
  "$V" ssh "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\\TdOps\\windows\\Backup-TdSite.ps1 -Label cutover-$stamp" >> "$ev/cutover.log" 2>&1
  mark backup_done
  # 3. copy: local VM -> lab-host -> blob (backups/cutover/cutover-<stamp>/, manifest last)
  local_dir="$EXPORTS/azure-lift/cutover-$stamp"; mkdir -p "$(dirname "$local_dir")"
  "$V" scp -r "vm:C:/TdBackups/cutover-$stamp" "$EXPORTS/azure-lift/" >> "$ev/cutover.log" 2>&1
  mark copied_local
  blob_up_dir backups "cutover/cutover-$stamp" "$local_dir" >> "$ev/cutover.log" 2>&1
  mark uploaded
  # 4. restore on the Azure VM (Get-TdBlobBackup + Restore-TdSite, row counts), 5. smoke on Azure (go/no-go)
  local result=switched keys
  if cmd_provision "fetch+restore" "RestorePrefix=cutover" "RestoreName=cutover-$stamp" > "$ev/restore.txt" 2>&1; then
    mark restored
    if cmd_smoke "cutover-$stamp" > "$ev/smoke.txt" 2>&1; then mark smoke_passed; else result=rolled-back; log "smoke on Azure FAILED"; fi
  else
    result=rolled-back; log "restore on Azure FAILED"
  fi
  if [[ "$result" == switched ]]; then
    # 6. switch clients: the probe's base URL
    echo "https://$FQDN" > "$tf"
    mark switched
    sleep 60
  else
    # no-go: clients stay on the source, which is opened for writes again
    vm_sql master "ALTER DATABASE [TicketDesk] SET READ_WRITE WITH ROLLBACK IMMEDIATE" >> "$ev/cutover.log" 2>&1
    mark rolled_back
    sleep 15
  fi
  touch "$stop"; wait "$pp" || true
  mark probe_end
  keys="probe_start freeze_start frozen backup_done copied_local uploaded restored smoke_passed switched rolled_back probe_end"
  if [[ "$result" == switched ]]; then
    # lab only: give the shared local VM back read-write (in production the old host stays frozen, then retires)
    vm_sql master "ALTER DATABASE [TicketDesk] SET READ_WRITE WITH ROLLBACK IMMEDIATE" >> "$ev/cutover.log" 2>&1
    mark source_unfrozen; keys+=" source_unfrozen"
  fi
  {
    printf '{'; local first=1 k
    for k in $keys; do
      [[ -n "${M[$k]:-}" ]] || continue
      ((first)) || printf ','; first=0; printf '"%s":"%s"' "$k" "${M[$k]}"
    done
    printf ',"result":"%s","backup":"cutover-%s","source":"https://localhost:8443","target":"https://%s"}\n' "$result" "$stamp" "$FQDN"
  } > "$ev/marks.json"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" --marks "$ev/marks.json" > "$ev/probe-summary.json"
  du -sb "$local_dir" | tee -a "$ev/cutover.log"
  ls -l "$local_dir" | tee -a "$ev/cutover.log"
  cat "$ev/marks.json"
}

cmd_rebuild() {
  local dir="${1:-}" ev t0 t_apply t_stage t_prov t_smoke rc=0
  ev="$EVID/rebuild-$(date -u +%Y%m%dT%H%M%SZ)"; mkdir -p "$ev"
  t0=$SECONDS; echo "rebuild start $(now)" | tee "$ev/rebuild.txt"
  make -C "$REPO" --no-print-directory azure-up-lift AZ_OWNER=agent-azure-lift TTL_HOURS="${TTL_HOURS:-3}" > "$ev/apply.txt" 2>&1 || rc=$?
  t_apply=$((SECONDS - t0)); tail -n 4 "$ev/apply.txt" | tee -a "$ev/rebuild.txt"
  ((rc == 0)) || die "apply failed (see $ev/apply.txt)"
  if [[ -n "$dir" ]]; then cmd_stage --backup "$dir" > "$ev/stage.txt" 2>&1; else cmd_stage --seed > "$ev/stage.txt" 2>&1; fi
  t_stage=$((SECONDS - t0))
  local restore=("RestorePrefix=seed" "RestoreName=baseline-s")
  [[ -n "$dir" ]] && restore=("RestorePrefix=$VM_HOST" "RestoreName=$(basename "$dir")")
  cmd_provision "$FULL_STEPS" "${restore[@]}" > "$ev/provision.txt" 2>&1 || rc=$?
  t_prov=$((SECONDS - t0))
  ((rc == 0)) || { tail -n 30 "$ev/provision.txt"; die "provision failed"; }
  cmd_smoke "rebuild" > "$ev/smoke.txt" 2>&1 || rc=$?
  t_smoke=$((SECONDS - t0))
  printf 'apply_end_s=%s stage_end_s=%s provision_end_s=%s smoke_end_s=%s smoke_rc=%s end=%s\n' \
    "$t_apply" "$t_stage" "$t_prov" "$t_smoke" "$rc" "$(now)" | tee -a "$ev/rebuild.txt"
  return $rc
}

cmd_local_sqlupdate() {
  need_lock
  [[ -f "$TD_W/plan/handoffs/r1-baseline.md" ]] || die "plan/handoffs/r1-baseline.md does not exist yet: the local CU waits for the baseline"
  local ev="$EVID/local-sqlupdate" pkg sha
  mkdir -p "$ev"
  pkg="$(sed -n "s/^    \[string\]\$PackageUri = '\(.*\)',$/\1/p" "$REPO/ops/windows/Update-TdSqlServer.ps1")"
  sha="$(sed -n "s/^    \[string\]\$PackageSha256 = '\(.*\)',$/\1/p" "$REPO/ops/windows/Update-TdSqlServer.ps1")"
  mkdir -p "$TD_W/vm/media"
  local f="$TD_W/vm/media/$(basename "$pkg")"
  [[ -f "$f" ]] && [[ "$(sha256sum "$f" | cut -c1-64)" == "${sha,,}" ]] || curl -fsSL -o "$f" "$pkg"
  [[ "$(sha256sum "$f" | cut -c1-64)" == "${sha,,}" ]] || die "CU package SHA-256 mismatch"
  { log "reset to baseline-s"; "$V" stop; "$V" reset baseline-s; "$V" start; "$V" wait-ssh 900
    "$V" ssh 'New-Item -ItemType Directory -Force C:\TdMedia\sql2022-cu | Out-Null'
    "$V" scp "$f" "vm:C:/TdMedia/sql2022-cu/$(basename "$f")"
    "$V" scp "$REPO/ops/windows/Update-TdSqlServer.ps1" "$REPO/ops/windows/TdOps.psm1" "vm:C:/TdOps/windows/"
    log "patch"; "$V" ssh 'powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\TdOps\windows\Update-TdSqlServer.ps1'
    wait_local_app 900 || log "local app not answering after 900 s"
    log "stop and save snapshot baseline-s-cu (baseline-s kept)"; "$V" stop
    if qemu-img snapshot -l "$TD_W/vm/ws2025-legacy.qcow2" | awk '{print $2}' | grep -qx baseline-s-cu; then
      qemu-img snapshot -d baseline-s-cu "$TD_W/vm/ws2025-legacy.qcow2"; fi
    "$V" snapshot baseline-s-cu; "$V" snapshots
    log "back to baseline-s (the baseline's reference state)"; "$V" reset baseline-s; "$V" start; "$V" wait-ssh 900; wait_local_app 900
  } 2>&1 | tee "$ev/local-sqlupdate.txt"
}

# Sourced by ops/azure/replatform.sh for the helpers above: no dispatch then.
[[ "${BASH_SOURCE[0]}" == "$0" ]] || return 0
[[ $# -gt 0 ]] || { sed -n '2,27p' "$0"; exit 2; }
c="$1"; shift
case "$c" in
  env) cmd_env ;;
  cert) cmd_cert ;;
  fetch-publish) cmd_fetch_publish ;;
  stage) cmd_stage "$@" ;;
  provision) cmd_provision "$@" ;;
  smoke) cmd_smoke "$@" ;;
  pull-backup) cmd_pull_backup ;;
  backup-drill) cmd_backup_drill ;;
  cutover) cmd_cutover ;;
  rebuild) cmd_rebuild "$@" ;;
  local-sqlupdate) cmd_local_sqlupdate ;;
  *) sed -n '2,27p' "$0"; exit 2 ;;
esac
