# Schema changes while the legacy and the new app share one database

The legacy EF6 app and the new EF Core app run against the same database until the legacy app is retired. Schema
changes therefore go expand → migrate → contract: add what the new app needs next to what the old app uses, keep both
correct while both write, and remove the old part only when nothing old is left. This file is the rulebook, the tool
(`td-db`), and the one change done end to end this round (`Tickets.AssignedDate`).

## What the legacy app tolerates (and why)

From the legacy source and the Extended Events capture of its SQL (`docs/sql/tuning.md`, `docs/evidence/sql-legacy-capture-20261107/`):

- **EF6 never looks at the physical schema at run time.** Every SELECT, INSERT and UPDATE names its columns
  explicitly (seen in all 958 captured statements); extra columns, indexes, triggers and procedures are invisible to it.
- **Startup checks the model, not the tables.** `DatabaseConfig.RegisterDatabase` sets
  `MigrateDatabaseToLatestVersion<TdDomainContext, Configuration>` and calls `Database.CompatibleWithModel(true)`, which
  compares the code model's hash with the last `__MigrationHistory` row for its context key. No new code-based
  migration, no new row, nothing runs. The Identity and push-notification contexts use automatic migrations
  (`docs/migration/legacy-build-notes.md`); they too diff the code model against their own stored snapshot, so a column
  we add to `dbo.Tickets` cannot trigger them. Do not change the shape of the Identity or `notifications.*` tables at
  all while the legacy app runs: an automatic migration with data loss allowed is one model change away.
- **Its other schema probes** look for `dbo.Settings` (TicketDesk 2.0 detection, `IsLegacyDatabase`), count base tables
  (`IsEmptyDatabase`, only while the setup wizard is on) and, per request, Identity 2's `INFORMATION_SCHEMA.COLUMNS`
  probe for `AspNetUsers`. None of them sees our objects. (Never add a table called `dbo.Settings`.)
- **Its write batches** are `UPDATE ... WHERE [TicketId] = @k AND [Version] = @v; SELECT [Version] ... WHERE
  @@ROWCOUNT > 0 AND [TicketId] = @k` and `INSERT ...; SELECT ... WHERE @@ROWCOUNT > 0 AND [TicketId] = scope_identity()`.
  A trigger must not change what those two functions return. Tested: after a statement whose trigger runs another
  UPDATE, `@@ROWCOUNT` is still the statement's own count, and `scope_identity()` is the INSERT's
  (`ExpandTests.Legacy_reassignment_batch_...`, `Legacy_insert_batch_...`, and a probe on a throwaway table).
- **Its session settings** are SqlClient's defaults: `ARITHABORT OFF` with `ANSI_WARNINGS ON`, which is enough for DML
  on tables with filtered indexes. A session with `QUOTED_IDENTIFIER OFF` (some old tools, sqlcmd scripts) cannot write to
  such a table at all (error 1934; pinned by a test). Ops scripts must keep the defaults.

**Verified at run time against the real legacy app (2026-11-08, legacy-adapters):** `td-db migrate` (9 scripts) on the lab VM's database under the running net48 legacy app, app pool restarted so EF6 re-ran its startup check: the app started normally, the golden replayed with one difference (D1 in `docs/migration/expected-differences.md`: the legacy tag autocomplete has no ORDER BY and `IX_TicketTags_TagName` changes the order it returns), and a test-only audit trigger logged every legacy write to `dbo.Tickets` during the replay: 29 statements (8 inserts, 21 updates, 10 assignee changes: NULL→user, user→user, user→NULL), `AssignedDate` right after each, 0 invariant violations over 10,008 tickets (`docs/evidence/legacy-adapters-20261108/step3-expand-schema/`). The lab `baseline-s` now carries the expanded schema (`ops/onprem/baseline/baseline-s.sh upgrade-shared`).

## Rules for an expand step

1. Add, never change: new nullable columns, or NOT NULL with a default. Never drop, rename or retype a column the EF6
   model maps until the contract stage.
