# Round 4 retire: evidence (2026-11-10/11 UTC)

Branch `ws/retire` (from `modernize` @ `c702afb`). The new app alone: no YARP fallback, no remote authentication, no shared
cookie, the 9 wave-`retire` routes answered with 410/404, Identity V3 hashes, the contract stage. Every directory holds the
command, the environment and the raw output; nothing here contains a password (the scripts refuse to write one).

| Directory | What | Command |
|---|---|---|
| `contract-copy/` | `td-db migrate --contract --legacy-retired` (C0001) on a fresh copy of td-sql `TicketDesk` (scale S): status before/after, timed run, invariant checks | `ops/dev/contract-check.sh TicketDesk_retire --copy --out-dir ...` |
| `contract-copy-L/` | the same on a copy of `TicketDeskL` (scale L, 1,000,000 tickets); the copy was dropped afterwards | `ops/dev/contract-check.sh TicketDeskL_retire --copy-from TicketDeskL --out-dir ...` |
| `tests/` | full solution test runs before (`modernize` @ `c702afb`, unchanged) and after (this branch), test lists, `before-after.md` (per assembly, removed and added tests) | `dotnet test --solution src/modern/TicketDesk.Modern.sln -c Release` (+ `--list-tests`), `npm test` in `src/web` |
| `replay-modern/` | the golden recording replayed against the new app alone; `summary.md` explains every mismatch; `modern.jsonl.xz` is the candidate recording (`xz -dk`) | `ops/dev/replay-modern.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir ...` |
| `perf-legacy-vs-new/` | k6 mix, legacy (Round-1 reference snapshot `baseline-s-legacy`, direct) and the new app alone, interleaved back-to-back pairs, 5 each; `pairs.md`, `summary.md`, per run `k6.txt`, `summary.json`, `host-load.txt`, `reset.txt`, (new app) `prepare.txt`, `app-start.txt`, `app.log` | below |
| `e2e/` | Playwright suite against the new app alone, 3 runs back to back | `ops/dev/e2e.sh --out-dir ... --runs 3` |
| `container/` | the web image built from this branch with the unchanged Dockerfile and a smoke check of the running container | `docker build -f src/modern/TicketDesk.Web/Dockerfile -t ticketdesk-web:r4-retire .`, then `run.txt` |

## Test environment (tests/)

`TD_SQL_TEST_CONN` = td-sql (SQL Server 2022 CU25 + full-text), `TD_SEED_USER_PASSWORD` (seeded users), Azurite
(`TD_AZURE_STORAGE_CONNECTION_HOST`), the Service Bus emulator (`TD_SERVICEBUS_TEST_CONN`). Before: `TD_MODERN_SEEDED_DB=TicketDesk`
(the shared copy of the lab's baseline data, expand stage). After: `TD_MODERN_SEEDED_DB=TicketDesk_retire`, a fresh copy of it
migrated through the contract stage (the contract proof); the same suite also passed 729/729 against the shared, still
expand-stage `TicketDesk` (every test database is now contracted before use). Test count 741 → 729: 25 gateway tests
(`GatewayTests`, `GatewayEdgeTests`: pass-through, Location rewrite, cookies, uploads/downloads through YARP, certificate pin,
outage, remote-auth wiring), 8 routing tests of the `Legacy` mode, groups, the legacy-cookie hold and remote authentication, 2
group/registry inventory tests, 2 legacy health-check tests and 1 Angular switch-to-legacy test left with the gateway; 6 shared
sign-in tests, 6 V2-hash tests and 1 trigger test were rewritten for the retired state; new: `RetiredRoutesTests` (15),
`SignInTests` (8), routing and inventory tests for the new app alone (static files, registry, 404s, status counts).

## Load pairs (perf-legacy-vs-new/)

```
TD_LOCK_OWNER=retire ops/dev/perf-attribution.sh --out-dir docs/evidence/retire-20261110/perf-legacy-vs-new --runs 5 \
  --snapshots baseline-s-legacy,baseline-s-search --target baseline-s-search=modern --label baseline-s-search=new-alone \
  --prepare baseline-s-search="ops/dev/td-db-app-login.sh apply && ops/dev/vm-db-contract.sh"
python3 -I ops/dev/perf-pairs.py docs/evidence/retire-20261110/perf-legacy-vs-new baseline-s-legacy baseline-s-search+new-alone --out .../pairs.md
```

Same mix (`tests/perf/ticketdesk-mix.js`, 10 operations/s, 30 s warm-up excluded, 60 s measured, 37 seeded help-desk users),
same scale-S data, each run from a fresh reset of its snapshot, A B A B ... Legacy: IIS in the lab VM (4 vCPU, 8 GB, KVM) on
`baseline-s-legacy`, k6 → https://localhost:8443. New app alone: `TicketDesk.Web` Release from this branch on the host, pinned to
4 cores (`taskset -c 24-27`, as many as the VM has vCPUs), started after the reset of `baseline-s-search` and the prepare step
(app login, contract stage on the VM's database), using the VM's SQL Express as `td_modern_app` (the same database engine and
data as the legacy app), local attachment folder, every page as its server page (the mix parses server HTML), k6 →
https://localhost:5483. Host load average during the runs 2.95–4.92 (shared host, other projects running). The VM lock was held
05:00–05:20Z; the VM was left reset to `baseline-s-search`.
