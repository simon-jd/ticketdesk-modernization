-- assigned-date-audit.sql - TEST ONLY, on a throwaway copy (the lab VM before a snapshot reset): log every write to
-- dbo.Tickets with the values after TR_Tickets_AssignedDate ran, so each legacy write can be checked, not only the end state.
--   sqlcmd -v Action=install | report | remove
SET NOCOUNT ON;
IF '$(Action)' = 'install'
BEGIN
    IF OBJECT_ID('dbo.zz_AssignedDateAudit') IS NULL
        CREATE TABLE dbo.zz_AssignedDateAudit (Id int IDENTITY PRIMARY KEY, LoggedAt datetimeoffset(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
            TicketId int NOT NULL, Op char(1) NOT NULL, OldAssignedTo nvarchar(256) NULL, NewAssignedTo nvarchar(256) NULL,
            AssignedDate datetimeoffset(7) NULL, LastUpdateDate datetimeoffset(7) NULL, NestLevel int NOT NULL);
    EXEC('CREATE OR ALTER TRIGGER dbo.zz_TR_Tickets_AssignedDateAudit ON dbo.Tickets AFTER INSERT, UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        INSERT dbo.zz_AssignedDateAudit (TicketId, Op, OldAssignedTo, NewAssignedTo, AssignedDate, LastUpdateDate, NestLevel)
        SELECT t.TicketId, CASE WHEN d.TicketId IS NULL THEN ''I'' ELSE ''U'' END, d.AssignedTo, t.AssignedTo, t.AssignedDate, t.LastUpdateDate, TRIGGER_NESTLEVEL()
        FROM inserted i JOIN dbo.Tickets t ON t.TicketId = i.TicketId LEFT JOIN deleted d ON d.TicketId = i.TicketId;
    END');
    EXEC sp_settriggerorder @triggername = 'dbo.zz_TR_Tickets_AssignedDateAudit', @order = 'Last', @stmttype = 'UPDATE';
    EXEC sp_settriggerorder @triggername = 'dbo.zz_TR_Tickets_AssignedDateAudit', @order = 'Last', @stmttype = 'INSERT';
    PRINT 'audit trigger installed';
END
ELSE IF '$(Action)' = 'report'
BEGIN
    -- one row per legacy statement (nest level 1 = the app's own INSERT/UPDATE; the expand trigger's own UPDATE shows as level 2)
    SELECT a.Id, a.TicketId, a.Op,
           CASE WHEN a.OldAssignedTo IS NULL AND a.NewAssignedTo IS NULL THEN '-'
                WHEN a.Op = 'I' THEN CASE WHEN a.NewAssignedTo IS NULL THEN 'insert unassigned' ELSE 'insert assigned' END
                WHEN a.OldAssignedTo IS NULL THEN 'NULL -> user'
                WHEN a.NewAssignedTo IS NULL THEN 'user -> NULL'
                WHEN a.OldAssignedTo <> a.NewAssignedTo THEN 'user -> other user'
                ELSE 'unchanged' END AS assignment,
           CONVERT(varchar(33), a.AssignedDate, 127) AS AssignedDate, CONVERT(varchar(33), a.LastUpdateDate, 127) AS LastUpdateDate,
           CASE WHEN a.NewAssignedTo IS NULL THEN CASE WHEN a.AssignedDate IS NULL THEN 'ok' ELSE 'BAD' END
                WHEN a.Op = 'I' OR a.OldAssignedTo IS NULL OR a.OldAssignedTo <> a.NewAssignedTo
                     THEN CASE WHEN a.AssignedDate = a.LastUpdateDate THEN 'ok (= this write''s LastUpdateDate)' ELSE 'BAD' END
                ELSE CASE WHEN a.AssignedDate IS NOT NULL AND a.AssignedDate <= a.LastUpdateDate THEN 'ok (kept)' ELSE 'BAD' END END AS verdict
    FROM dbo.zz_AssignedDateAudit a WHERE a.NestLevel = 1 ORDER BY a.Id;
    SELECT COUNT(*) AS writes,
           SUM(CASE WHEN Op = 'I' OR ISNULL(OldAssignedTo, N'') <> ISNULL(NewAssignedTo, N'') THEN 1 ELSE 0 END) AS assignment_changes
    FROM dbo.zz_AssignedDateAudit WHERE NestLevel = 1;
END
ELSE IF '$(Action)' = 'remove'
BEGIN
    DROP TRIGGER IF EXISTS dbo.zz_TR_Tickets_AssignedDateAudit;
    DROP TABLE IF EXISTS dbo.zz_AssignedDateAudit;
    PRINT 'audit trigger removed';
END
