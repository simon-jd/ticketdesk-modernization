# Round 3 angular: evidence (2026-11-10)

Branch `ws/angular`. All runs through the gateway used https://localhost:5463 (5443/8443 belonged to the coordinator's Entra
check), the legacy VM at snapshot `baseline-s-identity`, the shared sign-in cookie and the VM database as `td_modern_app`; one
VM-lock session 18:46–18:53Z (script: E2E ×3, golden replay, Lighthouse, reset).

| Folder | Command | Result |
|---|---|---|
| `e2e-gateway/` | `TD_LOCK_OWNER=angular ops/dev/e2e.sh --out-dir … --runs 3` | 10/10, 10/10, 10/10 (`runs.txt`; per run `results.json`, `summary.txt`) |
| `replay-waves-0-5/` | `TD_LOCK_OWNER=angular TD_MODERN_DB=vm TD_MODERN_LOGIN=app TD_GATEWAY_URL=https://localhost:5463/ ops/dev/replay-gateway.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir … --waves 0,1,2,3,4,5 --shared-cookie` | 262 compared, 155 matched, 109 explained, **0 unexplained** (`summary.md`). The first summary had 3 unexplained: the G6 rule named port 5443; it now matches any gateway port (`actual_regex`), and `summary.md` was regenerated from the same `gateway.jsonl.xz`/`report.json` with `replay-summary.py` |
| `frontend/` | `TD_LOCK_OWNER=angular ops/dev/frontend-after.sh --out-dir …` | `comparison.md`: legacy direct, server pages (registry New) and Angular, back to back; Lighthouse JSON as `.xz` |
| `bundle/` | `npm run build` + `curl -H 'Accept-Encoding: br'` per file | `files.csv`, `ng-build.txt` |
| `tests/` | `dotnet test --solution src/modern/TicketDesk.Modern.sln -c Release`; `npm test` | 699 passed; Vitest 28 passed |
| `container/` | `docker build -f src/modern/TicketDesk.Web/Dockerfile -t ticketdesk-web:r3-angular .`, run without the VM | `container-check.txt` |

Unpack: `xz -dk replay-waves-0-5/gateway.jsonl.xz`, `xz -dk frontend/angular/lighthouse-ticket-center.json.xz`.
