# Evidence: Round 1 azure-lift (2026-11-08, swedencentral)

Lift & shift of legacy TicketDesk onto an Azure Windows Server 2025 VM (`docs/dev/azure-lift.md`). Commands from
`ops/azure/lift.sh` on lab-host; times are UTC. No secret values in these files (the admin e-mail is written as
`<admin-email>`; passwords never left Key Vault / `$TD_W/.secrets`).

| Files | What |
|---|---|
| `prices/`, `sku-fit-swedencentral.txt`, `quota-swedencentral.txt` | unit prices (Retail Prices API, incl. blob storage), deployable sizes, vCPU quota before the first apply |
| `plan-lift.txt` | `make azure-plan STAGE=lift`: 43 to add, est. $0.1173/h |
| `certificate.txt`, `artifact-publish.txt` | the VM's lab-CA certificate (public part) and the publish package (rev `f510384`, 840 files, zip SHA-256) |
| `apply-l1.txt`, `verify-l1.txt`, `port-check.txt` | cycle L1 apply (425 s), `scripts/azure_verify.sh` (0 failed), reachable ports from home (443 only; the curl line prints the scheme twice, the result is HTTP 200 with verify=0) |
| `provision-<run>.txt/.json/.log` | every Run Command: lift.sh output, raw az JSON, the VM's transcript (SQLUPDATE, ROWCOUNTS, TDVERIFY, TDTIMINGS lines) |
| `smoke-*.json/.txt` | `legacy_smoke.py smoke` runs against the Azure VM (all 9/9 except `restore-fix-test`, which failed on the TLS key loss) |
| `replay-azure/` | golden replay against the Azure VM: 262 compared, 259 matched, 3 mismatched (`compare/report.md`), candidate as `.jsonl.xz` |
| `backup-drill/` + `provision-20261108T1616*/1617*/1618*` | backup → blob, write after it, loss, restore from blob, smoke (`marks.json`) |
| `probe-readonly-test/` | probe reads and sign-in against the Azure VM while its database was READ_ONLY: 30 reads, 0 failed |
| `cutover-20261108T162218Z/` | first cutover attempt: restore of the frozen backup failed (read-only), not usable |
| `cutover-20261108T212041Z/` | the cutover: `marks.json` (timeline, result switched), `probe.jsonl` + `probe-summary.json`, `restore.txt`, `source-rowcounts.txt`, `smoke.txt` |
| `rebuild-20261108T205254Z/` | rebuild from zero, cycle L2: apply, stage, provision, smoke, `rebuild.txt` (totals) |
| `destroy-l1.txt`, `destroy-l1b.txt`, `ttl-extend.txt` | L1 destroy (failed on the leftover OS disk, then 44 s), the ttl extension apply |
| `reaper-catch.txt`, `state-reconcile.txt` | the scheduled reaper deleting cycle L2 at ttl + 7 min; state refresh, stage `none`, vault purge, end state |
