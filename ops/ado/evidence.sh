#!/usr/bin/env bash
# Pipeline numbers from the Azure DevOps API into an evidence folder (ops/ado/ado-lib.sh; nothing secret is written).
#
#   ops/ado/evidence.sh runs DIR            DIR/runs.csv: every run of every pipeline (id, pipeline, branch, reason, result,
#                                           queued/start/finish UTC, queue wait s, duration s) + DIR/runs-summary.txt
#   ops/ado/evidence.sh timeline DIR RUN    DIR/run-RUN.txt: ops/ado/runs.sh show RUN (stages, jobs, tasks, checks, errors)
#   ops/ado/evidence.sh approvals DIR RUN   DIR/approvals-RUN.txt: each approval of the run (created, status, approver,
#                                           decided at, wait s) from the Approvals API + the checkpoint records
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
cmd="${1:-}"; DIR="${2:-}"; [[ -n "$cmd" && -n "$DIR" ]] || { sed -n '2,8p' "$0"; exit 2; }
mkdir -p "$DIR"
case "$cmd" in
  runs)
    ado_api GET "_apis/build/builds?api-version=7.1&\$top=500&queryOrder=queueTimeAscending" > "$DIR/.builds.json"
    python3 -I - "$DIR/.builds.json" "$DIR" <<'PY'
import csv, json, sys
from datetime import datetime
d = json.load(open(sys.argv[1]))["value"]
def t(s):  # "2026-11-11T09:04:23.1834567Z" -> datetime (seconds precision is enough here)
    return datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S") if s else None
rows = []
for b in d:
    q, s, f = t(b.get("queueTime")), t(b.get("startTime")), t(b.get("finishTime"))
    rows.append({"id": b["id"], "pipeline": b["definition"]["name"], "number": b["buildNumber"],
                 "branch": b["sourceBranch"].replace("refs/heads/", ""), "commit": b["sourceVersion"][:8], "reason": b["reason"],
                 "result": b.get("result", b["status"]), "queued_utc": b.get("queueTime", "")[:19], "start_utc": (b.get("startTime") or "")[:19],
                 "finish_utc": (b.get("finishTime") or "")[:19], "queue_wait_s": round((s - q).total_seconds()) if s and q else "",
                 "duration_s": round((f - s).total_seconds()) if f and s else ""})
rows.sort(key=lambda r: r["id"])
with open(f"{sys.argv[2]}/runs.csv", "w", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
out = []
for p in sorted({r["pipeline"] for r in rows}):
    rs = [r for r in rows if r["pipeline"] == p]
    res = {}
    for r in rs: res[r["result"]] = res.get(r["result"], 0) + 1
    ok = [r["duration_s"] for r in rs if r["result"] == "succeeded" and r["duration_s"] != ""]
    out.append(f"{p}: {len(rs)} runs {res}" + (f"; succeeded durations s: min {min(ok)}, median {sorted(ok)[len(ok)//2]}, max {max(ok)}" if ok else ""))
    for reason in sorted({r['reason'] for r in rs}):
        rr = [r for r in rs if r["reason"] == reason]
        out.append(f"  reason {reason}: {len(rr)} ({', '.join(f'{r['id']}={r['result']}' for r in rr)})")
open(f"{sys.argv[2]}/runs-summary.txt", "w").write("\n".join(out) + "\n")
print("\n".join(out))
PY
    rm -f "$DIR/.builds.json" ;;
  timeline)
    "$HERE/runs.sh" show "$3" > "$DIR/run-$3.txt"; head -1 "$DIR/run-$3.txt" ;;
  approvals)
    run="$3"
    ado_api GET "_apis/build/builds/$run/timeline?api-version=7.1" > "$DIR/.tl.json"
    ado_api GET "_apis/pipelines/approvals?\$expand=steps&api-version=7.1" > "$DIR/.ap.json"
    python3 -I - "$DIR/.tl.json" "$DIR/.ap.json" "$run" <<'PY' | tee "$DIR/approvals-$run.txt"
import json, sys
from datetime import datetime
def t(s): return datetime.strptime(s[:19], "%Y-%m-%dT%H:%M:%S") if s else None
tl = json.load(open(sys.argv[1]))["records"]
ap = json.load(open(sys.argv[2])).get("value", [])
cps = {r["id"]: r for r in tl if r["type"] in ("Checkpoint", "Checkpoint.Approval")}
for r in sorted(cps.values(), key=lambda r: r.get("startTime") or ""):
    s, f = t(r.get("startTime")), t(r.get("finishTime"))
    print(f"timeline {r['type']} '{r['name']}' state={r['state']} result={r.get('result')} start={r.get('startTime','')[:19]} finish={(r.get('finishTime') or '')[:19]} wait_s={round((f-s).total_seconds()) if s and f else '-'}")
ids = {r["id"] for r in tl if r["type"] == "Checkpoint.Approval"}
for a in ap:
    if a["id"] not in ids: continue
    c, l = t(a.get("createdOn")), t(a.get("lastModifiedOn"))
    for st in a.get("steps", []):
        print(f"approval {a['id']} status={a['status']} created={a['createdOn'][:19]} approver=identity:{(st.get('assignedApprover') or {}).get('id','')[:8]} "
              f"actualApprover=identity:{(st.get('actualApprover') or {}).get('id','-')[:8]} status={st['status']} initiated={(st.get('initiatedOn') or '')[:19]} "
              f"decided={(st.get('lastModifiedOn') or '')[:19]} comment={st.get('comment')!r} wait_s={round((l-c).total_seconds()) if c and l else '-'}")
PY
    rm -f "$DIR/.tl.json" "$DIR/.ap.json" ;;
  *) sed -n '2,8p' "$0"; exit 2 ;;
esac
