-- Expand, step 1 of 2, for Tickets.AssignedDate: when the ticket got its current assignee. The assignee-workload
-- report needs it; before this column it had to be derived from TicketEvents for every open ticket (docs/sql/tuning.md).
--
-- Why the legacy app survives this: EF6 lists its columns explicitly in every SELECT/INSERT/UPDATE and checks only its
-- own __MigrationHistory model hash, never the physical schema; a new NULLable column needs no value from it.
-- Dual write: the legacy app does not know the column, so this trigger sets it whenever a statement changes AssignedTo
-- without setting AssignedDate. The new app sets AssignedDate itself (EF Core is told about the trigger with
-- HasTrigger, so it does not use UPDATE ... OUTPUT, which SQL Server rejects on tables with triggers).
-- The trigger does not disturb EF6's "UPDATE ...; SELECT [Version] ... WHERE @@ROWCOUNT > 0" batches: @@ROWCOUNT after
-- the statement is the statement's own count (tested in TicketDesk.Database.Tests).
-- Contract (after the legacy app is retired): contract/C0001__assigned_date_contract.sql.

IF COL_LENGTH(N'dbo.Tickets', N'AssignedDate') IS NULL
    ALTER TABLE dbo.Tickets ADD AssignedDate datetimeoffset(7) NULL;
GO

CREATE OR ALTER TRIGGER dbo.TR_Tickets_AssignedDate ON dbo.Tickets
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        -- UPDATE: act only when the statement changed AssignedTo and did not set AssignedDate itself
        IF NOT UPDATE(AssignedTo) OR UPDATE(AssignedDate) RETURN;
        UPDATE t
        SET AssignedDate = CASE WHEN i.AssignedTo IS NULL THEN NULL ELSE i.LastUpdateDate END
        FROM dbo.Tickets AS t
        JOIN inserted AS i ON i.TicketId = t.TicketId
        JOIN deleted AS d ON d.TicketId = i.TicketId
        WHERE NOT (i.AssignedTo = d.AssignedTo OR (i.AssignedTo IS NULL AND d.AssignedTo IS NULL));
    END
    ELSE
    BEGIN
        -- INSERT: a ticket created with an assignee was assigned when it was created
        UPDATE t
        SET AssignedDate = i.CreatedDate
        FROM dbo.Tickets AS t
        JOIN inserted AS i ON i.TicketId = t.TicketId
        WHERE i.AssignedTo IS NOT NULL AND i.AssignedDate IS NULL;
    END
END;
