# infra — Azure lab (Terraform)

Lab region **swedencentral** (since 2026-11-08; westus could not host Azure SQL or any x64 VM for this
subscription — `docs/dev/AZURE-BUDGET.md`). One root (`terraform/envs/lab`) and eight modules (`terraform/modules/*`). Stage flags decide what exists; each
`envs/lab/stages/<stage>.tfvars` describes the **whole** desired state, so going up or down a stage is one apply.

| Stage file | enable_foundation | enable_sql | enable_vm | enable_aks | enable_entra | Est. $/h |
|---|---|---|---|---|---|---|
| `none` | – | – | – | – | ✓ | 0 |
| `foundation` | ✓ | ✓ | – | – | ✓ | 0.0232 |
| `lift` | ✓ | – | ✓ | – | ✓ | 0.1066 |
| `replatform` | ✓ | ✓ | ✓ | – | ✓ | 0.1173 |
| `aks` | ✓ | ✓ | – | ✓ | ✓ | 0.2630 |
| `cutover` | ✓ | ✓ | ✓ | ✓ | ✓ | 0.3571 |

**Status (2026-11-08):** in westus the foundation was proven only without SQL (`ProvisioningDisabled`); the lab
then moved to swedencentral, where the full foundation stage (incl. the SQL free DB behind its private endpoint) is
applied and destroyed from zero — timings in `docs/RESULTS.md`, evidence in
`docs/evidence/azure-foundation-20261108/`. The `lift` stage (legacy app on the Windows VM) is applied, provisioned
and destroyed by Round 1 azure-lift (`docs/dev/azure-lift.md`, evidence `docs/evidence/azure-lift-20261108/`); since Round 2
it has no Azure SQL database (the legacy VM never used it). The `replatform` stage (Round 2 azure-sql) is the VM plus
Azure SQL: the legacy app moves from SQL Express to the free-offer database through the private endpoint with the VM's
managed identity (`docs/sql/azure-sql-move.md`, `ops/azure/replatform.sh`, evidence `docs/evidence/azure-sql-2026110*/`).
The AKS stages are planned, not applied.

Modules:

- `network` — VNet `10.42.0.0/16`, subnets `snet-vm` (/24), `snet-pe` (/24), `snet-aks` (/22, Azure CNI overlay),
  one NSG per subnet. Inbound from the internet is denied (explicit `DenyInternetInbound` at 4000 on top of the
  defaults); the VM subnet allows only 443 from `admin_source_cidrs` (home public IP) — no RDP/WinRM/SSH, the VM is
  administered through Run Command; the AKS subnet opens 80/443 only for `aks_ingress_source_cidrs` (empty until
  cutover) and allows the load balancer's health probes (`AllowAzureLoadBalancerInbound`, source `AzureLoadBalancer`,
  priority 3900): `DenyInternetInbound` matches the probe source 168.63.129.16, so the default rule 65001 is never
  reached (session of 2026-11-12: the probe failed until the rule was added). `snet-vm` has service endpoints for Microsoft.KeyVault and Microsoft.Storage (free). No default outbound
  access on subnets: the VM uses its own public IP, AKS its load balancer (no NAT Gateway).
- `keyvault` — RBAC authorization, soft delete 7 days, no purge protection; firewall default deny + admin IPs +
  `snet-vm` (service endpoint); signed-in user gets Key Vault Administrator; optional private endpoint (off).
- `monitoring` — Log Analytics (PerGB2018, 30 days, daily cap 0.1 GB), workspace-based App Insights, e-mail
  action group.
- `sql` — Entra-only logical server, free-offer serverless DB (azapi, AutoPause on exhaustion), public access off,
  private endpoint + `privatelink.database.windows.net`. See `terraform/modules/sql/README.md` (open connections
  prevent auto-pause). Entra admin: the signed-in user, or with the VM (`replatform`) the user-assigned deploy
  identity `id-tdlab-sqldeploy`, which is attached to the VM (the server is reachable only inside the VNet, so the
  BACPAC import, contained users, `td-db migrate` and `reconcile` run there). The legacy app's database user is the
  VM's system-assigned identity, created `WITH SID = <client id>, TYPE = E` (a service-principal admin cannot look
  names up in Entra without Directory Readers on the server identity).
- `vm-windows` — Windows Server 2025 Datacenter Core smalldisk Gen2, 32 GB Standard_LRS, Trusted Launch,
  system-assigned identity with Key Vault Secrets User, random admin password in Key Vault (nobody signs in with
  it), public IP with DNS label `tdlab-legacy-<suffix>`, `patch_mode = Manual` (no update reboots in measurement
  windows), Defender Antivirus on. Size Standard_B2as_v2 (2 vCPU, 8 GiB). Provisioning is not in Terraform:
  `ops/azure/lift.sh` runs `ops/windows/*.ps1` through Run Command after the apply.
