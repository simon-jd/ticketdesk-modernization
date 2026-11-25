#!/usr/bin/env bash
# scale L, the new app with the ticket center read through /api/tickets (the shipped Angular page's list): TD_LIST_API=1
set -uo pipefail
W=~/Desktop/ticketdesk-modernization; C=$W/wt-load/ops/load/capacity.sh; O=$W/wt-load/docs/evidence/load-20261114/capacity/scale-l-api
export TD_LOAD_WARMUP_S=30 TD_LOAD_MEASURE_S=120 TD_TICKETS=1000000 TD_LIST_API=1
mkdir -p $O; $C start-new
for r in 25 50 100 150 200; do
  $C step new $r $O/rate-$r | tee -a $O/steps.txt
  python3 -I -c 'import csv, json, sys
s = json.load(open(sys.argv[1])); a = s["all"]
d = sum(int(x["dropped_iterations"] or 0) for x in csv.DictReader(open(sys.argv[2])) if x["phase"] == "measure")
sys.exit(0 if a["duration_ms"]["p(95)"] > 5000 or a["failed"]["rate"] > 0.2 or d > 0.2 * s["rate_per_s"] * s["measure_s"] else 1)' "$O/rate-$r/summary.json" "$O/rate-$r/timeline.csv" && { echo "stopped after $r (far past breaking)" >> $O/steps.txt; break; }
done
$C stop-new; echo "L-API-DONE $(date -u +%FT%TZ)"
