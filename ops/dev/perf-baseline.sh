#!/usr/bin/env bash
# Performance baseline: the k6 mix (tests/perf/ticketdesk-mix.js) against the legacy app directly, through the gateway
# (coexistence rounds; needs a checkout from before r4-retire) or against the new app alone (target modern, Round 4), each run
# from a fresh baseline-s reset, N runs per target, median of runs. Host load (uptime, docker stats, qemu CPU) is recorded
# before and after every run.
#
#   TD_LOCK_OWNER=<you> ops/dev/perf-baseline.sh --out-dir DIR [--runs 3] [--targets direct,gateway,modern]
#
# Target modern: TicketDesk.Web Release from this checkout (ops/dev/serve-modern.sh --vm-db) started after each reset and the
# run's prepare step, stopped after k6: the VM's SQL Express database as td_modern_app (the prepare step must create the login
# and contract the database: ops/dev/td-db-app-login.sh apply && ops/dev/vm-db-contract.sh), local attachments, every page as
# its server page (the mix parses server HTML; registry with the Angular pages on New), pinned to TD_MODERN_CPUS (default
# 24-27: as many cores as the legacy VM has vCPUs) on https://localhost:${TD_MODERN_PERF_PORT:-5483}.
#   ops/dev/perf-baseline.sh --aggregate DIR      (re-compute DIR/results.json + results.md from the run summaries)
#
# Rate/warm-up/measure come from TD_RATE (10 ops/s), TD_WARMUP_S (30), TD_MEASURE_S (60). Needs the shared VM lock.
# TD_GATEWAY_ARGS is passed to replay-gateway.sh --serve, e.g. "--waves 0,1,2,3" for the gateway with migrated routes.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
OUT=""; RUNS=3; TARGETS="direct,gateway"; AGG_ONLY=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT="$2"; shift 2 ;;
    --runs) RUNS="$2"; shift 2 ;;
    --targets) TARGETS="$2"; shift 2 ;;
    --aggregate) AGG_ONLY="$2"; shift 2 ;;
    *) sed -n '2,17p' "$0"; exit 2 ;;
  esac
done

