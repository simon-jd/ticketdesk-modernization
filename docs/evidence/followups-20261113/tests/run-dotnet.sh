#!/usr/bin/env bash
# run-dotnet.sh LABEL [dotnet test args...]: Release build + dotnet test of the solution in wt-followups with the CI's
# test environment (values read by name from $TD_W/.secrets, never printed). Output: scratch-followups/runs/LABEL/
set -euo pipefail
LABEL="$1"; shift
REPO=$HOME/Desktop/ticketdesk-modernization/wt-followups
OUTD=$HOME/Desktop/ticketdesk-modernization/scratch-followups/runs/$LABEL
rm -rf "$OUTD"; mkdir -p "$OUTD"
export CI_OUT="$OUTD/ci-out"
. "$HOME/Desktop/ticketdesk-modernization/.tools/env.sh"
. "$REPO/azure-pipelines/scripts/ci-lib.sh"
exec 3>/dev/null
cd "$REPO"
ci_test_env
SLN=src/modern/TicketDesk.Modern.sln
{ echo "# $(date -u +%FT%TZ) git $(git rev-parse --short HEAD)$(git diff --quiet HEAD -- src tools || echo ' (+uncommitted)') load $(cut -d' ' -f1-3 /proc/loadavg)"; } > "$OUTD/env.txt"
if [[ "${NOBUILD:-}" != 1 ]]; then dotnet build "$SLN" -c Release -nologo -v quiet -p:TdBuildWeb=false > "$OUTD/build.txt" 2>&1 || { tail -30 "$OUTD/build.txt"; exit 1; }; fi
set +e
dotnet test --solution "$SLN" -c Release --no-build --results-directory "$OUTD/trx" --report-xunit-trx "$@" > "$OUTD/test.txt" 2>&1
rc=$?
set -e
sed 's/\x1b\[[0-9;]*m//g' "$OUTD/test.txt" > "$OUTD/test-plain.txt"
{ grep -E '^failed ' "$OUTD/test-plain.txt" || true; tail -7 "$OUTD/test-plain.txt" | grep -E '^\s+(total|failed|succeeded|skipped|duration):'; echo "rc=$rc"; } > "$OUTD/summary.txt"
cat "$OUTD/summary.txt"
