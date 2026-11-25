# td-sqlperf: tuned on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`; per-run results in `results-tuned.json.xz` (`xz -dk`).

Started 2026-11-08T01:27:39.8204544+00:00, finished 2026-11-08T01:28:00.7568476+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T01:27:39Z
host: lab-host, 32 cores, load average 4.38 7.94 13.70
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: 3adfe38
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-default | cold | 5 | 53 / 54 | 41 / 44 | 1,960 | 1,950 | 53.66 / 55.51 | 0 |
| list-default | warm | 20 | 35 / 36 | 35.5 / 37 | 1,960 | 0 | 36.73 / 37.67 | 0 |
| list-open-p1 | cold | 5 | 53 / 55 | 41 / 42 | 3,326 | 3,316 | 53.92 / 56.35 | 0 |
| list-open-p1 | warm | 20 | 51 / 52 | 50 / 52 | 3,326 | 0 | 51.72 / 53.41 | 0 |
| list-open-p500 | cold | 5 | 62 / 63 | 46 / 47 | 3,414 | 7,154 | 62.93 / 64.06 | 0 |
| list-open-p500 | warm | 20 | 36 / 37 | 36 / 37 | 3,414 | 0 | 36.76 / 37.71 | 0 |
| list-unassigned | cold | 5 | 17 / 19 | 10 / 13 | 2,062 | 3,862 | 18.26 / 19.99 | 0 |
| list-unassigned | warm | 20 | 4 / 4 | 4 / 5 | 1,117 | 0 | 4.93 / 5.06 | 0 |
| list-assignedToMe | cold | 5 | 6 / 8 | 2 / 3 | 203 | 69 | 7.3 / 8.7 | 0 |
| list-assignedToMe | warm | 20 | 0 / 0 | 0 / 1 | 203 | 0 | 1.04 / 1.08 | 0 |
| list-mytickets-heavy | cold | 5 | 10 / 11 | 4 / 6 | 705 | 613 | 11.2 / 12.09 | 0 |
| list-mytickets-heavy | warm | 20 | 2 / 2 | 2 / 2 | 705 | 0 | 2.24 / 2.29 | 0 |
| list-mytickets-light | cold | 5 | 2 / 3 | 1 / 2 | 43 | 23 | 3.39 / 3.46 | 0 |
| list-mytickets-light | warm | 20 | 0 / 0 | 0 / 0 | 43 | 0 | 0.53 / 0.6 | 0 |
| list-history | cold | 5 | 77 / 78 | 63 / 71 | 3,325 | 3,311 | 77.79 / 79.26 | 0 |
| list-history | warm | 20 | 33 / 35 | 33.5 / 35 | 3,325 | 0 | 34.33 / 35.39 | 0 |
| list-project-big | cold | 5 | 17 / 18 | 11 / 11 | 564 | 471 | 17.93 / 19.3 | 0 |
| list-project-big | warm | 20 | 7 / 12 | 8 / 12 | 564 | 0 | 8.22 / 13.08 | 0 |
| list-project-small | cold | 5 | 9 / 10 | 1 / 3 | 213 | 264 | 9.69 / 11.01 | 0 |
| list-project-small | warm | 20 | 0 / 0 | 0.5 / 1 | 213 | 0 | 1 / 1.3 | 0 |
| list-status-sort-title | cold | 5 | 198 / 209 | 671 / 746 | 110,740 | 111,347 | 199 / 210 | 0 |
| list-status-sort-title | warm | 20 | 80 / 87 | 541 / 583 | 110,740 | 0 | 81.07 / 87.93 | 0 |
| list-tag | cold | 5 | 86 / 89 | 73 / 74 | 3,062 | 2,757 | 87.21 / 90.23 | 0 |
| list-tag | warm | 20 | 105 / 106 | 105 / 106 | 3,062 | 0 | 106 / 107 | 0 |
| detail | cold | 5 | 1 / 1 | 1 / 2 | 59 | 53 | 5.45 / 6.1 | 0 |
| detail | warm | 20 | 0 / 0 | 0 / 0 | 59 | 0 | 0.63 / 1.1 | 0 |
| report-open-aging | cold | 5 | 90 / 99 | 82 / 93 | 3,705 | 3,865 | 91.59 / 100 | 7 |
| report-open-aging | warm | 20 | 46 / 74 | 46 / 74 | 3,705 | 0 | 47.12 / 75.13 | 3 |
| report-workload | cold | 5 | 99 / 104 | 300 / 310 | 19,194 | 10,444 | 99.63 / 105 | 13 |
| report-workload | warm | 20 | 63.5 / 67 | 279 / 291 | 19,096 | 0 | 64.38 / 67.62 | 13 |
| report-time-to-close | cold | 5 | 73 / 116 | 283 / 452 | 4,046 | 1,977 | 73.99 / 117 | 15 |
| report-time-to-close | warm | 20 | 78.5 / 104 | 329 / 458 | 4,046 | 0 | 79.5 / 105 | 25.5 |

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
