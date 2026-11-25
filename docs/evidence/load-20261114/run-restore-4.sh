#!/usr/bin/env bash
# a 4th restore run (run 1's RTO was not measured: its single smoke attempt hit the old pod)
W=~/Desktop/ticketdesk-modernization
until grep -q DRILLS-DONE $W/logs/load/drills2.log; do sleep 10; done
. $W/.tools/env.sh
$W/wt-load/ops/load/drills.sh restore-drill 4 40 > $W/logs/load/restore-4.out 2>&1; tail -4 $W/logs/load/restore-4.out; echo "RESTORE-4-DONE $(date -u +%FT%TZ)"
