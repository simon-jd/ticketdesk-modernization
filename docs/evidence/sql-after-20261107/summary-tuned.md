# td-sqlperf: tuned on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`; per-run results in `results-tuned.json.xz` (`xz -dk`).

Started 2026-11-08T01:17:03.3788281+00:00, finished 2026-11-08T01:17:48.1390806+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T01:17:03Z
host: lab-host, 32 cores, load average 33.38 26.58 20.50
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: e509ff9 (uncommitted changes)
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-default | cold | 5 | 138 / 161 | 87 / 88 | 1,960 | 1,950 | 143 / 163 | 0 |
| list-default | warm | 20 | 67.5 / 70 | 68 / 70 | 1,960 | 0 | 70.91 / 74.31 | 0 |
| list-open-p1 | cold | 5 | 104 / 149 | 73 / 83 | 3,326 | 3,316 | 107 / 153 | 0 |
| list-open-p1 | warm | 20 | 64 / 93 | 63.5 / 92 | 3,326 | 0 | 66.73 / 97.16 | 0 |
| list-open-p500 | cold | 5 | 116 / 130 | 94 / 99 | 3,414 | 7,154 | 119 / 132 | 0 |
| list-open-p500 | warm | 20 | 70 / 74 | 69.5 / 74 | 3,414 | 0 | 71.88 / 75.67 | 0 |
| list-unassigned | cold | 5 | 56 / 82 | 35 / 40 | 2,062 | 3,862 | 58.33 / 85.84 | 0 |
| list-unassigned | warm | 20 | 14 / 16 | 14 / 16 | 1,117 | 0 | 16.6 / 19.84 | 0 |
| list-assignedToMe | cold | 5 | 25 / 51 | 7 / 12 | 203 | 69 | 27.32 / 54.33 | 0 |
| list-assignedToMe | warm | 20 | 1 / 5 | 1 / 5 | 203 | 0 | 2 / 7.65 | 0 |
| list-mytickets-heavy | cold | 5 | 19 / 30 | 11 / 15 | 705 | 613 | 22.4 / 33.04 | 0 |
| list-mytickets-heavy | warm | 20 | 4 / 11 | 4 / 10 | 705 | 0 | 6.19 / 11.8 | 0 |
| list-mytickets-light | cold | 5 | 6 / 12 | 2 / 5 | 43 | 23 | 7.4 / 14.08 | 0 |
| list-mytickets-light | warm | 20 | 1 / 2 | 1 / 3 | 43 | 0 | 2.88 / 5.09 | 0 |
| list-history | cold | 5 | 91 / 163 | 77 / 79 | 3,325 | 3,311 | 93.96 / 167 | 0 |
| list-history | warm | 20 | 65 / 67 | 64 / 67 | 3,325 | 0 | 66.64 / 69.06 | 0 |
| list-project-big | cold | 5 | 33 / 41 | 22 / 26 | 564 | 471 | 33.65 / 43.64 | 0 |
| list-project-big | warm | 20 | 16 / 17 | 16 / 17 | 564 | 0 | 18.2 / 21.01 | 0 |
| list-project-small | cold | 5 | 20 / 29 | 9 / 12 | 213 | 264 | 22.58 / 30.51 | 0 |
| list-project-small | warm | 20 | 2.5 / 5 | 3 / 4 | 213 | 0 | 4.59 / 9.47 | 0 |
| list-status-sort-title | cold | 5 | 351 / 363 | 1,577 / 1,764 | 110,740 | 111,348 | 354 / 364 | 0 |
| list-status-sort-title | warm | 20 | 184 / 239 | 977 / 1,085 | 110,740 | 0 | 189 / 240 | 0 |
| list-tag | cold | 5 | 151 / 165 | 125 / 139 | 3,062 | 2,757 | 154 / 166 | 0 |
| list-tag | warm | 20 | 132 / 142 | 131 / 141 | 3,062 | 0 | 136 / 145 | 0 |
| detail | cold | 5 | 7 / 12 | 3 / 7 | 59 | 53 | 17.98 / 28.87 | 0 |
| detail | warm | 20 | 0 / 0 | 0 / 1 | 59 | 0 | 1.47 / 8.16 | 0 |
| report-open-aging | cold | 5 | 114 / 118 | 105 / 108 | 3,705 | 3,865 | 116 / 119 | 11 |
| report-open-aging | warm | 20 | 62 / 75 | 62 / 75 | 3,705 | 0 | 63.54 / 76.82 | 7 |
| report-workload | cold | 5 | 283 / 376 | 505 / 741 | 19,190 | 10,444 | 286 / 379 | 46 |
| report-workload | warm | 20 | 226 / 258 | 398 / 431 | 19,096 | 0 | 229 / 259 | 34 |
| report-time-to-close | cold | 5 | 382 / 438 | 527 / 549 | 4,046 | 1,977 | 385 / 457 | 46 |
| report-time-to-close | warm | 20 | 182 / 331 | 476 / 604 | 4,046 | 0 | 184 / 334 | 38 |

Scenarios:

