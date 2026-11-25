-- GET /api/reports/assignee-workload: per person with open assigned tickets, the open count by status, the oldest open
-- ticket, hours since the current assignment (Tickets.AssignedDate, kept by TR_Tickets_AssignedDate while the legacy
-- app writes), and tickets closed in the 30 days before @AsOf. Top @Top by open tickets.
-- Before AssignedDate existed, "assigned since" had to be derived from TicketEvents for each open ticket (the
-- first-pass LINQ did exactly that: 22 million logical reads at scale L; docs/sql/tuning.md).
-- Reads: an ordered, covered scan of IX_Tickets_Open_Assignee (stream aggregate, no sort for the grouping), one range
-- of IX_Tickets_Closed_StatusDate for the 30-day window, display names for the @Top rows only. OPTION (RECOMPILE) as in
-- the other reports.
CREATE OR ALTER PROCEDURE dbo.usp_Report_AssigneeWorkload
    @AsOf datetimeoffset(7),
    @ProjectId int = NULL,
    @Top int = 50,
    @UnassignedOpen int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 500 THROW 50020, N'@Top must be between 1 and 500.', 1;
    DECLARE @windowStart datetimeoffset(7) = DATEADD(day, -30, @AsOf);

    SELECT @UnassignedOpen = COUNT(*)
    FROM dbo.Tickets AS t
    WHERE t.TicketStatus <> 3 AND t.AssignedTo IS NULL AND (@ProjectId IS NULL OR t.ProjectId = @ProjectId)
    OPTION (RECOMPILE);

    WITH workload AS (
        SELECT t.AssignedTo,
               SUM(CASE WHEN t.TicketStatus = 0 THEN 1 ELSE 0 END) AS Active,
               SUM(CASE WHEN t.TicketStatus = 1 THEN 1 ELSE 0 END) AS MoreInfo,
               SUM(CASE WHEN t.TicketStatus = 2 THEN 1 ELSE 0 END) AS Resolved,
               COUNT(*) AS [Open],
               MIN(t.CreatedDate) AS OldestOpenCreatedDate,
               AVG(CAST(DATEDIFF(hour, t.AssignedDate, @AsOf) AS float)) AS AvgHoursSinceAssigned,
               MAX(DATEDIFF(hour, t.AssignedDate, @AsOf)) AS MaxHoursSinceAssigned
        FROM dbo.Tickets AS t
        WHERE t.TicketStatus <> 3 AND t.AssignedTo IS NOT NULL AND (@ProjectId IS NULL OR t.ProjectId = @ProjectId)
        GROUP BY t.AssignedTo
    ), ranked AS (
        SELECT w.*, ROW_NUMBER() OVER (ORDER BY w.[Open] DESC, w.AssignedTo) AS Rank, COUNT(*) OVER () AS Assignees
        FROM workload AS w
    ), closed AS (
        SELECT c.AssignedTo, COUNT(*) AS ClosedLast30Days
        FROM dbo.Tickets AS c
        WHERE c.TicketStatus = 3 AND c.CurrentStatusDate >= @windowStart AND c.CurrentStatusDate < @AsOf
          AND c.AssignedTo IS NOT NULL AND (@ProjectId IS NULL OR c.ProjectId = @ProjectId)
        GROUP BY c.AssignedTo
    )
    SELECT r.AssignedTo, u.DisplayName AS AssigneeDisplayName, r.Active, r.MoreInfo, r.Resolved, r.[Open],
           r.OldestOpenCreatedDate, r.AvgHoursSinceAssigned, r.MaxHoursSinceAssigned,
           ISNULL(c.ClosedLast30Days, 0) AS ClosedLast30Days, r.Assignees
    FROM ranked AS r
    LEFT JOIN closed AS c ON c.AssignedTo = r.AssignedTo
    LEFT JOIN dbo.IdentityUsers AS u ON u.Id = r.AssignedTo
    WHERE r.Rank <= @Top
    ORDER BY r.Rank
    OPTION (RECOMPILE);
END;
