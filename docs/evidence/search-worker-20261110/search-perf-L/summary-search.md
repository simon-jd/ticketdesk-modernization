# td-sqlperf: search on TicketDeskLSearch

Started 2026-11-10T18:13:25.5749367+00:00, finished 2026-11-10T18:14:10.3440139+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-10T18:13:25Z
host: lab-host, 32 cores, load average 2.73 2.39 2.76
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskLSearch; git: db7ea39 (uncommitted changes)
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| search-printer | cold | 5 | 117 / 121 | 505 / 533 | 4,378 | 4,530 | 118 / 124 | 0 |
| search-printer | warm | 20 | 98.5 / 116 | 468 / 512 | 4,378 | 0 | 99.38 / 117 | 0 |
| search-network | cold | 5 | 150 / 160 | 712 / 758 | 12,411 | 12,659 | 152 / 161 | 0 |
| search-network | warm | 20 | 132 / 150 | 670 / 778 | 12,411 | 0 | 133 / 151 | 0 |
| search-team | cold | 5 | 130 / 140 | 567 / 589 | 9,826 | 10,011 | 131 / 141 | 0 |
| search-team | warm | 20 | 113 / 122 | 534 / 587 | 9,826 | 0 | 114 / 123 | 0 |
| search-two-words | cold | 5 | 175 / 190 | 853 / 862 | 12,411 | 12,663 | 176 / 191 | 0 |
| search-two-words | warm | 20 | 154 / 170 | 786 / 879 | 12,411 | 0 | 154 / 171 | 0 |
| search-required | cold | 5 | 472 / 516 | 2,215 / 2,537 | 756,177 | 12,685 | 473 / 517 | 0 |
| search-required | warm | 20 | 396 / 470 | 2,014 / 2,249 | 756,177 | 0 | 397 / 471 | 0 |
| search-excluded | cold | 5 | 169 / 194 | 794 / 888 | 12,411 | 12,671 | 170 / 195 | 0 |
| search-excluded | warm | 20 | 151 / 160 | 745 / 782 | 12,411 | 0 | 152 / 162 | 0 |
| search-phrase | cold | 5 | 39 / 45 | 126 / 142 | 1,793 | 1,879 | 40.27 / 46.22 | 0 |
| search-phrase | warm | 20 | 30 / 36 | 110 / 124 | 1,793 | 0 | 31.46 / 37.29 | 0 |
| search-prefix | cold | 5 | 115 / 122 | 517 / 532 | 4,378 | 4,523 | 116 / 123 | 0 |
| search-prefix | warm | 20 | 104 / 122 | 500 / 547 | 4,378 | 0 | 105 / 123 | 0 |
| search-tag-field | cold | 5 | 82 / 84 | 379 / 403 | 4,378 | 4,529 | 83.19 / 85.43 | 0 |
| search-tag-field | warm | 20 | 74 / 86 | 370 / 405 | 4,378 | 0 | 74.95 / 87.3 | 0 |
| search-ticket-id | cold | 5 | 4 / 5 | 2 / 2 | 6 | 3 | 4.67 / 5.84 | 0 |
| search-ticket-id | warm | 20 | 1 / 1 | 1 / 1 | 6 | 0 | 1.39 / 1.92 | 0 |
| search-project | cold | 5 | 73 / 83 | 349 / 407 | 2,978 | 3,039 | 73.94 / 84.32 | 0 |
| search-project | warm | 20 | 65 / 72 | 353 / 367 | 2,978 | 0 | 66.01 / 72.99 | 0 |
| search-internal-user | cold | 5 | 296 / 307 | 1,031 / 1,095 | 1,033,583 | 114,344 | 297 / 309 | 0 |
| search-internal-user | warm | 20 | 131 / 145 | 848 / 910 | 1,033,583 | 0 | 132 / 146 | 0 |
| search-no-hits | cold | 5 | 7 / 8 | 14 / 15 | 0 | 0 | 7.8 / 8.44 | 0 |
| search-no-hits | warm | 20 | 4 / 16 | 12 / 44 | 0 | 0 | 4.68 / 17.24 | 0 |

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
