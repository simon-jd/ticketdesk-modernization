# Round 5 follow-ups: evidence (2026-11-13 UTC)

Branch `ws/followups` (from `modernize` @ `be095b9`). The five state-changing GETs are POSTs with antiforgery, `td-db
create-admin` replaces the retired first-run setup's administrator step, and the three known flaky tests are fixed. Nothing
here contains a password, a connection string or a cookie value (the scripts read secrets by name and refuse to write them).
No legacy VM, no Azure: the new app alone against td-sql.

| Directory | What | Command |
|---|---|---|
| `replay-modern/` | the golden recording replayed against the new app alone at `b19f9f9` (Round 5b: every diff counted); `summary.md` explains every mismatch; `modern.jsonl.xz` is the candidate; `uncapped/` the cap and the Round 4 candidate | `ops/dev/replay-modern.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir ... --db TicketDesk_r5replay --url https://localhost:5483/` |
| `e2e/` | Playwright against the new app alone, 3 runs back to back (15 tests each) | `TD_E2E_URL=https://localhost:5487 TD_E2E_DB=TicketDesk_r5e2e ops/dev/e2e.sh --out-dir ... --runs 3` |
| `first-admin/` | the real run of `td-db create-admin` on a scratch empty installation and one sign-in through the running app | `real-run.sh` (output `real-run.txt`) |
| `flakes/` | the three flaky tests: causes (system_health deadlock reports, full-text poll and crawl log excerpts, Query Store diagnostics), repeat counts before and after | `flakes/README.md` |
| `tests/` | test counts before and after (.NET, Vitest, Playwright), the test list, Vitest output, the last full .NET run | `tests/before-after.md` |
| `round5b/` | the four items after the coordinator's review: FullTextMigrationTests, the uncapped gate, the empty install, the td-db image | `round5b/README.md` |

## Golden replay (replay-modern/)

258 compared: **87 matched, 177 explained, 0 unexplained**, every diff counted (2,079; until Round 5b td-replay kept only the
first 25 of an exchange). The 177 include 4 recorded redirect hops the new app no longer makes (X1) and 2 extra exchanges, as
in Round 4 (128 matched, 136 explained, 0 unexplained under the cap; uncapped with its own rules: 2 unexplained). The 41
exchanges that matched in Round 4 and differ now are all explained by `S3 (Round 5)` only: the layout's menu items that post
(`link-to-post`: same URL, same text), the style text for them, and, since every such page issues a token,
`Cache-Control: no-store, no-cache` and the antiforgery cookie. 10 recorded GET steps were sent as POSTs because a page of the
journey offers that URL as a post now (language ×2, sign-out ×2, sort ×3, reset, project ×2; `replay.txt`).
