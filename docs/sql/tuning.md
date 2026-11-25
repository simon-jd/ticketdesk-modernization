# SQL tuning: ticket center, ticket detail, reports (scale L)

Round 2, workstream 3. What the legacy app and the first EF Core port send to SQL Server for the ticket center, the
ticket page and three help-desk reports; what replaced it (`usp_TicketList`, three report procedures, eight
indexes, one expand column); measured before and after on the same data. Every number below comes from a file under
`docs/evidence/`; the table in [Before and after](#before-and-after) is `sql-compare-20261107/compare-naive-vs-tuned.md`.

## Data and method

- **Data**: scale L of `tools/Seed` (seed 42): 1,000,000 tickets, 4,538,818 events, 9,612,684 event notifications,
  2,029,356 subscribers, 1,461,932 tags, 25,000 users (3,750 help desk), 25 projects. Tickets clustered index 107,198
  pages (825 MB); database 6.9 GB. 500,169 open tickets (104,837 unassigned), 499,831 closed. Owners are Zipf
  distributed (the heaviest owner has 68,548 tickets, many have one); projects too (213,064 to 12,129 tickets).
  Evidence: `sql-dataset-20261107/` (`seed-L.txt`, `dataset-stats.txt`).
- **Server**: td-sql, SQL Server 2022 CU25 Developer (16.0.4262.2) in Docker, 8 GB memory cap, compatibility level
  160, database-scoped `MAXDOP 8` (the Azure SQL Database default; the container sees 32 cores), Query Store
  `READ_WRITE`, capture mode `ALL`, 1-minute intervals (`query-store-setup.txt`).
- **Two databases, back to back**: `TicketDeskLBase` (the seed, untouched: "before") and `TicketDeskL` (the seed after
  `td-db migrate`: "after"). Both runs within five minutes, host load average 2.6 to 7.4 (32 cores; other projects'
  containers share the host). `TicketDeskLBase` was dropped afterwards to free 7.7 GB; recreate it with
  `TD_SEED_DB=TicketDeskLBase ops/dev/seed.sh schema --create-database && ... run --scale L --seed 42`.
- **What is replayed**: `td-sqlperf capture` runs the app's own query classes against the database with an EF Core
  command interceptor and stores every command (text, parameters with their exact SqlClient types). `td-sqlperf bench`
  replays them with SqlClient (parameterized commands go out as `sp_executesql` RPCs, as EF Core sends them).
  One scenario = one API call = all its commands.
- **How it is measured**: per scenario, one run with `SET STATISTICS XML ON` saves the actual plans (`.sqlplan`, not
  timed); then 5 **cold** runs (`CHECKPOINT; DBCC DROPCLEANBUFFERS` before each; plan cache kept) and 20 **warm** runs
  (after one discarded warm-up), all with `SET STATISTICS IO, TIME ON`. Server elapsed and CPU are the outermost
  "Execution Times" of each command summed over the call; logical reads are summed over all tables and statements.
  Median and nearest-rank p95 (p95 is the maximum for the 5 cold runs). Raw messages of every run are in `raw.tar.xz`,
  plans as text (operator, actual rows, executions, reads, per-operator CPU) in `plans.txt`.
- **Naive port** = the Round 1 EF Core LINQ list (`TicketQueries.ListWithLinqAsync`, which was `ListAsync` until this
  round) and a first-pass LINQ version of each report (`LinqReportQueries`), written the way a team writes them first.
  The reports did not exist before this round, so their "before" is that first pass, labelled as such.
- **Legacy SQL** = what the EF6 app on the legacy VM actually sent, captured with Extended Events (below), replayed at
  scale L.
- **Same answers**: `td-sqlperf verify` runs 15 list requests and 6 report requests through both the naive and the
  tuned code on `TicketDeskL` and compares the results: all equal (`sql-after-20261107/verify-L.txt`); the xUnit
  tests do the same on scratch databases for 68 list requests and three report matrices.

## What the legacy app sends

Extended Events session `td_sqldata_capture` (rpc_completed, sql_batch_completed, database TicketDesk, ring buffer)
on the VM's SQL Server 2022 Express while `ops/dev/legacy_list_driver.py` used the ticket center as the lab admin:
all five lists, a second page, column sorts, two filters, a ticket page and its three panels (17 steps, 22 HTTP
requests). 958 events; 860 fell inside a step's request window (`sql-legacy-capture-20261107/statements.txt`,
`events.json.xz`, raw XE output `capture.xml.xz`). The capture ran under the shared VM lock (01:22 to 01:31 UTC) and the driver reset the admin's list
settings afterwards.

