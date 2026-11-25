#!/usr/bin/env bash
# Round 6 drills, second part (after the worker-kill drill's first attempt died at its PID lookup): worker kill, the pod kill
# with two web replicas, then the restore drill x3
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; D=$W/wt-load/ops/load/drills.sh; RATE="${1:?rate}"
. $W/.tools/env.sh
$D reset && $D drill worker-kill "$RATE" > $W/logs/load/drill-worker-kill.out 2>&1; tail -3 $W/logs/load/drill-worker-kill.out; echo "DRILL-worker-kill-DONE $(date -u +%FT%TZ)"
helm --kube-context kind-td-load upgrade td $W/wt-load/ops/k8s/ticketdesk -n ticketdesk --reuse-values --set web.replicas=2 --wait --timeout 5m > /dev/null
$D reset && $D drill pod-kill "$RATE" 2replicas > $W/logs/load/drill-pod-kill-2.out 2>&1; tail -3 $W/logs/load/drill-pod-kill-2.out; echo "DRILL-pod-kill-2replicas-DONE $(date -u +%FT%TZ)"
helm --kube-context kind-td-load upgrade td $W/wt-load/ops/k8s/ticketdesk -n ticketdesk --reuse-values --set web.replicas=1 --wait --timeout 5m > /dev/null
for n in 1 2 3; do $D restore-drill "$n" 40 > $W/logs/load/restore-$n.out 2>&1; tail -4 $W/logs/load/restore-$n.out; echo "RESTORE-$n-DONE $(date -u +%FT%TZ)"; done
echo DRILLS-DONE
