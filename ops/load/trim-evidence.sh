#!/usr/bin/env bash
# trim-evidence.sh DIR: keep the evidence light (AGENT-RULES): k6's own CSV output (k6.csv.gz, MBs per step) moves out of the
# repository to $TD_W/logs/load/raw-k6/<same path>; the compact per-request file requests.csv.xz stays; k6.txt keeps its
# command line and the end of the run (the per-second progress lines are dropped).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"; root="$(cd "${1:?dir}" && pwd)"
find "$root" -name k6.csv.gz | while read -r f; do
  rel="${f#"$root"/}"; dst="$TD_W/logs/load/raw-k6/$(basename "$root")/$rel"; mkdir -p "$(dirname "$dst")"; mv "$f" "$dst"
done
find "$root" -name k6.txt | while read -r f; do
  if grep -q 'running (' "$f"; then { head -1 "$f"; echo "# (k6 progress lines dropped)"; grep -v -E '^(running \(|mix  |\s*$)' "$f" | tail -n +2 | tail -60; } > "$f.tmp" && mv "$f.tmp" "$f"; fi
done
du -sh "$root"
