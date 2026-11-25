#!/usr/bin/env bash
# Playwright suite of the Angular pages (src/web/e2e) against the new app alone, which this script starts and stops: a fresh
# copy of td-sql TicketDesk (ops/dev/e2e-db.sh, contracted), a local attachment folder, no legacy app (retired in Round 4; the
# gateway mode of Round 3 needs a checkout from before r4-retire, docs/dev/rollback.md).
#
#   ops/dev/e2e.sh --out-dir DIR [--runs 3]          (--standalone is accepted for the Round 3 command line)
#
# The app listens on $TD_E2E_URL (default https://localhost:5463; 5443/8443 belong to other users of the box) on the database copy
# $TD_E2E_DB (default TicketDesk_e2e; another name keeps two runs apart) with a copy of
# the shipped registry (the switch-back test edits that copy). Each run writes DIR/run-N/{results.json,summary.txt}; the
# runs are back to back without a reset (tests use unique titles). Exit 1 if any run failed.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
OUT=""; RUNS=3
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT="$2"; shift 2 ;;
    --runs) RUNS="$2"; shift 2 ;;
    --standalone) shift ;;
    *) sed -n '2,10p' "$0"; exit 2 ;;
  esac
done
[[ -n "$OUT" ]] || { sed -n '2,10p' "$0"; exit 2; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"
URL="${TD_E2E_URL:-https://localhost:5463}"
WORK="$(mktemp -d /tmp/td-e2e-XXXXXX)"
REGISTRY="$WORK/migration-registry.json"
cp "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$REGISTRY"
mkdir -p "$WORK/attachments"
DB="${TD_E2E_DB:-TicketDesk_e2e}"
"$HERE/e2e-db.sh" "$DB" > "$OUT/db.txt"
MODE="the new app alone (td-sql $DB, contracted; local attachments)"
dotnet build "$REPO/src/modern/TicketDesk.Web" -c Release -v quiet -nologo >/dev/null

"$HERE/serve-modern.sh" --url "$URL" --registry "$REGISTRY" --attachments "$WORK/attachments" --db "$DB" > "$OUT/server.log" 2>&1 &
SERVER=$!
cleanup() { kill "$SERVER" 2>/dev/null || true; wait "$SERVER" 2>/dev/null || true; rm -rf "$REPO/src/web/e2e/.auth" "$WORK"; }
trap cleanup EXIT
for _ in $(seq 1 120); do curl -ksf "$URL/health/live" >/dev/null 2>&1 && break; sleep 1; done
curl -ksf "$URL/health/live" >/dev/null || { echo "server did not start" >&2; tail -20 "$OUT/server.log" >&2; exit 1; }

{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  url: $URL  runs: $RUNS"
  echo "# mode: $MODE"
  echo "# node $(node --version), playwright $(cd "$REPO/src/web" && npx playwright --version), browser: chromium headless shell (Playwright's)"
  echo "# host load: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$OUT/environment.txt"
curl -ks "$URL/api/migration/routes" > "$OUT/migration-status.json" || true

failed=0
mkdir -p "$REPO/src/web/e2e/.auth"
for n in $(seq 1 "$RUNS"); do
  RUN="$OUT/run-$n"; mkdir -p "$RUN"
  set +e
  (cd "$REPO/src/web" && TD_E2E_BASE_URL="$URL" TD_E2E_OUT="$RUN" TD_E2E_REGISTRY="$REGISTRY" npx playwright test) > "$RUN/summary.txt" 2>&1
  code=$?
  set -e
  python3 -I - "$RUN/results.json" "$n" "$code" <<'PY' | tee -a "$OUT/runs.txt"
import json, sys
d = json.load(open(sys.argv[1]))
s = d["stats"]
print(f"run {sys.argv[2]}: exit {sys.argv[3]}, expected (passed) {s['expected']}, unexpected (failed) {s['unexpected']}, flaky {s['flaky']}, skipped {s['skipped']}, {s['duration']/1000:.1f} s")
PY
  (( code == 0 )) || failed=1
done
# keep the evidence small: no failure screenshots of passing runs exist; drop Playwright's per-test folders when empty
find "$OUT" -type d -name test-results -empty -delete 2>/dev/null || true
exit $failed
