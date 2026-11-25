-- GET /api/tickets: one page of the ticket center plus the total count, in one round trip.
--
-- Lists: the five legacy lists (UserTicketListSetting.GetDefaultListSettings), ANDed with the explicit filters, the
-- same semantics as the API's LINQ reference (TicketQueries.Filter), which the tests compare against.
--
-- Safe sorting (S5): @Sort is "field,-field,..."; every field is looked up in the whitelist below and only the
-- whitelisted column expression is pasted into the ORDER BY. Unknown fields raise error 50013. TicketId is appended
-- as the final tie-breaker so paging is deterministic.
--
-- Parameter sniffing, deliberately:
--  * Dynamic SQL from fixed fragments gives each filter/sort shape its own cached plan, so a plan compiled for
--    "list=unassigned" is never reused for "tag=x&sort=title". All values stay parameters of sp_executesql (no
--    injection, plan reuse within a shape); only status codes from @StatusMask's bits become literals, so the filtered
--    indexes (WHERE TicketStatus <> 3) match.
--  * Within the indexed shapes (the five lists, project, owner, assignee) the index puts the equality column first and
--    the ORDER BY after it, so the cached plan (seek, read one page in order) is right for every value.
--  * The tag count is the one statement whose plan flips with the compile-time estimate: its serial plan (adaptive join)
--    costs just around the parallelism threshold, and the parallel plan with a bitmap filter is about twice as fast
--    for common and rare tags alike (docs/sql/tuning.md, Query Store: plan 226 was forced for it on TicketDeskL).
--    A forced plan lives in one database's Query Store, does not travel in a BACPAC and stops matching when the text
--    changes, so the choice lives here instead: the tag shape's count asks for the parallel plan
--    (USE HINT 'ENABLE_PARALLEL_PLAN_PREFERENCE'; accepted by SQL Server 2016 SP1+ and Azure SQL Database, though not
--    listed in sys.dm_exec_valid_use_hints). No other shape gets a hint.
--  * The page is a deferred join: the inner query finds the page's TicketIds in a narrow index (OFFSET/FETCH there),
--    the outer query reads the full rows and display names for those 20 ids only, so deep pages do 20 lookups, not
--    offset + 20.
CREATE OR ALTER PROCEDURE dbo.usp_TicketList
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
    -- read the last committed versions (V0013): no S locks, so a concurrent ticket write cannot deadlock this read through
    -- the IX_Tickets_LastUpdateDate -> PK key lookup; only where snapshot isolation is on (V0013), and never inside a caller's
    -- transaction (switching to SNAPSHOT there fails once that transaction has read data)
    IF @@TRANCOUNT = 0 AND (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) = 1
        SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
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
