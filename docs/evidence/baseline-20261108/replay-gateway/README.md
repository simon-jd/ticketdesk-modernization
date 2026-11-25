# Golden recording replayed through the gateway

`ops/dev/replay-gateway.sh`, 2026-11-08 16:04Z, working tree `74805ef` + `7c6ae12` + `e800817` (see `../replay-legacy/`):
TicketDesk.Web (Release, Production settings) on https://localhost:5443 with the YARP fallback to the legacy app
(https://localhost:8443, certificate pinned), nothing migrated; VM reset to `baseline-s`; `td-replay replay` of the
re-recorded `tests/characterization/golden/legacy-golden.jsonl.xz`; `td-replay compare` against it.

- replay: 262 exchanges, 15 journeys, 0 journey errors (`replay.txt`)
- compare: **262 compared, 262 matched, 0 mismatched** (`compare.txt`, `compare-golden-gateway/report.md`)
- the gateway logged nothing but start/stop (`gateway.log`); the replayed responses are `gateway.jsonl.xz` (`xz -dc`)
- the earlier golden (`customErrors` Off) also replayed 262/262 through the gateway (07:30Z before and 15:17Z after merging
  modernize `8dc5a0f`; superseded)
