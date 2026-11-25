-- td-db: no-transaction
-- Indexes for the ticket-center lists and the reports. Each one is justified by an actual plan in docs/sql/tuning.md;
-- none exists for arbitrary sorts on unindexed columns (title, category, ...), which still scan.
--
-- The list indexes put the equality column first and then the list's ORDER BY, so one plan (seek, read the first
-- page in index order) is the best plan for every parameter value: the heaviest owner and an owner with three tickets,
-- the biggest and the smallest project. That is half of the answer to parameter sniffing (usp_TicketList has the rest).
-- Filtered indexes need literal predicates in the query (TicketStatus <> 3), which usp_TicketList writes.
--
-- ONLINE where the edition has it (Enterprise/Developer, Azure SQL), offline on Express/Standard (the legacy VM);
-- created only if missing, so the script can be rerun after a failure. The runner's LOCK_TIMEOUT bounds how long a
-- build waits for its schema lock behind live traffic.
SET NOCOUNT ON;
DECLARE @online bit = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN 1 ELSE 0 END;
DECLARE @with nvarchar(200) = CASE WHEN @online = 1 THEN N' WITH (ONLINE = ON, SORT_IN_TEMPDB = ON)' ELSE N' WITH (SORT_IN_TEMPDB = ON)' END;
DECLARE @indexes TABLE (Seq int PRIMARY KEY, TableName nvarchar(256), IndexName sysname, Body nvarchar(1000));
INSERT @indexes (Seq, TableName, IndexName, Body) VALUES
    -- list=opentickets (ORDER BY status, targetDate, dueDate, -lastUpdateDate), its count, the open-ticket aging report
    (1, N'dbo.Tickets', N'IX_Tickets_Open_Status',
        N'(TicketStatus, TargetDate, DueDate, LastUpdateDate DESC) INCLUDE (ProjectId, CreatedDate) WHERE TicketStatus <> 3'),
    -- list=unassigned (help desk landing list) and its count; the workload report's unassigned count
    (2, N'dbo.Tickets', N'IX_Tickets_Open_Unassigned',
        N'(TargetDate, DueDate, LastUpdateDate DESC) WHERE TicketStatus <> 3 AND AssignedTo IS NULL'),
    -- list=assignedToMe; the assignee-workload report (stream aggregate in AssignedTo order, covered)
    (3, N'dbo.Tickets', N'IX_Tickets_Open_Assignee',
        N'(AssignedTo, TicketStatus, TargetDate, DueDate, LastUpdateDate DESC) INCLUDE (ProjectId, CreatedDate, AssignedDate) WHERE TicketStatus <> 3'),
    -- list=mytickets
    (4, N'dbo.Tickets', N'IX_Tickets_Open_Owner',
        N'(Owner, TargetDate, DueDate, LastUpdateDate DESC) WHERE TicketStatus <> 3'),
    -- default list (-lastUpdateDate) and list=historytickets (TicketStatus = 3, -lastUpdateDate)
    (5, N'dbo.Tickets', N'IX_Tickets_LastUpdateDate',
        N'(LastUpdateDate DESC) INCLUDE (TicketStatus)'),
    -- projectId filter with the default sort; makes the EF6 index IX_ProjectId redundant (dropped in the contract stage)
    (6, N'dbo.Tickets', N'IX_Tickets_Project_LastUpdateDate',
        N'(ProjectId, LastUpdateDate DESC)'),
    -- time-to-close report; tickets closed in the last 30 days per assignee (workload report)
    (7, N'dbo.Tickets', N'IX_Tickets_Closed_StatusDate',
        N'(CurrentStatusDate) INCLUDE (CreatedDate, ProjectId, AssignedTo) WHERE TicketStatus = 3'),
    -- tag filter (EXISTS on TicketTags by name)
    (8, N'dbo.TicketTags', N'IX_TicketTags_TagName',
        N'(TagName, TicketId)');

DECLARE @seq int = 1, @table nvarchar(256), @name sysname, @body nvarchar(1000), @sql nvarchar(max);
WHILE EXISTS (SELECT 1 FROM @indexes WHERE Seq = @seq)
BEGIN
    SELECT @table = TableName, @name = IndexName, @body = Body FROM @indexes WHERE Seq = @seq;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(@table) AND name = @name)
    BEGIN
        SET @sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@name) + N' ON ' + @table + N' ' + @body + @with + N';';
        EXEC sys.sp_executesql @sql;
        RAISERROR('created %s', 0, 1, @name) WITH NOWAIT;
    END;
    SET @seq += 1;
END;