2. Dual-write for the legacy writer with a trigger (`SET NOCOUNT ON`; set-based; acts only when the statement did not
   write the new column itself), and tell EF Core about it: `ToTable("Tickets", tb => tb.HasTrigger(...))`. Without
   that, EF Core 7+ writes `UPDATE ... OUTPUT INSERTED.[Version]`, which SQL Server rejects on a table with a trigger
   (error 334; pinned by a test).
3. Backfill in key-range batches small enough to stay under lock escalation (5,000 locks): 4,000 tickets per statement,
   each its own transaction, rerunnable (only rows still NULL), started after the trigger exists so concurrent legacy
   writes are never lost.
4. Indexes `ONLINE = ON` where the edition has it (Developer/Enterprise, Azure SQL); Express (the legacy VM) builds
   offline. Every DDL runs with `LOCK_TIMEOUT` 10 s and is retried, so a schema change waits seconds behind live
   traffic instead of queueing every request behind its schema-modification lock.
5. After a backfill that widens rows, rebuild the clustered index online if possible (the scale-L backfill split
   107,198 pages into 137,721; V0004 put it back to 107,141).
6. Readers move to the new column in the new app; the legacy app keeps reading what it always read.
7. The contract script is written and tested with the expand, and applied only with `--contract --legacy-retired`.

## td-db

`src/modern/TicketDesk.Database` (`td-db`): SQL scripts embedded in the assembly, a journal table
`dbo.__ModernSchemaHistory` (name, kind, SHA-256 checksum, applied at/by, duration), and four commands. Connection
strings only from environment variables.

