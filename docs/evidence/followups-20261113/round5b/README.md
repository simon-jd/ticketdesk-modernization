# Round 5b (2026-11-13 UTC, after the coordinator's review)

| Item | Result | Files |
|---|---|---|
| 1. `FullTextMigrationTests.Rows_written_..._without_any_hook` waits (bounded, 3 min) for CONTAINSTABLE to find the updated title and the new comment; the index state only in the failure message (`46f3c91`) | class **50 of 50**, `TicketDesk.Database.Tests` **10 of 10** (run at the same time, 50 + 10 loops) | `class-fulltextmigration-x50-runs.txt`, `database-tests-x10-runs.txt` |
| 2. The compare keeps every diff (report.json); `--max-diffs` shortens only report.md; F1 covers panel positions 2-41 (`b554f13`) | golden replay on the new app alone, uncapped: 258 compared, **87 matched, 177 explained, 0 unexplained**; 2,079 diffs counted, 22 exchanges with more than 25 | `../replay-modern/`, `../replay-modern/uncapped/README.md` |
| 3. Empty install: nothing created `notifications.*` (legacy: automatic EF6 migration), so the app's own new-user path answered **500**; `td-seed schema` creates the five tables as the restored legacy database has them, admin/new then **302** and writes the e-mail destination, `td-db create-admin` writes it the same way (`b19f9f9`) | before: `POST /admin/new` 500, no destination table; after: 302, 5 tables, 2 destinations (admin + new user) | `empty-install-before.txt`, `empty-install-after.txt`, `empty-install-check.sh` |
| 4. td-db image built locally (`docker build -f src/modern/TicketDesk.Database/Dockerfile -t ticketdesk-db:r5b-followups .`, not pushed; chiseled runtime, user 1654) | `create-admin --help` exit 0; `migrate --contract --legacy-retired` 15 scripts; `create-admin --password-stdin` exit 0 (V3 hash, e-mail destination); a second run refused, exit 1 | `tddb-image-build.txt`, `tddb-image-run.txt`, `tddb-image-run.sh` |

Full solution on `b19f9f9`: **772 of 772** (`full-after.txt`; +2: `FirstAdministratorTests.Help_prints_the_usage_and_exits_0`,
`HtmlDomComparerTests.Reports_every_diff_of_a_page_not_only_the_first_25`). Vitest 30 of 30 (`../tests/vitest-after.txt`).
Playwright not rerun (no UI change since `acf55e1`: 15 of 15 ×3, `../e2e/`).
