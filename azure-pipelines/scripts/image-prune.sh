#!/usr/bin/env bash
# Retention of the CI's ci-<run id> images in the agent host's Docker (images.sh calls it after every build).
#
#   image-prune.sh record TAG              note TAG's build time in the ledger (first time only)
#   image-prune.sh prune [--dry-run] [REPO...]   default REPOs: ticketdesk-web ticketdesk-worker
#
# A CD run may deploy any CI run's tag until its kind-local approval times out (24 h after the CD run was queued, which is
# when its CI run finished). So a ci-* tag is removed only when ALL of these hold:
#   - it is not CI_IMAGE_KEEP_TAG (this run's tag),
#   - it is not among the CI_IMAGE_KEEP_MIN (3) highest run ids still present,
#   - the ledger recorded it more than CI_IMAGE_KEEP_HOURS (30 = 24 h approval timeout + 6 h margin) ago.
# Build time comes from the ledger, not from Docker: an image rebuilt from the layer cache keeps its original creation date.
# A tag the ledger has never seen (built before the ledger existed) is recorded "now" and kept, so it ages from now.
# Ledger: CI_IMAGE_LEDGER (default <agent work folder>/_cache/ci-images.tsv: "tag<TAB>epoch"). CI_IMAGE_NOW overrides the
# clock (tests). Only tags matching ^ci-[0-9]+$ in the named repositories are ever touched.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
LEDGER="${CI_IMAGE_LEDGER:-${AGENT_WORKFOLDER:-$TD_W/.tools/ado-agent/_work}/_cache/ci-images.tsv}"
NOW="${CI_IMAGE_NOW:-$(date +%s)}"
mkdir -p "$(dirname "$LEDGER")"; touch "$LEDGER"
record() { grep -q "^$1	" "$LEDGER" || printf '%s\t%s\n' "$1" "$NOW" >> "$LEDGER"; }

case "${1:-}" in
  record) [[ "${2:-}" =~ ^ci-[0-9]+$ ]] || { echo "record: tag ci-<run id> expected" >&2; exit 2; }; record "$2" ;;
  prune)
    shift; dry=0; [[ "${1:-}" == --dry-run ]] && { dry=1; shift; }
    repos=("$@"); (( ${#repos[@]} )) || repos=(ticketdesk-web ticketdesk-worker)
    for repo in "${repos[@]}"; do
      mapfile -t tags < <(docker image ls "$repo" --format '{{.Tag}}' | grep -E '^ci-[0-9]+$' | sort -t- -k2 -n -r || true)
      for t in "${tags[@]}"; do record "$t"; done
      python3 -I - "$LEDGER" "$NOW" "${CI_IMAGE_KEEP_HOURS:-30}" "${CI_IMAGE_KEEP_MIN:-3}" "${CI_IMAGE_KEEP_TAG:-}" "${tags[@]}" <<'PY' |
import sys
ledger, now, hours, keep_min, keep_tag, tags = sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), int(sys.argv[4]), sys.argv[5], sys.argv[6:]
seen = dict(l.rstrip("\n").split("\t") for l in open(ledger) if "\t" in l)
newest = set(t for t in tags if t != keep_tag)
newest = set(sorted(newest, key=lambda t: int(t[3:]), reverse=True)[:keep_min])
for t in tags:
    age_h = (now - int(seen[t])) / 3600
    if t == keep_tag: why = "keep: this run's tag"
    elif t in newest: why = f"keep: among the {keep_min} newest"
    elif age_h <= hours: why = f"keep: {age_h:.1f} h old (<= {hours:g} h)"
    else: why = f"remove: {age_h:.1f} h old"
    print(f"{t}\t{why}")
PY
      while IFS=$'\t' read -r t why; do
        echo "$repo:$t $why"
        if [[ "$why" == remove* ]] && (( ! dry )); then docker image rm "$repo:$t" > /dev/null; fi
      done
    done ;;
  *) sed -n '2,17p' "$0"; exit 2 ;;
esac
