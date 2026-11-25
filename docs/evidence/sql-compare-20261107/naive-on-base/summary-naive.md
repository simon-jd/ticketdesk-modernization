# td-sqlperf: naive on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`; per-run results in `results-naive.json.xz` (`xz -dk`).

Started 2026-11-08T01:23:56.3847771+00:00, finished 2026-11-08T01:27:39.6215564+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T01:23:56Z
host: lab-host, 32 cores, load average 2.63 11.25 16.04
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskLBase; git: 3adfe38
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-default | cold | 5 | 263 / 282 | 1,389 / 1,481 | 109,130 | 109,021 | 267 / 290 | 0 |
| list-default | warm | 20 | 182 / 212 | 1,096 / 1,190 | 109,130 | 0 | 185 / 215 | 0 |
| list-open-p1 | cold | 5 | 284 / 314 | 1,189 / 1,301 | 214,548 | 107,144 | 287 / 316 | 0 |
| list-open-p1 | warm | 20 | 146 / 174 | 1,040 / 1,317 | 214,548 | 0 | 150 / 177 | 0 |
| list-open-p500 | cold | 5 | 274 / 276 | 1,192 / 1,217 | 214,510 | 107,076 | 278 / 280 | 0 |
| list-open-p500 | warm | 20 | 148 / 164 | 1,036 / 1,214 | 214,510 | 0 | 151 / 167 | 0 |
| list-unassigned | cold | 5 | 189 / 192 | 640 / 696 | 214,479 | 107,033 | 192 / 196 | 0 |
| list-unassigned | warm | 20 | 74 / 79 | 550 / 595 | 214,479 | 0 | 76.82 / 81.88 | 0 |
| list-assignedToMe | cold | 5 | 172 / 177 | 516 / 520 | 214,481 | 107,043 | 176 / 181 | 0 |
| list-assignedToMe | warm | 20 | 56 / 59 | 427 / 436 | 214,481 | 0 | 59.6 / 62.09 | 0 |
| list-mytickets-heavy | cold | 5 | 182 / 186 | 588 / 721 | 214,480 | 107,043 | 186 / 189 | 0 |
| list-mytickets-heavy | warm | 20 | 67 / 77 | 498 / 588 | 214,480 | 0 | 69.54 / 79.66 | 0 |
| list-mytickets-light | cold | 5 | 167 / 179 | 534 / 552 | 214,408 | 106,904 | 170 / 183 | 0 |
| list-mytickets-light | warm | 20 | 59 / 66 | 454 / 496 | 214,408 | 0 | 62.97 / 68.79 | 0 |
| list-history | cold | 5 | 246 / 280 | 1,019 / 1,208 | 214,557 | 107,164 | 250 / 283 | 0 |
| list-history | warm | 20 | 124 / 158 | 905 / 1,068 | 214,557 | 0 | 126 / 161 | 0 |
| list-project-big | cold | 5 | 164 / 165 | 510 / 532 | 107,721 | 107,524 | 167 / 169 | 0 |
| list-project-big | warm | 20 | 58 / 74 | 374 / 445 | 107,721 | 0 | 61.33 / 76.96 | 0 |
| list-project-small | cold | 5 | 146 / 150 | 269 / 316 | 107,384 | 107,339 | 149 / 153 | 0 |
| list-project-small | warm | 20 | 27 / 42 | 196 / 257 | 107,384 | 0 | 29.58 / 46.56 | 0 |
| list-status-sort-title | cold | 5 | 240 / 273 | 989 / 1,069 | 214,535 | 107,160 | 243 / 276 | 0 |
| list-status-sort-title | warm | 20 | 118 / 131 | 864 / 960 | 214,535 | 0 | 120 / 134 | 0 |
| list-tag | cold | 5 | 240 / 247 | 905 / 981 | 122,547 | 115,964 | 244 / 250 | 0 |
| list-tag | warm | 20 | 136 / 157 | 840 / 993 | 122,547 | 0 | 139 / 160 | 0 |
| detail | cold | 5 | 0 / 1 | 1 / 2 | 59 | 53 | 5.99 / 7.6 | 0 |
| detail | warm | 20 | 0 / 0 | 0 / 0 | 59 | 0 | 1.21 / 1.42 | 0 |
| report-open-aging | cold | 5 | 141 / 145 | 460 / 477 | 107,201 | 106,872 | 142 / 146 | 0 |
| report-open-aging | warm | 20 | 41 / 46 | 312 / 340 | 107,201 | 0 | 43.56 / 48.09 | 0 |
| report-workload | cold | 5 | 6,573 / 6,656 | 14,023 / 14,433 | 22,296,021 | 223,243 | 6,578 / 6,663 | 0 |
| report-workload | warm | 20 | 6,322 / 6,468 | 14,112 / 14,576 | 22,295,977 | 0 | 6,328 / 6,474 | 0 |
| report-time-to-close | cold | 5 | 138 / 143 | 277 / 332 | 107,201 | 106,889 | 139 / 145 | 0 |
| report-time-to-close | warm | 20 | 26.5 / 30 | 211 / 220 | 107,201 | 0 | 27.86 / 31.41 | 0 |

Scenarios:

