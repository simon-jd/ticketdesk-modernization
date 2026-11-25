# The three known flaky tests (Round 5 follow-ups, 2026-11-13 UTC)

Repeat runs of the whole .NET solution on lab-host (32 cores; the box is shared, host load 3-12 during the runs) against
td-sql (SQL Server 2022 CU25 + full-text, Linux container), Azurite and the Service Bus emulator, the CI's test environment
(`azure-pipelines/scripts/ci-lib.sh ci_test_env`): `dotnet test --solution src/modern/TicketDesk.Modern.sln -c Release
--no-build`, one run after the other (`repeat.sh`, below). Per loop `*-runs.txt` (one line per run: exit code, total, failed,
seconds, load) and `*-failed.txt` (the failed tests by run).

## Causes

| Test | Cause | Fix |
|---|---|---|
| `RetiredRoutesTests.First_run_setup_answers_410_with_a_pointer_and_runs_nothing_X1` | "runs nothing" was checked with the IdentityUsers count of the test assembly's shared scratch database; `ApiFixture` (ApiTests, ReportsApiTests) adds its pending user to that database when those classes start, in parallel with this class: "Expected 24, Actual 25" (`before/before-full-x10-run1-x1.txt`) | the class has a scratch database of its own (`RetiredRoutesDatabase`), `c689466` |
| `SearchTests.A_ticket_written_by_the_new_app_is_found_once_the_index_caught_up` | the test polled `FullTextStatus.WaitAsync` (sys.fulltext_indexes + OBJECTPROPERTYEX every 250 ms) until every index was idle. That read deadlocks with the full-text auto population's own transaction (`IFTSAutoNested`), and SQL Server chose the population as the victim (`deadlocks-summary.txt`: 4 times on 2026-11-13, each in a search fixture's copy, plus 2026-11-11T08:57:47Z, the failure recorded in `docs/evidence/aks-20261111/tests/dotnet-test-after-merge-flake.txt`). After that the index reported populate status 6 ("an error has occurred") or "crawl not completed" with its population "Starting" for about a minute or longer, while the row was already in the index (item count 10005, crawl log "completed, 0 failed": `search-run14-*.txt`, `search-run16-ft-poll.txt`), so the 60 s wait timed out | the test waits for the real condition, the search finding the ticket (bounded 60 s), and the fixture for its four rows to be found by their words (bounded 3 min); `FullTextStatus.ReadAsync` (td-db, the deploy step's `td-db fulltext --wait`) reads at `DEADLOCK_PRIORITY -10` on a pool of its own, so the read is the victim, and `WaitAsync` reads again |
| `MigrationTests.Only_the_tag_count_asks_for_the_parallel_plan` | Query Store shows a new query asynchronously: the test read `sys.query_store_query` right after the two procedure calls; under load the second call's count statement was not there yet and was there 3 s later (`qs-hunt2-run5-diagnostics.txt`: `counts 1 [...TagName...] later=3`, Query Store READ_WRITE, capture mode ALL, readonly_reason 0). The failures came with extra load on td-sql (3 of 25 runs while `ft-poll.sh` queried every scratch database each second; 0 of 27 without it, but CI saw it once on 2026-11-11 without it) | the test waits (bounded 30 s) until both count statements are in Query Store, then checks them |

## Repeat counts

Full runs of the solution (770 tests after, 746 before); a run counts a test once even when it failed twice. "poller": `ft-poll.sh`
ran at the same time (1 s full-text polls of every scratch database: the condition under which the Query Store failures came).

| Loop (`before/`, `after/`) | Code | Runs | X1 | Search catch-up | Query Store tag count | Other |
|---|---|---:|---:|---:|---:|---|
| `before-1`, `before-2` | `be095b9` | 2 | 0 | 1 (before-1, no poller) | 0 | |
| `before-full-x10` (poller) | `be095b9` | 10 | **5** | 0 | 0 | |
| `flake-hunt-full-x20` (poller) | `d831485` (X1 fixed) | 20 | 0 | **2** (runs 14, 16) | **2** (runs 3, 19) | run 19: the assembly fixture's DROP DATABASE was a deadlock victim of the poller's cursor (185 cleanup failures, an artifact of the poller) |
| `qs-hunt-full-x15` | search and FullTextStatus fixes uncommitted, Query Store diagnostics | 15 | 0 | 0 | 0 | |
| `qs-hunt2-full-x20` (poller), stopped after 5 | same | 5 | 0 | 0 | **1** (run 5, diagnostics: `qs-hunt2-run5-diagnostics.txt`) | |
| `after-full-x20` | `acf55e1` | 20 | 0 | 0 | 0 | `FullTextMigrationTests.Rows_written_after_the_migration_become_searchable_without_any_hook` 1 (run 13) |
| `after-full-poller-x20` (poller) | `acf55e1` | 20 | 0 | 0 | 0 | the same test 2 (runs 17, 18) |
| `after-class-migration-x50` | `acf55e1` | 50 | | | 0 | MigrationTests class |
| `after-class-retired-api-x50` | `acf55e1` | 50 | 0 | | | RetiredRoutesTests + ApiTests (the class that raced it) |
| `after-class-search-x20` | `acf55e1` | 20 | | 0 | | SearchTests class |

The three class loops ran at the same time (each other's load, `after-classes.sh`). Before the fixes: X1 5 of 12 runs on `be095b9`;
search 3 of 32 pre-fix runs; Query Store 3 of 36 pre-fix runs with the poller, 0 of 16 without. After: 0 of 40 full runs and 0 of
50 / 50 / 20 class runs for each of the three.

## FullTextMigrationTests (seen after the fixes, fixed in Round 5b)

`Rows_written_after_the_migration_become_searchable_without_any_hook` failed 3 of 40 full runs after the fixes (3 of 60 runs
built with this round's `FullTextStatus` change, 0 of 32 without; the coordinator: not significant, Fisher p ≈ 0.27): twice in
its first `FullTextStatus.WaitAsync` (2 min, the fresh scratch database's initial full population), once in the second (1 min,
after its writes), no deadlock report; the run-13 crawl log shows "Full population initialized" and no "completed"
(`after/after-full-x20-run13-crawl-log.txt`). Round 5b (`46f3c91`): the test waits, bounded, for CONTAINSTABLE to find its new
rows, the state only for the failure message: class 50 of 50, `TicketDesk.Database.Tests` 10 of 10 (`../round5b/`).

## Commands

    repeat.sh LABEL N [--project P] [dotnet test args]   # N runs, no build; CI environment read by name from $TD_W/.secrets
    after-classes.sh                                     # the three class loops in parallel
    ft-poll.sh OUT                                       # 1 s full-text state of td-sql's scratch databases (see its CAUTION line)
    # system_health deadlock reports:
    #   SELECT ... FROM sys.fn_xe_file_target_read_file('system_health*.xel', NULL, NULL, NULL) WHERE object_name = N'xml_deadlock_report'
