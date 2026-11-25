# td-sqlperf: tuned on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`.

Started 2026-11-08T07:28:06.1165515+00:00, finished 2026-11-08T07:28:07.6752451+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T07:28:05Z
host: lab-host, 32 cores, load average 6.77 5.50 2.69
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: 0f6d1bb
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-tag | cold | 5 | 89 / 111 | 174 / 227 | 3,382 | 3,032 | 90.18 / 115 | 0 |
| list-tag | warm | 20 | 27 / 51 | 115 / 165 | 3,126 | 0 | 28.06 / 52.58 | 0 |

Scenarios:

- `list-tag`: GET /api/tickets?tag=<most used tag> (default sort) (1 command(s); plans: `plans/tuned.list-tag.cmd1.stmt1.sqlplan`, `plans/tuned.list-tag.cmd1.stmt2.sqlplan`, `plans/tuned.list-tag.cmd1.stmt3.sqlplan`)
