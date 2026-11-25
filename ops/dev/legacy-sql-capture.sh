#!/usr/bin/env bash
# Extended Events capture of the SQL the legacy EF6 app sends, on the legacy VM's SQL Server Express (127.0.0.1,11433).
# Take the shared VM lock first (plan/AGENT-RULES.md, "Shared legacy VM"); this script refuses to start without it.
#
#   ops/dev/legacy-sql-capture.sh start            create + start session td_sqldata_capture (ring buffer, TicketDesk DB only)
#   ops/dev/legacy-sql-capture.sh export OUT.xml   write the ring buffer's events (XML)
#   ops/dev/legacy-sql-capture.sh stop             stop and drop the session
#
# sa password from $TD_W/.secrets/legacy-lab.env (passed in SQLCMDPASSWORD, never printed).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
. "$TD_W/.tools/env.sh"
ENV_FILE="$TD_W/.secrets/legacy-lab.env"
HOSTPORT="$(sed -n 's/^TD_SQL_HOST_PORT=//p' "$ENV_FILE" | tail -1)"; HOSTPORT="${HOSTPORT:-127.0.0.1,11433}"
export SQLCMDPASSWORD; SQLCMDPASSWORD="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$ENV_FILE" | tail -1)"
q() { sqlcmd -S "$HOSTPORT" -U sa -C -b "$@"; }
SESSION=td_sqldata_capture
case "${1:-}" in
  start)
    [[ -d "$TD_W/plan/locks/vm.lock" ]] || { echo "take the VM lock first (plan/AGENT-RULES.md)" >&2; exit 2; }
    q -Q "IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'$SESSION') DROP EVENT SESSION [$SESSION] ON SERVER;
CREATE EVENT SESSION [$SESSION] ON SERVER
  ADD EVENT sqlserver.rpc_completed (ACTION (sqlserver.client_app_name, sqlserver.session_id, sqlserver.database_name)
    WHERE sqlserver.database_name = N'TicketDesk'),
  ADD EVENT sqlserver.sql_batch_completed (ACTION (sqlserver.client_app_name, sqlserver.session_id, sqlserver.database_name)
    WHERE sqlserver.database_name = N'TicketDesk')
  ADD TARGET package0.ring_buffer (SET max_memory = 16384, max_events_limit = 20000)
  WITH (MAX_DISPATCH_LATENCY = 1 SECONDS, EVENT_RETENTION_MODE = NO_EVENT_LOSS);
ALTER EVENT SESSION [$SESSION] ON SERVER STATE = START;
SELECT name, create_time FROM sys.dm_xe_sessions WHERE name = N'$SESSION';" ;;
  export)
    out="${2:?usage: export OUT.xml}"
    sleep 2  # MAX_DISPATCH_LATENCY
    q -h -1 -y 0 -Q "SET NOCOUNT ON; SELECT CAST(t.target_data AS nvarchar(max)) FROM sys.dm_xe_session_targets t
JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address WHERE s.name = N'$SESSION' AND t.target_name = N'ring_buffer';" > "$out"
    echo "wrote $out ($(wc -c < "$out") bytes)" ;;
  stop)
    q -Q "IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'$SESSION') DROP EVENT SESSION [$SESSION] ON SERVER;" ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac
