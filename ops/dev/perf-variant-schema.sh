#!/usr/bin/env bash
# perf-variant-schema.sh - perf-attribution variant: put the expand schema (td-db migrate: Tickets.AssignedDate + trigger,
# indexes, procedures) on the VM database of whatever snapshot is running, restart the app pool, wait for the app.
# Used as --prepare on baseline-s-legacy to separate the schema from the build and the blob store. Needs the VM lock.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TD_DB_CONN="Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env");TrustServerCertificate=True;Encrypt=True" \
  "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate | tail -1
"$REPO/ops/onprem/vm/vmctl.sh" ssh 'Import-Module WebAdministration; Restart-WebAppPool TicketDesk; "app pool restarted"' < /dev/null
"$REPO/ops/onprem/baseline/baseline-s.sh" wait-app
