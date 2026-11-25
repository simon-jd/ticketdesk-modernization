#!/usr/bin/env bash
# Round 6 capacity plan: 3 interleaved repeats per system (order alternates), 30 s warm-up + 120 s measured per step
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; C=$W/wt-load/ops/load/capacity.sh; O=$W/wt-load/docs/evidence/load-20261114/capacity/runs
export TD_LOCK_OWNER=load TD_LOAD_WARMUP_S=30 TD_LOAD_MEASURE_S=120
L="10 20 25 30 40"; N="100 150 200 225 250 300"
$W/logs/load/env-capture.sh > $O/../environment.txt 2>&1
for r in 1 2 3; do
  if (( r % 2 )); then
    $C curve legacy $O/legacy-r$r $L; $C curve new $O/new-r$r $N; $C stop-new
  else
    $C curve new $O/new-r$r $N; $C stop-new; $C curve legacy $O/legacy-r$r $L
  fi
  echo "REPEAT-$r-DONE $(date -u +%FT%TZ)"
done
echo PLAN-DONE
