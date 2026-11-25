#!/usr/bin/env bash
# CI: Angular front end (src/web): npm ci (agent-local npm cache), Vitest through `ng test` with a JUnit report, production
# build into src/modern/TicketDesk.Web/wwwroot/app (the .NET build and the web image use it).
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
cd "$CI_REPO/src/web"
OUT="$CI_OUT/tests/web"; mkdir -p "$OUT"
node --version; npm --version
ci_section "npm ci"
npm ci --no-audit --no-fund
ci_section "Vitest"
npm test -- --reporters=default --reporters=junit --output-file="$OUT/vitest-junit.xml"
python3 -I "$CI_REPO/azure-pipelines/scripts/test-counts.py" junit "$OUT/vitest-junit.xml" | tee "$OUT/summary.txt"
ci_section "ng build"
npm run build
test -f "$CI_REPO/src/modern/TicketDesk.Web/wwwroot/app/index.html"
