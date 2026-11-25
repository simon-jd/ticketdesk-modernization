#!/usr/bin/env bash
# Capture and replay the app's SQL against a td-sql database with STATISTICS IO/TIME and actual plans.
#
#   ops/dev/sql-perf.sh capture --set naive|tuned --out cases.json          (runs the app's query code, records its SQL)
#   ops/dev/sql-perf.sh bench --cases cases.json --out DIR [--warm 20] [--cold 5] [--only a,b]
#   ops/dev/sql-perf.sh compare --before results-a.json --after results-b.json --out FILE.md
#   ops/dev/sql-perf.sh verify | plans --dir DIR --out FILE
#
# .json.xz inputs (packed evidence) are decompressed to a temporary file first. Bench output is written
# uncompressed: pack it with ops/dev/pack-evidence.sh before committing it.
#
# Database: TD_PERF_DB (default TicketDeskL). The connection string is passed in TD_PERF_CONN, never on the command line.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
DB="${TD_PERF_DB:-TicketDeskL}"
. "$TD_W/.tools/env.sh"
BIN="$REPO/src/modern/perf/TicketDesk.SqlPerf/bin/Release/net10.0/td-sqlperf"
if [[ ! -x "$BIN" || -n "${TD_PERF_REBUILD:-}" ]]; then
  dotnet build "$REPO/src/modern/perf/TicketDesk.SqlPerf" -c Release -v quiet -nologo >/dev/null
fi
export TD_PERF_CONN; TD_PERF_CONN="$("$HERE/td-sql.sh" conn "$DB")"
export TD_PERF_ENV; TD_PERF_ENV="date: $(date -u +%FT%TZ)
host: $(hostname), $(nproc) cores, load average $(cut -d' ' -f1-3 /proc/loadavg)
sql: $("$HERE/td-sql.sh" sql -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(64)) + ', MAXDOP(db)=' + (SELECT CAST(value AS nvarchar(8)) FROM [$DB].sys.database_scoped_configurations WHERE name = 'MAXDOP')" | tr -d '\r')
database: $DB; git: $(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet || echo ' (uncommitted changes)')"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
args=(); i=0
for a in "$@"; do
  if [[ "$a" == *.xz && -f "$a" ]]; then
    i=$((i + 1)); out="$TMP/$i-$(basename "${a%.xz}")"; xz -dc "$a" > "$out"; args+=("$out")
  else
    args+=("$a")
  fi
done
"$BIN" "${args[@]}"
