# td-sqlperf: naive on TicketDeskL

> Packed for git (`ops/dev/pack-evidence.sh`): the `plans/...sqlplan` files named below and the raw STATISTICS messages are in `plans.tar.xz` and `raw.tar.xz` (`tar -xJf`); every plan is rendered in `plans.txt`; per-run results in `results-naive.json.xz` (`xz -dk`).

Started 2026-11-08T00:53:50.2890487+00:00, finished 2026-11-08T00:57:22.5039297+00:00.  
Cold: CHECKPOINT + DBCC DROPCLEANBUFFERS before every run (buffer pool empty; plan cache kept). Warm: one discarded warm-up run, then the measured runs back to back. median and nearest-rank p95 over the measured runs (p95 = maximum when n < 20).  
Server = SET STATISTICS TIME (outermost execution time, summed over the call's commands); client = wall clock around execute + read all rows.

```
date: 2026-11-08T00:53:49Z
host: lab-host, 32 cores, load average 26.54 31.51 30.01
sql: 16.0.4262.2 Developer Edition (64-bit), MAXDOP(db)=8
database: TicketDeskL; git: e509ff9
```

| Scenario | Mode | n | Server elapsed median / p95 ms | CPU median / p95 ms | Logical reads (median) | Physical + read-ahead (median) | Client median / p95 ms | Compile CPU ms |
|---|---|---|---|---|---|---|---|---|
| list-default | cold | 5 | 245 / 260 | 1,247 / 1,347 | 109,130 | 109,008 | 250 / 264 | 0 |
| list-default | warm | 20 | 200 / 215 | 1,192 / 1,362 | 109,130 | 0 | 204 / 219 | 0 |
| list-open-p1 | cold | 5 | 301 / 323 | 1,280 / 1,418 | 214,548 | 107,137 | 304 / 327 | 0 |
| list-open-p1 | warm | 20 | 134 / 151 | 980 / 1,076 | 214,548 | 0 | 137 / 154 | 0 |
| list-open-p500 | cold | 5 | 285 / 295 | 1,220 / 1,348 | 214,510 | 107,069 | 288 / 299 | 0 |
| list-open-p500 | warm | 20 | 147 / 171 | 1,074 / 1,236 | 214,510 | 0 | 150 / 174 | 0 |
| list-unassigned | cold | 5 | 187 / 198 | 665 / 684 | 214,479 | 107,017 | 191 / 201 | 0 |
| list-unassigned | warm | 20 | 72 / 74 | 532 / 547 | 214,479 | 0 | 75.18 / 77.49 | 0 |
| list-assignedToMe | cold | 5 | 175 / 209 | 554 / 824 | 214,481 | 107,031 | 179 / 214 | 0 |
| list-assignedToMe | warm | 20 | 55 / 58 | 414 / 437 | 214,481 | 0 | 58.22 / 61.39 | 0 |
| list-mytickets-heavy | cold | 5 | 181 / 188 | 622 / 638 | 214,480 | 107,026 | 184 / 192 | 0 |
| list-mytickets-heavy | warm | 20 | 69.5 / 88 | 524 / 636 | 214,480 | 0 | 72.29 / 90.53 | 0 |
| list-mytickets-light | cold | 5 | 172 / 195 | 577 / 726 | 214,408 | 106,901 | 175 / 198 | 0 |
| list-mytickets-light | warm | 20 | 58 / 61 | 442 / 462 | 214,408 | 0 | 60.81 / 63.78 | 0 |
| list-history | cold | 5 | 231 / 242 | 903 / 920 | 214,557 | 107,146 | 235 / 245 | 0 |
| list-history | warm | 20 | 114 / 127 | 822 / 942 | 214,557 | 0 | 117 / 130 | 0 |
| list-project-big | cold | 5 | 160 / 165 | 496 / 532 | 107,721 | 107,513 | 164 / 168 | 0 |
| list-project-big | warm | 20 | 60 / 63 | 390 / 407 | 107,721 | 0 | 62.59 / 65.41 | 0 |
| list-project-small | cold | 5 | 149 / 166 | 318 / 344 | 107,384 | 107,325 | 152 / 169 | 0 |
| list-project-small | warm | 20 | 28 / 30 | 205 / 223 | 107,384 | 0 | 31.27 / 33.77 | 0 |
| list-status-sort-title | cold | 5 | 238 / 337 | 956 / 1,140 | 214,535 | 107,128 | 241 / 340 | 0 |
| list-status-sort-title | warm | 20 | 118 / 128 | 877 / 947 | 214,535 | 0 | 121 / 130 | 0 |
| list-tag | cold | 5 | 227 / 236 | 789 / 865 | 122,547 | 115,961 | 229 / 239 | 0 |
| list-tag | warm | 20 | 123 / 136 | 729 / 811 | 122,547 | 0 | 125 / 138 | 0 |
| detail | cold | 5 | 0 / 1 | 2 / 3 | 59 | 53 | 6.21 / 8.84 | 0 |
| detail | warm | 20 | 0 / 0 | 0 / 0 | 59 | 0 | 0.59 / 0.87 | 0 |
| report-open-aging | cold | 5 | 139 / 141 | 409 / 483 | 107,201 | 106,864 | 141 / 142 | 0 |
| report-open-aging | warm | 20 | 41.5 / 44 | 310 / 331 | 107,201 | 0 | 43.54 / 45.75 | 0 |
| report-workload | cold | 5 | 6,037 / 6,507 | 13,215 / 13,608 | 22,296,161 | 223,234 | 6,041 / 6,513 | 0 |
| report-workload | warm | 20 | 6,038 / 6,129 | 13,474 / 13,764 | 22,296,035 | 0 | 6,044 / 6,135 | 0 |
| report-time-to-close | cold | 5 | 140 / 148 | 322 / 359 | 107,201 | 106,869 | 141 / 150 | 0 |
| report-time-to-close | warm | 20 | 28 / 30 | 224 / 243 | 107,201 | 0 | 29.14 / 30.92 | 0 |

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