aggregate() {
  python3 -I - "$1" <<'PY'
import json, glob, os, statistics, sys
d = sys.argv[1]
res = {}
for f in sorted(glob.glob(os.path.join(d, "*-run*", "summary.json"))):
    target = os.path.basename(os.path.dirname(f)).split("-run")[0]
    s = json.load(open(f))
    v = s["all"]["duration_ms"]; fl = s["all"]["failed"]
    row = {"run": os.path.basename(os.path.dirname(f)), "requests": v["count"], "p50": v["med"], "p95": v["p(95)"], "p99": v["p(99)"],
           "error_rate": fl["rate"], "checks": (s.get("checks") or {}).get("rate"),
           "dropped_iterations": (s.get("dropped_iterations") or {}).get("count", 0),
           "by_op_p95": {op: (x["duration_ms"] or {}).get("p(95)") for op, x in s["by_op"].items()}}
    res.setdefault(target, {"runs": []})["runs"].append(row)
    res[target]["settings"] = {k: s[k] for k in ("target", "rate_per_s", "warmup_s", "measure_s", "users")}
for t, r in res.items():
    runs = r["runs"]
    r["median"] = {k: statistics.median([x[k] for x in runs]) for k in ("p50", "p95", "p99", "error_rate", "requests")}
    r["median"]["by_op_p95"] = {op: statistics.median([x["by_op_p95"][op] for x in runs if x["by_op_p95"][op] is not None])
                                for op in runs[0]["by_op_p95"]}
json.dump(res, open(os.path.join(d, "results.json"), "w"), indent=2)
lines = ["| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |",
         "|---|---:|---:|---:|---:|---:|---:|---|"]
for t, r in res.items():
    m = r["median"]; o = m["by_op_p95"]
    lines.append(f"| {t} | {len(r['runs'])} | {m['requests']:.0f} | {m['p50']:.1f} | {m['p95']:.1f} | {m['p99']:.1f} | {m['error_rate']*100:.2f}% | "
                 + " / ".join(f"{o[k]:.1f}" for k in ("list", "view", "filter", "comment", "create")) + " |")
lines += ["", "Per run:", "", "| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
for t, r in res.items():
    for x in r["runs"]:
        lines.append(f"| {x['run']} | {x['requests']} | {x['p50']:.1f} | {x['p95']:.1f} | {x['p99']:.1f} | {x['error_rate']*100:.2f}% | "
                     f"{(x['checks'] or 0)*100:.2f}% | {x['dropped_iterations']} |")
open(os.path.join(d, "results.md"), "w").write("\n".join(lines) + "\n")
print("\n".join(lines[:2 + len(res)]))
PY
}
if [[ -n "$AGG_ONLY" ]]; then aggregate "$AGG_ONLY"; exit 0; fi
[[ -n "$OUT" ]] || { sed -n '2,17p' "$0"; exit 2; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"

host_load() {  # host conditions: other projects share this machine
  echo "## $(date -u +%FT%TZ) $1"
  uptime
  nproc; free -g | head -2
  echo "# docker stats (top 8 by CPU)"
  docker stats --no-stream --format '{{.CPUPerc}}\t{{.MemUsage}}\t{{.Name}}' | sort -rn | head -8
  echo "# legacy VM (qemu) process"
  ps -o pid,pcpu,rss,etime,args -p "$(cat "$TD_W/vm/run/qemu.pid")" | cut -c1-120
}

MIX="${TD_PERF_MIX:-$REPO/tests/perf/ticketdesk-mix.js}"  # TD_PERF_MIX: another mix file (e.g. an older revision)
USERS="$OUT/users-helpdesk.json"
cp "$REPO/tests/perf/users-helpdesk.json" "$USERS"
PW="$(sed -n 's/^TD_SEED_USER_PASSWORD=//p' "$TD_W/.secrets/td-seed.env")"
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  $(k6 version | head -1)"
  echo "# mix: ${MIX#$REPO/} ($(sha256sum "$MIX" | cut -c1-12))  rate ${TD_RATE:-10} ops/s  warm-up ${TD_WARMUP_S:-30} s  measure ${TD_MEASURE_S:-60} s  runs $RUNS"
  echo "# legacy VM: ${TD_VM_CPUS:-4} vCPU, ${TD_VM_MEM:-8G} (vmctl.sh), KVM, qemu user-mode networking, 127.0.0.1 forwards; k6 on the same host"
  echo "# gateway: TicketDesk.Web Release, Kestrel https://localhost:5443, YARP -> https://localhost:8443 (ops/dev/replay-gateway.sh --serve ${TD_GATEWAY_ARGS:-})"
  echo "# modern: TicketDesk.Web Release alone (ops/dev/serve-modern.sh --vm-db), Kestrel https://localhost:${TD_MODERN_PERF_PORT:-5483}, taskset -c ${TD_MODERN_CPUS:-24-27}, VM SQL Express as td_modern_app, server pages"
} > "$OUT/environment.txt"

GW_PID=""
stop_gw() {
  if [[ -n "$GW_PID" ]]; then kill "$GW_PID" 2>/dev/null || true; wait "$GW_PID" 2>/dev/null || true; GW_PID=""; fi
}
trap stop_gw EXIT
for target in ${TARGETS//,/ }; do
  case "$target" in
    direct) URL="https://localhost:8443" ;;
    gateway) URL="https://localhost:5443"
      # shellcheck disable=SC2086
      "$HERE/replay-gateway.sh" --serve ${TD_GATEWAY_ARGS:-} > "$OUT/gateway.log" 2>&1 & GW_PID=$!
      for _ in $(seq 1 60); do curl -ksf "$URL/health/live" >/dev/null 2>&1 && break; sleep 1; done ;;
    modern) URL="https://localhost:${TD_MODERN_PERF_PORT:-5483}"
      dotnet build "$REPO/src/modern/TicketDesk.Web" -c Release -v quiet -nologo >/dev/null
      MODERN_WORK="$(mktemp -d /tmp/td-perf-modern-XXXXXX)"; mkdir -p "$MODERN_WORK/attachments"
      python3 -I -c 'import json, sys
