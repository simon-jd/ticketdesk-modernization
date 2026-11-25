-- GET /api/reports/time-to-close: tickets closed in [@From, @To) (TicketStatus = 3, CurrentStatusDate in the window),
-- hours from CreatedDate to closing: count, mean, p50, p90 and max, per project and overall (ProjectId NULL).
-- EF Core cannot translate percentiles, so the first-pass LINQ fetched one row per closed ticket and aggregated in
-- .NET; here the server returns one row per project.
-- Percentiles are PERCENTILE_CONT's definition (linear interpolation between ranks floor and ceiling of p * (n - 1)),
-- computed from one ROW_NUMBER per grouping: the first version used PERCENTILE_CONT ... OVER, which sorted the rows
-- once per window function (870 ms CPU at scale L, docs/sql/tuning.md).
-- One range seek on IX_Tickets_Closed_StatusDate per grouping (per project, overall).
CREATE OR ALTER PROCEDURE dbo.usp_Report_TimeToClose
    @From datetimeoffset(7),
    @To datetimeoffset(7),
    @ProjectId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @From IS NULL OR @To IS NULL OR @From >= @To THROW 50030, N'@From must be earlier than @To.', 1;

    WITH closed AS (
        SELECT t.ProjectId, CAST(DATEDIFF(second, t.CreatedDate, t.CurrentStatusDate) AS float) / 3600.0 AS Hours
        FROM dbo.Tickets AS t
        WHERE t.TicketStatus = 3 AND t.CurrentStatusDate >= @From AND t.CurrentStatusDate < @To
          AND (@ProjectId IS NULL OR t.ProjectId = @ProjectId)
    ), ranked AS (
        SELECT c.ProjectId, c.Hours,
               ROW_NUMBER() OVER (PARTITION BY c.ProjectId ORDER BY c.Hours) - 1 AS i,
               COUNT(*) OVER (PARTITION BY c.ProjectId) AS n
        FROM closed AS c
        UNION ALL
        SELECT NULL, c.Hours, ROW_NUMBER() OVER (ORDER BY c.Hours) - 1, COUNT(*) OVER ()
        FROM closed AS c
    ), grouped AS (
        SELECT r.ProjectId, r.n,
               AVG(r.Hours) AS AvgHours, MAX(r.Hours) AS MaxHours,
               MAX(CASE WHEN r.i = FLOOR(0.5 * (r.n - 1)) THEN r.Hours END) AS P50Lo,
               MAX(CASE WHEN r.i = CEILING(0.5 * (r.n - 1)) THEN r.Hours END) AS P50Hi,
               MAX(CASE WHEN r.i = FLOOR(0.9 * (r.n - 1)) THEN r.Hours END) AS P90Lo,
               MAX(CASE WHEN r.i = CEILING(0.9 * (r.n - 1)) THEN r.Hours END) AS P90Hi
        FROM ranked AS r
        GROUP BY r.ProjectId, r.n
    )
    SELECT g.ProjectId, p.ProjectName, CAST(g.n AS int) AS Closed, g.AvgHours,
           g.P50Lo + CAST(0.5 * (g.n - 1) - FLOOR(0.5 * (g.n - 1)) AS float) * (g.P50Hi - g.P50Lo) AS P50Hours,
           g.P90Lo + CAST(0.9 * (g.n - 1) - FLOOR(0.9 * (g.n - 1)) AS float) * (g.P90Hi - g.P90Lo) AS P90Hours,
           g.MaxHours
    FROM grouped AS g
    LEFT JOIN dbo.Projects AS p ON p.ProjectId = g.ProjectId
    ORDER BY CASE WHEN g.ProjectId IS NULL THEN 0 ELSE 1 END, p.ProjectName, g.ProjectId
    OPTION (RECOMPILE);
END;
