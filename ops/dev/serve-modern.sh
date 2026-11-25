#!/usr/bin/env bash
# serve-modern.sh - run the new app alone (TicketDesk.Web Release from this checkout; no legacy app behind it since r4-retire)
# in the foreground over HTTPS with the lab certificate, for replays, E2E and load tests. Secrets stay in the process environment.
#
#   ops/dev/serve-modern.sh --url https://localhost:5473/ --registry FILE --attachments DIR (--db NAME | --vm-db)
#     --db NAME   td-sql database NAME (as sa; e.g. a copy from ops/dev/e2e-db.sh, contracted)
#     --vm-db     the lab VM's SQL Express database TicketDesk as the least-privilege login td_modern_app
#                 (ops/dev/td-db-app-login.sh apply after a VM reset; the database must be contracted)
#     --app-server HOST,PORT   the same login and database on another server (Round 6 load: a scratch SQL container;
#                 td-db-app-login.sh apply with TD_APP_LOGIN_SERVER=HOST,PORT first)
# The build is not done here (callers build once): dotnet build src/modern/TicketDesk.Web -c Release.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
URL=""; REGISTRY=""; ATTACH=""; DB=""; VM=0; SERVER="127.0.0.1,11433"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --url) URL="${2%/}"; shift 2 ;;
    --registry) REGISTRY="$2"; shift 2 ;;
    --attachments) ATTACH="$2"; shift 2 ;;
    --db) DB="$2"; shift 2 ;;
    --vm-db) VM=1; shift ;;
    --app-server) VM=1; SERVER="$2"; shift 2 ;;
    *) sed -n '2,11p' "$0"; exit 2 ;;
  esac
done
[[ -n "$URL" && -n "$REGISTRY" && -n "$ATTACH" && ( -n "$DB" || "$VM" == 1 ) ]] || { sed -n '2,11p' "$0"; exit 2; }
CA="$TD_W/.secrets/legacy-lab-ca"
export ASPNETCORE_URLS="$URL" ASPNETCORE_ENVIRONMENT=Production
export Kestrel__Certificates__Default__Path="$CA/server.pfx"
export Kestrel__Certificates__Default__Password; Kestrel__Certificates__Default__Password="$(sed -n 's/^TD_CERT_PFX_PASSWORD=//p' "$TD_W/.secrets/legacy-lab-cert.env")"
export Migration__RegistryFile="$REGISTRY" Attachments__Store=Local Attachments__RootPath="$ATTACH"
export ConnectionStrings__TicketDesk
if (( VM )); then
  pw="$(sed -n 's/^TD_MODERN_APP_SQL_PASSWORD=//p' "$TD_W/.secrets/td-modern-app.env")"
  ConnectionStrings__TicketDesk="Server=$SERVER;Database=TicketDesk;User Id=td_modern_app;Password=$pw;TrustServerCertificate=True;Encrypt=True"
else
  ConnectionStrings__TicketDesk="$("$HERE/td-sql.sh" conn "$DB")"
fi
cd "$REPO/src/modern/TicketDesk.Web"   # content root: appsettings.json (key ring in the database, log levels)
exec dotnet "$REPO/src/modern/TicketDesk.Web/bin/Release/net10.0/TicketDesk.Web.dll"
