#!/usr/bin/env bash
# perf-attribution.sh - direct k6 runs (tests/perf/ticketdesk-mix.js) on several VM snapshots, interleaved
# (A B A B ...) so host-load drift hits every snapshot alike; each run starts from a fresh reset of its snapshot.
#
#   TD_LOCK_OWNER=<you> ops/dev/perf-attribution.sh --out-dir DIR [--runs 3] [--snapshots baseline-s-legacy,baseline-s]
#        [--prepare SNAP=CMD]   run CMD (bash -c, after the reset, before k6) for runs on SNAP; repeatable.
#                               A prepared variant is reported as SNAP+LABEL with --label SNAP=LABEL.
#        [--after CMD]          run CMD after every k6 run (e.g. ops/dev/legacy-query-stats.sh); TD_PERF_RAW=1 keeps the
#                               per-request k6 timeline (k6-raw.csv.gz)
#        [--target SNAP=TARGET] perf-baseline.sh target for runs on SNAP (default direct = the legacy app on the VM; modern =
#                               the new app alone on the VM's database, Round 4: legacy vs new back to back)
#
# Output: DIR/<snapshot>/direct-runN/{reset.txt,host-load.txt,k6.txt,summary.json}, DIR/<snapshot>/results.{json,md}
# (perf-baseline.sh --aggregate), DIR/summary.md. Needs the shared VM lock (perf-baseline.sh resets the VM).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT=""; RUNS=3; SNAPS="baseline-s-legacy,baseline-s"; AFTER=""
declare -A PREP=() LABEL=() TARGET=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT="$2"; shift 2 ;;
    --runs) RUNS="$2"; shift 2 ;;
    --snapshots) SNAPS="$2"; shift 2 ;;
    --prepare) PREP["${2%%=*}"]="${2#*=}"; shift 2 ;;
    --label) LABEL["${2%%=*}"]="${2#*=}"; shift 2 ;;
    --after) AFTER="$2"; shift 2 ;;
    --target) TARGET["${2%%=*}"]="${2#*=}"; shift 2 ;;
    *) sed -n '2,13p' "$0"; exit 2 ;;
  esac
done
[[ -n "$OUT" ]] || { sed -n '2,13p' "$0"; exit 2; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"
name() { echo "$1${LABEL[$1]:++${LABEL[$1]}}" | tr ':' '-'; }
for run in $(seq 1 "$RUNS"); do
  for entry in ${SNAPS//,/ }; do
    snap="${entry%%:*}" n="$(name "$entry")" t="${TARGET[$entry]:-direct}"
    tmp="$OUT/.tmp-$n-$run"; rm -rf "$tmp"
    TD_BASELINE_SNAPSHOT="$snap" TD_PERF_PREPARE="${PREP[$entry]:-}" TD_PERF_AFTER="$AFTER" \
      "$HERE/perf-baseline.sh" --out-dir "$tmp" --runs 1 --targets "$t" | sed "s/^/[$n] /"
    mkdir -p "$OUT/$n"
    mv "$tmp/$t-run1" "$OUT/$n/$t-run$run"
    [[ ! -f "$tmp/modern-registry.json" || -f "$OUT/$n/modern-registry.json" ]] || cp "$tmp/modern-registry.json" "$OUT/$n/"
    [[ -f "$OUT/$n/environment.txt" ]] || cp "$tmp/environment.txt" "$OUT/$n/environment.txt"
    [[ -f "$OUT/$n/users-helpdesk.json" ]] || cp "$tmp/users-helpdesk.json" "$OUT/$n/"
    rm -rf "$tmp"
  done
done
{
  echo "# p95 by snapshot and target (interleaved, $RUNS runs each)"; echo
  for entry in ${SNAPS//,/ }; do
    n="$(name "$entry")"; echo "## $n (target ${TARGET[$entry]:-direct})"; [[ -n "${PREP[$entry]:-}" ]] && echo "prepare: \`${PREP[$entry]}\`"; echo
    "$HERE/perf-baseline.sh" --aggregate "$OUT/$n" >/dev/null; cat "$OUT/$n/results.md"; echo
  done
} > "$OUT/summary.md"
cat "$OUT/summary.md"
