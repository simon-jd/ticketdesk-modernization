#!/usr/bin/env bash
# Run the new TicketDesk.Web (src/modern) locally against the td-sql container (the new app alone: the legacy app is retired).
#
#   ops/dev/run-modern.sh
#
# Environment (all optional):
#   TD_MODERN_DB           database in td-sql (default TicketDeskSynth); it must carry the td-db schema including the
#                          contract stage (td-db migrate --contract --legacy-retired), e.g. a copy from ops/dev/e2e-db.sh
#   TD_MODERN_URLS         listen address (default http://127.0.0.1:5080)
# The connection string (with the sa password) goes into the process environment only; it is never printed.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"

export ConnectionStrings__TicketDesk; ConnectionStrings__TicketDesk="$("$HERE/td-sql.sh" conn "${TD_MODERN_DB:-TicketDeskSynth}")"
export ASPNETCORE_URLS="${TD_MODERN_URLS:-http://127.0.0.1:5080}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
exec dotnet run --project "$REPO/src/modern/TicketDesk.Web" -c Release --no-launch-profile
