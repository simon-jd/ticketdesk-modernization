# Evidence: AKS round, Phase A (2026-11-11, local; no Azure resource created)

| Path | What | Command |
|---|---|---|
| `prices/` | Retail Prices API, swedencentral (+ Global meters), raw + `prices.tsv` (`raw/monitor-alerts.json.xz`: > 512 KB) | `python3 -I scripts/azure_prices.py docs/evidence/aks-20261111/prices swedencentral` |
| `estimate.txt` | session estimate and the alternatives | `python3 -I ops/azure/aks_estimate.py docs/evidence/aks-20261111/prices/prices.tsv` |
| `plan-cutover.txt`, `plan-aks.txt` | resource addresses + summary of the plans from the empty shared state | `make azure-plan STAGE=cutover|aks AZ_OWNER=agent-aks AZ_EXTRA_ARGS=-no-color` |
| `provider-registrations.txt` | resource providers after the first plan (the plan registered ServiceBus and AlertsManagement) | `az provider show` |
| `legacy-package/` | legacy build b4c1428 (key ring wrap), binding redirects, local VM deploy + smoke + reset | `ops/onprem/legacy-artifact.sh build`, `ops/dev/check-binding-redirects.cs`, `logs/aks/legacy-vm-check.sh` |
| `kind/` | the chart on kind against td-sql: deploy, smoke, TLS rotation, egress fault, contract job, gateway CA trust | `ops/k8s/kind.sh up|db|images|secrets|deploy|smoke|rotate-cert|netpol-drill|legacy-tls`, `ops/k8s/app_smoke.py` |
| `session-dry-run.txt` | the session's steps and local checks | `ops/azure/aks.sh session --dry-run` |
| `ado-cd/` | v2 through the CD pipeline: AKS RBAC Writer/Admin definitions vs the chart's kinds (`role/coverage.txt`), template equivalence of `aks.sh` and `cd-aks.sh` (`template/`), VM guard with stub az (`vm-guard/`), fallback on kind with the real v2 step and stub queue-cd (`fallback-kind/`) | `ops/azure/aks-cd-check.sh roles|template|vm-guard|fallback DIR` |
| `ado-cd/deadlock/` | the HTTP 500 of case-ok: deadlock graphs from system_health, the repeat run before and after the fix (td-db V0013, aks-v2b) | `TAGX=aks-v2|aks-v2b ops/k8s/repro-list-deadlock.sh` (kind, probe, 20 helm upgrades), `sys.fn_xe_file_target_read_file` |
| `deadline/` | the session deadline with stubs: the whole session, the deadline passing in the CD approval wait (`session-cd-wait.txt`); a hung step stopped by the watchdog, retire-vm still run (`session-hung-step.txt`); the real restore drill (PITR not restorable, restore hanging) and alert drill (never fires) at the deadline; the destroy ran every time | `TD_SESSION_DEADLINE_OVERRIDE` etc. inside `ops/azure/aks-cd-check.sh deadline DIR` |