Per list page EF6 sent 28 to 30 statements plus ~30 `sp_reset_connection`:

- the ticket count and the page, from the Entity SQL the ticket center builds out of the user's saved list settings.
  The status filter is a **parameter** wrapped in EF6's null semantics:
  `WHERE NOT (([TicketStatus] = @TicketStatus) AND (0 = (CASE WHEN (@TicketStatus IS NULL) THEN cast(1 as bit) ELSE cast(0 as bit) END)))`,
  and the assignee filter `([AssignedTo] = @AssignedTo) OR (([AssignedTo] IS NULL) AND (@AssignedTo IS NULL))` with
  `@AssignedTo nvarchar(4000)`;
- six role-membership `EXISTS` checks and six `TOP (2)` role lookups (not cached per request), user and claims lookups,
  the user's `UserSettings` row, `SELECT COUNT(1) FROM Projects` (to decide whether to show the project column), and an
  Identity 2 schema probe (`SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS where TABLE_NAME='AspNetUsers'`);
- for `sortList`/`filterList`, an `UPDATE [dbo].[UserSettings]` (sorting is a state-changing GET).

Replayed at scale L (user-id literals replaced by the scale-L help desk user; SELECTs only), a legacy list page reads
214,461 pages warm (opentickets: 128 ms elapsed, 956 ms CPU; unassigned 80 ms / 590 ms); the ticket page reads 57.
On the expanded schema the same legacy SQL gets faster only where an unfiltered index matches (historytickets
214,461 → 3,263 reads, 132 → 33 ms; opentickets 1.9× fewer reads): **the parameterized status predicate cannot use the
filtered indexes**, so unassigned, assignedToMe and mytickets stay at 214,000 reads for the legacy app during
coexistence (`sql-compare-20261107/compare-legacy-base-vs-expanded.md`). That is a reason to move the ticket center
early, not to index for it.

## Before and after

Warm medians (p95 in parentheses), scale L, same five minutes. "Before" = naive port on `TicketDeskLBase`, "after" =
tuned code on `TicketDeskL`. Full table with cold runs: `sql-compare-20261107/compare-naive-vs-tuned.md`.

| Call | Elapsed ms before → after | CPU ms before → after | Logical reads before → after |
|---|---|---|---|
| list=opentickets, page 1 | 146 (174) → 51 (52) | 1,040 → 50 | 214,548 → 3,326 |
| list=opentickets, page 500 | 148 (164) → 36 (37) | 1,036 → 36 | 214,510 → 3,414 |
| list=unassigned (help desk landing list) | 74 (79) → 4 (4) | 550 → 4 | 214,479 → 1,117 |
| list=assignedToMe (busiest help desk user) | 56 (59) → 0 (0) | 427 → 0 | 214,481 → 203 |
| list=mytickets, owner with 68,548 tickets | 67 (77) → 2 (2) | 498 → 2 | 214,480 → 705 |
| list=mytickets, owner with 3 open tickets | 59 (66) → 0 (0) | 454 → 0 | 214,408 → 43 |
| list=historytickets | 124 (158) → 33 (35) | 905 → 34 | 214,557 → 3,325 |
| no list, default sort | 182 (212) → 35 (36) | 1,096 → 36 | 109,130 → 1,960 |
| projectId = biggest project | 58 (74) → 7 (12) | 374 → 8 | 107,721 → 564 |
| projectId = smallest project | 27 (42) → 0 (0) | 196 → 0.5 | 107,384 → 213 |
| status=Active,MoreInfo & sort=title | 118 (131) → 80 (87) | 864 → 541 | 214,535 → 110,740 |
| tag=printer (188,919 tickets) | 136 (157) → 105 (106); 27 with the forced plan | 840 → 105; 115 forced | 122,547 → 3,062 |
| ticket detail (17 events) | 0 (0) → 0 (0) | 0 → 0 | 59 → 59 |
| report: open tickets by project/status/age | 41 (46) → 46 (74) | 312 → 46 | 107,201 → 3,705 |
| report: assignee workload, top 50 | 6,322 (6,468) → 64 (67) | 14,112 → 279 | 22,295,977 → 19,096 |
| report: time to close, 90 days | 27 (30) → 79 (104) | 211 → 329 | 107,201 → 4,046 |

