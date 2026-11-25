#!/usr/bin/env bash
# Packs bench evidence for git (plan/AGENT-RULES.md, "Evidence size"): every plans/ and raw/ directory becomes
# <dir>.tar.xz, a .tar.gz becomes .tar.xz, and any other file over 512 KiB becomes <file>.xz (xz -9). The readable
# summaries (summary-*.md, plans.txt, compare-*.md, statements.txt) stay as they are. Deterministic: the same input
# gives the same bytes (sorted tar entries, fixed mtime/owner, single-threaded xz), so packing in every commit of a
# history rewrite yields identical blobs.
#
#   ops/dev/pack-evidence.sh docs/evidence/sql-before-20261107 [...]
#   unpack: tar -xJf plans.tar.xz   |   xz -dk results-naive.json.xz   (ops/dev/sql-perf.sh reads .xz directly)
set -euo pipefail
for root in "$@"; do
  [[ -d "$root" ]] || continue
  find "$root" -depth -type d \( -name plans -o -name raw \) -print0 | while IFS= read -r -d '' d; do
    tar --sort=name --mtime=@0 --owner=0 --group=0 --numeric-owner --mode='a+rX,u+w,go-w' \
        -C "$(dirname "$d")" -cf - "$(basename "$d")" | xz -9 -T1 -c > "$d.tar.xz"
    rm -rf "$d"
  done
  find "$root" -type f -name '*.tar.gz' -print0 | while IFS= read -r -d '' f; do
    gzip -dc "$f" | xz -9 -T1 -c > "${f%.gz}.xz"
    rm -f "$f"
  done
  find "$root" -type f ! -name '*.xz' -size +512k -print0 | while IFS= read -r -d '' f; do
    xz -9 -T1 -c "$f" > "$f.xz"
    rm -f "$f"
  done
done
