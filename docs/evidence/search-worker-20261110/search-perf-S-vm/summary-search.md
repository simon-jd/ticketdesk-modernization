# td-sqlperf: search on TicketDesk

Started 2026-11-10T19:04:11.9122309+00:00, finished 2026-11-10T19:04:21.6795275+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-10T19:04:10Z
host: lab VM TDLEGACY01 (qemu/KVM, 4 vCPU, 8 GB) on lab-host, host load average 3.47 2.92 2.67
sql: 16.0.1000.6 Express Edition (64-bit), full-text 1 (VM SQL Express)
database: TicketDesk on snapshot baseline-s-search (scale S); git: 20f1269
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| search-printer | cold | 5 | 6 / 6 | 0 / 16 | 48 | 46 | 6.87 / 9.19 | 0 |
| search-printer | warm | 20 | 4 / 5 | 0 / 16 | 48 | 0 | 5.34 / 5.62 | 0 |
| search-network | cold | 5 | 9 / 11 | 0 / 0 | 2,250 | 126 | 10.42 / 12.25 | 0 |
| search-network | warm | 20 | 5 / 5 | 0 / 16 | 2,250 | 0 | 5.93 / 6.16 | 0 |
| search-team | cold | 5 | 10 / 12 | 15 / 16 | 3,110 | 99 | 10.58 / 13.32 | 0 |
| search-team | warm | 20 | 5 / 5 | 0 / 16 | 3,110 | 0 | 5.77 / 6.06 | 0 |
| search-two-words | cold | 5 | 12 / 13 | 0 / 0 | 2,250 | 126 | 13.41 / 13.61 | 0 |
| search-two-words | warm | 20 | 7 / 8 | 0 / 16 | 2,250 | 0 | 8.3 / 9.26 | 0 |
| search-required | cold | 5 | 255 / 422 | 250 / 375 | 5,474 | 206 | 256 / 423 | 0 |
| search-required | warm | 20 | 168 / 178 | 172 / 172 | 5,474 | 0 | 169 / 179 | 0 |
| search-excluded | cold | 5 | 33 / 37 | 16 / 32 | 129 | 126 | 34.04 / 38.9 | 0 |
| search-excluded | warm | 20 | 12 / 13 | 15 / 16 | 129 | 0 | 13.16 / 13.95 | 0 |
| search-phrase | cold | 5 | 3 / 4 | 0 / 15 | 20 | 19 | 4.53 / 4.64 | 0 |
| search-phrase | warm | 20 | 2 / 2 | 0 / 16 | 20 | 0 | 2.64 / 3.04 | 0 |
| search-prefix | cold | 5 | 6 / 7 | 0 / 15 | 48 | 46 | 7.44 / 7.66 | 0 |
| search-prefix | warm | 20 | 5 / 6 | 0 / 16 | 48 | 0 | 5.98 / 6.72 | 0 |
| search-tag-field | cold | 5 | 4 / 8 | 0 / 16 | 48 | 46 | 5.39 / 9.01 | 0 |
| search-tag-field | warm | 20 | 3 / 3 | 0 / 16 | 48 | 0 | 4.12 / 4.35 | 0 |
| search-ticket-id | cold | 5 | 2 / 3 | 0 / 0 | 6 | 3 | 2.6 / 4.14 | 0 |
| search-ticket-id | warm | 20 | 0 / 0 | 0 / 0 | 6 | 0 | 0.94 / 1.01 | 0 |
| search-project | cold | 5 | 7 / 9 | 15 / 16 | 37 | 35 | 8.05 / 10.14 | 0 |
| search-project | warm | 20 | 3 / 6 | 0 / 16 | 37 | 0 | 4.42 / 6.95 | 0 |
| search-internal-user | cold | 5 | 47 / 50 | 31 / 32 | 1,409 | 1,406 | 48.35 / 51.85 | 0 |
| search-internal-user | warm | 20 | 33 / 48 | 16 / 16 | 1,409 | 24 | 33.76 / 49.35 | 0 |
| search-no-hits | cold | 5 | 6 / 7 | 0 / 16 | 0 | 0 | 7.23 / 8.2 | 0 |
| search-no-hits | warm | 20 | 0 / 2 | 0 / 0 | 0 | 0 | 0.98 / 2.71 | 0 |

Scenarios:

- `search-printer`: GET /search?term=printer (golden query; 66,582 titles at L) (1 command(s); plans: `plans/search.search-printer.cmd1.stmt1.sqlplan`)
- `search-network`: GET /search?term=network (golden path query) (1 command(s); plans: `plans/search.search-network.cmd1.stmt1.sqlplan`)
- `search-team`: GET /search?term=team (most frequent title word, 167,045 titles at L) (1 command(s); plans: `plans/search.search-team.cmd1.stmt1.sqlplan`)
- `search-two-words`: GET /search?term=printer network (either word) (1 command(s); plans: `plans/search.search-two-words.cmd1.stmt1.sqlplan`)
- `search-required`: GET /search?term=+printer +remote (both words, any field) (1 command(s); plans: `plans/search.search-required.cmd1.stmt1.sqlplan`)
- `search-excluded`: GET /search?term=printer -remote (1 command(s); plans: `plans/search.search-excluded.cmd1.stmt1.sqlplan`)
- `search-phrase`: GET /search?term="remote staff" (1 command(s); plans: `plans/search.search-phrase.cmd1.stmt1.sqlplan`)
- `search-prefix`: GET /search?term=print* (1 command(s); plans: `plans/search.search-prefix.cmd1.stmt1.sqlplan`)
- `search-tag-field`: GET /search?term=tags:printer (1 command(s); plans: `plans/search.search-tag-field.cmd1.stmt1.sqlplan`)
- `search-ticket-id`: GET /search?term=<detail ticket id> (1 command(s); plans: `plans/search.search-ticket-id.cmd1.stmt1.sqlplan`)
- `search-project`: GET /search?term=printer, selected project = biggest (1 command(s); plans: `plans/search.search-project.cmd1.stmt1.sqlplan`)
- `search-internal-user`: GET /search?term=printer as an internal user (S12 filter; owner with the most tickets) (1 command(s); plans: `plans/search.search-internal-user.cmd1.stmt1.sqlplan`)
- `search-no-hits`: GET /search?term=zzqqxxnohits (golden query) (1 command(s); plans: `plans/search.search-no-hits.cmd1.stmt1.sqlplan`)
