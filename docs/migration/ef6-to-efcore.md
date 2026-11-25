# EF6 → EF Core: behaviour differences that matter for TicketDesk

Round 1, `modern-core`. Legacy: EF 6.1.3 with three contexts on one database (`TdDomainContext`, `TdIdentityContext`,
`TdPushNotificationContext`). New: EF Core 10.0.14 with one context, `TicketDeskDbContext`
(`src/modern/TicketDesk.Data`), over the schema exactly as EF6 left it. Nothing below changes the schema.

Each row names the test that pins the EF Core side. Test classes live in `src/modern/tests/`:
`Data.Tests/{ModelTests,EfBehaviourTests,SchemaMappingTests,SeededReadTests}`, `Identity.Tests/UserStoreTests`,
`Modules.Tests/TicketSortTests`, `Web.Tests/ApiTests`. SQL tests run against the td-sql container: a scratch database
(EF6-implied schema plus XS seed, created and dropped by the tests) and the seeded `TicketDeskSynth` (scale S).
Set `TD_MODERN_SEEDED_DB=TicketDesk` to run the same checks against the restored legacy database once it exists.

**Verify** marks a statement about the legacy side that comes from reading the code, not from a recording. The Round 1
`baseline` recording should confirm or correct it.

## Summary

| # | Topic | EF6 (legacy) | EF Core (new) | Pinned by |
|---|---|---|---|---|
| 1 | Schema ownership, migration history | Code-based migrations for the domain; **automatic** migrations (data loss allowed) for Identity and notifications; `__MigrationHistory` rows per `ContextKey`, model hash checked at startup | No migrations, no `EnsureCreated`/`Migrate`. `__MigrationHistory` is not in the model and is never written; no `__EFMigrationsHistory` is created | `ModelTests.No_migrations_and_no_migration_history_tables_in_the_model`, `EfBehaviourTests.Nothing_writes_migration_history` |
| 2 | Exact mapping | Attributes plus fluent config spread over three contexts | Fluent configuration only; every table, column (store type, length, precision, nullability, identity), primary key (in order) and foreign key (with delete action) compared against `sys.*` | `SchemaMappingTests.Model_matches_scratch_database_schema`, `..._seeded_database_schema` |
| 3 | Lazy loading | `virtual` navigations and proxies: touching `ticket.TicketEvents` runs a query | No proxies. Navigations stay empty unless `Include`d or projected | `EfBehaviourTests.Lazy_loading_is_off_navigations_stay_empty_without_Include` |
| 4 | Cascade conventions | Snapshot says Ticket→Project cascades, but `Td2.5.017` created the FK **without** cascade; notifications→subscribers `WillCascadeOnDelete(false)` | Delete behaviour taken from the database: `Restrict` for those two, `Cascade` elsewhere (EF Core's convention would cascade every required FK) | `ModelTests.Delete_behaviours_follow_the_database_not_ef_conventions`, `EfBehaviourTests.Database_cascades_ticket_deletes_and_restricts_project_deletes` |
| 5 | Client evaluation | Untranslatable LINQ throws `NotSupportedException` | Throws `InvalidOperationException`, except in the final `Select`, where client code is allowed | `EfBehaviourTests.Untranslatable_predicate_throws_instead_of_client_evaluating` |
| 6 | Entity SQL | Ticket center builds `ObjectQuery.Where("it.Col = @p")` / `OrderBy("it.Col DESC")` from saved user settings and the `columnName` query parameter | No Entity SQL in EF Core. Expression trees with a 16-field whitelist (fixes S5) | `TicketSortTests.*`, `ApiTests.Sort_fields_outside_the_whitelist_are_rejected`, `ApiTests.Whitelisted_sorts_match_SQL_order` |
| 7 | Null comparison | LINQ emulates C# nulls, but **Entity SQL does not**: `it.AssignedTo = @AssignedTo` with a null parameter becomes `= NULL` and matches nothing | `t.AssignedTo == null` and comparisons with a null variable become `IS NULL` | `EfBehaviourTests.Null_comparison_uses_csharp_semantics_where_entity_sql_used_sql_semantics`, `ApiTests.Legacy_lists_match_SQL` |
| 8 | String comparison, collation | `=` runs in SQL Server: case-insensitive, trailing spaces ignored (`SQL_Latin1_General_CP1_CI_AS`) | Same SQL, same result, and code now depends on it (see the Identity store below) | `EfBehaviourTests.String_equality_follows_the_database_collation`, `UserStoreTests.Lookups_by_name_and_email_ignore_case_without_normalized_columns` |
| 9 | Include and split queries | All `Include`s in one query (joins, rows multiply) | A query with two or more collection `Include`s **throws** unless it says `AsSplitQuery()` or `AsSingleQuery()`; ticket detail uses split (root + 3 queries) | `EfBehaviourTests.Several_collection_includes_must_choose_split_or_single_query` |
| 10 | Concurrency tokens | `[Timestamp]` rowversion on Projects, Tickets, TicketEvents, TicketEventNotifications; `System.Data.Entity.Infrastructure.DbUpdateConcurrencyException` | `IsRowVersion()` on the same four; `Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException`. Identity tables have no token in either | `EfBehaviourTests.Rowversion_conflict_throws_DbUpdateConcurrencyException`, `ModelTests.Version_columns_are_rowversion_concurrency_tokens`, `ModelTests.Concurrency_tokens_exist_only_where_the_legacy_schema_has_rowversion` |
| 11 | Database-generated dates | `CreatedDate`, `EventDate` are `[DatabaseGenerated(Computed)]`: never written, read back after save; the column default `SYSDATETIMEOFFSET()` supplies them | `ValueGeneratedOnAddOrUpdate` with before- and after-save behaviour `Ignore`: a value set in code is dropped, the database clock wins | `ModelTests.Ef6_computed_dates_are_never_written_and_read_back`, `EfBehaviourTests.Database_generated_dates_are_never_written_and_EventId_comes_from_identity` |
| 12 | Defaults EF6 always wrote | `Tickets.ProjectId DEFAULT ((1))`, `TicketEvents.ForActivity DEFAULT ((16384))` exist for old rows; EF6 always sent a value | No `HasDefaultValue` (with one, EF Core skips writing 0 and the database default would silently apply) | `ModelTests.Columns_with_legacy_defaults_that_ef6_always_wrote_have_no_ef_default` |
| 13 | Identity column inside a composite key | `TicketEvents (TicketId, EventId IDENTITY)` worked by attribute | EF Core applies the identity convention only to single-column keys: `EventId` is configured `UseIdentityColumn()` | `ModelTests.EventId_is_an_identity_column_inside_the_composite_key` |
| 14 | DateTime kinds and offsets | Domain dates are `datetimeoffset` written from `DateTime.Now` (server-local offset). `IdentityUsers.LockoutEndDateUtc` is `datetime`; disabling a user sets it to `DateTime.MaxValue` | `datetimeoffset` round-trips with its offset and compares by instant; a UTC container writes `+00:00`. `LockoutEndDateUtc` is read as `DateTimeKind.Utc`, local values are converted, and `DateTime.MaxValue` is clamped to `9999-12-31 23:59:59.997` | `EfBehaviourTests.DateTimeOffset_keeps_its_offset_and_compares_by_instant`, `EfBehaviourTests.LockoutEndDateUtc_reads_as_utc_and_DateTime_MaxValue_is_clamped`, `UserStoreTests.Disabling_with_DateTimeOffset_MaxValue_locks_out_permanently` |
| 15 | `SaveChanges` side effects | `TdDomainContext.SaveChanges` fills audit/status fields, tags, the create event, subscribers and notifications, resets `SelectedProjectId` on project delete, raises static events; `ValidateEntity` checks the Create permission | **Not** in the context. The Round 1 API is read-only. Round 2 write paths must put these rules into explicit domain services; `ITicketChangeNotifier` (Notifications module) replaces the static events | none yet (no write path) |
| 16 | Complex types | Settings JSON mapped as EF6 complex types with a `Serialized` property (`ListSettingsJson`, `*SettingsJson`) | Plain `string?` columns; deserialization moves into the owning module when a route needs it | `SchemaMappingTests.*`, `SeededReadTests.Row_counts_through_EF_match_SQL_for_every_mapped_table` |
| 17 | TPH roles | `IdentityRole` and `TicketDeskRole` share `IdentityRoles` with a `Discriminator` column | One `TicketDeskRole` entity; `Discriminator` is an ordinary column, new roles get `TicketDeskRole` | `SeededReadTests.Users_per_role_through_EF_match_SQL` |
| 18 | Identity 2 schema under Core Identity | Identity 2.2.1 columns only | No `NormalizedUserName`, `NormalizedEmail`, `ConcurrencyStamp`, `LockoutEnd`, role claims or user tokens, so custom stores instead of the EF Core Identity stores (details below) | `UserStoreTests.*` |

## Details

### 1. Schema ownership and the EF6 automatic migrations

The legacy app runs migration initializers at startup. `TdIdentityContext` and `TdPushNotificationContext` use automatic
migrations with `AutomaticMigrationDataLossAllowed`, so their model must keep matching the hash in `__MigrationHistory`.
The new app never writes schema or history, so it cannot trip that check. The Round 2 expand changes for Identity
(`NormalizedUserName` and the rest, see inventory §5) will. Before adding any column, disable the legacy initializers on
the VM (`Database.SetInitializer<TdIdentityContext>(null)` via config) and test that the legacy app starts against the
expanded schema.

### 4. Cascades: database vs change tracker

Both apps rely on the database for cascades when deleting a ticket set-wise. EF Core also cascades **tracked** dependents
in memory (`CascadeTiming.Immediate`), and with `Restrict` on notifications→subscribers a tracked graph can fail before
any SQL runs. Write paths should delete set-wise (`ExecuteDelete`) or load exactly what they delete. Deleting a project
that still has tickets fails with SQL error 547 in both apps. The legacy controller moves the tickets first.

### 7. The "unassigned" list (verify)

`UserTicketListSetting` gives help-desk users an `unassigned` list with filter `AssignedTo = null`. `ApplyToQuery`
turns it into the Entity SQL `it.AssignedTo = @AssignedTo` with a null `ObjectParameter`. Entity SQL uses SQL null
semantics, so the legacy list probably shows **no tickets**. The scratch test shows that `= NULL` matches 0 rows while
`IS NULL` matches many. The new list returns the tickets with no assignee, which is what the list is meant to show.
This is candidate expected difference **E1** in `expected-differences.md`. Confirm it with the baseline recording (the
ticket-list journey as a help-desk user) before classifying it.

### 8. Collation is now load-bearing

The Identity store finds users by `UserName == normalizedName`. Core Identity passes the upper-cased name, and the
case-insensitive collation makes it match. The Azure SQL move (Round 2) must keep a `_CI_` collation on these columns.
The test checks the column collation explicitly. The legacy self-demotion guard compares the role name
`"tdAdministrators"` in SQL and depends on the same collation.

### 11. Database clock vs application clock

`CreatedDate` and `EventDate` come from the SQL Server clock and offset (`SYSDATETIMEOFFSET()`). The other dates
(`LastUpdateDate`, `CurrentStatusDate`, ...) come from the application (`DateTime.Now`). On the lab VM both clocks
share the VM's offset. In a UTC container next to a database in another offset, one ticket can carry two offsets.
Round 2 write paths should write `DateTimeOffset.UtcNow`, and the UI converts dates for display.

### 14. The "disabled user" marker (verify)

The legacy admin page disables a user with `SetLockoutEndDateAsync(id, DateTimeOffset.MaxValue)`, which Identity 2
stores as `DateTime.MaxValue` in a `datetime` column. SQL Server `datetime` holds 1/300 s ticks, and
`23:59:59.9999999` rounds past the end of the range. The new store therefore clamps to `9999-12-31 23:59:59.997`.
The legacy page detects "disabled" with `== DateTime.MaxValue`, which a stored `...59.997` value does not satisfy, so the
legacy page may show such a user as locked rather than disabled. Lockout itself works either way, and both apps treat
the user as locked out. Record a disable in the baseline to see what EF6 actually stores.

### 18. Core Identity over the Identity 2 tables

`TicketDeskUserStore` and `TicketDeskRoleStore` (`src/modern/TicketDesk.Identity`) implement the store interfaces
directly over `TicketDeskDbContext`:

- normalized names and e-mails are computed (`Normalize().ToUpperInvariant()`, like the default lookup normalizer) and
  never stored; lookups compare against `UserName`/`Email` under the CI collation;
- `LockoutEnd` ↔ `LockoutEndDateUtc` (UTC, clamped);
- the password hasher runs with `PasswordHasherCompatibilityMode.IdentityV2` (plan/DECISIONS.md): seeded V2 hashes
  verify as `Success` (not `SuccessRehashNeeded`), and new or changed passwords are stored as V2. The tests verify them
  with the seeder's independent reimplementation of the Identity 2 algorithm;
- there is no concurrency token on users. Last writer wins, as in the legacy app;
- password-reset and confirmation tokens come from ASP.NET Core Data Protection. Legacy OWIN tokens do not carry over,
  and tokens in flight at cutover stop working.
