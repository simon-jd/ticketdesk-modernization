-- Query Store on TicketDeskL: every query that ran with more than one plan, per plan: executions, mean duration,
-- CPU and logical reads, first/last run (UTC), whether forced. Here: the naive EF Core statements before the
-- td-db indexes (00:53-00:57 UTC) and after them (rerun on the expanded schema).
SET NOCOUNT ON;
USE TicketDeskL;
EXEC sys.sp_query_store_flush_db;
WITH p AS (
  SELECT q.query_id, p.plan_id, p.is_forced_plan,
         SUM(rs.count_executions) AS execs,
         SUM(rs.avg_duration * rs.count_executions) / NULLIF(SUM(rs.count_executions), 0) / 1000.0 AS avg_ms,
         SUM(rs.avg_cpu_time * rs.count_executions) / NULLIF(SUM(rs.count_executions), 0) / 1000.0 AS avg_cpu_ms,
         SUM(rs.avg_logical_io_reads * rs.count_executions) / NULLIF(SUM(rs.count_executions), 0) AS avg_reads,
         MIN(rs.first_execution_time) AS first_run, MAX(rs.last_execution_time) AS last_run
  FROM sys.query_store_query q
  JOIN sys.query_store_plan p ON p.query_id = q.query_id
  JOIN sys.query_store_runtime_stats rs ON rs.plan_id = p.plan_id
  GROUP BY q.query_id, p.plan_id, p.is_forced_plan
)
SELECT p.query_id, p.plan_id, p.is_forced_plan AS forced, p.execs, CAST(p.avg_ms AS decimal(12,1)) AS avg_ms,
       CAST(p.avg_cpu_ms AS decimal(12,1)) AS avg_cpu_ms, CAST(p.avg_reads AS bigint) AS avg_reads,
       CONVERT(varchar(19), p.first_run, 126) AS first_run, CONVERT(varchar(19), p.last_run, 126) AS last_run,
       LEFT(REPLACE(REPLACE(t.query_sql_text, CHAR(13), ' '), CHAR(10), ' '), 110) AS query_text
FROM p
JOIN sys.query_store_query q ON q.query_id = p.query_id
JOIN sys.query_store_query_text t ON t.query_text_id = q.query_text_id
WHERE p.query_id IN (SELECT query_id FROM sys.query_store_plan GROUP BY query_id HAVING COUNT(*) > 1)
  AND t.query_sql_text LIKE N'%Tickets%'
ORDER BY p.query_id, p.first_run;
