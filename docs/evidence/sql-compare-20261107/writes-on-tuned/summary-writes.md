# td-sqlperf: writes on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`.

Started 2026-11-08T01:32:53.2661512+00:00, finished 2026-11-08T01:32:54.1525833+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T01:32:53Z
host: lab-host, 32 cores, load average 3.57 5.96 11.26
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: 3adfe38 (uncommitted changes)
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| write-reassign | cold | 5 | 4 / 5 | 1 / 2 | 47 | 31 | 5.47 / 7.6 | 1 |
| write-reassign | warm | 20 | 0 / 0 | 0 / 1 | 47 | 0 | 0.83 / 1.06 | 0 |
| write-insert | cold | 5 | 5 / 6 | 1 / 2 | 36 | 25 | 6.22 / 7.97 | 1 |
| write-insert | warm | 20 | 0 / 0 | 0 / 0 | 36 | 0 | 0.56 / 0.68 | 0 |

Scenarios:

- `write-reassign`: EF6-shaped single-ticket reassignment UPDATE, rolled back (1 command(s); plans: `plans/writes.write-reassign.cmd1.stmt1.sqlplan`, `plans/writes.write-reassign.cmd1.stmt2.sqlplan`, `plans/writes.write-reassign.cmd1.stmt3.sqlplan`, `plans/writes.write-reassign.cmd1.stmt4.sqlplan`, `plans/writes.write-reassign.cmd1.stmt5.sqlplan`)
- `write-insert`: EF6-shaped ticket INSERT, rolled back (1 command(s); plans: `plans/writes.write-insert.cmd1.stmt1.sqlplan`, `plans/writes.write-insert.cmd1.stmt2.sqlplan`, `plans/writes.write-insert.cmd1.stmt3.sqlplan`, `plans/writes.write-insert.cmd1.stmt4.sqlplan`, `plans/writes.write-insert.cmd1.stmt5.sqlplan`)
