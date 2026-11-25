-- check-assigned-date.sql - Tickets.AssignedDate after legacy writes (expand schema, trigger TR_Tickets_AssignedDate).
-- sqlcmd -v Since="<datetimeoffset>" : tickets written at or after Since are listed with the trigger's value next to the
-- backfill rule (latest TakeOver/Assign/ReAssign/Pass event, or ReOpen by the assignee; CreatedDate if assigned at creation).
SET NOCOUNT ON;
DECLARE @since datetimeoffset(7) = CAST('$(Since)' AS datetimeoffset(7));
SELECT 'all tickets' AS scope, COUNT(*) AS tickets,
       SUM(CASE WHEN AssignedTo IS NOT NULL THEN 1 ELSE 0 END) AS assigned,
       SUM(CASE WHEN (AssignedTo IS NULL AND AssignedDate IS NOT NULL) OR (AssignedTo IS NOT NULL AND AssignedDate IS NULL) THEN 1 ELSE 0 END) AS invariant_violations
FROM dbo.Tickets;
SELECT t.TicketId, CASE WHEN t.CreatedDate >= @since THEN 'created' ELSE 'updated' END AS written,
       CASE WHEN t.AssignedTo IS NULL THEN '(none)' ELSE 'assigned' END AS assignee,
       CONVERT(varchar(33), t.AssignedDate, 127) AS AssignedDate, CONVERT(varchar(33), t.LastUpdateDate, 127) AS LastUpdateDate,
       CONVERT(varchar(33), CASE WHEN t.AssignedTo IS NULL THEN NULL ELSE COALESCE(e.LastAssigned, t.CreatedDate) END, 127) AS RuleDate,
       CASE WHEN t.AssignedTo IS NULL THEN CASE WHEN t.AssignedDate IS NULL THEN 'ok' ELSE 'BAD (unassigned with a date)' END
            WHEN t.AssignedDate IS NULL THEN 'BAD (assigned without a date)'
            WHEN t.AssignedDate = COALESCE(e.LastAssigned, t.CreatedDate) THEN 'ok (= rule)'
            ELSE 'differs from rule' END AS verdict
FROM dbo.Tickets AS t
OUTER APPLY (SELECT MAX(ev.EventDate) AS LastAssigned FROM dbo.TicketEvents AS ev
             WHERE ev.TicketId = t.TicketId AND ((ev.ForActivity & 464) <> 0 OR ((ev.ForActivity & 1024) <> 0 AND ev.EventBy = t.AssignedTo))) AS e
WHERE t.LastUpdateDate >= @since OR t.CreatedDate >= @since
ORDER BY t.TicketId;