- `list-default`: GET /api/tickets?(no list; default sort -lastUpdateDate) (1 command(s); plans: `plans/tuned.list-default.cmd1.stmt1.sqlplan`, `plans/tuned.list-default.cmd1.stmt2.sqlplan`, `plans/tuned.list-default.cmd1.stmt3.sqlplan`)
- `list-open-p1`: GET /api/tickets?list=opentickets (sort status,targetDate,dueDate,-lastUpdateDate) (1 command(s); plans: `plans/tuned.list-open-p1.cmd1.stmt1.sqlplan`, `plans/tuned.list-open-p1.cmd1.stmt2.sqlplan`, `plans/tuned.list-open-p1.cmd1.stmt3.sqlplan`)
- `list-open-p500`: GET /api/tickets?list=opentickets&page=500 (deep page) (1 command(s); plans: `plans/tuned.list-open-p500.cmd1.stmt1.sqlplan`, `plans/tuned.list-open-p500.cmd1.stmt2.sqlplan`, `plans/tuned.list-open-p500.cmd1.stmt3.sqlplan`)
- `list-unassigned`: GET /api/tickets?list=unassigned (help desk) (1 command(s); plans: `plans/tuned.list-unassigned.cmd1.stmt1.sqlplan`, `plans/tuned.list-unassigned.cmd1.stmt2.sqlplan`, `plans/tuned.list-unassigned.cmd1.stmt3.sqlplan`)
- `list-assignedToMe`: GET /api/tickets?list=assignedToMe (busiest help desk user) (1 command(s); plans: `plans/tuned.list-assignedToMe.cmd1.stmt1.sqlplan`, `plans/tuned.list-assignedToMe.cmd1.stmt2.sqlplan`, `plans/tuned.list-assignedToMe.cmd1.stmt3.sqlplan`)
- `list-mytickets-heavy`: GET /api/tickets?list=mytickets (owner with the most tickets) (1 command(s); plans: `plans/tuned.list-mytickets-heavy.cmd1.stmt1.sqlplan`, `plans/tuned.list-mytickets-heavy.cmd1.stmt2.sqlplan`, `plans/tuned.list-mytickets-heavy.cmd1.stmt3.sqlplan`)
- `list-mytickets-light`: GET /api/tickets?list=mytickets (owner with 3-10 open tickets) (1 command(s); plans: `plans/tuned.list-mytickets-light.cmd1.stmt1.sqlplan`, `plans/tuned.list-mytickets-light.cmd1.stmt2.sqlplan`, `plans/tuned.list-mytickets-light.cmd1.stmt3.sqlplan`)
- `list-history`: GET /api/tickets?list=historytickets (closed, sort status,-lastUpdateDate) (1 command(s); plans: `plans/tuned.list-history.cmd1.stmt1.sqlplan`, `plans/tuned.list-history.cmd1.stmt2.sqlplan`, `plans/tuned.list-history.cmd1.stmt3.sqlplan`)
- `list-project-big`: GET /api/tickets?projectId=<biggest project> (default sort) (1 command(s); plans: `plans/tuned.list-project-big.cmd1.stmt1.sqlplan`, `plans/tuned.list-project-big.cmd1.stmt2.sqlplan`, `plans/tuned.list-project-big.cmd1.stmt3.sqlplan`)
- `list-project-small`: GET /api/tickets?projectId=<smallest project> (default sort) (1 command(s); plans: `plans/tuned.list-project-small.cmd1.stmt1.sqlplan`, `plans/tuned.list-project-small.cmd1.stmt2.sqlplan`, `plans/tuned.list-project-small.cmd1.stmt3.sqlplan`)
- `list-status-sort-title`: GET /api/tickets?status=Active&status=MoreInfo&sort=title (1 command(s); plans: `plans/tuned.list-status-sort-title.cmd1.stmt1.sqlplan`, `plans/tuned.list-status-sort-title.cmd1.stmt2.sqlplan`, `plans/tuned.list-status-sort-title.cmd1.stmt3.sqlplan`)
- `list-tag`: GET /api/tickets?tag=<most used tag> (default sort) (1 command(s); plans: `plans/tuned.list-tag.cmd1.stmt1.sqlplan`, `plans/tuned.list-tag.cmd1.stmt2.sqlplan`, `plans/tuned.list-tag.cmd1.stmt3.sqlplan`)
- `detail`: GET /api/tickets/{id} (ticket with the most events) (5 command(s); plans: `plans/tuned.detail.cmd1.stmt1.sqlplan`, `plans/tuned.detail.cmd2.stmt1.sqlplan`, `plans/tuned.detail.cmd3.stmt1.sqlplan`, `plans/tuned.detail.cmd4.stmt1.sqlplan`, `plans/tuned.detail.cmd5.stmt1.sqlplan`)
- `report-open-aging`: GET /api/reports/open-tickets (usp_Report_OpenTicketsAging) (1 command(s); plans: `plans/tuned.report-open-aging.cmd1.stmt1.sqlplan`)
- `report-workload`: GET /api/reports/assignee-workload?top=50 (usp_Report_AssigneeWorkload, Tickets.AssignedDate) (1 command(s); plans: `plans/tuned.report-workload.cmd1.stmt1.sqlplan`, `plans/tuned.report-workload.cmd1.stmt2.sqlplan`)
- `report-time-to-close`: GET /api/reports/time-to-close (last 90 days; usp_Report_TimeToClose) (1 command(s); plans: `plans/tuned.report-time-to-close.cmd1.stmt1.sqlplan`)
