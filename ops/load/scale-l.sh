#!/usr/bin/env bash
# scale-l.sh - Round 6 load: the new app alone at scale L (1,000,000 tickets) on the capacity setup (td-load-sql = SQL Server
# 2022 Express, app + SQL on CPUs 24-27): TicketDeskL from td-sql (COPY_ONLY, compressed) restored as TicketDesk in
# td-load-sql, td-db migrate --contract --legacy-retired, td_modern_app, one sign-in per help-desk user, a prime, then the
# rates in order (stops after a step far past breaking). The capacity base (new-base.bak) stays in the container.
#   scale-l.sh prepare EVDIR | run EVDIR RATE...
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"; REPO="$(cd "$HERE/../.." && pwd)"
# shellcheck disable=SC1091
. "$TD_W/.tools/env.sh"
C="$HERE/capacity.sh"; SCRATCH="$TD_W/scratch-load"; SQL_NAME=td-load-sql
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
new_sql() { SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -e SQLCMDPASSWORD "$SQL_NAME" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1" -h -1 -W -Q "$2"; }
case "${1:-}" in
  prepare)
    ev="$2"; mkdir -p "$ev"
    {
      echo "# $(date -u +%FT%TZ) scale L for the new app alone"
      if [[ "${TD_L_SKIP_RESTORE:-0}" != 1 ]]; then   # 1: the restore is done (resume after a failed later step)
      "$REPO/ops/dev/td-sql.sh" sql -b -Q "BACKUP DATABASE TicketDeskL TO DISK = N'/var/opt/mssql/backup/r6-L.bak' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 50" | tail -2
      docker cp td-sql:/var/opt/mssql/backup/r6-L.bak "$SCRATCH/r6-L.bak"; docker exec td-sql rm -f /var/opt/mssql/backup/r6-L.bak
      chmod 600 "$SCRATCH/r6-L.bak"; echo "backup $(stat -c %s "$SCRATCH/r6-L.bak") bytes (compressed)"
      "$C" stop-new
      TD_SQL_NAME="$SQL_NAME" "$REPO/ops/dev/td-sql.sh" restore "$SCRATCH/r6-L.bak" TicketDesk | tail -2
      docker exec "$SQL_NAME" rm -f /var/opt/mssql/backup/r6-L.bak
      fi
      ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "$SQL_NAME" | awk '{print $1}')"
      TD_DB_CONN="Server=$ip,1433;Database=TicketDesk;User Id=sa;Password=$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD);TrustServerCertificate=True;Encrypt=True" \
        "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate --contract --legacy-retired 2>&1 | tail -4
      # td_modern_app (the login exists on td-load-sql since the scale-S preparation). TicketDeskL was seeded without the
      # notifications.* tables (td-seed only creates them since Round 5b), so the role is granted without its 6 grants on
      # them; nothing in the mix reads them while Service Bus is off (a failed request would show it)
      { echo "IF DATABASE_PRINCIPAL_ID(N'td_modern_app') IS NULL CREATE USER td_modern_app FOR LOGIN td_modern_app;"; echo "GO"
        grep -v 'notifications\.' "$REPO/ops/db/td_modern_app_role.sql"; echo "GO"
        echo "IF IS_ROLEMEMBER(N'td_modern_app_role', N'td_modern_app') = 0 ALTER ROLE td_modern_app_role ADD MEMBER td_modern_app;"; echo "GO"
        echo "SELECT 'grants: ' + CAST(COUNT(*) AS varchar(10)) FROM sys.database_permissions WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'td_modern_app_role');"
      } | SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -i -e SQLCMDPASSWORD "$SQL_NAME" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d TicketDesk -h -1 -W | tail -1
      new_sql TicketDesk "SET NOCOUNT ON; SELECT 'tickets ' + CAST(COUNT_BIG(*) AS varchar(12)) FROM dbo.Tickets; SELECT 'events ' + CAST(COUNT_BIG(*) AS varchar(12)) FROM dbo.TicketEvents; SELECT 'data MB ' + CAST(SUM(FILEPROPERTY(name, 'SpaceUsed')) * 8 / 1024 AS varchar(12)) FROM sys.database_files WHERE type = 0"
      "$C" start-new
      python3 -I "$HERE/signin_all.py" --base https://localhost:5493 --users "$REPO/tests/perf/users-helpdesk.json" --password-env "$TD_W/.secrets/td-seed.env:TD_SEED_USER_PASSWORD"
    } 2>&1 | tee -a "$ev/prepare.txt" ;;
  run)
    ev="$2"; shift 2; mkdir -p "$ev"
    export TD_TICKETS=1000000   # the view operation picks ids over the 1M tickets (k6 reads system env into __ENV)
    TD_LOAD_WARMUP_S=0 TD_LOAD_MEASURE_S=90 "$C" step new 10 "$ev/prime" > "$ev/prime.txt" 2>&1 || true
    rm -f "$ev/prime/k6.csv.gz" "$ev/prime/requests.csv.xz"
    for r in "$@"; do
      "$C" step new "$r" "$ev/rate-$r" | tee -a "$ev/steps.txt"
      python3 -I -c 'import csv, json, sys
s = json.load(open(sys.argv[1])); a = s["all"]
d = sum(int(x["dropped_iterations"] or 0) for x in csv.DictReader(open(sys.argv[2])) if x["phase"] == "measure")
sys.exit(0 if a["duration_ms"]["p(95)"] > 5000 or a["failed"]["rate"] > 0.2 or d > 0.2 * s["rate_per_s"] * s["measure_s"] else 1)' "$ev/rate-$r/summary.json" "$ev/rate-$r/timeline.csv" && break
    done ;;
  *) sed -n '2,7p' "$0"; exit 2 ;;
esac
