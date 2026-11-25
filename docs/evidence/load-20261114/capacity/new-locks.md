# New app: locks behind the knee (td-load-sql, capacity runs)

SQL Server counters since the container started (12 rows, cumulative over every new-app curve):

```
Lock Timeouts/sec|Key|43929
Number of Deadlocks/sec|Key|0
Lock Waits/sec|Key|30842
Lock Timeouts/sec|Page|143372
Number of Deadlocks/sec|Page|1915
Lock Waits/sec|Page|114842
Lock Timeouts/sec|Object|26
Number of Deadlocks/sec|Object|0
Lock Waits/sec|Object|1
Lock Timeouts/sec|_Total|189148
Number of Deadlocks/sec|_Total|1915
Lock Waits/sec|_Total|152882
```

Deadlock reports in system_health: **1287** (2026-11-14T01:35:37.238Z .. 2026-11-14T02:17:17.310Z). Lock resources in them:

- 3894 x pagelock on dbo.Tickets, mode S
- 3624 x pagelock on dbo.Tickets, mode IX

Statements in each deadlock (inputbuf, classified):

- 946 deadlocks: comment write: SaveChanges (INSERT TicketEvents, notifications, UPDATE Tickets) + ticket-center COUNT(*) of the open tickets (EF Core, READ COMMITTED)
- 341 deadlocks: comment write: SaveChanges (INSERT TicketEvents, notifications, UPDATE Tickets) + create write: SaveChanges (INSERT Tickets ...) + ticket-center COUNT(*) of the open tickets (EF Core, READ COMMITTED)

Head blockers of the SQL blocking chains at 200 ops/s (sampler, every 4 s while >= 5 requests were blocked; 12 records over new-r2/r3):

| Records | Session status | Open transactions | Wait | Last statement |
|---:|---|---:|---|---|
| 4 | sleeping | 1 | - | INSERT TicketEventNotifications (comment/create SaveChanges) |
| 3 | running | 1 | PREEMPTIVE_OS_FLUSHFILEBUFFERS | (no statement text) |
| 2 | sleeping | 1 | - | INSERT TicketEvents (comment SaveChanges) |
| 1 | running | 1 | WRITELOG | (no statement text) |
| 1 | running | 1 | - | UPDATE UserSettings (filter post) |
| 1 | running | 1 | - | (no statement text) |
