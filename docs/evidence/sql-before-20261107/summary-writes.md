# td-sqlperf: writes on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`.

Started 2026-11-08T01:02:14.3508475+00:00, finished 2026-11-08T01:02:15.0651542+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T01:02:14Z
host: lab-host, 32 cores, load average 2.76 8.60 19.02
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: e509ff9
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| write-reassign | cold | 5 | 1 / 1 | 1 / 1 | 6 | 3 | 2.64 / 3.35 | 0 |
| write-reassign | warm | 20 | 0 / 0 | 0 / 0 | 6 | 0 | 0.34 / 0.41 | 0 |
| write-insert | cold | 5 | 1 / 2 | 0 / 1 | 12 | 8 | 2.64 / 3.36 | 0 |
| write-insert | warm | 20 | 0 / 0 | 0 / 0 | 12 | 0 | 0.26 / 0.33 | 0 |

Scenarios:

- `write-reassign`: EF6-shaped single-ticket reassignment UPDATE, rolled back (1 command(s); plans: `plans/writes.write-reassign.cmd1.stmt1.sqlplan`, `plans/writes.write-reassign.cmd1.stmt2.sqlplan`)
- `write-insert`: EF6-shaped ticket INSERT, rolled back (1 command(s); plans: `plans/writes.write-insert.cmd1.stmt1.sqlplan`, `plans/writes.write-insert.cmd1.stmt2.sqlplan`)