| Kind | Folder | Applied |
|---|---|---|
| V (expand) | `Migrations/expand/V0001__...sql` | once, in number order; editing an applied one is **drift** and blocks `migrate` (V0001's trigger bug was fixed by V0005, not by editing V0001) |
| R (programmable) | `Migrations/programmable/R__...sql` | after the V scripts, again whenever the text changes (procedures, `CREATE OR ALTER`) |
| C (contract) | `Migrations/contract/C0001__...sql` | only with `td-db migrate --contract --legacy-retired` |

A script runs in one transaction unless it starts with `-- td-db: no-transaction` (batched backfills, index builds),
in which case it must be idempotent. `td-db status` lists applied, pending, contract-pending and drift (exit 1 on drift).
The new app never migrates at startup; `td-db migrate` is a deploy step (pipeline job, or a Kubernetes Job before the
rollout). The new app's test fixtures run the same migrator on every scratch database, so tests always see the schema
the app will run on.

## The change: Tickets.AssignedDate

The assignee-workload report needs "how long has this ticket been with its current assignee". Without a column that
means the latest assignment event per open ticket: the first EF Core version read 22.3 million pages at scale L
(`docs/sql/tuning.md`).

| Step | Script | What | Scale L |
|---|---|---|---|
| expand | V0001 | `ALTER TABLE dbo.Tickets ADD AssignedDate datetimeoffset(7) NULL`; trigger `TR_Tickets_AssignedDate`: when a statement changes `AssignedTo` without setting `AssignedDate`, set it to the row's `LastUpdateDate` (the legacy app sets that on every change), NULL when unassigned; on INSERT with an assignee, `CreatedDate` | 0.03 s |
| backfill | V0002 | latest TakeOver/Assign/ReAssign/Pass event, or a ReOpen by the current assignee ("assign to me"); `CreatedDate` if assigned at creation; 4,000-ticket batches | 895,163 rows, 7.03 s |
| index | V0003 | `IX_Tickets_Open_Assignee ... INCLUDE (..., AssignedDate)` (with the other list/report indexes) | 4.41 s for all eight, online |
| rebuild | V0004 | clustered index rebuilt online (skipped on Express) | 4.06 s |
| fix | V0005 | trigger body: `EXISTS (SELECT i.AssignedTo EXCEPT SELECT d.AssignedTo)` instead of `NOT (i = d OR both NULL)`, which is UNKNOWN when exactly one side is NULL and missed assign-from-unassigned and unassign (found by `ExpandTests`) | 0.02 s |
| readers moved | R `usp_report_assignee_workload` | averages and maxima of `DATEDIFF(hour, AssignedDate, @AsOf)` | report 6.3 s → 64 ms |
| new-app writes | EF Core model | `Ticket.AssignedDate` mapped, `HasTrigger` declared; the ticket write path (wave 4) sets it explicitly | - |
| contract | C0001 (after retirement) | drop the trigger; repair stragglers; `CK_Tickets_AssignedDate` (assigned ⇔ date) `WITH CHECK`; drop EF6's `IX_ProjectId` (a prefix of `IX_Tickets_Project_LastUpdateDate`); same release removes `HasTrigger` so EF Core goes back to `UPDATE ... OUTPUT` | tested on a scratch DB |

Timings: `docs/evidence/sql-after-20261107/migrate-L.txt`, `migrate-L-2.txt`. The backfill's correctness: every
ticket's `AssignedDate` equals the rule computed independently (`ExpandTests.Backfill_matches_...`), and the
workload report from the column equals the report derived from the events, at scale L too
(`sql-after-20261107/verify-L.txt`: "report assignee-workload ... equal").

Tests (`src/modern/tests/TicketDesk.Database.Tests`): the journal (order, run-once, drift, repeatable re-apply, CLI
refusing contract without the flag), the backfill rule, EF6-shaped reassign/unassign/insert batches, set-based
updates, a writer that sets the column itself, legacy session settings vs filtered indexes, EF Core saving through the
trigger, and the contract stage enforcing the invariant (a writer that forgets the date gets error 547).

## Contract checklist (when the legacy app is gone)

Round 4 (`r4-retire`) did 1, 2 and 4 in code and proved 3 on copies; applying 3 to the shared databases is the
coordinator's step at the merge.

1. Ticket write path sets `AssignedDate` (it already must, since the trigger only covers statements that omit it). Done
   since wave 4 (`TicketWriter`); with the constraint in place every test that writes tickets now checks it.
2. Remove `tb.HasTrigger("TR_Tickets_AssignedDate")` from `TicketConfiguration`. **Done** (r4-retire): EF Core writes
   `UPDATE ... OUTPUT` again, so this build needs the contracted schema (on an expand-only database a ticket update fails
   with error 334, pinned by `ExpandTests.EF_Core_without_HasTrigger_cannot_update_tickets_while_the_expand_trigger_exists`).
   Order of a deployment: `td-db migrate --contract --legacy-retired` first, then the new image.
3. `td-db migrate --contract --legacy-retired` in a maintenance window: the `WITH CHECK` constraint scans Tickets under a
   schema-modification lock. **Proven on copies** (r4-retire, `ops/dev/contract-check.sh DB --copy`): a fresh copy of
   td-sql `TicketDesk` (scale S, `docs/evidence/retire-20261110/contract-copy/`) and of `TicketDeskL` (scale L,
   `contract-copy-L/`); the whole test suite ran green on the contracted copy (`TD_MODERN_SEEDED_DB=TicketDesk_retire`), and
   every test database (scratch databases, baseline copies) is now migrated through the contract stage before use.
   `ops/dev/e2e-db.sh` contracts its copy too. Shared `TicketDesk` / `TicketDeskL` and the Azure database: coordinator.
4. Switch Identity to `PasswordHasherCompatibilityMode.IdentityV3` (DECISIONS.md) in the same window if convenient; it
   is not a schema change. **Done** (r4-retire, rehash at sign-in, expected difference I2).

Undoing the contract (only to bring the legacy app back, `docs/dev/rollback.md`): drop `CK_Tickets_AssignedDate` and
create the trigger again from the body of V0005 (`td-db scripts` lists the embedded scripts); `IX_ProjectId` is not needed
by the legacy app at run time (EF6 checks only its model hash).
