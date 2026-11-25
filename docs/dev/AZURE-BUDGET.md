# Azure budget and cost controls — TicketDesk lab

## Cap and approval

- **Project budget: $15** on the "Azure for Students" subscription. Working cap: the ledger's running estimate
  must stay **≤ $13.50** (the remaining $1.50 is margin for estimation error).
- **User approval (2026-11-07, in chat), one time:** "`terraform apply` inside resource group
  `rg-ticketdesk-lab` needs no further per-apply approval as long as the ledger's running estimate stays
  ≤ $13.50. Anything beyond that: stop and report."
- The consumption/cost APIs return no cost on this subscription. The ledger `docs/dev/azure-spend.md` is the
  control; `make azure-ledger` (run automatically by every `make azure-up-*`) refuses when the running estimate
  is over $13.50.

## Rules (from `plan/AGENT-RULES.md`, Azure section)

- Region **swedencentral** only (since 2026-11-08; see "Why swedencentral" below). Everything in **`rg-ticketdesk-lab`**, created by Terraform (`infra/terraform/envs/lab`).
- Tags on every resource: `project=ticketdesk`, `owner=agent-<name>`, `ttl=<UTC expiry>`,
  `est_usd_per_hour=<estimate>` (Terraform sets them; `make azure-up-*` writes `ttl` = now + `TTL_HOURS`, default 4).
- Before creating anything: add ledger rows (resource, SKU, region, unit price from the Retail Prices API,
  start, planned end, estimate, actual delete time) and check project total + new estimate ≤ $13.50.
- Anything over **$0.02/h** is deleted in the same session — nothing billable runs overnight.
- OS disks Standard_LRS, 30–32 GB. Images go to GHCR or are built on the cluster (no paid ACR).
- Banned: NAT Gateway, DNS Private Resolver, Azure Firewall, Application Gateway, Front Door, Bastion, Event Hubs,
  Sentinel/Defender paid plans, Premium/Ultra disks, GPU SKUs, paid Marketplace images, reservations/savings
  plans, AKS Standard/Premium tier; never touch `rg-mlinfra-westus` or `NetworkWatcherRG`.
- A first VNet in a region can make Azure auto-create `NetworkWatcher_<region>` inside `NetworkWatcherRG`: free and
  expected; leave it (and that RG) alone.
- Entra ID: at most 2 app registrations for this project, named `ticketdesk-lab-*`, recorded in the ledger,
  deleted at the end. No security groups (use app roles).
- No secrets in output, the repo or reports: Key Vault or `$TD_W/.secrets/`.

## Procedure for every Azure step

1. `scripts/azure_sku_fit.py swedencentral 4` before any compute (see "Quota" below), and `make azure-plan STAGE=<stage>`.
2. Add the rows to `docs/dev/azure-spend.md`; `make azure-ledger` must say OK.
3. Install the scheduled reaper again (below; it was removed at the end of the project, 2026-11-12), `make azure-reap`,
   then `make azure-up-<stage> TTL_HOURS=<session length>`.
4. Verify with `az`; record evidence under `docs/evidence/<topic>-<date>/`.
5. `make azure-down-<stage>` (or `make azure-down` to delete the whole RG), fill in "Actual delete",
   `make azure-reap` and `make azure-status` (end of session: the RG must be gone or hold only $0 items).

Targets (`infra/azure.mk`, included by the root `Makefile`):

| Target | Does |
|---|---|
| `make azure-check` | `terraform fmt -check` + `validate` |
| `make azure-plan STAGE=<none\|foundation\|lift\|aks\|cutover>` | plan a stage |
| `make azure-up-<stage>` | ledger check, then apply the stage (prints elapsed seconds) |
| `make azure-down-<stage>` | back to the previous stage: foundation→none, lift→foundation, aks→foundation, cutover→aks |
| `make azure-down` | delete `rg-ticketdesk-lab` with `az group delete`, then refresh Terraform state (Entra app stays) |
| `make azure-reap` / `azure-reap-dry` | `scripts/azure_reaper.sh`: delete expired-`ttl` resources in this RG only (the RG itself when its ttl passed) |
| `make azure-status` | resources in the RG with ttl/est tags, and the ledger total |
| `make azure-destroy-all` | end of project: destroy everything including the Entra app |
| `make azure-prices` | re-fetch unit prices into `docs/evidence/azure-prices-<date>/` |

## Reaper schedule and ttl

The reaper **was scheduled** from 2026-11-08 (approved by the user, installed by the coordinator) until the end of the
project on 2026-11-12, in the crontab of `labuser` on lab-host. Install it again before another Azure session:

