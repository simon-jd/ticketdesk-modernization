#!/usr/bin/env bash
# Round 6 drills on kind (steady load RATE ops/s), then the restore drill x3
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; D=$W/wt-load/ops/load/drills.sh; RATE="${1:?rate}"
. $W/.tools/env.sh
$D up && $D db && $D deploy || { echo "SETUP-FAILED"; exit 1; }
python3 -I $W/wt-load/ops/k8s/app_smoke.py --base https://localhost:18453 --cacert $W/.secrets/legacy-lab-ca/ca.crt --read-only 2>&1 | tail -1
for d in pod-kill db-egress sql-restart worker-kill; do
  $D reset && $D drill $d "$RATE" | tail -3; echo "DRILL-$d-DONE $(date -u +%FT%TZ)"
done
# the same pod kill with two web replicas
helm --kube-context kind-td-load upgrade td $W/wt-load/ops/k8s/ticketdesk -n ticketdesk --reuse-values --set web.replicas=2 --wait --timeout 5m > /dev/null
$D reset && $D drill pod-kill "$RATE" 2replicas | tail -3; echo "DRILL-pod-kill-2replicas-DONE $(date -u +%FT%TZ)"
helm --kube-context kind-td-load upgrade td $W/wt-load/ops/k8s/ticketdesk -n ticketdesk --reuse-values --set web.replicas=1 --wait --timeout 5m > /dev/null
for n in 1 2 3; do $D restore-drill "$n" 40 | tail -4; echo "RESTORE-$n-DONE $(date -u +%FT%TZ)"; done
echo DRILLS-DONE
