# azure-sql, cycles R2-R4 (2026-11-10) — rebuild from zero, measured cutover, golden, latency, rotation

Driver `ops/azure/replatform.sh`; R3 and R4 ran unattended as `replatform.sh session` (its trap destroys at the end).

| Path | What |
|---|---|
| `bacpac-local/` | `replatform.sh bacpac`: shared baseline .bak -> BACPAC on td-sql (Windows users stripped), 3,637,947 bytes in 7 s |
| `rebuild-20261110T005724Z/` | R2 rebuild: app 500 at start (EF 6.1 `Database.Exists()` probes master; fixed in 52fdc17) |
| `reaper-catch-r2.txt` | R2 ended by the reaper; state reconcile (this apply also reverted ws/identity's Entra change: see the handoff) |
| `plan-none-after-merge.txt` | after merging modernize (1194e3b): stage none "No changes", replatform plan 52 to add, nothing in module.entra |
| `rebuild-20261110T073541Z/` | R3 rebuild: apply 427 s, provisioning up to deploy (warm-up 200), sign-in 500 (Identity 2 V1-schema check; fixed in 80690ee) |
| `rebuild-repair/` | 80690ee staged (35 s), deploy + verify on Azure SQL (67 s), smoke 9/9 (16 s) |
| `session-20261110T075427Z/session.log` | R3 session steps and times |
| `latency-express/`, `latency-azure/` | k6 mix on the same VM, SQL Express vs Azure SQL |
| `cutover-rehearsal-20261110T080807Z/` | rehearsal: switched, write freeze 75.4 s, 0/190 reads failed, 20/20 tables match |
| `cutover-measured-20261110T081515Z/` | **measured cutover**: `summary.txt`, `marks.json` (VM + lab-host marks), `probe-summary.json` (phases), `probe.jsonl.xz`, `reconcile.json`, `window-transcript.log`, `smoke.txt` |
| `plan-tag/` | usp_TicketList tag=printer on Azure SQL: 6 actual plans (`.sqlplan`), `plan-tag.json` (0 forced plans, 0 Query Store hints) |
| `fresh-azure-*/`, `golden-azure/` | baseline BACPAC import + migrate on Azure SQL; golden 259/262 (G6 x3) |
| `cert-rotation/` | R3: failed (empty PFX from a pipe; fixed); R4: see below |
| `db-reset-*.txt` | free-offer database replaced between runs (~110 s each) |
| `destroy-20261110T083056Z.txt` | R3 destroy by the session trap: 220 s |
