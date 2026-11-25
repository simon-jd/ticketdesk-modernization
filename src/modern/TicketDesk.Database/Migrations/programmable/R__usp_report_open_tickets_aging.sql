-- GET /api/reports/open-tickets: open tickets (status <> Closed) by project, status and age bucket.
-- Age = DATEDIFF(hour, CreatedDate, @AsOf); buckets <1d, 1-7d, 7-30d, 30-90d, >90d (AgeBuckets in the Reports module).
-- DATEDIFF(hour, c, a) counts hour boundaries in UTC, so DATEDIFF(hour, c, a) < N exactly when c is at or after
-- (start of a's UTC hour) - (N - 1) hours: the four bucket edges are computed once and each row costs four comparisons
-- instead of a DATEDIFF per CASE branch (the first version spent 180 ms CPU on that at scale L).
-- One covered scan of IX_Tickets_Open_Status (filtered, ~1/30 of the clustered index). OPTION (RECOMPILE): a report
-- runs a few times a day, so a compile per call costs nothing and lets the optimizer drop the unused @ProjectId branch.
CREATE OR ALTER PROCEDURE dbo.usp_Report_OpenTicketsAging
    @AsOf datetimeoffset(7),
    @ProjectId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @epoch datetimeoffset(7) = '2000-01-01T00:00:00+00:00';
    DECLARE @hour datetimeoffset(7) = DATEADD(hour, DATEDIFF(hour, @epoch, @AsOf), @epoch);   -- start of @AsOf's UTC hour
    DECLARE @e0 datetimeoffset(7) = DATEADD(hour, -23, @hour),     -- DATEDIFF < 24   -> bucket 0
            @e1 datetimeoffset(7) = DATEADD(hour, -167, @hour),    -- DATEDIFF < 168  -> bucket 1
            @e2 datetimeoffset(7) = DATEADD(hour, -719, @hour),    -- DATEDIFF < 720  -> bucket 2
            @e3 datetimeoffset(7) = DATEADD(hour, -2159, @hour);   -- DATEDIFF < 2160 -> bucket 3
    SELECT x.ProjectId, p.ProjectName, x.TicketStatus, x.AgeBucket, x.Tickets
    FROM (
        SELECT t.ProjectId, t.TicketStatus, b.AgeBucket, COUNT(*) AS Tickets
        FROM dbo.Tickets AS t
        CROSS APPLY (SELECT CASE WHEN t.CreatedDate >= @e0 THEN 0 WHEN t.CreatedDate >= @e1 THEN 1 WHEN t.CreatedDate >= @e2 THEN 2
                                 WHEN t.CreatedDate >= @e3 THEN 3 ELSE 4 END AS AgeBucket) AS b
        WHERE t.TicketStatus <> 3 AND (@ProjectId IS NULL OR t.ProjectId = @ProjectId)
        GROUP BY t.ProjectId, t.TicketStatus, b.AgeBucket
    ) AS x
    JOIN dbo.Projects AS p ON p.ProjectId = x.ProjectId
    ORDER BY p.ProjectName, x.ProjectId, x.TicketStatus, x.AgeBucket
    OPTION (RECOMPILE);
END;
