# Coordinator rerun after merging `ws/retire` (modernize `15fb06b`, 2026-11-11)

Run from `$W/scratch-coord-r4/run.sh` (tmux `td-coord-r4`), same environment as `docs/evidence/retire-20261110/README.md`.

- Contract stage on the shared td-sql databases: `ops/dev/contract-check.sh TicketDesk` (C0001 21 ms) and `TicketDeskL` (225 ms), exit 0.
- `dotnet test --solution src/modern/TicketDesk.Modern.sln -c Release`: 729 total, 729 passed.
- Vitest (`npm test` in `src/web`): 5 files, 28 tests passed.
- Golden replay on the new app alone (`ops/dev/replay-modern.sh`, copy `TicketDesk_coord_replay`, https://localhost:5493/): 258 compared, 128 matched, 136 explained, 0 unexplained, 0 exchanges by legacy. The first attempt stopped before the replay: `td-db fulltext --wait 300` saw `dbo.Tickets` "populating" for 300 s with 10,000 items and 0 pending (`replay-attempt1-fulltext-wait.txt`); a new crawl finished a minute later and the rerun passed.
- E2E (`ops/dev/e2e.sh --runs 1`, https://localhost:5483): 10/10.
