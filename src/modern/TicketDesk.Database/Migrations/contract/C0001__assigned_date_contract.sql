-- Contract for Tickets.AssignedDate. Scheduled for AFTER the legacy app is retired:
--   td-db migrate --contract --legacy-retired
-- In the same release: remove tb.HasTrigger("TR_Tickets_AssignedDate") from TicketConfiguration (EF Core then goes
-- back to UPDATE ... OUTPUT) and make the ticket write path the only place that sets AssignedDate.
-- The CHECK constraint is added WITH CHECK, which scans Tickets under a schema-modification lock: run it in the
-- retirement maintenance window, not during traffic.

-- 1. The new app is the only writer and sets AssignedDate itself: the dual-write trigger goes.
DROP TRIGGER IF EXISTS dbo.TR_Tickets_AssignedDate;
GO

-- 2. The invariant becomes a constraint: assigned tickets have an AssignedDate, unassigned ones do not.
--    (Repair first, in case anything wrote AssignedTo between the last trigger run and now.)
UPDATE dbo.Tickets SET AssignedDate = NULL WHERE AssignedTo IS NULL AND AssignedDate IS NOT NULL;
UPDATE dbo.Tickets SET AssignedDate = LastUpdateDate WHERE AssignedTo IS NOT NULL AND AssignedDate IS NULL;
GO
IF OBJECT_ID(N'dbo.CK_Tickets_AssignedDate', N'C') IS NULL
    ALTER TABLE dbo.Tickets WITH CHECK ADD CONSTRAINT CK_Tickets_AssignedDate
        CHECK ((AssignedTo IS NULL AND AssignedDate IS NULL) OR (AssignedTo IS NOT NULL AND AssignedDate IS NOT NULL));
GO

-- 3. IX_ProjectId (EF6 migration Td2.5.017) is a left prefix of IX_Tickets_Project_LastUpdateDate, which also supports
--    the FK to Projects; the legacy app's migration history was the only reason to keep it.
DROP INDEX IF EXISTS IX_ProjectId ON dbo.Tickets;