- `files` (stages with the VM) — the `storage` module again as `st<name>files<suffix>`, container
  `ticketdesk-attachments` for the legacy app's attachments. The legacy Blob SDK (WindowsAzure.Storage 8.2.0) knows only
  keys and SAS and touches container-level APIs on every path, so this account allows shared keys; the app gets an
  account SAS (blob service, container + object, read/add/create/write/delete/list, 8 days, re-issued weekly through
  `time_rotating`) as the Key Vault secret `td-attachments-connection`, written into Web.config by the deploy step
  through the managed identity. The ops account stays Entra-only. `td-remoteapp-api-key` (random UUID) is in Key
  Vault too.
- `storage` (lift stage) — Standard_LRS StorageV2 account with private containers `artifacts`, `backups`, `logs`;
  shared keys off (Entra only), firewall default deny + admin IPs + `snet-vm`; the signed-in user gets Storage
  Blob Data Contributor; the root module gives the VM identity Reader on `artifacts` and Contributor on `backups`
  and `logs`. The lift stage also stores `td-sql-sa-password` (generated), `td-app-admin-password` and the
  `td-iis-legacy` certificate (lab CA, `make azure-lift-cert`) in Key Vault.
- `aks` — Free tier, 2 × Standard_B2as_v2 (x64) system pool, Azure CNI overlay + Cilium, Entra ID + Azure RBAC
  (local accounts off), API server limited to the admin IPs, OIDC issuer + workload identity, Key Vault CSI
  driver, Container Insights via managed identity + data collection rule. Planned only in Round 1.
- `entra` — app registration `ticketdesk-lab-web` (web redirect URIs on localhost for now), app roles
  `TdAdministrators`, `TdHelpDeskUsers`, `TdInternalUsers`, `TdPendingUsers` with fixed IDs, service principal with
  assignment required, signed-in user assigned to `TdAdministrators`. Lives in the tenant, kept across stages.

Providers: Terraform ≥ 1.17, azurerm ~> 5.13, azuread ~> 3.10, azapi ~> 2.13 (only for the free-offer database),
random, time. `.terraform.lock.hcl` is committed. The azurerm provider registers only the resource providers this
lab uses (`resource_provider_registrations = "none"` + an explicit list; Microsoft.Sql was registered by it on
2026-11-08) and uses Entra auth for storage with no storage data-plane calls (`storage_use_azuread`,
`data_plane_available = false`). VM force delete is not used: it left the OS disk behind on 2026-11-08.

## State: local, outside the repo

State is a **local backend** at `$TD_W/.tfstate/lab/terraform.tfstate` (directory 0700, files 0600), passed with
`-backend-config=path=…` by `make azure-init`. Reasons: one operator box (lab-host), no paid or
pre-existing storage account to bootstrap, and the state holds secrets (the VM admin password, the App Insights
connection string) that must never land in git. All worktrees share that one state file, so only one agent
applies at a time. Moving to an `azurerm` backend (storage account with Entra auth) is a later decision if
Azure DevOps pipelines need shared state.

## Use

```bash
source $TD_W/.tools/env.sh
make azure-check                   # fmt -check + validate
make azure-plan STAGE=foundation
make azure-up-foundation           # ledger check first; ttl tag = now + TTL_HOURS (4)
make azure-down-foundation         # back to "none" (Entra app only)
make azure-down                    # emergency: delete the RG, refresh state
scripts/azure_verify.sh            # check the live resources with az (names/settings only)
make azure-up-foundation AZ_EXTRA_ARGS="-var kv_private_endpoint=true"  # one-off override example
make azure-lift-up TTL_HOURS=4     # lift stage (cert + apply, owner agent-azure-lift); then docs/dev/azure-lift.md
```

The lift stage reads two secrets at apply time from `$TD_W/.secrets` (`legacy-lab.env` TD_ADMIN_PASSWORD,
`legacy-lab-cert.env` TD_CERT_PFX_PASSWORD) and the PFX `legacy-lab-ca/azure-lift.pfx`: `scripts/tf_lift_env.sh`
exports them as `TF_VAR_*` inside the make recipe (never echoed). `AZ_OWNER=agent-<name>` sets the owner tag.

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

Machine-specific inputs live in `$TD_W/.secrets/azure-lab.tfvars` (0600, not committed): `admin_source_cidrs`
(home public IP /32) and `alert_email`. Subscription and tenant come from `az account show`. Budget rules,
ledger and quota findings: `docs/dev/AZURE-BUDGET.md`, `docs/dev/azure-spend.md`.
