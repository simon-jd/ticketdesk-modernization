#!/usr/bin/env bash
# Replay the golden recording against the new app ALONE (no legacy app, no gateway: Round 4, r4-retire) and compare it with
# the recording made directly against the legacy app. The app runs from this checkout on a fresh copy of td-sql's TicketDesk
# (baseline-s data, migrated through the contract stage by ops/dev/e2e-db.sh), with a local attachment folder, behind
# Kestrel with the lab certificate (the recording was made over HTTPS, so cookies keep their Secure attribute).
#
#   ops/dev/replay-modern.sh --from GOLDEN.jsonl[.xz] --out-dir DIR [--registry FILE] [--db NAME] [--url https://localhost:5473/]
#
# --registry: the migration registry (default: the shipped src/modern/TicketDesk.Web/migration-registry.json, i.e. the three
# Angular pages on Angular; td-replay reads the server page behind an Angular answer). --db: the copy's name (default
# TicketDesk_replay, dropped and re-created). No VM, no lock. Output: DIR/{environment,db,replay,compare}.txt, DIR/modern.jsonl,
# DIR/compare-golden-modern/, DIR/summary.md (replay-summary.py: every mismatch explained or UNEXPLAINED), DIR/app.log.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
FROM=""; OUT=""; REGISTRY="$REPO/src/modern/TicketDesk.Web/migration-registry.json"; DB=TicketDesk_replay; URL="https://localhost:5473/"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --from) FROM="$2"; shift 2 ;;
    --out-dir) OUT="$2"; shift 2 ;;
    --registry) REGISTRY="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"; shift 2 ;;
    --db) DB="$2"; shift 2 ;;
    --url) URL="${2%/}/"; shift 2 ;;
    *) sed -n '2,13p' "$0"; exit 2 ;;
  esac
done
[[ -n "$FROM" && -n "$OUT" ]] || { sed -n '2,13p' "$0"; exit 2; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"
WORK="$(mktemp -d /tmp/td-replay-modern-XXXXXX)"
APP_PID=""
cleanup() { if [[ -n "$APP_PID" ]]; then kill "$APP_PID" 2>/dev/null || true; wait "$APP_PID" 2>/dev/null || true; fi; rm -rf "$WORK"; }
trap cleanup EXIT

dotnet build "$REPO/src/modern/TicketDesk.Web" -c Release -v quiet -nologo >/dev/null
dotnet build "$REPO/tools/Replay/TicketDesk.Replay.Cli" -c Release -v quiet -nologo >/dev/null
dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
BASELINE="$FROM"
case "$FROM" in
  *.xz) BASELINE="$WORK/baseline.jsonl"; xz -dc "$FROM" > "$BASELINE" ;;
  *.gz) BASELINE="$WORK/baseline.jsonl"; gunzip -c "$FROM" > "$BASELINE" ;;
esac

# the data: a fresh copy of the baseline database, contracted; full-text indexes populated before the search journey
{ echo "\$ ops/dev/e2e-db.sh $DB"; "$HERE/e2e-db.sh" "$DB"; } > "$OUT/db.txt" 2>&1
CONN="$("$HERE/td-sql.sh" conn "$DB")"
{ echo "\$ td-db status | td-db fulltext --wait 300"; TD_DB_CONN="$CONN" "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" status | tail -3
  TD_DB_CONN="$CONN" "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" fulltext --wait 300; } >> "$OUT/db.txt" 2>&1
mkdir -p "$WORK/attachments"
cp "$REGISTRY" "$WORK/migration-registry.json"

"$HERE/serve-modern.sh" --url "$URL" --registry "$WORK/migration-registry.json" --attachments "$WORK/attachments" --db "$DB" > "$OUT/app.log" 2>&1 &
APP_PID=$!
for _ in $(seq 1 60); do curl -ksf "${URL}health/ready" >/dev/null 2>&1 && break; sleep 1; done
curl -ksf "${URL}health/ready" >/dev/null || { echo "the app did not get ready" >&2; tail -20 "$OUT/app.log" >&2; exit 1; }

{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet HEAD -- src tools || echo ' (+ uncommitted changes)')"
  echo "# target: the new app alone at $URL (TicketDesk.Web Release, Kestrel, lab certificate); no legacy app, no gateway"
  echo "# baseline recording: $FROM ($(sha256sum "$FROM" | cut -c1-64))"
  echo "# database: td-sql $DB = fresh copy of TicketDesk, td-db through the contract stage; attachments: empty local folder"
  echo "# registry: ${REGISTRY#$REPO/} ($(python3 -I -c 'import json,sys; r=json.load(open(sys.argv[1]))["Migration"]["Routes"]; print(", ".join(f"{k}={v}" for k,v in r.items()))' "$REGISTRY"))"
  echo "# host load: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$OUT/environment.txt"
curl -ksf "${URL}api/migration/routes" > "$OUT/migration-status.json" || true

BIN="$REPO/tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay"
COMMON=(--target "$URL" --insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" --secrets-file "$TD_W/.secrets/td-seed.env"
        --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes "$REPO/docs/migration/routes.csv")
echo "\$ td-replay replay --from $FROM ${COMMON[*]} --out $OUT/modern.jsonl --run-id modern" > "$OUT/replay.txt"
"$BIN" replay --from "$BASELINE" "${COMMON[@]}" --out "$OUT/modern.jsonl" --run-id modern >> "$OUT/replay.txt" 2>&1 || true
tail -1 "$OUT/replay.txt"
echo "\$ td-replay compare --baseline $FROM --candidate $OUT/modern.jsonl --out-dir $OUT/compare-golden-modern" > "$OUT/compare.txt"
"$BIN" compare --baseline "$BASELINE" --candidate "$OUT/modern.jsonl" --out-dir "$OUT/compare-golden-modern" >> "$OUT/compare.txt" 2>&1 || true
tail -1 "$OUT/compare.txt"
python3 -I "$HERE/replay-summary.py" --candidate "$OUT/modern.jsonl" --report "$OUT/compare-golden-modern/report.json" \
  --routes "$REPO/docs/migration/routes.csv" --explain "$REPO/docs/migration/replay-explanations.json" \
  --registry "$REGISTRY" --standalone --out "$OUT/summary.md" || true
head -3 "$OUT/summary.md"
for f in "$TD_W/.secrets/legacy-lab.env" "$TD_W/.secrets/td-seed.env"; do
  while IFS='=' read -r k v; do
    [[ "$k" == *PASSWORD* && -n "$v" ]] || continue
    if grep -rqF -e "$v" "$OUT"; then echo "a password is in the output: refusing" >&2; exit 1; fi
  done < "$f"
done
