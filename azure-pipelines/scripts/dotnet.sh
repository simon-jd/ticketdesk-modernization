#!/usr/bin/env bash
# CI: restore (NuGet audit: TreatWarningsAsErrors in src/modern and tools turns NU1901-NU1904 into restore errors), Release
# build, every test of the solution (xUnit v3 on Microsoft.Testing.Platform) against the host's td-sql, Azurite and Service
# Bus emulator, TRX per test project, Cobertura coverage (dotnet-coverage), and a counts summary. Exit != 0 on any failure.
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
cd "$CI_REPO"
SLN=src/modern/TicketDesk.Modern.sln
COV_VERSION=18.11.2
COV="$TD_W/.tools/dotnet-coverage/dotnet-coverage"
ci_require_services td-sql td-azurite td-sb-emulator

ci_section "dotnet restore (NuGet audit)"
dotnet restore "$SLN" -nologo
ci_section "dotnet build -c Release"
dotnet build "$SLN" -c Release --no-restore -nologo -p:TdBuildWeb=false

if [[ ! -x "$COV" ]] || ! "$COV" --version 2>/dev/null | grep -q "$COV_VERSION"; then
  dotnet tool update dotnet-coverage --version "$COV_VERSION" --tool-path "$(dirname "$COV")"
fi
ci_test_env
RES="$CI_OUT/tests/dotnet"; rm -rf "$RES"; mkdir -p "$RES" "$CI_OUT/coverage"
ci_section "dotnet test (coverage)"
set +e
"$COV" collect --settings azure-pipelines/coverage.settings.xml -f cobertura -o "$CI_OUT/coverage/dotnet.cobertura.xml" \
  "dotnet test --solution $SLN -c Release --no-build --results-directory $RES/run --report-xunit-trx" 2>&1 | tee "$RES/dotnet-test.txt"
rc=${PIPESTATUS[0]}
set -e
python3 -I "$CI_REPO/azure-pipelines/scripts/test-counts.py" trx "$RES/run" | tee "$RES/summary.txt"
(( rc == 0 )) && exit 0

# Flaky-test policy: when at most 3 tests failed, re-run exactly those once (per test project, --filter-method). The step
# passes only if every one of them passes on the retry; each is reported as a warning (flaky) in the run and the summary.
# More failures, or a failure that repeats, fail the step (a real break). Both runs' TRX files are published.
mapfile -t FAILED < <(python3 -I "$CI_REPO/azure-pipelines/scripts/test-counts.py" failed "$RES/run")
if (( ${#FAILED[@]} == 0 || ${#FAILED[@]} > 3 )); then
  echo "##vso[task.logissue type=error]${#FAILED[@]} failed tests: no retry" >&3; exit "$rc"
fi
ci_section "retry of ${#FAILED[@]} failed test(s)"
retry_rc=0
for f in "${FAILED[@]}"; do
  proj="${f%%|*}"; method="${f#*|}"
  echo "retry: $method ($proj)" | tee -a "$RES/summary.txt"
  dotnet test --project "$proj" -c Release --no-build --results-directory "$RES/retry" --report-xunit-trx --filter-method "$method" \
    >> "$RES/dotnet-test-retry.txt" 2>&1 || retry_rc=1
done
python3 -I "$CI_REPO/azure-pipelines/scripts/test-counts.py" trx "$RES/retry" | sed 's/^/retry /' | tee -a "$RES/summary.txt"
if (( retry_rc != 0 )); then
  echo "##vso[task.logissue type=error]failed again on retry: ${FAILED[*]#*|}" >&3; exit 1
fi
for f in "${FAILED[@]}"; do echo "##vso[task.logissue type=warning]flaky test (failed, passed on retry): ${f#*|}" >&3; done
echo "FLAKY (passed on retry): ${FAILED[*]#*|}" | tee -a "$RES/summary.txt"
