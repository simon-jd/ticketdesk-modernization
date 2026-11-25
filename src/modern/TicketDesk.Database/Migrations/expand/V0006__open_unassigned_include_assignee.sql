-- td-db: no-transaction
-- IX_Tickets_Open_Unassigned gets AssignedTo as an included column (docs/sql/tuning.md, carried from Round 2 sql-data).
-- The index is filtered on "TicketStatus <> 3 AND AssignedTo IS NULL", but a filter column that is neither a key nor
-- an included column cannot be evaluated from the index: the unassigned list's plan looked up all 20 page rows in the
-- clustered index just to re-check AssignedTo IS NULL, and its count chose a wider index. With AssignedTo included the
-- list is a seek of 20 rows in order and the count a scan of this (small) index only.
--
-- Rebuilt in place (DROP_EXISTING), ONLINE where the edition has it (Enterprise/Developer, Azure SQL), offline on
-- Express/Standard (the legacy VM's SQL Express: the index holds only the open unassigned tickets, milliseconds).
-- Skipped when AssignedTo is already included, so the script can be rerun after a failure.
SET NOCOUNT ON;
DECLARE @online bit = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN 1 ELSE 0 END;
DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Tickets') AND name = N'IX_Tickets_Open_Unassigned') THEN 1 ELSE 0 END;
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'dbo.Tickets') AND i.name = N'IX_Tickets_Open_Unassigned'
      AND c.name = N'AssignedTo' AND ic.is_included_column = 1)
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE NONCLUSTERED INDEX IX_Tickets_Open_Unassigned ON dbo.Tickets (TargetDate, DueDate, LastUpdateDate DESC)'
        + N' INCLUDE (AssignedTo) WHERE TicketStatus <> 3 AND AssignedTo IS NULL WITH ('
        + CASE WHEN @exists = 1 THEN N'DROP_EXISTING = ON, ' ELSE N'' END
        + N'SORT_IN_TEMPDB = ON' + CASE WHEN @online = 1 THEN N', ONLINE = ON' ELSE N'' END + N');';
    EXEC sys.sp_executesql @sql;
    RAISERROR('IX_Tickets_Open_Unassigned: AssignedTo included', 0, 1) WITH NOWAIT;
END;
