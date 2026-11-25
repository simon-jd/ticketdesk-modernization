-- td-db: no-transaction
-- Row versioning for the new app's list reads (AKS round, 2026-11-11). Found on kind under the AKS session's probe (one read
-- and one comment write per second): usp_TicketList, at READ COMMITTED, holds S on an IX_Tickets_LastUpdateDate key and asks
-- for S on the same ticket's PK_dbo.Tickets key (the key lookup), while the comment write holds X on that PK key (its UPDATE
-- of the ticket row) and asks for X on the IX_Tickets_LastUpdateDate key (LastUpdateDate changes): deadlock, the reader is the
-- victim (1205), the request answers 500. 5 of 5 deadlock graphs in system_health had exactly this shape
-- (docs/evidence/aks-20261111/ado-cd/deadlock/).
--
-- ALLOW_SNAPSHOT_ISOLATION lets one session read the last committed version of a row instead of waiting for its lock;
-- R__usp_ticket_list switches to SNAPSHOT itself, nothing else changes: READ_COMMITTED_SNAPSHOT stays as it is (turning it
-- on needs exclusive access to the database and would change every reader of both apps), writers keep their locks, the
-- legacy app is not affected. Setting it on waits for open transactions (PENDING_ON) but takes no exclusive access and kills
-- no connection. Azure SQL Database has it on by default; this makes the lab's SQL Server and an imported database the same.
-- ALTER DATABASE cannot run in a user transaction (hence no-transaction); rerunnable.
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID()) <> 1
    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;
