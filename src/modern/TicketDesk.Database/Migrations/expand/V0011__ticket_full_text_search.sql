-- td-db: no-transaction
-- Full-text search for ticket search in the new app (Round 3; replaces the legacy Lucene.Net index on the VM disk, L4/C1).
-- The legacy index held Title, Details, the tag list and every event comment of a ticket; here that is
--   * dbo.Tickets      (Title, Details, TagList)  key: the primary key (TicketId)
--   * dbo.TicketEvents (Comment)                   key: UX_TicketEvents_EventId, new: a full-text key must be one unique,
--                                                  non-null column, and the EF6 primary key is (TicketId, EventId)
-- in one catalog, with automatic change tracking: SQL Server indexes every insert and update in the background, whichever
-- app wrote the row, so the legacy app needs no hook and its writes are searchable in the new app (C1 resolved).
--
-- Expand-only: one new index and the full-text objects; no column, constraint or existing index changes, so EF6 (which
-- lists columns explicitly and checks only its __MigrationHistory model hash) does not notice.
-- Full-text DDL cannot run in a user transaction (hence no-transaction); every step checks first, so a rerun after a
-- failure continues where it stopped. Word breaker and stemmer: English (LCID 1033), like the legacy StandardAnalyzer;
-- stoplist: the system stoplist. Population runs asynchronously after this script: `td-db fulltext --wait` waits for it.
--
-- Needs the Full-Text Search feature: SQL Server on Linux with the mssql-server-fts package (td-sql image
-- ops/dev/td-sql-image), SQL Server Express with Advanced Services + feature FullText (ops/windows/Install-TdSqlExpress.ps1
-- -FullText), Azure SQL Database (built in).
SET NOCOUNT ON;
IF CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS int) <> 1
    THROW 51011, N'V0011: Full-Text Search is not installed on this SQL Server instance (see the header of V0011__ticket_full_text_search.sql).', 1;

DECLARE @online bit = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN 1 ELSE 0 END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TicketEvents') AND name = N'UX_TicketEvents_EventId')
BEGIN
    DECLARE @ix nvarchar(400) = N'CREATE UNIQUE NONCLUSTERED INDEX UX_TicketEvents_EventId ON dbo.TicketEvents (EventId)'
        + CASE WHEN @online = 1 THEN N' WITH (ONLINE = ON, SORT_IN_TEMPDB = ON)' ELSE N' WITH (SORT_IN_TEMPDB = ON)' END + N';';
    EXEC sys.sp_executesql @ix;
    PRINT N'created UX_TicketEvents_EventId';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'ftc_TicketDesk')
BEGIN
    CREATE FULLTEXT CATALOG ftc_TicketDesk;
    PRINT N'created full-text catalog ftc_TicketDesk';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.Tickets'))
BEGIN
    DECLARE @pk sysname = (SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Tickets') AND is_primary_key = 1);
    DECLARE @ft nvarchar(1000) = N'CREATE FULLTEXT INDEX ON dbo.Tickets (Title LANGUAGE 1033, Details LANGUAGE 1033, TagList LANGUAGE 1033) KEY INDEX '
        + QUOTENAME(@pk) + N' ON ftc_TicketDesk WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM);';
    EXEC sys.sp_executesql @ft;
    PRINT N'created full-text index on dbo.Tickets (Title, Details, TagList)';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.TicketEvents'))
BEGIN
    CREATE FULLTEXT INDEX ON dbo.TicketEvents (Comment LANGUAGE 1033) KEY INDEX UX_TicketEvents_EventId
        ON ftc_TicketDesk WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM);
    PRINT N'created full-text index on dbo.TicketEvents (Comment)';
END
GO
