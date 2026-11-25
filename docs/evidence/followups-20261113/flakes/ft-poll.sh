#!/usr/bin/env bash
# CAUTION: its per-database cursor (USE [db]) deadlocked with the tests' DROP DATABASE (run 19 of flake-hunt-full-x20: the
# assembly fixture's drop was the victim, 185 cleanup failures). Use it only to watch or as deliberate extra load (after-full-poller-x20), never in a run whose count is a result by itself.
# ft-poll.sh OUTFILE: every second, the full-text state of every td_routes_* database on td-sql (index populate status,
# pending changes) plus instance-wide populations/outstanding batches. sa password read by name, never printed.
OUT="$1"
export SQLCMDPASSWORD="$(sed -n 's/^MSSQL_SA_PASSWORD=//p' $HOME/Desktop/ticketdesk-modernization/.secrets/td-sql.env | head -1)"
SQLCMD=$HOME/Desktop/ticketdesk-modernization/.tools/bin/sqlcmd
Q=$(cat <<'SQL'
SET NOCOUNT ON;
DECLARE @r TABLE (db sysname, tbl sysname, crawl bit, pop int, pend bigint, items bigint);
DECLARE @n sysname, @s nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.databases WHERE (name LIKE 'td[_]routes[_]%' OR name LIKE 'td[_]modern[_]test[_]%') AND state = 0;
OPEN c; FETCH NEXT FROM c INTO @n;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @s = N'SELECT DB_NAME(), OBJECT_NAME(i.object_id), i.has_crawl_completed, CAST(OBJECTPROPERTYEX(i.object_id,''TableFullTextPopulateStatus'') AS int), CAST(OBJECTPROPERTYEX(i.object_id,''TableFullTextPendingChanges'') AS bigint), CAST(OBJECTPROPERTYEX(i.object_id,''TableFullTextItemCount'') AS bigint) FROM sys.fulltext_indexes i';
  BEGIN TRY INSERT @r EXEC (N'USE ' + N'[' + @n + N']; ' + @s); END TRY BEGIN CATCH END CATCH
  FETCH NEXT FROM c INTO @n;
END
SELECT CONVERT(varchar(23), SYSUTCDATETIME(), 121) + ' pops=' + CAST((SELECT COUNT(*) FROM sys.dm_fts_index_population) AS varchar) + ' batches=' + CAST((SELECT COUNT(*) FROM sys.dm_fts_outstanding_batches) AS varchar)
  + ' dbs=' + CAST((SELECT COUNT(DISTINCT db) FROM @r) AS varchar) + ' | ' + ISNULL((SELECT STRING_AGG(CAST(RIGHT(db, 6) + ':' + tbl + ':' + CAST(crawl AS varchar) + '/' + CAST(pop AS varchar) + '/' + CAST(pend AS varchar) + '/' + CAST(items AS varchar) AS nvarchar(max)), ' ') FROM @r WHERE db LIKE 'td[_]routes[_]%'), '');
SQL
)
while true; do
  "$SQLCMD" -S 127.0.0.1,14333 -U sa -C -h -1 -W -y 0 -Q "$Q" >> "$OUT" 2>&1
  sleep 1
done
