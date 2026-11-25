#!/usr/bin/env bash
# vm-db-contract.sh - bring the lab VM's database to the new app's schema, contract stage included (td-db migrate --contract
# --legacy-retired, as sa from legacy-lab.env; the password stays in the process environment). For runs of the new app alone on
# the VM's SQL Express (ops/dev/perf-baseline.sh target modern): the snapshots carry the expand stage only, and the next
# snapshot reset undoes this. Only for the holder of the VM lock (TD_LOCK_OWNER).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
"$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "take the VM lock first (TD_LOCK_OWNER)" >&2; exit 1; }
dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
pw="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")"
export TD_DB_CONN="Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=$pw;TrustServerCertificate=True;Encrypt=True"
"$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate --contract --legacy-retired
