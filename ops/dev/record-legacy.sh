#!/usr/bin/env bash
# Record every scripted journey against the legacy app twice, each time from the baseline-s VM snapshot, and compare the
# two recordings (shows what the normalizers still let through on an unchanged app). Evidence: docs/evidence/replay-legacy-<date>/
# unless --out-dir is given.
#
#   TD_LOCK_OWNER=<you> ops/dev/record-legacy.sh [--no-reset] [--runs N] [--out-dir DIR] [--journeys a,b]
#
# The VM reset needs the shared VM lock (ops/onprem/vm/vmlock.sh); --no-reset records against the current state.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
SECRETS="$TD_W/.secrets/legacy-lab.env"
SEED_SECRETS="$TD_W/.secrets/td-seed.env"
. "$TD_W/.tools/env.sh"
TARGET="${TD_LEGACY_URL:-$(sed -n 's/^TD_URL=//p' "$SECRETS" | tail -1)}"
TARGET="${TARGET:-https://localhost:8443/}"
RESET=1; RUNS=2; E="$REPO/docs/evidence/replay-legacy-$(date -u +%Y%m%d)"; EXTRA=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-reset) RESET=0; shift ;;
    --runs) RUNS="$2"; shift 2 ;;
    --out-dir) E="$2"; shift 2 ;;
    *) EXTRA+=("$1"); shift ;;
  esac
done
mkdir -p "$E"
cd "$REPO"
dotnet build tools/Replay/TicketDesk.Replay.Cli -c Release -v quiet -nologo >/dev/null
BIN=tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay
COMMON=(--target "$TARGET" --insecure --secrets-file "$SECRETS" --secrets-file "$SEED_SECRETS" --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL
        --routes docs/migration/routes.csv "${EXTRA[@]}")
echo "# date: $(date -u +%FT%TZ)  git: $(git rev-parse --short HEAD)  target: $TARGET  reset: $RESET (snapshot baseline-s)" > "$E/environment.txt"
for run in $(seq 1 "$RUNS"); do
  if (( RESET )); then "$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$E/reset-run$run.txt" 2>&1; tail -1 "$E/reset-run$run.txt"; fi
  echo "\$ td-replay record ${COMMON[*]} --out $E/legacy-run$run.jsonl --run-id legacy-run$run" > "$E/record-run$run.txt"
  "$BIN" record "${COMMON[@]}" --out "$E/legacy-run$run.jsonl" --run-id "legacy-run$run" >> "$E/record-run$run.txt" 2>&1 || true
  tail -1 "$E/record-run$run.txt"
done
for run in $(seq 2 "$RUNS"); do
  echo "\$ td-replay compare --baseline $E/legacy-run1.jsonl --candidate $E/legacy-run$run.jsonl --out-dir $E/compare-run1-run$run" > "$E/compare-run$run.txt"
  "$BIN" compare --baseline "$E/legacy-run1.jsonl" --candidate "$E/legacy-run$run.jsonl" --out-dir "$E/compare-run1-run$run" >> "$E/compare-run$run.txt" 2>&1 || true
  tail -1 "$E/compare-run$run.txt"
done
