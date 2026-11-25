SET NOCOUNT ON;
USE [TicketDeskL];
SELECT DB_NAME() AS db, CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) AS version, compatibility_level, recovery_model_desc, is_query_store_on
FROM sys.databases WHERE name = DB_NAME();
SELECT actual_state_desc, desired_state_desc, query_capture_mode_desc, interval_length_minutes, max_storage_size_mb FROM sys.database_query_store_options;
SELECT name AS scoped_config, value FROM sys.database_scoped_configurations WHERE name IN ('MAXDOP','PARAMETER_SNIFFING','LEGACY_CARDINALITY_ESTIMATION');
SELECT value_in_use AS server_maxdop FROM sys.configurations WHERE name = 'max degree of parallelism';
SELECT value_in_use AS cost_threshold FROM sys.configurations WHERE name = 'cost threshold for parallelism';
SELECT value_in_use AS max_server_memory_mb FROM sys.configurations WHERE name = 'max server memory (MB)';
-- files
SELECT name, type_desc, size * 8 / 1024 AS allocated_mb, FILEPROPERTY(name, 'SpaceUsed') * 8 / 1024 AS used_mb FROM sys.database_files;
-- per table: rows, reserved/data/index MB
SELECT s.name + '.' + t.name AS [table], SUM(CASE WHEN p.index_id IN (0,1) THEN p.row_count ELSE 0 END) AS [rows],
  SUM(p.reserved_page_count) * 8 / 1024 AS reserved_mb,
  SUM(CASE WHEN p.index_id IN (0,1) THEN p.used_page_count ELSE 0 END) * 8 / 1024 AS heap_or_clustered_mb,
  SUM(CASE WHEN p.index_id > 1 THEN p.used_page_count ELSE 0 END) * 8 / 1024 AS nonclustered_mb
FROM sys.dm_db_partition_stats p JOIN sys.tables t ON t.object_id = p.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id
GROUP BY s.name, t.name ORDER BY reserved_mb DESC, [table];
-- data shape that matters for the list and reports
SELECT TicketStatus, COUNT_BIG(*) AS tickets, SUM(CASE WHEN AssignedTo IS NULL THEN 1 ELSE 0 END) AS unassigned FROM dbo.Tickets GROUP BY TicketStatus ORDER BY TicketStatus;
SELECT TOP 5 ProjectId, COUNT_BIG(*) AS tickets FROM dbo.Tickets GROUP BY ProjectId ORDER BY tickets DESC;
SELECT TOP 3 ProjectId, COUNT_BIG(*) AS tickets FROM dbo.Tickets GROUP BY ProjectId ORDER BY tickets ASC;
SELECT COUNT(DISTINCT Owner) AS owners, MAX(c) AS max_per_owner, MIN(c) AS min_per_owner FROM (SELECT Owner, COUNT_BIG(*) c FROM dbo.Tickets GROUP BY Owner) x;
SELECT COUNT(DISTINCT AssignedTo) AS assignees, MAX(c) AS max_open_per_assignee, MIN(c) AS min_open_per_assignee FROM (SELECT AssignedTo, COUNT_BIG(*) c FROM dbo.Tickets WHERE TicketStatus <> 3 AND AssignedTo IS NOT NULL GROUP BY AssignedTo) x;
SELECT r.Name AS role, COUNT_BIG(*) AS users FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId GROUP BY r.Name ORDER BY r.Name;
SELECT MIN(CreatedDate) AS first_created, MAX(CreatedDate) AS last_created, MAX(LastUpdateDate) AS last_update FROM dbo.Tickets;
SELECT AVG(CAST(DATALENGTH(Details) AS bigint)) AS avg_details_bytes, MAX(DATALENGTH(Details)) AS max_details_bytes FROM dbo.Tickets;
