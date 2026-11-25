#!/usr/bin/env bash
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; C=$W/wt-load/ops/load/capacity.sh; O=$W/wt-load/docs/evidence/load-20261114/capacity/scale-l
export TD_LOAD_WARMUP_S=30 TD_LOAD_MEASURE_S=120 TD_TICKETS=1000000
$C start-new
for r in 5 10; do $C step new $r $O/rate-$r | tee -a $O/steps.txt; done
$C stop-new; echo "L-LOW-DONE $(date -u +%FT%TZ)"
