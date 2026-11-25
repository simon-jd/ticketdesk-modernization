-- Parameter-sniffing probe for the one usp_TicketList shape whose filter has no index that also gives the order:
-- tag filter + default sort. Run each tag after compiling the shape for the other tag, then look in Query Store.
SET NOCOUNT ON;
USE TicketDeskL;
DECLARE @c int;
PRINT '=== A: cache cleared, compiled for the RARE tag (mobile-108), then the COMMON tag (printer) reuses that plan';
ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
EXEC dbo.usp_TicketList @Tag = N'mobile-108', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
SET STATISTICS IO, TIME ON;
PRINT '--- A1 rare tag, own plan';
EXEC dbo.usp_TicketList @Tag = N'mobile-108', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
PRINT '--- A2 common tag, plan compiled for the rare tag';
EXEC dbo.usp_TicketList @Tag = N'printer', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
SET STATISTICS IO, TIME OFF;
PRINT '=== B: cache cleared, compiled for the COMMON tag, then the RARE tag reuses that plan';
ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
EXEC dbo.usp_TicketList @Tag = N'printer', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
SET STATISTICS IO, TIME ON;
PRINT '--- B1 common tag, own plan';
EXEC dbo.usp_TicketList @Tag = N'printer', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
PRINT '--- B2 rare tag, plan compiled for the common tag';
EXEC dbo.usp_TicketList @Tag = N'mobile-108', @Sort = '-lastUpdateDate', @TotalCount = @c OUTPUT;
SET STATISTICS IO, TIME OFF;
EXEC sys.sp_query_store_flush_db;