Cold runs show the same picture (opentickets 284 → 53 ms, workload 6,573 → 99 ms). Two calls are not faster in
elapsed time: the open-ticket report (the naive plan scanned the clustered index on eight cores; the tuned one reads
29× fewer pages on one core, 6.8× less CPU) and time to close (see its section; the naive "before" also leaves the
percentile work to .NET, which these numbers do not include).

The same table for the legacy app's own SQL (per page, all its statements): unassigned 80 ms / 590 ms CPU / 214,461
reads → new app 4 ms / 4 ms / 1,117; assignedToMe 29 / 221 / 107,261 → 0 / 0 / 203; opentickets 128 / 956 / 214,461 →
51 / 50 / 3,326; historytickets 132 / 1,001 / 214,461 → 33 / 34 / 3,325 (`legacy-on-base` vs `tuned`).

What the indexes alone do: the naive EF Core statements rerun on the expanded schema (EF Core writes
`TicketStatus <> 3` as a literal, so the filtered indexes match) drop to 3,350 reads for page 1 of opentickets, but the
deep page still costs 33,994 reads (10,000 key lookups; the procedure's deferred join does 20) and the workload report
still reads 21.9 million pages (it derives "assigned since" from the events), which only the new column fixes
(`sql-querystore-20261107/naive-on-expanded/`).

## Ticket center: usp_TicketList

**Before (naive)**: two statements per call plus a display-name query. `COUNT(*)` and the page both scan the clustered
index (107,198 pages each) and the page sorts all qualifying rows for a `TOP (20)`:

```
Top | rows 20
  Parallelism (Gather Streams) [parallel]
    Sort [parallel] | rows 1,329 (est 20), execs 8
      Clustered Index Scan Tickets.PK_dbo.Tickets [parallel] | rows 500,169, reads 107,198, where: TicketStatus<>(3)
```

**After**: one round trip (count, page and both display names), `sp_executesql` per filter/sort shape:

```
-- list=assignedToMe: count
Stream Aggregate
  Index Seek IX_Tickets_Open_Assignee (filtered) | rows 131, reads 7, seek: @UserId
-- page: the 20 keys from the narrow index, then the rows
Nested Loops (Left Outer Join)            -- display names, 20 seeks each into IdentityUsers
  Sort | rows 20
    Nested Loops (Inner Join)
      Top | rows 20
        Index Seek IX_Tickets_Open_Assignee (filtered) | rows 20, reads 4, seek: @UserId
      Clustered Index Seek PK_dbo.Tickets | rows 20, execs 20, reads 60
```

What changed and why:

- **Indexes that put the equality column first and the list's ORDER BY after it** (`IX_Tickets_Open_Assignee`,
  `_Open_Owner`, `_Open_Status`, `_Open_Unassigned`, `_Project_LastUpdateDate`, `_LastUpdateDate`): the first page is
  the first 20 index entries, no sort. Filtered on `TicketStatus <> 3` (half the table) where the list only shows open
  tickets. The procedure writes the status predicate as a literal so the filters match (the legacy app's parameterized
  predicate never could).
- **Deferred join for paging**: the inner query does `OFFSET/FETCH` over TicketIds in the narrow index; only the 20
  survivors are read from the clustered index. Page 500 of opentickets: 10,000 index rows (121 reads) and 20 lookups,
  not 10,020 lookups.
- **Display names in the same statement** (two `LEFT JOIN`s to `IdentityUsers`) instead of a second round trip.
- **Safe sorting (S5)**: `@Sort` is split with `STRING_SPLIT(..., 1)`; every field is looked up in a fixed whitelist and
  only the whitelisted column expression reaches the `ORDER BY`; unknown fields raise 50013, duplicates 50014, and
  `TicketId` is appended as the tie-breaker. The API validates first (400), the procedure validates again. Tests try
  `Title; DROP TABLE dbo.Tickets--`, `(SELECT 1)`, unlisted columns.
