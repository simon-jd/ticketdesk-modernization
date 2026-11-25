-- Fix for V0001's trigger, found by ExpandTests before any release: a change from NULL to an assignee (or from an
-- assignee to NULL) was missed, because "NOT (i.AssignedTo = d.AssignedTo OR (both NULL))" is UNKNOWN, not TRUE,
-- when exactly one side is NULL. EXISTS (SELECT i EXCEPT SELECT d) is the NULL-safe "is different" test (EXCEPT
-- treats NULLs as equal and compares with the column collation, like =). V0001 stays as it was applied; this
-- versioned script replaces the trigger body.
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
        WHERE EXISTS (SELECT i.AssignedTo EXCEPT SELECT d.AssignedTo);
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
