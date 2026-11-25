#!/usr/bin/env bash
# CI: Playwright suite of the Angular pages (src/web/e2e) against the new app alone (app.sh; same mode as
# ops/dev/e2e.sh --standalone, but its own database copy TicketDesk_ci_e2e and port, so it never collides with an agent's
# run). The suite reads the lab admin / seeded user from $TD_W/.secrets (host). JUnit + JSON results in $CI_OUT/tests/e2e.
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
. "$(dirname "${BASH_SOURCE[0]}")/app.sh"
cd "$CI_REPO"
ci_require_services td-sql
ci_sql_env
for k in TD_ADMIN_EMAIL TD_ADMIN_PASSWORD; do ci_secret legacy-lab.env "$k" >/dev/null; done
ci_secret td-seed.env TD_SEED_USER_PASSWORD >/dev/null
OUT="$CI_OUT/tests/e2e"; rm -rf "$OUT"; mkdir -p "$OUT"
ci_app_start e2e "${CI_E2E_PORT:-5491}" "$OUT"
[[ -d src/web/node_modules ]] || (cd src/web && npm ci --no-audit --no-fund)
set +e
(cd src/web && rm -rf e2e/.auth && mkdir -p e2e/.auth && TD_E2E_BASE_URL="$APP_URL" TD_E2E_OUT="$OUT" TD_E2E_REGISTRY="$APP_REGISTRY" \
  PLAYWRIGHT_JUNIT_OUTPUT_NAME="$OUT/junit.xml" PLAYWRIGHT_JSON_OUTPUT_NAME="$OUT/results.json" \
  npx playwright test --reporter=list,junit,json)
rc=$?
set -e
rm -rf src/web/e2e/.auth
python3 -I "$CI_REPO/azure-pipelines/scripts/test-counts.py" junit "$OUT/junit.xml" | tee "$OUT/summary.txt"
exit "$rc"
