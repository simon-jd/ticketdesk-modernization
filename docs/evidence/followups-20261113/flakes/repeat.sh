#!/usr/bin/env bash
# repeat.sh LABEL N [--project P] [dotnet test args...]: N runs (no build) of the solution, or of one test project with
# --project; one summary line per run in runs/LABEL/runs.txt, failed test names collected in runs/LABEL/failed.txt.
set -uo pipefail
LABEL="$1"; N="$2"; shift 2
REPO=$HOME/Desktop/ticketdesk-modernization/wt-followups
D=$HOME/Desktop/ticketdesk-modernization/scratch-followups/runs/$LABEL
rm -rf "$D"; mkdir -p "$D"
export CI_OUT="$D/ci-out"
. "$HOME/Desktop/ticketdesk-modernization/.tools/env.sh"
. "$REPO/azure-pipelines/scripts/ci-lib.sh"
set +e
exec 3>/dev/null
cd "$REPO"
ci_test_env
TARGET=(--solution src/modern/TicketDesk.Modern.sln)
if [[ "${1:-}" == --project ]]; then TARGET=(--project "$2"); shift 2; fi
echo "# $(date -u +%FT%TZ) git $(git rev-parse --short HEAD)$(git diff --quiet HEAD -- src tools || echo ' (+uncommitted)') target ${TARGET[*]} args $*" > "$D/runs.txt"
for i in $(seq 1 "$N"); do
  t0=$(date +%s.%N); l0=$(cut -d' ' -f1 /proc/loadavg)
  dotnet test "${TARGET[@]}" -c Release --no-build "$@" > "$D/run-$i.txt" 2>&1; rc=$?
  t1=$(date +%s.%N)
  sed -i 's/\x1b\[[0-9;]*m//g' "$D/run-$i.txt"
  tot=$(grep -E '^\s+total:' "$D/run-$i.txt" | tail -1 | awk '{print $2}'); fl=$(grep -E '^\s+failed:' "$D/run-$i.txt" | tail -1 | awk '{print $2}')
  grep -E '^failed ' "$D/run-$i.txt" | sed "s/^/run $i: /" >> "$D/failed.txt"
  printf 'run %d rc=%d total=%s failed=%s %.1fs load=%s\n' "$i" "$rc" "$tot" "$fl" "$(echo "$t1 - $t0" | bc)" "$l0" >> "$D/runs.txt"
  if [[ "$rc" == 0 ]]; then rm -f "$D/run-$i.txt"; fi
done
echo "# done $(date -u +%FT%TZ): $(grep -c 'rc=0' "$D/runs.txt")/$N clean" >> "$D/runs.txt"
