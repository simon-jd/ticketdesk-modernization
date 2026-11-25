# Test counts before (modernize @ be095b9) and after (ws/followups @ b19f9f9)

| Suite | Before | After | Added |
|---|---:|---:|---|
| .NET, TicketDesk.Web.Tests | 170 | 185 | `StateChangingPostTests` (8 theory rows + 6 facts = 14; incl. the check that no recorded GET of the legacy recording becomes a post), `FirstAdministratorWebTests` (1); `WaveParityTests`: the 2 culture-switch rows moved from the GET theory to a POST theory (0 net) |
| .NET, TicketDesk.Database.Tests | 96 | 103 | `FirstAdministratorTests` (7, one of them Round 5b: `--help`) |
| .NET, TicketDesk.Identity.Tests | 44 | 46 | `FirstAdministratorSignInTests` (2) |
| .NET, TicketDesk.Replay.Tests | 101 | 103 | `PostedLinkTests` (1), Round 5b `HtmlDomComparerTests.Reports_every_diff_of_a_page_not_only_the_first_25` (1) |
| .NET, Modules / Data / Seed / Worker | 275 / 32 / 20 / 8 | 275 / 32 / 20 / 8 | |
| **.NET total** | **746** | **772** | +26 (770 at `acf55e1`, +2 in Round 5b) |
| Vitest (src/web) | 28 | 30 | `app.spec.ts` (2: the frame's Log off form posts the XSRF token; no form before the session) |
| Playwright (src/web/e2e) | 10 | 15 | `state-changes.spec.ts` (5: language menu, project menu, server ticket center sort + reset dialog, Log off in the Angular frame, sign-out page by GET) |

Before: `before-1` / `before-2` full runs on `be095b9` (console totals 746; the per-assembly numbers are from the TRX files of
`before-1` plus the console total, `../flakes/before/before-1-summary.txt`), Vitest `vitest-before.txt` (28). Playwright 10:
README, CI section (runs 17/18). After: `list-tests-after.txt` (`dotnet test --solution src/modern/TicketDesk.Modern.sln -c
Release --no-build --list-tests`), `vitest-after.txt`, `../e2e/runs.txt` (15 x 3, at `acf55e1`), `full-after.txt` (770 at `acf55e1`), `../round5b/full-after.txt` (772 at `b19f9f9`).
