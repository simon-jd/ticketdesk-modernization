#!/usr/bin/env bash
# replay-candidate.sh - golden replay against a changed legacy app, directly and through the gateway, each run from a fresh
# baseline-s: reset the VM, deploy the candidate, optional schema migration / preparation, replay, compare with the golden.
#
#   TD_LOCK_OWNER=<you> ops/dev/replay-candidate.sh --out-dir DIR [--package ZIP] [--migrate] [--prepare 'CMD']
#                                                    [--targets direct,gateway] [--no-reset]
#
#   --package ZIP   ops/onprem/legacy-artifact.sh deploy ZIP after each reset (omit: the app as the snapshot has it)
#   --migrate       td-db migrate on the VM's TicketDesk database, then restart the app pool (EF6 re-runs its startup check)
#   --prepare CMD   shell command run after deploy/migrate, before the replay (e.g. reset the lab blob store)
# Writes DIR/<target>/{candidate.jsonl, compare/report.md, ...} and DIR/summary.txt. Needs the shared VM lock.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
GOLDEN="$REPO/tests/characterization/golden/legacy-golden.jsonl.xz"
OUT=""; PKG=""; MIGRATE=0; PREPARE=""; TARGETS="direct,gateway"; RESET=1
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT="$2"; shift 2 ;;
    --package) PKG="$2"; shift 2 ;;
    --migrate) MIGRATE=1; shift ;;
    --prepare) PREPARE="$2"; shift 2 ;;
    --targets) TARGETS="$2"; shift 2 ;;
    --no-reset) RESET=0; shift ;;
    *) sed -n '2,13p' "$0"; exit 2 ;;
  esac
done
[[ -n "$OUT" ]] || { sed -n '2,13p' "$0"; exit 2; }
mkdir -p "$OUT"
log() { echo "$(date -u +%FT%TZ) $*" | tee -a "$OUT/summary.txt"; }
vm_conn() { printf 'Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=%s;TrustServerCertificate=True;Encrypt=True' \
  "$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")"; }
dotnet build "$REPO/tools/Replay/TicketDesk.Replay.Cli" -c Release -v quiet -nologo >/dev/null
BIN="$REPO/tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay"
if (( MIGRATE )); then dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null; fi
TDDB="$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db"
BASELINE="$OUT/golden.jsonl"; xz -dc "$GOLDEN" > "$BASELINE"
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  golden: $(sha256sum "$GOLDEN" | cut -c1-16)"
  echo "# package: ${PKG:-(snapshot app)} ${PKG:+sha256 $(sha256sum "$PKG" | cut -c1-64)}  migrate: $MIGRATE  prepare: ${PREPARE:-none}"
  echo "# host load: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$OUT/environment.txt"

prepare_target() {
  local dir="$1"
  if (( RESET )); then "$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$dir/reset.txt" 2>&1; log "$(tail -1 "$dir/reset.txt")"; fi
  if [[ -n "$PKG" ]]; then "$REPO/ops/onprem/legacy-artifact.sh" deploy "$PKG" > "$dir/deploy.txt" 2>&1 || { tail -20 "$dir/deploy.txt"; exit 1; }; log "$(tail -1 "$dir/deploy.txt")"; fi
  if (( MIGRATE )); then
    TD_DB_CONN="$(vm_conn)" "$TDDB" migrate > "$dir/migrate.txt" 2>&1 || { cat "$dir/migrate.txt"; exit 1; }
    log "td-db migrate: $(tail -1 "$dir/migrate.txt")"
    "$REPO/ops/onprem/vm/vmctl.sh" ssh 'Import-Module WebAdministration; Restart-WebAppPool TicketDesk; "app pool restarted"' >> "$dir/migrate.txt" 2>&1
  fi
  if [[ -n "$PREPARE" ]]; then bash -c "$PREPARE" > "$dir/prepare.txt" 2>&1 || { cat "$dir/prepare.txt"; exit 1; }; log "prepare: $(tail -1 "$dir/prepare.txt")"; fi
  "$REPO/ops/onprem/baseline/baseline-s.sh" wait-app >> "$dir/reset.txt" 2>&1
  "$REPO/ops/onprem/legacy-artifact.sh" info > "$dir/deployed.txt" 2>&1 || true
}

COMMON=(--insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" --secrets-file "$TD_W/.secrets/td-seed.env"
        --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes "$REPO/docs/migration/routes.csv")
for t in ${TARGETS//,/ }; do
  d="$OUT/$t"; mkdir -p "$d"
  log "== $t"
  prepare_target "$d"
  case "$t" in
    direct)
      echo "\$ td-replay replay --from golden --target https://localhost:8443/ ${COMMON[*]} --out $d/candidate.jsonl" > "$d/replay.txt"
      "$BIN" replay --from "$BASELINE" --target https://localhost:8443/ "${COMMON[@]}" --out "$d/candidate.jsonl" --run-id "candidate-direct" >> "$d/replay.txt" 2>&1 || true
      log "replay: $(tail -1 "$d/replay.txt")"
      echo "\$ td-replay compare --baseline golden --candidate $d/candidate.jsonl --out-dir $d/compare" > "$d/compare.txt"
      "$BIN" compare --baseline "$BASELINE" --candidate "$d/candidate.jsonl" --out-dir "$d/compare" >> "$d/compare.txt" 2>&1 || true
      log "compare: $(tail -1 "$d/compare.txt")" ;;
    gateway)
      "$REPO/ops/dev/replay-gateway.sh" --from "$GOLDEN" --out-dir "$d" --no-reset > "$d/gateway-run.txt" 2>&1 || true
      log "gateway: $(tail -2 "$d/gateway-run.txt" | tr '\n' ' ')" ;;
    *) echo "unknown target $t" >&2; exit 2 ;;
  esac
done
rm -f "$BASELINE"
