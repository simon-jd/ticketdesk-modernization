-- td-db: no-transaction
-- Expand, step 2 of 2: backfill Tickets.AssignedDate for tickets assigned before the trigger existed.
-- Rule, from the legacy TicketActionManager: the latest TakeOver (16), Assign (64), ReAssign (128) or Pass (256) event,
-- or a ReOpen (1024) written by the current assignee (ReOpen with "assign to me"); a ticket assigned when it was
-- created has no such event and gets its CreatedDate.
-- Batches of 4,000 tickets by key range, each statement its own short transaction: under the 5,000-lock escalation
-- threshold, so the batch holds row locks, never a table lock, legacy writes wait at most one batch and the log stays
-- small. Rerunnable: only rows still NULL are touched, so rows the trigger has already set
-- (tickets reassigned while the backfill runs) keep the trigger's value.
SET NOCOUNT ON;
DECLARE @batch int = 4000, @from int = 0, @max int, @rows int, @total bigint = 0;
SELECT @max = MAX(TicketId) FROM dbo.Tickets;
WHILE @from < ISNULL(@max, 0)
BEGIN
    UPDATE t
    SET AssignedDate = COALESCE(e.LastAssigned, t.CreatedDate)
    FROM dbo.Tickets AS t
    OUTER APPLY (
        SELECT MAX(ev.EventDate) AS LastAssigned
        FROM dbo.TicketEvents AS ev
        WHERE ev.TicketId = t.TicketId
          AND ((ev.ForActivity & 464) <> 0 OR ((ev.ForActivity & 1024) <> 0 AND ev.EventBy = t.AssignedTo))
    ) AS e
    WHERE t.TicketId > @from AND t.TicketId <= @from + @batch
      AND t.AssignedTo IS NOT NULL AND t.AssignedDate IS NULL;
    SET @rows = @@ROWCOUNT;
    SET @total += @rows;
    SET @from += @batch;
    IF (@from / @batch) % 50 = 0 RAISERROR('AssignedDate backfill: through TicketId %d, %I64d rows', 0, 1, @from, @total) WITH NOWAIT;
END;
RAISERROR('AssignedDate backfill done: %I64d rows', 0, 1, @total) WITH NOWAIT;
