#!/usr/bin/env bash
# render-parity.sh - byte-for-byte check of the new app's legacy text rendering (Markdown + HtmlSanitizer) against the legacy
# implementation running on the lab VM with the legacy site's own DLLs.
#
#   TD_LOCK_OWNER=<you> ops/dev/render-parity/render-parity.sh --out-dir DIR      (needs the VM lock: runs a process on the VM)
#
# Corpus: every ticket's Details and event's Comment of the baseline data (td-sql database TicketDesk = baseline-s data),
# the golden journeys' texts, and tricky inputs (tricky_inputs.py), each tricky/golden text as Markdown and as HTML.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
. "$TD_W/.tools/env.sh"
OUT=""; while [[ $# -gt 0 ]]; do case "$1" in --out-dir) OUT="$2"; shift 2 ;; *) sed -n '2,9p' "$0"; exit 2 ;; esac; done
[[ -n "$OUT" ]] || { sed -n '2,9p' "$0"; exit 2; }
mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"
"$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "take the VM lock first" >&2; exit 1; }
V="$REPO/ops/onprem/vm/vmctl.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
SQL="$REPO/ops/dev/td-sql.sh"
"$SQL" sql -d TicketDesk -y 0 -Q "SET NOCOUNT ON; SELECT (SELECT CONCAT('t', TicketId) AS id, IsHtml AS h, Details AS x FROM dbo.Tickets FOR JSON PATH, INCLUDE_NULL_VALUES) AS j" > "$TMP/tickets.raw"
"$SQL" sql -d TicketDesk -y 0 -Q "SET NOCOUNT ON; SELECT (SELECT CONCAT('e', TicketId, '-', EventId) AS id, IsHtml AS h, Comment AS x FROM dbo.TicketEvents FOR JSON PATH, INCLUDE_NULL_VALUES) AS j" > "$TMP/events.raw"
xz -dc "$REPO/tests/characterization/golden/legacy-golden.jsonl.xz" > "$TMP/golden.jsonl"
python3 -I "$HERE/make_corpus.py" --tickets "$TMP/tickets.raw" --events "$TMP/events.raw" --golden "$TMP/golden.jsonl" --out "$OUT/corpus.tsv"
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)"
  echo "# legacy: LegacyRender.cs compiled on the VM with the .NET Framework csc against C:\\inetpub\\ticketdesk\\bin (MarkdownSharp, HtmlSanitizer, AngleSharp), run with en-US"
  echo "# new: ops/dev/render-parity/render-parity.cs (TicketDesk.Tickets LegacyTextRenderer) on .NET $(dotnet --version), en-US"
} > "$OUT/environment.txt"
"$V" ssh 'Remove-Item -Recurse -Force C:\TdRender -ErrorAction SilentlyContinue; New-Item -ItemType Directory C:\TdRender | Out-Null; Copy-Item C:\inetpub\ticketdesk\bin\MarkdownSharp.dll,C:\inetpub\ticketdesk\bin\HtmlSanitizer.dll,C:\inetpub\ticketdesk\bin\AngleSharp.dll C:\TdRender' < /dev/null
"$V" scp "$HERE/LegacyRender.cs" "vm:C:/TdRender/LegacyRender.cs" < /dev/null
"$V" scp "$OUT/corpus.tsv" "vm:C:/TdRender/corpus.tsv" < /dev/null
"$V" ps < "$HERE/legacy-render.ps1" | tee "$OUT/legacy-dlls.txt"
"$V" scp "vm:C:/TdRender/legacy-out.tsv" "$OUT/legacy-out.tsv" < /dev/null
"$V" ssh 'Remove-Item -Recurse -Force C:\TdRender' < /dev/null
echo "\$ dotnet run ops/dev/render-parity/render-parity.cs -- corpus.tsv legacy-out.tsv OUT" > "$OUT/compare.txt"
dotnet run "$HERE/render-parity.cs" -- "$OUT/corpus.tsv" "$OUT/legacy-out.tsv" "$OUT" >> "$OUT/compare.txt" 2>&1 && rc=0 || rc=$?
xz -9 -f "$OUT/corpus.tsv" "$OUT/legacy-out.tsv" "$OUT/new-out.tsv"   # evidence size rule: raw files over 512 KB as .xz
cat "$OUT/report.md"
exit $rc