- `list-default`: GET /api/tickets?(no list; default sort -lastUpdateDate) (3 command(s); plans: `plans/naive.list-default.cmd1.stmt1.sqlplan`, `plans/naive.list-default.cmd2.stmt1.sqlplan`, `plans/naive.list-default.cmd3.stmt1.sqlplan`)
- `list-open-p1`: GET /api/tickets?list=opentickets (sort status,targetDate,dueDate,-lastUpdateDate) (3 command(s); plans: `plans/naive.list-open-p1.cmd1.stmt1.sqlplan`, `plans/naive.list-open-p1.cmd2.stmt1.sqlplan`, `plans/naive.list-open-p1.cmd3.stmt1.sqlplan`)
- `list-open-p500`: GET /api/tickets?list=opentickets&page=500 (deep page) (3 command(s); plans: `plans/naive.list-open-p500.cmd1.stmt1.sqlplan`, `plans/naive.list-open-p500.cmd2.stmt1.sqlplan`, `plans/naive.list-open-p500.cmd3.stmt1.sqlplan`)
- `list-unassigned`: GET /api/tickets?list=unassigned (help desk) (3 command(s); plans: `plans/naive.list-unassigned.cmd1.stmt1.sqlplan`, `plans/naive.list-unassigned.cmd2.stmt1.sqlplan`, `plans/naive.list-unassigned.cmd3.stmt1.sqlplan`)
- `list-assignedToMe`: GET /api/tickets?list=assignedToMe (busiest help desk user) (3 command(s); plans: `plans/naive.list-assignedToMe.cmd1.stmt1.sqlplan`, `plans/naive.list-assignedToMe.cmd2.stmt1.sqlplan`, `plans/naive.list-assignedToMe.cmd3.stmt1.sqlplan`)
- `list-mytickets-heavy`: GET /api/tickets?list=mytickets (owner with the most tickets) (3 command(s); plans: `plans/naive.list-mytickets-heavy.cmd1.stmt1.sqlplan`, `plans/naive.list-mytickets-heavy.cmd2.stmt1.sqlplan`, `plans/naive.list-mytickets-heavy.cmd3.stmt1.sqlplan`)
- `list-mytickets-light`: GET /api/tickets?list=mytickets (owner with 3-10 open tickets) (3 command(s); plans: `plans/naive.list-mytickets-light.cmd1.stmt1.sqlplan`, `plans/naive.list-mytickets-light.cmd2.stmt1.sqlplan`, `plans/naive.list-mytickets-light.cmd3.stmt1.sqlplan`)
- `list-history`: GET /api/tickets?list=historytickets (closed, sort status,-lastUpdateDate) (3 command(s); plans: `plans/naive.list-history.cmd1.stmt1.sqlplan`, `plans/naive.list-history.cmd2.stmt1.sqlplan`, `plans/naive.list-history.cmd3.stmt1.sqlplan`)
- `list-project-big`: GET /api/tickets?projectId=<biggest project> (default sort) (3 command(s); plans: `plans/naive.list-project-big.cmd1.stmt1.sqlplan`, `plans/naive.list-project-big.cmd2.stmt1.sqlplan`, `plans/naive.list-project-big.cmd3.stmt1.sqlplan`)
- `list-project-small`: GET /api/tickets?projectId=<smallest project> (default sort) (3 command(s); plans: `plans/naive.list-project-small.cmd1.stmt1.sqlplan`, `plans/naive.list-project-small.cmd2.stmt1.sqlplan`, `plans/naive.list-project-small.cmd3.stmt1.sqlplan`)
- `list-status-sort-title`: GET /api/tickets?status=Active&status=MoreInfo&sort=title (3 command(s); plans: `plans/naive.list-status-sort-title.cmd1.stmt1.sqlplan`, `plans/naive.list-status-sort-title.cmd2.stmt1.sqlplan`, `plans/naive.list-status-sort-title.cmd3.stmt1.sqlplan`)
- `list-tag`: GET /api/tickets?tag=<most used tag> (default sort) (3 command(s); plans: `plans/naive.list-tag.cmd1.stmt1.sqlplan`, `plans/naive.list-tag.cmd2.stmt1.sqlplan`, `plans/naive.list-tag.cmd3.stmt1.sqlplan`)
- `detail`: GET /api/tickets/{id} (ticket with the most events) (5 command(s); plans: `plans/naive.detail.cmd1.stmt1.sqlplan`, `plans/naive.detail.cmd2.stmt1.sqlplan`, `plans/naive.detail.cmd3.stmt1.sqlplan`, `plans/naive.detail.cmd4.stmt1.sqlplan`, `plans/naive.detail.cmd5.stmt1.sqlplan`)
- `report-open-aging`: GET /api/reports/open-tickets (first-pass LINQ) (2 command(s); plans: `plans/naive.report-open-aging.cmd1.stmt1.sqlplan`, `plans/naive.report-open-aging.cmd2.stmt1.sqlplan`)
- `report-workload`: GET /api/reports/assignee-workload?top=50 (first-pass LINQ, assigned-since from TicketEvents) (5 command(s); plans: `plans/naive.report-workload.cmd1.stmt1.sqlplan`, `plans/naive.report-workload.cmd2.stmt1.sqlplan`, `plans/naive.report-workload.cmd3.stmt1.sqlplan`, `plans/naive.report-workload.cmd4.stmt1.sqlplan`, `plans/naive.report-workload.cmd5.stmt1.sqlplan`)
- `report-time-to-close`: GET /api/reports/time-to-close (last 90 days; first-pass LINQ, percentiles in .NET) (2 command(s); plans: `plans/naive.report-time-to-close.cmd1.stmt1.sqlplan`, `plans/naive.report-time-to-close.cmd2.stmt1.sqlplan`)
