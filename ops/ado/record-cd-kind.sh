#!/usr/bin/env bash
# After the user approved a CD run's kind-local stage: wait for the run, then write its numbers into the evidence folder.
#   ops/ado/record-cd-kind.sh RUN [DIR]   (default DIR docs/evidence/pipelines-20261111/cd-kind of this checkout)
# Writes DIR/approvals-RUN.txt (approval requested / decided / wait), DIR/run-RUN.txt (timeline), DIR/smoke-RUN.txt (the
# Smoke step's output) and prints the line for docs/RESULTS.md.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
RUN="${1:?run id}"; DIR="${2:-$HERE/../../docs/evidence/pipelines-20261111/cd-kind}"; mkdir -p "$DIR"
"$HERE/runs.sh" wait "$RUN" --timeout 1800 || true
"$HERE/evidence.sh" approvals "$DIR" "$RUN" >/dev/null
"$HERE/evidence.sh" timeline "$DIR" "$RUN" >/dev/null
"$HERE/runs.sh" log "$RUN" Smoke | sed -E 's/^[0-9T:.Z-]+ //' | sed -n '/Starting Command Output/,/Finishing/p' > "$DIR/smoke-$RUN.txt" || true
sed -i "s|$HOME|~|g" "$DIR"/*-"$RUN".txt
python3 -I - "$DIR/approvals-$RUN.txt" "$DIR/run-$RUN.txt" <<'PY'
import re, sys
ap = open(sys.argv[1]).read(); tl = open(sys.argv[2]).read()
wait = re.search(r"Checkpoint.Approval' .*? wait_s=(\S+)", ap)
status = re.search(r"^approval \S+ status=(\S+)", ap, re.M)
deploy = re.search(r"^Job\s+completed\s+(\S+)\s+(\d+) s\s+Deploy to kind", tl, re.M)
result = re.search(r" result=(\S+) ", tl.splitlines()[0])
print(f"CD run: result {result.group(1) if result else '?'}; approval {status.group(1) if status else '?'} after "
      f"{wait.group(1) if wait else '?'} s; kind deployment job {deploy.group(1) + ' in ' + deploy.group(2) + ' s' if deploy else '?'}")
PY