```
*/15 * * * * PATH=/usr/bin:/bin flock -n /tmp/td-azure-reaper.lock $HOME/Desktop/ticketdesk-modernization/ticketdesk/scripts/azure_reaper.sh >> $HOME/Desktop/ticketdesk-modernization/logs/azure-reaper.log 2>&1 # ticketdesk-lab reaper
```

- It runs `scripts/azure_reaper.sh` from the `modernize` checkout (`$W/ticketdesk`). Log: `$W/logs/azure-reaper.log`.
- Remove it: `crontab -l | grep -v 'ticketdesk-lab reaper' | crontab -`
- When the resource group's `ttl` tag passes, the reaper deletes **the whole group**. `make azure-up-<stage>` sets
  `ttl` = now + `TTL_HOURS` (default 4): pick `TTL_HOURS` to cover the session, and extend it before a long run by
  re-applying the same stage with a larger value (`make azure-up-<stage> TTL_HOURS=8`; tags update in place).
- After the reaper deletes the group, Terraform state still lists the resources: run `make azure-down` (refreshes
  state) or the next `make azure-up-<stage>` (recreates them).

## Why swedencentral (decided 2026-11-08, `plan/DECISIONS.md`)

The subscription's region policy (deny effect) allows only westus, norwayeast, francecentral, denmarkeast and
swedencentral. In **westus** (the first lab region) Round 1 found:
- Azure SQL: `ProvisioningDisabled` ("Provisioning is restricted in this region") — the first foundation apply failed
  on the SQL server (`docs/evidence/azure-foundation-20261107/apply-cycle1.txt`, `sql-capabilities.txt`).
- VMs: every x64 B-series size (and A2_v2, D2s_v3, …) `NotAvailableForSubscription`; the x64 families that were
  offered had quota 0; only Arm64 Bpsv2 fit (`.../sku-fit-westus.txt`, `quota-westus.txt`).

That westus evidence stays in the repo as the record. In **swedencentral** Azure SQL is `Available` (GP_S_Gen5_2 with
the free-offer exhaustion behaviors AutoPause/BillOverUsage) and the x64 B sizes are deployable.

## Quota and which sizes fit (swedencentral, checked 2026-11-08T07:28Z)

Evidence: `docs/evidence/azure-foundation-20261108/quota-swedencentral.txt` (`az vm list-usage -l swedencentral`),
`.../sku-fit-swedencentral.txt` (`scripts/azure_sku_fit.py swedencentral 4`: deployable = not restricted and family
quota > 0), `.../region-checks.txt` (image, AKS versions, SQL capability).

- Total regional vCPUs **6**; Basv2 10, Bsv2 10, Bpsv2 10, BS 4 (BS sizes B2s/B2ms are not offered here).
- Deployable ≤ 4 vCPU x64: Basv2 (B2als_v2, **B2as_v2**, B4als_v2, B4as_v2, …) and Bsv2 (B2ls_v2, B2s_v2, …).
- Lab size, x64 only (one linux/amd64 image for local docker and AKS): **Standard_B2as_v2** (2 vCPU, 8 GiB) for the
  Windows VM ($0.087/h Windows) and the AKS nodes ($0.0778/h Linux).

| Need | Size | vCPU use (cap 6) |
|---|---|---|
| Windows VM (lift & shift) | 1 × Standard_B2as_v2, WS2025 Datacenter Core smalldisk Gen2 (image `26100.33674.261024` available) | 2 |
| AKS node pool | 2 × Standard_B2as_v2 (module default) | 4 |
| Cutover (VM + AKS) | VM + 2 nodes | 6 = full: no AKS upgrade surge during cutover (upgrade when the VM is gone) |

AKS in swedencentral offers Kubernetes 1.31–1.37 (1.37 newest, no preview; `region-checks.txt`); the module leaves
`kubernetes_version` unset so AKS picks its default.

## Stage cost (hourly, as configured)

From Terraform output `est_usd_per_hour` (`make azure-plan STAGE=…`, evidence
`docs/evidence/azure-foundation-20261108/plan-*.txt`): foundation **$0.0232/h**, lift **$0.1173/h**, aks
**$0.2630/h** (2 nodes, incl. the $0.05/h "FreeTierInfrastructureCost" meter), cutover **$0.3571/h**; the ledger also
counts each private DNS zone as a full month ($0.50) per creation. Unit prices: `docs/dev/azure-spend.md`.
