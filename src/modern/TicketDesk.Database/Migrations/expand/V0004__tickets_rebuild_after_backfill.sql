-- td-db: no-transaction
-- The backfill (V0002) added 10 bytes to ~90% of the rows of a table that was full (bulk loaded), splitting most of
-- its pages (scale L: clustered index 107,198 -> 137,721 pages, docs/sql/tuning.md). Rebuild the clustered index where
-- the edition can do it online (Enterprise/Developer, Azure SQL). On Express/Standard (the legacy VM) an offline
-- rebuild locks Tickets for its whole duration, so it is skipped and left to a maintenance window; the bacpac import
-- into Azure SQL writes every page fresh anyway.
SET NOCOUNT ON;
DECLARE @pk sysname = (SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Tickets') AND index_id = 1);
IF CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8)
BEGIN
    DECLARE @sql nvarchar(400) = N'ALTER INDEX ' + QUOTENAME(@pk) + N' ON dbo.Tickets REBUILD WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);';
    EXEC sys.sp_executesql @sql;
    RAISERROR('rebuilt %s online', 0, 1, @pk) WITH NOWAIT;
END
ELSE
    RAISERROR('%s not rebuilt: no online rebuild in this edition; rebuild it in a maintenance window', 0, 1, @pk) WITH NOWAIT;
