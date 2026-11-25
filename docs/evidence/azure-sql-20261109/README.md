# azure-sql, cycle R1 (2026-11-09 02:20-11:03 UTC) — re-platform stage, SQL Express baseline, cutover rehearsals

Driver: `ops/azure/replatform.sh` (sources `ops/azure/lift.sh`); VM steps: `ops/azure/Invoke-TdAzureProvision.ps1`.
Every `provision-<runid>.{txt,json,log}` is one Run Command (command line in `.txt`, transcript in `.log`).

| Path | What |
|---|---|
| `apply-r1.txt` | `make azure-up-replatform` from nothing: 52 resources, 430 s |
| `apply-r1-ttl-extend.txt` | ttl extended in place (99 s) |
| `provision-20261109T023514Z.*` | base provisioning: fetch 6.9, prereqs 188.5, SQL Express 306.4, CU 185.2, site 6.5, sql tools 11.4, restore 7.3 s; deploy failed (Key Vault answers HEAD with 405; fixed) |
| `provision-20261109T062549Z.*` | deploy (attachments: Azure Blob, account SAS from Key Vault; remote-app key from Key Vault), `files-check`: Test-TdBlobStore 12/12 against the Azure attachments account |
| `smoke-express-first.*` | smoke 9/9 on SQL Express |
| `fresh-*/` | fresh states: shared baseline restored, attachments emptied, Lucene index rebuilt |
| `golden-express/` | golden replay against the Azure VM on SQL Express (current build 9fd2eed): 262 compared, 259 matched, 3 = G6 (host name) |
| `latency-express/` | k6 mix (5 ops/s, 20 s warm-up, 60 s) from lab-host: p50 240.5, p95 1,313.4 ms, 559 requests, 0 errors; TCP connect ~0.2 s |
| `cutover-rehearsal-*` | four rehearsals; all with writes frozen by DENY and reads served: (1) export copy could not drop the app's Windows user (it owns the schema EF created) — fixed; rollback redeployed and reset 3 reads — fixed (rollback = REVOKE only); (2) target paused (serverless), retry note leaked into a return value — fixed, `azsql-wake` added; (3, 4) wake 0.2-0.3 s, freeze 0.0-0.1 s, copy 7.7-7.8 s, export 21.2-21.9 s, import 36.4-39.0 s, contained user 0.2-0.7 s, **reconcile 20/20 tables MATCH** (3.7-4.2 s), **0 failed reads** of 220/224; the switch failed (File.Replace got "" for null; and (4) ran the old scripts: the window had no fetch) — both fixed |
| `db-reset-*.txt` | free-offer database replaced (`terraform apply -replace`, 109 s) |
| `tag-hint-tdsql/` | the tag-count hint vs the forced plan on td-sql TicketDeskL (`summary.txt`) |
| `prices/` | Retail Prices API, swedencentral, 2026-11-09 |
| `reaper-start.txt`, `reaper-catch-r1.txt` | reaper at start; the reaper's delete at ttl (10:54:18 -> 11:00-11:03) and the state reconcile |