reg = json.load(open(sys.argv[1]))
reg["Migration"]["Routes"] = {k: "New" for k in reg["Migration"]["Routes"]}  # server pages: the mix reads their HTML
print(json.dumps(reg, indent=2))
' "$REPO/src/modern/TicketDesk.Web/migration-registry.json" > "$MODERN_WORK/migration-registry.json"
      cp "$MODERN_WORK/migration-registry.json" "$OUT/modern-registry.json" ;;
    *) echo "unknown target $target" >&2; exit 2 ;;
  esac
  for run in $(seq 1 "$RUNS"); do
    R="$OUT/$target-run$run"; mkdir -p "$R"
    "$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$R/reset.txt" 2>&1
    if [[ -n "${TD_PERF_PREPARE:-}" ]]; then  # optional variant step after the reset (perf-attribution.sh --prepare)
      { echo "\$ $TD_PERF_PREPARE"; bash -c "$TD_PERF_PREPARE"; } > "$R/prepare.txt" 2>&1
    fi
    if [[ "$target" == modern ]]; then  # the new app alone, started on the freshly reset (and prepared) database
      taskset -c "${TD_MODERN_CPUS:-24-27}" "$HERE/serve-modern.sh" --url "$URL" --registry "$MODERN_WORK/migration-registry.json" \
        --attachments "$MODERN_WORK/attachments" --vm-db > "$R/app.log" 2>&1 & GW_PID=$!
      for _ in $(seq 1 90); do curl -ksf "$URL/health/ready" >/dev/null 2>&1 && break; sleep 1; done
      { echo "\$ taskset -pc <app pid>"; taskset -pc "$GW_PID"; echo "\$ curl $URL/health/ready"; curl -ks "$URL/health/ready"; echo; } > "$R/app-start.txt" 2>&1
    fi
    host_load "before" > "$R/host-load.txt" 2>&1
    echo "\$ k6 run -e TD_TARGET=$URL -e TD_USERS_FILE=users-helpdesk.json -e TD_SUMMARY=summary.json ${MIX#$REPO/}" > "$R/k6.txt"
    K6_EXTRA=(); [[ "${TD_PERF_RAW:-0}" == 1 ]] && K6_EXTRA=(--out "csv=$R/k6-raw.csv.gz")  # per-request timeline (gzip)
    TD_PASSWORD="$PW" k6 run --no-color "${K6_EXTRA[@]}" -e TD_TARGET="$URL" -e TD_USERS_FILE="$USERS" -e TD_SUMMARY="$R/summary.json" \
      -e TD_RATE="${TD_RATE:-10}" -e TD_WARMUP_S="${TD_WARMUP_S:-30}" -e TD_MEASURE_S="${TD_MEASURE_S:-60}" \
      "$MIX" >> "$R/k6.txt" 2>&1 || true
    host_load "after" >> "$R/host-load.txt" 2>&1
    if [[ -n "${TD_PERF_AFTER:-}" ]]; then  # optional step after the run (perf-attribution.sh --after), e.g. SQL statistics
      { echo "\$ $TD_PERF_AFTER"; bash -c "$TD_PERF_AFTER"; } > "$R/after.txt" 2>&1
    fi
    [[ "$target" != modern ]] || stop_gw
    echo "$target run $run: $(python3 -c "import json,sys; s=json.load(open('$R/summary.json'))['all']; print('p50 %.1f p95 %.1f p99 %.1f ms, %d req, errors %.2f%%' % (s['duration_ms']['med'], s['duration_ms']['p(95)'], s['duration_ms']['p(99)'], s['duration_ms']['count'], 100*s['failed']['rate']))" 2>/dev/null || echo 'no summary')"
  done
  stop_gw
done
grep -l -F "$PW" -r "$OUT" && { echo "password found in output: refusing" >&2; exit 1; } || true
aggregate "$OUT"
