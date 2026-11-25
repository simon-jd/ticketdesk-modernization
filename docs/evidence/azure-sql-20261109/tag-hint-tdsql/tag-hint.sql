-- The tag-count fix in code (R__usp_ticket_list.sql, USE HINT ENABLE_PARALLEL_PLAN_PREFERENCE) against the Query Store
-- forced plan it replaces, on td-sql's TicketDeskL (scale L; plan 226 forced for query 118 since Round 2 sql-data).
-- dbo.usp_TicketList        = the procedure as applied on TicketDeskL (its tag count runs under forced plan 226)
-- dbo.usp_TicketList_hint   = this round's procedure text under another name (no forced plan can match its count)
-- Each: one warm-up call per tag, then 5 timed calls per tag; elapsed from SYSDATETIME, CPU from sys.dm_exec_requests
-- (all threads of the request). usp_TicketList_hint is dropped at the end; nothing else changes in TicketDeskL.
SET NOCOUNT ON;
USE TicketDeskL;
GO
CREATE OR ALTER PROCEDURE dbo.usp_TicketList_hint
    @List varchar(20) = NULL,          -- unassigned | assignedToMe | mytickets | opentickets | historytickets | NULL
    @UserId nvarchar(256) = NULL,      -- current user (assignedToMe, mytickets)
    @StatusMask tinyint = NULL,        -- bit n set = TicketStatus n allowed (1 Active, 2 MoreInfo, 4 Resolved, 8 Closed)
    @ProjectId int = NULL,
    @Owner nvarchar(256) = NULL,
    @AssignedTo nvarchar(256) = NULL,
    @Tag nvarchar(100) = NULL,
    @Sort varchar(200) = NULL,         -- NULL = the list's default sort
    @Page int = 1,
    @PageSize int = 20,
    @TotalCount int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page IS NULL OR @Page < 1 OR @PageSize IS NULL OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50010, N'@Page must be >= 1 and @PageSize between 1 and 100.', 1;
    IF @StatusMask IS NOT NULL AND @StatusMask NOT BETWEEN 1 AND 15
        THROW 50011, N'@StatusMask must be between 1 and 15.', 1;

    DECLARE @where nvarchar(max) = N'', @defaultSort varchar(200);
    IF @List IS NULL SET @defaultSort = '-lastUpdateDate';
    ELSE IF @List = 'unassigned'
        SELECT @where += N' AND t.TicketStatus <> 3 AND t.AssignedTo IS NULL', @defaultSort = 'targetDate,dueDate,-lastUpdateDate';
    ELSE IF @List = 'assignedToMe'
        SELECT @where += N' AND t.TicketStatus <> 3 AND t.AssignedTo = @UserId', @defaultSort = 'status,targetDate,dueDate,-lastUpdateDate';
    ELSE IF @List = 'mytickets'
        SELECT @where += N' AND t.TicketStatus <> 3 AND t.Owner = @UserId', @defaultSort = 'targetDate,dueDate,-lastUpdateDate';
    ELSE IF @List = 'opentickets'
        SELECT @where += N' AND t.TicketStatus <> 3', @defaultSort = 'status,targetDate,dueDate,-lastUpdateDate';
    ELSE IF @List = 'historytickets'
        SELECT @where += N' AND t.TicketStatus = 3', @defaultSort = 'status,-lastUpdateDate';
    ELSE
        THROW 50012, N'Unknown @List.', 1;

    IF @StatusMask IS NOT NULL
        SET @where += N' AND t.TicketStatus IN (' + STUFF(
              CASE WHEN @StatusMask & 1 = 1 THEN N',0' ELSE N'' END
            + CASE WHEN @StatusMask & 2 = 2 THEN N',1' ELSE N'' END
            + CASE WHEN @StatusMask & 4 = 4 THEN N',2' ELSE N'' END
            + CASE WHEN @StatusMask & 8 = 8 THEN N',3' ELSE N'' END, 1, 1, N'') + N')';
    IF @ProjectId IS NOT NULL SET @where += N' AND t.ProjectId = @ProjectId';
    IF @Owner IS NOT NULL SET @where += N' AND t.Owner = @Owner';
    IF @AssignedTo IS NOT NULL SET @where += N' AND t.AssignedTo = @AssignedTo';
    IF @Tag IS NOT NULL SET @where += N' AND EXISTS (SELECT 1 FROM dbo.TicketTags AS g WHERE g.TicketId = t.TicketId AND g.TagName = @Tag)';

    -- sort whitelist: API field name (case-insensitive) -> column expression; one statement parses, checks and builds
    DECLARE @order nvarchar(max), @keys int, @unknown int, @duplicates int, @hasTicketId int;
    WITH k AS (
        SELECT s.ordinal AS Ordinal, LTRIM(RTRIM(REPLACE(REPLACE(s.value, '-', ''), '+', ''))) AS Field,
               CASE WHEN LTRIM(s.value) LIKE '-%' THEN 1 ELSE 0 END AS Descending
        FROM STRING_SPLIT(COALESCE(NULLIF(LTRIM(RTRIM(@Sort)), ''), @defaultSort), ',', 1) AS s
    ), f AS (
        SELECT Name, Expr FROM (VALUES
            ('ticketId', N't.TicketId'), ('projectId', N't.ProjectId'), ('title', N't.Title'), ('status', N't.TicketStatus'),
            ('ticketStatus', N't.TicketStatus'), ('priority', N't.Priority'), ('ticketType', N't.TicketType'),
            ('category', N't.Category'), ('owner', N't.Owner'), ('assignedTo', N't.AssignedTo'), ('createdBy', N't.CreatedBy'),
            ('createdDate', N't.CreatedDate'), ('lastUpdateDate', N't.LastUpdateDate'), ('currentStatusDate', N't.CurrentStatusDate'),
            ('dueDate', N't.DueDate'), ('targetDate', N't.TargetDate'), ('resolutionDate', N't.ResolutionDate')) AS v (Name, Expr)
    )
    SELECT @keys = COUNT(*),
           @unknown = SUM(CASE WHEN f.Expr IS NULL THEN 1 ELSE 0 END),
           @duplicates = COUNT(f.Expr) - COUNT(DISTINCT f.Expr),
           @hasTicketId = MAX(CASE WHEN f.Expr = N't.TicketId' THEN 1 ELSE 0 END),
           @order = STRING_AGG(CAST(f.Expr AS nvarchar(max)) + CASE WHEN k.Descending = 1 THEN N' DESC' ELSE N'' END, N', ')
                    WITHIN GROUP (ORDER BY k.Ordinal)
    FROM k LEFT JOIN f ON f.Name = k.Field COLLATE Latin1_General_CI_AS;
    IF @unknown > 0 THROW 50013, N'Unknown sort field.', 1;
    IF @keys > 5 OR @duplicates > 0 THROW 50014, N'At most 5 distinct sort fields.', 1;
    IF @hasTicketId = 0 SET @order += N', t.TicketId';

    DECLARE @params nvarchar(400) = N'@UserId nvarchar(256), @ProjectId int, @Owner nvarchar(256), @AssignedTo nvarchar(256), @Tag nvarchar(100)';
    DECLARE @countSql nvarchar(max) = N'SELECT @TotalCount = COUNT(*) FROM dbo.Tickets AS t WHERE 1 = 1' + @where
        + CASE WHEN @Tag IS NOT NULL THEN N' OPTION (USE HINT(''ENABLE_PARALLEL_PLAN_PREFERENCE''))' ELSE N'' END + N';';
    DECLARE @countParams nvarchar(500) = @params + N', @TotalCount int OUTPUT';
    EXEC sys.sp_executesql @countSql, @countParams, @UserId, @ProjectId, @Owner, @AssignedTo, @Tag, @TotalCount OUTPUT;

    DECLARE @pageSql nvarchar(max) = N'SELECT t.TicketId, t.ProjectId, t.Title, t.TicketType, t.Category, t.Priority, t.TicketStatus,
    t.Owner, o.DisplayName AS OwnerDisplayName, t.AssignedTo, a.DisplayName AS AssignedToDisplayName,
    t.CreatedDate, t.LastUpdateDate, t.DueDate, t.TargetDate, t.TagList
