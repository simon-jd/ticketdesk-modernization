#!/usr/bin/env bash
# Lighthouse + axe "after" for the Angular pages, with the round-1 settings (tests/frontend/frontend-baseline.mjs: Lighthouse
# 12.8.2 desktop preset, 3 runs, median by performance; axe-core 4.14.0 default rules), back to back in one session:
#   DIR/legacy        the legacy app directly (https://localhost:8443), the "before" re-measured now
#   DIR/server-pages  through the gateway with the three pages on their server (Razor) pages (registry mode New)
#   DIR/angular       through the gateway with the three pages on Angular (the shipped registry)
#
#   TD_LOCK_OWNER=<you> ops/dev/frontend-after.sh --out-dir DIR            [needs the VM lock]
#
# The VM is reset to $TD_BASELINE_SNAPSHOT (default baseline-s-identity) first; the gateway (shared cookie, VM database as
# td_modern_app) listens on $TD_E2E_URL (default https://localhost:5463) with a copy of the registry that this script edits.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
OUT=""
while [[ $# -gt 0 ]]; do case "$1" in --out-dir) OUT="$2"; shift 2 ;; *) sed -n '2,13p' "$0"; exit 2 ;; esac; done
[[ -n "$OUT" ]] || { sed -n '2,13p' "$0"; exit 2; }
[[ -n "${TD_LOCK_OWNER:-}" ]] && "$REPO/ops/onprem/vm/vmlock.sh" check "$TD_LOCK_OWNER" >/dev/null || { echo "take the VM lock first (TD_LOCK_OWNER)" >&2; exit 1; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"
URL="${TD_E2E_URL:-https://localhost:5463}"
WORK="$(mktemp -d /tmp/td-frontend-XXXXXX)"
REGISTRY="$WORK/migration-registry.json"
cp "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$REGISTRY"

"$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$OUT/reset.txt" 2>&1; tail -1 "$OUT/reset.txt"
export TD_MODERN_DB=vm TD_MODERN_LOGIN=app
"$HERE/td-db-app-login.sh" apply > "$OUT/app-login.txt" 2>&1; tail -1 "$OUT/app-login.txt"
TD_GATEWAY_URL="$URL/" "$HERE/replay-gateway.sh" --serve --registry "$REGISTRY" --shared-cookie > "$OUT/gateway.log" 2>&1 &
GW=$!
trap 'kill $GW 2>/dev/null || true; wait $GW 2>/dev/null || true; rm -rf "$WORK"' EXIT
for _ in $(seq 1 120); do curl -ksf "$URL/health/live" >/dev/null 2>&1 && break; sleep 1; done
curl -ksf "$URL/health/live" >/dev/null || { echo "gateway did not start" >&2; tail -20 "$OUT/gateway.log" >&2; exit 1; }
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  gateway: $URL -> https://localhost:8443/"
  echo "# snapshot: ${TD_BASELINE_SNAPSHOT:-baseline-s-identity}  new-app db: VM (td_modern_app)  shared cookie: on"
  echo "# node $(node --version); host load: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$OUT/environment.txt"

angular_pages() { curl -ks "$URL/api/migration/routes" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["angularPages"])'; }
set_mode() {
  python3 -I - "$REGISTRY" "$1" <<'PY'
import json, os, sys
p, mode = sys.argv[1], sys.argv[2]
reg = json.load(open(p))
for rid in ("tickets.index", "ticket.view", "ticket.new.get"):
    reg["Migration"]["Routes"][rid] = mode
json.dump(reg, open(p + ".tmp", "w"), indent=2)
os.replace(p + ".tmp", p)
PY
}
measure() { (cd "$REPO/tests/frontend" && node frontend-baseline.mjs --base "$1" --out "$2" --runs 3) > "$2.txt" 2>&1; cat "$2.txt"; }

[[ -d "$REPO/tests/frontend/node_modules" ]] || (cd "$REPO/tests/frontend" && npm ci --no-audit --no-fund >/dev/null)
echo "== legacy (direct)"; measure https://localhost:8443 "$OUT/legacy"
set_mode New
for _ in $(seq 1 50); do [[ "$(angular_pages)" == 0 ]] && break; sleep 0.2; done
echo "== server pages (registry New), angular pages: $(angular_pages)"; measure "$URL" "$OUT/server-pages"
set_mode Angular
for _ in $(seq 1 50); do [[ "$(angular_pages)" == 3 ]] && break; sleep 0.2; done
echo "== angular, angular pages: $(angular_pages)"; measure "$URL" "$OUT/angular"
curl -ks "$URL/api/migration/routes" > "$OUT/migration-status.json"
python3 -I "$HERE/frontend-compare.py" --baseline "$REPO/docs/evidence/baseline-20261108/frontend/summary.json" \
  --legacy "$OUT/legacy/summary.json" --server "$OUT/server-pages/summary.json" --angular "$OUT/angular/summary.json" --out "$OUT/comparison.md"
