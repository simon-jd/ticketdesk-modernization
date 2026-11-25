#!/usr/bin/env bash
# contract-check.sh - the contract stage on one td-sql database, with the record: td-db status before, `td-db migrate
# --contract --legacy-retired` (timed), status after, and the invariants C0001 establishes (trigger gone, IX_ProjectId gone,
# CK_Tickets_AssignedDate trusted, no ticket breaking it). Exit 1 if a check fails.
#
#   ops/dev/contract-check.sh DB [--copy | --copy-from SRC] [--out-dir DIR]
#     --copy   first make DB a fresh copy of td-sql's TicketDesk (ops/dev/e2e-db.sh DB --no-contract); --copy-from: of SRC
# Run it on the shared databases (TicketDesk, TicketDeskL) only in the retirement window: the CHECK constraint is added WITH
# CHECK, which scans Tickets under a schema-modification lock (docs/sql/schema-changes.md).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
DB="${1:?database}"; shift
COPY=0; OUT=""; SRC=TicketDesk
while [[ $# -gt 0 ]]; do
  case "$1" in
    --copy) COPY=1; shift ;;
    --copy-from) COPY=1; SRC="$2"; shift 2 ;;
    --out-dir) OUT="$2"; shift 2 ;;
    *) sed -n '2,10p' "$0"; exit 2 ;;
  esac
done
[[ "$DB" =~ ^[A-Za-z0-9_]+$ ]] || { echo "bad database name: $DB" >&2; exit 2; }
[[ -n "$OUT" ]] || OUT="$(mktemp -d)"
mkdir -p "$OUT"
dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
TDDB="$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db"
if (( COPY )); then
  # the copy only; the contract below is the step being recorded (e2e-db.sh refuses to overwrite TicketDesk/TicketDeskL)
  { echo "\$ TD_COPY_SOURCE=$SRC ops/dev/e2e-db.sh $DB --no-contract"; TD_COPY_SOURCE="$SRC" "$HERE/e2e-db.sh" "$DB" --no-contract; } > "$OUT/copy.txt"
fi
export TD_DB_CONN; TD_DB_CONN="$("$HERE/td-sql.sh" conn "$DB")"
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  database: td-sql $DB$( (( COPY )) && echo " (fresh copy of $SRC)")"
  echo "# td-sql: $(docker inspect td-sql --format '{{.Config.Image}}')  host load: $(cut -d' ' -f1-3 /proc/loadavg)"
} > "$OUT/environment.txt"
echo "\$ td-db status" > "$OUT/status-before.txt"; "$TDDB" status >> "$OUT/status-before.txt" 2>&1 || true
echo "\$ td-db migrate --contract --legacy-retired" > "$OUT/migrate-contract.txt"
start=$(date +%s.%N)
"$TDDB" migrate --contract --legacy-retired >> "$OUT/migrate-contract.txt" 2>&1
echo "# wall clock $(echo "$(date +%s.%N) - $start" | bc) s" >> "$OUT/migrate-contract.txt"
echo "\$ td-db status" > "$OUT/status-after.txt"; "$TDDB" status >> "$OUT/status-after.txt" 2>&1
"$HERE/td-sql.sh" sql -d "$DB" -b -W -s '|' -Q "
SET NOCOUNT ON;
SELECT 'trigger TR_Tickets_AssignedDate' AS [check], CAST(COUNT(*) AS varchar(10)) AS value, '0' AS expected FROM sys.triggers WHERE name = N'TR_Tickets_AssignedDate'
UNION ALL SELECT 'index IX_ProjectId', CAST(COUNT(*) AS varchar(10)), '0' FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Tickets') AND name = N'IX_ProjectId'
UNION ALL SELECT 'CK_Tickets_AssignedDate trusted and enabled', CAST(COUNT(*) AS varchar(10)), '1' FROM sys.check_constraints WHERE name = N'CK_Tickets_AssignedDate' AND is_not_trusted = 0 AND is_disabled = 0
UNION ALL SELECT 'tickets breaking assigned <=> AssignedDate', CAST(COUNT(*) AS varchar(10)), '0' FROM dbo.Tickets
  WHERE (AssignedTo IS NULL AND AssignedDate IS NOT NULL) OR (AssignedTo IS NOT NULL AND AssignedDate IS NULL)
UNION ALL SELECT 'contract scripts in the journal', CAST(COUNT(*) AS varchar(10)), '1' FROM dbo.__ModernSchemaHistory WHERE Kind = 'C'
UNION ALL SELECT 'tickets (for the record)', CAST(COUNT(*) AS varchar(10)), '-' FROM dbo.Tickets
UNION ALL SELECT 'assigned tickets (for the record)', CAST(COUNT(*) AS varchar(10)), '-' FROM dbo.Tickets WHERE AssignedTo IS NOT NULL;
SELECT ScriptName, Kind, AppliedAt, DurationMs FROM dbo.__ModernSchemaHistory WHERE Kind = 'C';
" > "$OUT/checks.txt"
cat "$OUT/checks.txt"
awk -F'|' 'NF == 3 && $1 !~ /^-+$/ && $3 != "-" && $3 != "expected" && $2 != $3 { bad = 1; print "FAILED: " $1 " = " $2 " (expected " $3 ")" } END { exit bad }' "$OUT/checks.txt"
