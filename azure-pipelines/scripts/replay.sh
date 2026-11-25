#!/usr/bin/env bash
# CI: golden replay against the new app alone (app.sh, database copy TicketDesk_ci_replay; no legacy VM, so no VM lock):
# td-replay replays tests/characterization/golden/legacy-golden.jsonl.xz (recorded against the legacy app on baseline-s),
# compares each answer with the legacy one, and ops/dev/replay-summary.py classifies every mismatch with
# docs/migration/replay-explanations.json. Gate (replay-gate.py): every unexplained mismatch must be in the ratchet list
# azure-pipelines/replay-allowed.txt, which only shrinks (0 rows = 0 unexplained).
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
. "$(dirname "${BASH_SOURCE[0]}")/app.sh"
cd "$CI_REPO"
ci_require_services td-sql
ci_sql_env
for k in TD_ADMIN_EMAIL TD_ADMIN_NAME TD_ADMIN_PASSWORD; do ci_secret legacy-lab.env "$k" >/dev/null; done
ci_secret td-seed.env TD_SEED_USER_PASSWORD >/dev/null
OUT="$CI_OUT/replay"; rm -rf "$OUT"; mkdir -p "$OUT"
dotnet build tools/Replay/TicketDesk.Replay.Cli -c Release -nologo -v quiet >/dev/null
BIN=tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay
ci_app_start replay "${CI_REPLAY_PORT:-5492}" "$OUT"
GOLDEN=tests/characterization/golden/legacy-golden.jsonl.xz
BASE="$APP_WORK/golden.jsonl"; xz -dc "$GOLDEN" > "$BASE"
echo "# golden $GOLDEN sha256 $(sha256sum "$GOLDEN" | cut -c1-16)  target $APP_URL (new app alone, $APP_DB)  git $(git rev-parse --short HEAD)" | tee "$OUT/environment.txt"
"$BIN" replay --from "$BASE" --target "$APP_URL/" --insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" \
  --secrets-file "$TD_W/.secrets/td-seed.env" --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes docs/migration/routes.csv \
  --out "$APP_WORK/candidate.jsonl" --run-id ci > "$OUT/replay.txt" 2>&1 || true
tail -1 "$OUT/replay.txt"
"$BIN" compare --baseline "$BASE" --candidate "$APP_WORK/candidate.jsonl" --out-dir "$OUT/compare" > "$OUT/compare.txt" 2>&1 || true
tail -1 "$OUT/compare.txt"
python3 -I ops/dev/replay-summary.py --candidate "$APP_WORK/candidate.jsonl" --report "$OUT/compare/report.json" \
  --routes docs/migration/routes.csv --explain docs/migration/replay-explanations.json --registry "$APP_REGISTRY" \
  --out "$OUT/summary.md" || true
sed -n '3p' "$OUT/summary.md"
# gate: no unexplained mismatch outside the ratchet list (azure-pipelines/replay-allowed.txt)
python3 -I azure-pipelines/scripts/replay-gate.py "$OUT/summary.md" azure-pipelines/replay-allowed.txt | tee "$OUT/gate.txt"