FROM (SELECT t.TicketId FROM dbo.Tickets AS t WHERE 1 = 1' + @where + N'
      ORDER BY ' + @order + N' OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY) AS k
JOIN dbo.Tickets AS t ON t.TicketId = k.TicketId
LEFT JOIN dbo.IdentityUsers AS o ON o.Id = t.Owner
LEFT JOIN dbo.IdentityUsers AS a ON a.Id = t.AssignedTo
ORDER BY ' + @order + N';';
    DECLARE @pageParams nvarchar(500) = @params + N', @Offset int, @PageSize int';
    DECLARE @offset int = (@Page - 1) * @PageSize;
    EXEC sys.sp_executesql @pageSql, @pageParams, @UserId, @ProjectId, @Owner, @AssignedTo, @Tag, @offset, @PageSize;
END;

GO
SET NOCOUNT ON;
DECLARE @r TABLE (proc_name sysname, tag nvarchar(100), run int, elapsed_ms decimal(10,1), cpu_ms int, total int);
DECLARE @procs TABLE (seq int, name sysname); INSERT @procs VALUES (1, N'dbo.usp_TicketList'), (2, N'dbo.usp_TicketList_hint');
DECLARE @tags TABLE (seq int, tag nvarchar(100)); INSERT @tags VALUES (1, N'printer'), (2, N'mobile-108');
DECLARE @p sysname, @t nvarchar(100), @i int, @c int, @t0 datetime2(7), @cpu0 int, @sql nvarchar(400);
DECLARE @ps int = 1, @ts int;
WHILE @ps <= 2
BEGIN
    SELECT @p = name FROM @procs WHERE seq = @ps;
    SET @ts = 1;
    WHILE @ts <= 2
    BEGIN
        SELECT @t = tag FROM @tags WHERE seq = @ts;
        SET @sql = N'EXEC ' + @p + N' @Tag = @tag, @Sort = ''-lastUpdateDate'', @TotalCount = @c OUTPUT;';
        EXEC sys.sp_executesql @sql, N'@tag nvarchar(100), @c int OUTPUT', @t, @c OUTPUT; -- warm-up
        SET @i = 1;
        WHILE @i <= 5
        BEGIN
            SELECT @t0 = SYSDATETIME(), @cpu0 = cpu_time FROM sys.dm_exec_requests WHERE session_id = @@SPID;
            EXEC sys.sp_executesql @sql, N'@tag nvarchar(100), @c int OUTPUT', @t, @c OUTPUT;
            INSERT @r SELECT @p, @t, @i, DATEDIFF(microsecond, @t0, SYSDATETIME()) / 1000.0, cpu_time - @cpu0, @c FROM sys.dm_exec_requests WHERE session_id = @@SPID;
            SET @i += 1;
        END;
        SET @ts += 1;
    END;
    SET @ps += 1;