- **The remaining cost is the total count**: `COUNT(*)` over 500,169 open tickets reads 3,134 pages and takes 33 to 50 ms
  CPU in batch mode; it is most of every opentickets/historytickets/default call. The legacy app (PagedList) counted on
  every request too. Not done, for lack of a measured need: an indexed view of counts (a write hotspot on every status
  change) or dropping exact totals for big lists.
- **Not indexed on purpose**: arbitrary sorts on columns without an index (title, category, ...). `status=Active,MoreInfo
  &sort=title` still scans the clustered index for the page (110,740 reads, half of before because the count now uses a
  filtered index). Add an index when usage shows such a sort is common, not before.

### Parameter sniffing, deliberately

1. **Dynamic SQL from fixed fragments**: each filter/sort shape is its own cached statement, so a plan compiled for
   `list=unassigned` is never reused for `tag=x&sort=title`. Values stay `sp_executesql` parameters (no injection; plan
   reuse inside a shape); only status codes (from `@StatusMask`'s bits) become literals. At most a few hundred shapes.
2. **Indexes that make one plan right for every value** in the indexed shapes: the heaviest owner (34,322 open tickets)
   and an owner with 3 both get "seek, read 20 in order": 705 and 43 reads; the biggest and smallest project 564 and 213.
   Nothing to sniff.
3. **The one shape where the order and the filter come from different indexes** is the tag filter. Probed in both
   orders (`sql-querystore-20261107/tag-sniffing.sql`, `.out`): compiled for the rare tag (`mobile-108`, 1,967
   tickets) or for the common one (`printer`, 188,919), the page statement chooses the same plan (walk
   `IX_Tickets_LastUpdateDate` in order, probe `IX_TicketTags_TagName`): 93 probes for the common tag, 8,710 (27,800
   reads, 13 ms) for the rare one. No regression there, so no `OPTION (RECOMPILE)` (its compile cost would be paid by
   every call of every shape). The count statement does flip, see Query Store.
4. Reports use `OPTION (RECOMPILE)`: a few calls a day, and it lets the optimizer drop the unused `@ProjectId IS NULL OR`
   branch.

## Query Store

`sql-querystore-20261107/qs-plan-changes.sql` lists every query with more than one plan and its runtime stats per plan
(`qs-plan-changes.txt`). Two uses:

**Comparing plans before and after the index change.** The naive EF Core statements ran on `TicketDeskL` before
`td-db migrate` (00:53 to 00:57 UTC) and again afterwards; each kept its `query_id` and got a second plan. For example
the opentickets count (query 27) went from plan 27 (107,198 reads, 49 ms, 224 ms CPU) to plan 108 (3,133 reads, 47 ms,
47 ms CPU); the assignedToMe count (query 33) from 107,198 reads / 51.7 ms to 7 reads / 0.2 ms. Query Store's
"regressed by duration" view flags query 51 (the naive open-ticket report: 64.6 → 141.7 ms): the new plan reads 3,701
pages instead of 107,198 and uses 141 ms CPU instead of 361, but runs serially. Forcing the old plan would buy back
77 ms of elapsed time for 29× the reads and 2.5× the CPU, so it was not forced; the report now runs through
`usp_Report_OpenTicketsAging` anyway.

**A plan regression that was forced.** `usp_TicketList`'s count for the tag shape (query 118) has two plans: 136
(serial, adaptive join; what the app's first compile produced, 90 executions, 111 ms average) and 226 (parallel,
bitmap filter; compiled during the sniffing probe). Each was forced in turn and both tags timed under it
(`force-plan.sql`, `force-plan-summary.txt`, procedure medians of 5 runs, host load 5.2):

| Forced plan | printer (188,919 tickets) | mobile-108 (1,967 tickets) |
|---|---|---|
| 226, parallel | 44 ms elapsed, 115 ms CPU | 24 ms, 49 ms CPU |
| 136, serial | 72 ms, 73 ms CPU | 48 ms, 49 ms CPU |

Plan 226 halves the latency for both values at the same CPU for rare tags and +42 ms CPU for common ones. It is forced
on `TicketDeskL` (`sp_query_store_force_plan @query_id = 118, @plan_id = 226`, `force-plan-applied.txt`); the API call
`tag=printer` went to 27 ms warm (115 ms CPU) from 105 ms (`tuned-list-tag-forced/`). Why the optimizer flips: the
serial plan's estimated cost sits just around the parallelism threshold, so the compile-time row estimate decides.
Caveats: a forced plan lives in that database's Query Store and **does not travel in a .bacpac**, and it stops
applying if the statement text changes. The Azure SQL move runbook therefore re-applies it (or the equivalent Query
Store hint, `sp_query_store_set_hints ... 'OPTION (USE HINT(''ENABLE_PARALLEL_PLAN_PREFERENCE''))'`) after the import;
the alternative is the same hint in the procedure's tag fragment.

**Replaced by code (Round 2 azure-sql).** The fix now lives in `R__usp_ticket_list.sql`: only the tag shape's count
statement ends with `OPTION (USE HINT('ENABLE_PARALLEL_PLAN_PREFERENCE'))` (accepted by SQL Server 2016 SP1+ and Azure
SQL Database, though `sys.dm_exec_valid_use_hints` does not list it); `MigrationTests` checks that exactly that
statement carries it. Measured on `TicketDeskL` against the forced plan it replaces, the new procedure text under
another name so no forced plan can match it (`docs/evidence/azure-sql-20261109/tag-hint-tdsql/`, 5 calls per tag,
timer steps of ~4 ms): the count runs with DOP 8 like plan 226; procedure medians printer 20.3 ms / 77 ms CPU (forced
plan: 24.3 / 111), mobile-108 28.4 / 61 (forced: 20.3 / 48), against 72 / 48 ms for the serial plan 136. On Azure SQL
the plan is shown without any forcing (`plan-tag`, evidence of the Azure run). The forced plan on `TicketDeskL` stays as
the documented Query Store exercise.

## Ticket detail

Unchanged: EF Core with `AsSplitQuery`, five statements (ticket + project, events, tags, subscribers, display names),
all clustered or narrow index seeks, 59 reads, under 1 ms warm before and after. The plans justify nothing more.

## Reports

**Open tickets by project, status and age bucket** (`usp_Report_OpenTicketsAging`). Before: clustered scan of all open
tickets (107,201 reads, 312 ms CPU on 8 cores, 41 ms). After: covered scan of `IX_Tickets_Open_Status` (3,705 reads),
batch-mode hash aggregate, 46 ms CPU on one core. The first version computed `DATEDIFF(hour, CreatedDate, @AsOf)` in
every `CASE` branch (180 ms CPU, `sql-after-20261107/v1.tar.xz`); the current one computes the four bucket edges once from
the UTC hour of `@AsOf` (`DATEDIFF(hour, c, a) < N` exactly when `c >= hourstart(a) - (N-1) h`), so each row costs
four comparisons. A test pins the edges with tickets one tick either side of each boundary in UTC, -08:00 and +05:30.

**Assignee workload** (`usp_Report_AssigneeWorkload`). Before: EF Core translated "assigned since" (latest assignment
event per open ticket) into a correlated subquery and repeated it three times, once for each aggregate that used it:
395,332 open assigned tickets × 3 seeks into TicketEvents plus an eager index spool over all 4.5 million events,
22,295,977 reads, 6.3 s, 14 s CPU. After: the expand column `Tickets.AssignedDate` (`schema-changes.md`) and an ordered,
covered scan of `IX_Tickets_Open_Assignee` with a stream aggregate (no sort for the grouping), one range of
`IX_Tickets_Closed_StatusDate` for "closed in the last 30 days", display names for the top 50 only: 19,096 reads,
64 ms, 279 ms CPU.

**Time to close** (`usp_Report_TimeToClose`). Before: EF Core cannot translate percentiles, so the first pass fetched
137,203 rows (one per closed ticket in the window, clustered scan, 107,201 reads) and aggregated in .NET. After: one
range seek on `IX_Tickets_Closed_StatusDate` (2,021 reads) per grouping and one row per project back. The first
version used `PERCENTILE_CONT ... OVER`, which sorted the rows once per window function (870 ms CPU, same archive); the current
one computes `PERCENTILE_CONT`'s definition from one `ROW_NUMBER` per grouping (329 ms CPU warm in the comparison run). It is still
the one report that is slower in elapsed time than its naive SQL (79 vs 27 ms warm; the naive number excludes the .NET
sort of 137,203 values). Exact p50/p90 over 137,203 rows twice (per project, overall) costs two sorts;
`APPROX_PERCENTILE_CONT` would remove them but change the numbers the reports promise.

## Write cost of the new indexes and the trigger

EF6-shaped statements inside rolled-back transactions (`sql-compare-20261107/compare-writes.md`): a reassignment
`UPDATE ... WHERE TicketId = @3 AND Version = @4; SELECT Version WHERE @@ROWCOUNT > 0` reads 6 pages before and 47
after (six nonclustered indexes contain `LastUpdateDate`, one contains `AssignedTo`, plus the trigger's update); a
ticket `INSERT` 12 → 36. Both stay under 1 ms warm. A help desk writes far less than it reads; the list pages above
read 60× to 5,000× less.

## Indexes added (V0003) and their size at scale L

| Index | Key / include / filter | Pages | For |
|---|---|---|---|
| `IX_Tickets_Open_Status` | (TicketStatus, TargetDate, DueDate, LastUpdateDate DESC) incl. ProjectId, CreatedDate; `TicketStatus <> 3` | 3,702 | opentickets, open-ticket report |
| `IX_Tickets_Open_Unassigned` | (TargetDate, DueDate, LastUpdateDate DESC); `TicketStatus <> 3 AND AssignedTo IS NULL` | 539 | unassigned |
| `IX_Tickets_Open_Assignee` | (AssignedTo, TicketStatus, TargetDate, DueDate, LastUpdateDate DESC) incl. ProjectId, CreatedDate, AssignedDate; `TicketStatus <> 3` | 8,317 | assignedToMe, workload report |
| `IX_Tickets_Open_Owner` | (Owner, TargetDate, DueDate, LastUpdateDate DESC); `TicketStatus <> 3` | 7,448 | mytickets |
| `IX_Tickets_LastUpdateDate` | (LastUpdateDate DESC) incl. TicketStatus | 3,134 | default list, historytickets, counts |
| `IX_Tickets_Project_LastUpdateDate` | (ProjectId, LastUpdateDate DESC) | 3,137 | project filter (makes EF6's `IX_ProjectId` redundant; dropped in the contract stage) |
| `IX_Tickets_Closed_StatusDate` | (CurrentStatusDate) incl. CreatedDate, ProjectId, AssignedTo; `TicketStatus = 3` | 6,991 | time to close, closed-in-30-days |
| `IX_TicketTags_TagName` | (TagName, TicketId) | 6,289 | tag filter |

Sizes: `sql-after-20261107/index-sizes-L.txt`. The expand backfill split most clustered pages (107,198 → 137,721);
V0004 rebuilds the clustered index online where the edition allows (back to 107,141 pages).

## Open

- `IX_Tickets_Open_Unassigned` should `INCLUDE (AssignedTo)`: without the filtered column in the index the optimizer
  re-checks `AssignedTo IS NULL` with 20 clustered lookups on the page and counts through `IX_Tickets_Open_Assignee`
  (923 reads) instead of the 539-page filtered index. A new versioned script (`DROP_EXISTING`), not an edit of V0003. Done: `V0006__open_unassigned_include_assignee.sql` (Round 2 azure-sql; applied by `td-db migrate` on Azure SQL).
- Exact totals dominate the big lists (see above); time to close is CPU-bound on exact percentiles.
- ~~The forced plan for query 118 is an operational fix on `TicketDeskL`; decide between a Query Store hint and a code
  hint before the Azure SQL move.~~ Code hint since Round 2 azure-sql (above).

Evidence layout: each bench directory has `summary-<set>.md` (the table), `plans.txt` (every plan rendered as an
operator tree) and, packed with `ops/dev/pack-evidence.sh` (`xz -9`, plan/AGENT-RULES.md "Evidence size"),
`results-<set>.json(.xz)` (every run, every command), `plans.tar.xz` (`.sqlplan` files) and `raw.tar.xz` (STATISTICS
messages); the legacy replays keep plans and raw messages in one `plans-and-raw.tar.xz`, the first tuned iteration is
`sql-after-20261107/v1.tar.xz`. Unpack with `tar -xJf` / `xz -dk`; `ops/dev/sql-perf.sh compare` and `bench` read
`.json.xz` directly. New bench output is uncompressed: run `ops/dev/pack-evidence.sh <dir>` before committing it.
