-- Query 118 (usp_TicketList's count for the tag shape) has two plans in Query Store: 136 (serial, adaptive join) and
-- 226 (parallel, bitmap). Force each in turn and time both tags under it; then decide.
SET NOCOUNT ON;
USE TicketDeskL;
DECLARE @c int, @i int;
DECLARE @plans TABLE (plan_id bigint);
INSERT @plans VALUES (226), (136);
DECLARE @p bigint;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT plan_id FROM @plans;
OPEN cur; FETCH NEXT FROM cur INTO @p;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sys.sp_query_store_force_plan @query_id = 118, @plan_id = @p;
    ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
    EXEC dbo.usp_TicketList @Tag = N'printer', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT; -- warm-up / compile under the forced plan
    EXEC dbo.usp_TicketList @Tag = N'mobile-108', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
    RAISERROR('=== forced plan %I64d', 0, 1, @p) WITH NOWAIT;
    SET STATISTICS TIME ON;
    SET @i = 0;
    WHILE @i < 5 BEGIN RAISERROR('--- common tag (printer)', 0, 1) WITH NOWAIT; EXEC dbo.usp_TicketList @Tag = N'printer', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT; SET @i += 1; END;
    SET @i = 0;
    WHILE @i < 5 BEGIN RAISERROR('--- rare tag (mobile-108)', 0, 1) WITH NOWAIT; EXEC dbo.usp_TicketList @Tag = N'mobile-108', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT; SET @i += 1; END;
    SET STATISTICS TIME OFF;
    EXEC sys.sp_query_store_unforce_plan @query_id = 118, @plan_id = @p;
    FETCH NEXT FROM cur INTO @p;
END;
CLOSE cur; DEALLOCATE cur;
EXEC sys.sp_query_store_flush_db;
SELECT plan_id, is_forced_plan, force_failure_count, last_force_failure_reason_desc FROM sys.query_store_plan WHERE query_id = 118;