END;
SELECT 'RUN' AS k, proc_name, tag, run, elapsed_ms, cpu_ms, total FROM @r ORDER BY proc_name, tag, run;
-- the count statements as cached: degree of parallelism of the last execution, and whether a forced plan is in play
SELECT 'STMT' AS k, CASE WHEN st.text LIKE N'%ENABLE_PARALLEL_PLAN_PREFERENCE%' THEN 'hint' ELSE 'forced-plan' END AS variant,
       qs.execution_count, qs.last_dop, qs.min_dop, qs.max_dop, qs.total_worker_time / qs.execution_count / 1000 AS avg_cpu_ms,
       qs.total_elapsed_time / qs.execution_count / 1000 AS avg_elapsed_ms
FROM sys.dm_exec_query_stats AS qs CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
CROSS APPLY sys.dm_exec_plan_attributes(qs.plan_handle) AS pa
WHERE pa.attribute = N'dbid' AND CAST(pa.value AS int) = DB_ID() AND st.objectid IS NULL
  AND st.text LIKE N'%SELECT @TotalCount = COUNT(*) FROM dbo.Tickets AS t WHERE 1 = 1 AND EXISTS (SELECT 1 FROM dbo.TicketTags%' AND st.text NOT LIKE N'%dm_exec%';
SELECT 'FORCED' AS k, q.query_id, p.plan_id, p.is_forced_plan, p.is_parallel_plan, p.last_execution_time
FROM sys.query_store_plan AS p JOIN sys.query_store_query AS q ON q.query_id = p.query_id WHERE p.is_forced_plan = 1;
DROP PROCEDURE dbo.usp_TicketList_hint;
SELECT 'HOST' AS k, @@VERSION AS version;
