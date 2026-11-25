# Round 2b tickets: evidence (2026-11-09/10)

| Folder | What | Command |
|---|---|---|
| `perf-attribution/` | Task 0: legacy direct p95, Round-1 state (`baseline-s-legacy`) vs the shared `baseline-s`; answer in its README | `ops/dev/perf-attribution.sh` (see README there) |
| `rendering/` | Ticket text rendering (Markdown + HtmlSanitizer) byte for byte against the legacy DLLs on the VM: 55,829 / 55,829 equal | `TD_LOCK_OWNER=tickets ops/dev/render-parity/render-parity.sh --out-dir rendering` |
| `replay-step1-ticket-page/` | Golden through the gateway, waves 0–3 with the ticket page group (ticket view, details, events, activity buttons, subscription): 262 compared, 228 matched, 35 explained, **0 unexplained**; 124 exchanges answered by the new app; legacy still writes the tickets | `TD_LOCK_OWNER=tickets TD_MODERN_DB=vm ops/dev/replay-gateway.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir replay-step1-ticket-page --waves 0,1,2,3 --remote-auth` |
| `replay-step2-wave4/` | The same with wave 4 (new ticket, load-activity + 15 activity posts, file upload/delete): 262 compared, 199 matched, 64 explained, **0 unexplained**; 186 exchanges on 49 routes answered by the new app (all 50 migrated rows; `navigation.project-menu` is a child row) | `... --out-dir replay-step2-wave4 --waves 0,1,2,3,4 --remote-auth` |
| `replay-step2-wave4/db-legacy-vs-new-writes.txt` | Rows the journeys wrote (tickets 10001–10008) with the legacy write path (step 1) vs the new app's (step 2): TicketEvents 29/29 (descriptions, comments incl. CRLF, activity), TicketSubscribers 11/11, TicketEventNotifications 44/44 equal, AssignedDate equal on all 8; differences: tags of 10002 (L7, intended) and, on 10001, CurrentStatusDate vs LastUpdateDate (legacy took two `DateTime.Now` readings on create, the new app one) | `ops/dev/ticket-db-dump.sh OUT.json` after each replay, `ops/dev/ticket-db-dump.sh --diff A B` |
| `perf-after/` | p95 direct vs gateway with waves 0–4 migrated, back to back | `TD_MODERN_DB=vm TD_GATEWAY_ARGS="--waves 0,1,2,3,4 --remote-auth" TD_LOCK_OWNER=tickets ops/dev/perf-baseline.sh --out-dir perf-after --runs 3` |
| `tests/` | `dotnet test` of the solution | `dotnet test --solution src/modern/TicketDesk.Modern.sln -c Release` (env as in the routes handoff) |

Large raw files are `.xz` (`ops/dev/pack-evidence.sh`); `gateway.jsonl.xz` holds every replayed exchange. Expected
differences: `docs/migration/expected-differences.md` (rules: `docs/migration/replay-explanations.json`).
