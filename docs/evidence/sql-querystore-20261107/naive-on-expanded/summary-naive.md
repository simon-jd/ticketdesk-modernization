# td-sqlperf: naive on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`.

Started 2026-11-08T07:25:52.5647045+00:00, finished 2026-11-08T07:26:40.3534645+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T07:25:52Z
host: lab-host, 32 cores, load average 7.88 5.15 2.16
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: 0f6d1bb
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-default | warm | 5 | 37 / 38 | 36 / 38 | 1,996 | 0 | 39.32 / 40.59 | 0 |
| list-open-p1 | warm | 5 | 36 / 45 | 37 / 45 | 3,350 | 0 | 39.12 / 47.93 | 0 |
| list-open-p500 | warm | 5 | 82 / 84 | 81 / 84 | 33,994 | 0 | 85.35 / 87.8 | 0 |
| list-unassigned | warm | 5 | 4 / 4 | 4 / 4 | 1,070 | 0 | 5.43 / 5.57 | 0 |
| list-assignedToMe | warm | 5 | 0 / 0 | 0 / 0 | 158 | 0 | 1.15 / 1.28 | 0 |
| list-mytickets-heavy | warm | 5 | 1 / 2 | 1 / 3 | 672 | 0 | 2.51 / 3.69 | 0 |
| list-mytickets-light | warm | 5 | 0 / 0 | 0 / 0 | 32 | 0 | 0.7 / 0.87 | 0 |
| list-history | warm | 5 | 32 / 33 | 33 / 34 | 3,359 | 0 | 35.07 / 35.97 | 0 |
| list-project-big | warm | 5 | 7 / 7 | 7 / 8 | 587 | 0 | 8.52 / 8.89 | 0 |
| list-project-small | warm | 5 | 0 / 1 | 1 / 1 | 251 | 0 | 2.28 / 2.79 | 0 |
| list-status-sort-title | warm | 5 | 141 / 145 | 754 / 772 | 111,243 | 0 | 144 / 149 | 0 |
| list-tag | warm | 5 | 67 / 74 | 67 / 76 | 3,287 | 0 | 68.97 / 76.94 | 0 |
| detail | warm | 5 | 0 / 0 | 0 / 0 | 59 | 0 | 0.67 / 0.7 | 0 |
| report-open-aging | warm | 5 | 128 / 142 | 128 / 142 | 3,705 | 0 | 129 / 143 | 0 |
| report-workload | warm | 5 | 5,849 / 6,066 | 13,825 / 14,154 | 21,884,610 | 0 | 5,853 / 6,070 | 0 |
| report-time-to-close | warm | 5 | 18 / 19 | 18 / 19 | 1,924 | 0 | 19.59 / 19.81 | 0 |

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
