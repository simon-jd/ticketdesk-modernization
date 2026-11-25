# Golden characterization recording (legacy TicketDesk 2.5)

`legacy-golden.jsonl.xz` is what the legacy app answered to the 15 scripted journeys of `td-replay`
(`tools/Replay/TicketDesk.Replay/TicketDeskJourneys.cs`): 262 HTTP exchanges (212 steps + 50 followed redirects),
recorded 2026-11-09 01:51Z against the lab VM reset to snapshot `baseline-s` in its round-2 **shared** state (legacy-adapters): the net48 build with the System.Web adapters, the expanded schema and attachments in the lab blob store. Each line is one exchange (request, response,
journey variables) and hop 0 of every step carries its step definition, so the file replays without the journey code.

| | |
|---|---|
| Data | legacy DB `ticketdesk-clean.bak` seeded with td-seed scale S, seed 42 (10,000 tickets, 251 users, 5 projects), then `td-db migrate` (expand schema: `Tickets.AssignedDate` + trigger, indexes, procedures), Lucene index rebuilt; attachments in an empty Azurite container `ticketdesk-attachments`: `ops/onprem/baseline/baseline-s.sh install` |
| App | legacy TicketDesk (net48 + System.Web adapters, package `td-legacy-publish-b4edcc3.zip`) on IIS 10 + SQL Server 2022 Express (Windows Server 2025 VM, `ops/onprem/vm/vmctl.sh`), https://localhost:8443/; deployed with `ops/windows/Deploy-TdApp.ps1` (`customErrors` RemoteOnly; remote-auth key; `AzureStorage` → Azurite 3.37.0 at 10.0.2.2) |
| Recorder | `td-replay record`, journeys as of `1cf6da0`, comparer as of `7c6ae12` (unchanged since the round-1 recording) |
| Stability | run A vs run B, each from a fresh `baseline-s` reset: 262 compared, 262 matched, 0 mismatches, with the built-in normalizers only (no ignore rules) — `docs/evidence/legacy-adapters-20261109/replay-legacy/` |
| Through the gateway | replayed through `TicketDesk.Web` (YARP fallback, nothing migrated): 262 compared, 259 matched, 3 expected differences (G6: the push-notification settings page shows the gateway's URL, since the legacy app honours X-Forwarded-Host) — `docs/evidence/legacy-adapters-20261109/replay-gateway/` |
| History | 2026-11-08 07:28Z with `customErrors` Off; 16:03Z after S9 was closed (old vs new 260/262: the two legacy 500 pages, `docs/evidence/baseline-20261108/s9/`); 2026-11-09 01:51Z on the shared state. 16:03Z recording vs this one: 262 compared, 261 matched; the one difference is the tag autocomplete order (former D1: the legacy query has no ORDER BY and the expand index changes the plan) — `docs/evidence/legacy-adapters-20261109/replay-legacy/compare-old-golden-vs-new/`. Replaying the older recording against the shared state also showed 1 difference (D1), with the disk and the blob store alike |
| Round-1 state | snapshot `baseline-s-legacy` (4.6.2 build, EF6 schema, disk store) is kept; it is the state the 16:03Z recording was made on (`git show e5cd08f:tests/characterization/golden/legacy-golden.jsonl.xz`; A vs B 262/262, `docs/evidence/baseline-20261108/replay-legacy/`) |
| Secrets | none: passwords and the lab admin's e-mail/name are written as `{secret:NAME}`; cookie values are not recorded |

## Replay against a target and compare

Every run starts from `baseline-s` (the journeys create tickets, a user and a project; ticket ids must line up); `baseline-s.sh reset` also empties the lab blob store. Through the gateway G6 (above) is expected.
Take the shared VM lock first (`ops/onprem/vm/vmlock.sh acquire <you> <purpose> <minutes>`).

```bash
. $W/.tools/env.sh
# through the gateway (starts TicketDesk.Web on https://localhost:5443, resets the VM, replays, compares)
TD_LOCK_OWNER=<you> ops/dev/replay-gateway.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir out/gw

# any other target
TD_LOCK_OWNER=<you> ops/onprem/baseline/baseline-s.sh reset
xz -dc tests/characterization/golden/legacy-golden.jsonl.xz > /tmp/golden.jsonl
td-replay replay --from /tmp/golden.jsonl --target https://HOST/ --insecure \
  --secrets-file $W/.secrets/legacy-lab.env --secrets-file $W/.secrets/td-seed.env \
  --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes docs/migration/routes.csv --out out/candidate.jsonl
td-replay compare --baseline /tmp/golden.jsonl --candidate out/candidate.jsonl --out-dir out/compare
```
(`td-replay` = `tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay`.) Differences that a migrated route
is meant to have are listed in `docs/migration/expected-differences.md`; everything else in `out/compare/report.md` is a
regression until explained.

## Re-record

Only when the journeys change or the legacy app is intentionally changed (never to make a migrated route pass):

```bash
TD_LOCK_OWNER=<you> ops/dev/record-legacy.sh --out-dir docs/evidence/baseline-<date>/replay-legacy   # reset, run A, reset, run B, compare
xz -9 -c docs/evidence/baseline-<date>/replay-legacy/legacy-run1.jsonl > tests/characterization/golden/legacy-golden.jsonl.xz
```
Accept the new golden only when run A vs run B shows 0 mismatches. Add a normalizer or `--ignore-selector` only for
content that is genuinely volatile, with a comment saying why.

## Route coverage

87 of the 89 rows in `docs/migration/routes.csv` are routable (2 are child actions). **82 of 87 are exercised.**
Not covered, on purpose:

| Route | Why |
|---|---|
| `admin/data-management/remove-demo-data`, `create-demo-data` | replace the baseline data set and unload the app domain |
| `POST first-run-setup/create-database` | S1; the wizard is disabled on the lab host (`SetupEnabled=false`); the GETs of the wizard are recorded (302 to `/`) |
| `admin/search-manager/remove-index`, `populate-index` | race the background indexer started by the ticket journeys: in 1 of 2 runs the populate hit a failed Lucene merge (`_l.cfs` missing) and the leftover `write.lock` then made every page return 500. `populate-index` on an idle app is run by `baseline-s.sh install` (200, ~2.5 s) |

## Legacy behaviour captured that the new app will change

- `GET /file/get-attachments-info?tempId=…` returns 500 (route `GetPendingFile` does not exist): the generic MVC error
  view, served as `application/json` because the action had already set that content type.
- `GET /tickets/unassigned` as an internal (non-help-desk) user returns 500 (generic error page).
- `GET /emailtest/index/1` renders a ticket e-mail anonymously (S2).
- `POST /user/register` creates and signs in a user although registration is disabled (S4).
- `ReOpen`'s "assign to me" checkbox is named `reactivate`, so the controller never sees it.

Each of these has an entry in `docs/migration/expected-differences.md` (L1–L4, S2, S4).
