#!/usr/bin/env bash
# legacy-query-stats.sh - the VM SQL Server's cached statement statistics for the TicketDesk database (read only):
# top statements by total CPU with executions, average CPU/elapsed/logical reads, plan hash and recompile count.
#   ops/dev/legacy-query-stats.sh [TOP]        (sa password from $TD_W/.secrets/legacy-lab.env, never printed)
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
TOP="${1:-12}"
SQLCMDPASSWORD="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")" \
  "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d TicketDesk -W -s '|' -Q "SET NOCOUNT ON;
SELECT TOP ($TOP) qs.execution_count AS execs,
  CAST(qs.total_worker_time / qs.execution_count / 1000.0 AS decimal(10,2)) AS avg_cpu_ms,
  CAST(qs.total_elapsed_time / qs.execution_count / 1000.0 AS decimal(10,2)) AS avg_elapsed_ms,
  CAST(qs.max_elapsed_time / 1000.0 AS decimal(10,2)) AS max_elapsed_ms,
  qs.total_logical_reads / qs.execution_count AS avg_reads,
  CONVERT(varchar(18), qs.query_plan_hash, 1) AS plan_hash, qs.plan_generation_num AS plan_gen,
  REPLACE(REPLACE(SUBSTRING(st.text, CHARINDEX('SELECT', st.text + 'SELECT'), 90), CHAR(13), ' '), CHAR(10), ' ') AS stmt
FROM sys.dm_exec_query_stats qs CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.dbid = DB_ID() OR st.text LIKE '%[[]dbo].[[]Tickets]%'
ORDER BY qs.total_worker_time DESC"
