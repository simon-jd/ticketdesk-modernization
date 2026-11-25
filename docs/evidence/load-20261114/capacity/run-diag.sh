#!/usr/bin/env bash
# Round 6: the legacy 4th curve (25, 30: r3 stopped after its 20 step), then the two diagnostic variants
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; C=$W/wt-load/ops/load/capacity.sh; G=$W/wt-load/ops/load/diag.sh
O=$W/wt-load/docs/evidence/load-20261114/capacity
export TD_LOCK_OWNER=load TD_LOAD_WARMUP_S=30 TD_LOAD_MEASURE_S=120
$C curve legacy $O/runs/legacy-r4 25 30; echo "LEGACY-R4-DONE $(date -u +%FT%TZ)"
mkdir -p $O/diag/legacy-minthreads-r1
$G legacy-threads-on 2>&1 | tee $O/diag/legacy-minthreads-r1/config.txt
$C curve legacy $O/diag/legacy-minthreads-r1 20 25 30 40
$G legacy-threads-off 2>&1 | tee -a $O/diag/legacy-minthreads-r1/config.txt; echo "LEGACY-DIAG-DONE $(date -u +%FT%TZ)"
mkdir -p $O/diag/new-rcsi-r1
$G new-rcsi-base 2>&1 | tee $O/diag/new-rcsi-r1/config.txt
TD_LOAD_DB_BAK=new-base-rcsi.bak $C curve new $O/diag/new-rcsi-r1 150 200 250 300; $C stop-new; echo "NEW-DIAG-DONE $(date -u +%FT%TZ)"
echo DIAG-DONE
