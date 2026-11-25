#!/usr/bin/env bash
# After legacy-env reports DB_READY: restore the clean legacy backup into td-sql, compare its schema with the
# EF6-implied one, verify the seeder against it and load scale S. Evidence goes to docs/evidence/legacy-db-<date>/.
#
#   ops/dev/legacy-db-check.sh [BAK]      (default: the DB_READY path from plan/handoffs/r0-legacy-env.status)
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
BAK="${1:-$(sed -n 's/.*DB_READY \([^ ]*\).*/\1/p' "$TD_W/plan/handoffs/r0-legacy-env.status" | tail -1)}"
BAK="${BAK//\$W/$TD_W}"
[[ -f "$BAK" ]] || { echo "backup not found: '$BAK'" >&2; exit 1; }
E="$REPO/docs/evidence/legacy-db-$(date -u +%Y%m%d)"
mkdir -p "$E"
cd "$REPO"
{
  echo "# date: $(date -u +%FT%TZ)  git: $(git rev-parse --short HEAD)"
  echo "# backup: $BAK  sha256: $(sha256sum "$BAK" | cut -c1-64)  size: $(stat -c %s "$BAK") bytes"
  echo "# sql: $("$HERE/td-sql.sh" status 2>/dev/null | sed -n 2p)"
} > "$E/environment.txt"

echo "\$ ops/dev/td-sql.sh restore $BAK TicketDesk" > "$E/restore.txt"
"$HERE/td-sql.sh" restore "$BAK" TicketDesk >> "$E/restore.txt" 2>&1

COLS="SET NOCOUNT ON; SELECT s.name + '.' + t.name + '.' + c.name + CHAR(9) + ty.name + CHAR(9)
  + CASE WHEN c.max_length = -1 THEN 'max' WHEN ty.name IN ('nvarchar','nchar') THEN CAST(c.max_length/2 AS varchar(10)) ELSE CAST(c.max_length AS varchar(10)) END
  + CHAR(9) + CASE c.is_nullable WHEN 1 THEN 'null' ELSE 'not null' END + CHAR(9) + CASE c.is_identity WHEN 1 THEN 'identity' ELSE '' END
  FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id ORDER BY 1"
FKS="SET NOCOUNT ON; SELECT OBJECT_SCHEMA_NAME(parent_object_id) + '.' + OBJECT_NAME(parent_object_id) + ' -> ' + OBJECT_NAME(referenced_object_id)
  + CHAR(9) + name + CHAR(9) + delete_referential_action_desc FROM sys.foreign_keys ORDER BY 1, 2"
for db in TicketDesk TicketDeskSynth; do
  "$HERE/td-sql.sh" sql -d "$db" -h -1 -W -Q "$COLS" > "$E/columns-$db.tsv" 2>&1 || true
  "$HERE/td-sql.sh" sql -d "$db" -h -1 -W -Q "$FKS" > "$E/foreign-keys-$db.tsv" 2>&1 || true
done
"$HERE/td-sql.sh" sql -d TicketDesk -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT ContextKey, COUNT(*), MAX(MigrationId) FROM dbo.__MigrationHistory GROUP BY ContextKey" > "$E/migration-history.txt" 2>&1 || true
"$HERE/td-sql.sh" sql -d TicketDesk -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT s.name + '.' + t.name, SUM(p.rows) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1" > "$E/clean-row-counts.txt" 2>&1 || true
diff "$E/columns-TicketDeskSynth.tsv" "$E/columns-TicketDesk.tsv" > "$E/columns-implied-vs-real.diff" || true
diff "$E/foreign-keys-TicketDeskSynth.tsv" "$E/foreign-keys-TicketDesk.tsv" > "$E/foreign-keys-implied-vs-real.diff" || true

{ echo '$ TD_SEED_DB=TicketDesk ops/dev/seed.sh verify'; TD_SEED_DB=TicketDesk "$HERE/seed.sh" verify; } > "$E/seed-verify.txt" 2>&1 || true
echo "\$ TD_SEED_DB=TicketDesk ops/dev/seed.sh run --scale S --seed 42 --json $E/seed-S.json" > "$E/seed-S.txt"
( time TD_SEED_DB=TicketDesk "$HERE/seed.sh" run --scale S --seed 42 --json "$E/seed-S.json" ) >> "$E/seed-S.txt" 2>&1 || true
echo "evidence in $E"; tail -3 "$E/seed-verify.txt"; grep -E 'tickets loaded|real' "$E/seed-S.txt" || true
