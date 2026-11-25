# td-sqlperf: search on TicketDeskLSearch

Started 2026-11-10T18:50:16.0300090+00:00, finished 2026-11-10T18:50:20.2401865+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-10T18:50:15Z
host: lab-host, 32 cores, load average 2.92 2.43 2.13
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskLSearch; git: 06d7bc6 (uncommitted changes)
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| search-internal-user | cold | 5 | 247 / 263 | 787 / 825 | 111,365 | 112,059 | 249 / 265 | 0 |
| search-internal-user | warm | 20 | 118 / 126 | 706 / 769 | 111,365 | 0 | 120 / 128 | 0 |

Scenarios:

- `search-internal-user`: GET /search?term=printer as an internal user (S12 filter; owner with the most tickets) (1 command(s); plans: `plans/search.search-internal-user.cmd1.stmt1.sqlplan`)
