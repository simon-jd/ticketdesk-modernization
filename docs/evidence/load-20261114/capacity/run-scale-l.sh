#!/usr/bin/env bash
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; S=$W/wt-load/ops/load/scale-l.sh; O=$W/wt-load/docs/evidence/load-20261114/capacity/scale-l
export TD_LOAD_WARMUP_S=30 TD_LOAD_MEASURE_S=120
$S prepare $O && echo "L-PREPARED $(date -u +%FT%TZ)" || { echo L-PREPARE-FAILED; exit 1; }
$S run $O 50 100 150 200; $W/wt-load/ops/load/capacity.sh stop-new
echo "L-DONE $(date -u +%FT%TZ)"
