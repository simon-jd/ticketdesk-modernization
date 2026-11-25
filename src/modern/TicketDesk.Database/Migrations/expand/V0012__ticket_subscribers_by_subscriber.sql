-- td-db: no-transaction
-- Search's per-ticket rule (S12) for internal users asks "is this user subscribed to the ticket?" for every full-text hit.
-- TicketSubscribers has only (TicketId, SubscriberId) and a TicketId index, so the question was a seek per hit (at scale L
-- "printer" as an internal user: 230k seeks, 1.03M logical reads). With an index on SubscriberId the user's subscriptions
-- are one seek, which the search joins with its hits.
-- Expand-only (a new nonclustered index); ONLINE where the edition has it, offline on Express (the legacy VM), created only
-- if missing so a rerun after a failure continues.
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TicketSubscribers') AND name = N'IX_TicketSubscribers_SubscriberId')
BEGIN
    DECLARE @online bit = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN 1 ELSE 0 END;
    DECLARE @ix nvarchar(400) = N'CREATE NONCLUSTERED INDEX IX_TicketSubscribers_SubscriberId ON dbo.TicketSubscribers (SubscriberId, TicketId)'
        + CASE WHEN @online = 1 THEN N' WITH (ONLINE = ON, SORT_IN_TEMPDB = ON)' ELSE N' WITH (SORT_IN_TEMPDB = ON)' END + N';';
    EXEC sys.sp_executesql @ix;
    PRINT N'created IX_TicketSubscribers_SubscriberId';
END
