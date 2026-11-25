# Azure spend ledger — TicketDesk lab

The consumption/cost APIs return no cost on the Azure for Students subscription, so **this ledger is the cost
control**. Rules: `docs/dev/AZURE-BUDGET.md`. Before creating anything: add the rows below, then
`make azure-ledger` (fails when the running estimate exceeds **$13.50**). After deleting: fill in
"Actual delete". Estimates are conservative (monthly-billed items are counted as a full month per creation;
free allowances are counted only where they are certain).

Running estimate: `python3 scripts/azure_ledger.py total` (sums the Estimate column of the table below).

## Ledger

<!-- ledger:begin -->
| # | Resource | SKU / meter | Region | Unit price (Retail Prices API) | Start (UTC) | Planned end (UTC) | Estimate USD | Actual delete (UTC) | Notes |
|---|---|---|---|---|---|---|---|---|---|
| F1.1 | `pe-sql-tdlab` (private endpoint) | Standard Private Endpoint | westus (Global meter) | $0.01/h + $0.01/GB processed | — | — | 0.00 | — | **never created**: SQL server failed first (cycle 1) |
| F1.2 | `privatelink.database.windows.net` | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-08 00:34 | 01:34 | 0.50 | 2026-11-08 01:01 | counted as a full month |
| F1.3 | `log-tdlab-westus` + `appi-tdlab-westus` | PerGB2018, daily cap 0.1 GB | westus | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-08 00:34 | 01:34 | 0.30 | 2026-11-08 01:01 | worst case: one full day at the cap beyond the free 5 GB |
| F1.4 | `kv-tdlab-36d2d4` | Key Vault Standard | westus | $0.03 per 10K operations | 2026-11-08 00:34 | 01:34 | 0.03 | 2026-11-08 01:01 (purged) | one 10K block |
| F1.5 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer, AutoPause | westus | $0 within the free limit | — | — | 0.00 | — | **create failed: `ProvisioningDisabled` in westus** |
| F1.6 | RG, VNet, 3 subnets, 3 NSGs, action group, role assignment | — | westus | no meter; action-group e-mail first 1,000/month $0 | 2026-11-08 00:34 | 01:34 | 0.00 | 2026-11-08 01:01 | |
| F1.7 | Entra app `ticketdesk-lab-web` + service principal + TdAdministrators assignment | app registration | University tenant | $0 | 2026-11-08 00:34 | project end | 0.00 | kept | app 1 of max 2; delete at project end (`make azure-destroy-all`) |
| I1.1 | Entra app `ticketdesk-lab-web`: redirect URI `https://localhost:5443/signin-oidc` + one client secret (expires 2026-12-31) | app registration change | University tenant | $0 | 2026-11-10 07:3x | project end | 0.00 | kept | round 2b identity, `ops/dev/entra-apply.sh apply` (targeted `module.entra`); secret only in `$TD_W/.secrets/entra-web.env` and the local state |
| F2.1 | `pe-kv-kv-tdlab-36d2d4` (Key Vault private endpoint + NIC, stands in for the blocked SQL one) | Standard Private Endpoint | westus (Global meter) | $0.01/h + $0.01/GB | 2026-11-08 01:03 | 02:03 | 0.02 | 2026-11-08 01:14 | cycle 2: foundation without SQL, rebuild from zero |
| F2.2 | `privatelink.vaultcore.azure.net` | Private DNS zone | global | $0.50 per zone-month | 2026-11-08 01:03 | 02:03 | 0.50 | 2026-11-08 01:14 | counted as a full month |
| F2.3 | `log-tdlab-westus` + `appi-tdlab-westus` | PerGB2018, daily cap 0.1 GB | westus | $0 first 5 GB/month, then $2.99/GB | 2026-11-08 01:03 | 02:03 | 0.30 | 2026-11-08 01:14 | recreated workspace: assume a fresh daily cap |
| F2.4 | `kv-tdlab-36d2d4` | Key Vault Standard | westus | $0.03 per 10K operations | 2026-11-08 01:03 | 02:03 | 0.03 | 2026-11-08 01:14 | |
| F2.5 | RG, VNet, subnets, NSGs, action group | — | westus | no meter | 2026-11-08 01:03 | 02:03 | 0.00 | 2026-11-08 01:14 | |
| F3.1 | `pe-kv-tdlab-36d2d4` (+ NIC) | Standard Private Endpoint | westus (Global meter) | $0.01/h + $0.01/GB | 2026-11-08 01:15 | 02:15 | 0.02 | 2026-11-08 01:25 | cycle 3: second rebuild-from-zero sample |
| F3.2 | `privatelink.vaultcore.azure.net` | Private DNS zone | global | $0.50 per zone-month | 2026-11-08 01:15 | 02:15 | 0.50 | 2026-11-08 01:25 | counted as a full month again |
| F3.3 | `log-tdlab-westus` + `appi-tdlab-westus` | PerGB2018, daily cap 0.1 GB | westus | $0 first 5 GB/month, then $2.99/GB | 2026-11-08 01:15 | 02:15 | 0.30 | 2026-11-08 01:25 | |
| F3.4 | `kv-tdlab-36d2d4` | Key Vault Standard | westus | $0.03 per 10K operations | 2026-11-08 01:15 | 02:15 | 0.03 | 2026-11-08 01:25 | |
| F3.5 | RG, VNet, subnets, NSGs, action group | — | westus | no meter | 2026-11-08 01:15 | 02:15 | 0.00 | 2026-11-08 01:25 | action group deleted 01:21 by the reaper test (backdated ttl) |
| S1.1 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-08 07:34 | 08:34 | 0.02 | 2026-11-08 07:45 | cycle S1: swedencentral foundation incl. SQL, rebuild from zero (Entra app exists) |
| S1.2 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-08 07:34 | 08:34 | 0.50 | 2026-11-08 07:45 | counted as a full month |
| S1.3 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-08 07:34 | 08:34 | 0.30 | 2026-11-08 07:45 | worst case: one full day at the cap beyond the free 5 GB |
| S1.4 | `kv-tdlab-36d2d4` (+2 secrets, role assignment) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-08 07:34 | 08:34 | 0.03 | 2026-11-08 07:45 | one 10K block |
| S1.5 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer ("2 vCore - Free"), AutoPause on exhaustion | swedencentral | $0 within the free limit (paid: $0.573934/vCore-h, $0.13685/GB-month — not billed with AutoPause) | 2026-11-08 07:34 | 08:34 | 0.00 | 2026-11-08 07:45 | |
| S1.6 | RG, VNet, 3 subnets, 3 NSGs, action group | — | swedencentral | no meter; action-group e-mail first 1,000/month $0 | 2026-11-08 07:34 | 08:34 | 0.00 | 2026-11-08 07:45 | |
| S2.1 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-08 07:46 | 08:46 | 0.08 | 2026-11-08 15:16 | cycle S2: apply failed at 07:49 (lab-host DNS lookup error while waiting for Key Vault); the session then stopped on a usage limit and nothing ran the reaper (RG ttl 11:45 passed), so everything lived **7.5 h** until the destroy at 15:16 — 7.5 h x $0.01 |
| S2.2 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-08 07:46 | 08:46 | 0.50 | 2026-11-08 15:16 | counted as a full month |
| S2.3 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-08 07:46 | 08:46 | 0.30 | 2026-11-08 15:16 | worst case: one full day at the cap beyond the free 5 GB |
| S2.4 | `kv-tdlab-36d2d4` (+2 secrets, role assignment) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-08 07:46 | 08:46 | 0.03 | 2026-11-08 15:16 | one 10K block |
| S2.5 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer ("2 vCore - Free"), AutoPause on exhaustion | swedencentral | $0 within the free limit (paid: $0.573934/vCore-h, $0.13685/GB-month — not billed with AutoPause) | 2026-11-08 07:46 | 08:46 | 0.00 | 2026-11-08 15:16 | free offer: $0 online or paused |
| S2.6 | RG, VNet, 3 subnets, 3 NSGs, action group | — | swedencentral | no meter; action-group e-mail first 1,000/month $0 | 2026-11-08 07:46 | 08:46 | 0.00 | 2026-11-08 15:16 | |
| L1.1 | `vm-tdlab-legacy` (+ NIC, managed boot diagnostics) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-08 15:50 | 20:50 | 0.44 | 2026-11-08 20:52 | lift cycle L1 (azure-lift): provisioning, backup drill, cutover rehearsal; 5 h planned |
| L1.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-08 15:50 | 20:50 | 0.01 | 2026-11-08 20:52 | first destroy left the OS disk behind (VM force delete): deleted with `az disk delete` 20:51, force delete removed from the provider config |
| L1.3 | `pip-tdlab-legacy` (DNS label `tdlab-legacy-36d2d4`) | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-08 15:50 | 20:50 | 0.03 | 2026-11-08 20:52 | |
| L1.4 | `sttdlab36d2d4` (containers artifacts, backups, logs) | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month stored; writes $0.05/10K, reads $0.004/10K | 2026-11-08 15:50 | 20:50 | 0.01 | 2026-11-08 20:52 | under 1 GB and under 10K operations; shared keys off |
| L1.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-08 15:50 | 20:50 | 0.05 | 2026-11-08 20:52 | part of the lift stage (foundation SQL), unused by the legacy VM |
| L1.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-08 15:50 | 20:50 | 0.50 | 2026-11-08 20:52 | counted as a full month |
| L1.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-08 15:50 | 20:50 | 0.30 | 2026-11-08 20:52 | worst case: one full day at the cap beyond the free 5 GB |
| L1.8 | `kv-tdlab-36d2d4` (+ 5 secrets, 1 certificate, 2 role assignments) | Key Vault Standard | swedencentral | $0.03 per 10K operations (certificate operations $3 per renewal request: none made) | 2026-11-08 15:50 | 20:50 | 0.03 | 2026-11-08 20:52 | one 10K block; imported certificate, no CA-issued renewals |
| L1.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer, AutoPause on exhaustion | swedencentral | $0 within the free limit | 2026-11-08 15:50 | 20:50 | 0.00 | 2026-11-08 20:52 | |
| L1.10 | RG, VNet, 3 subnets (2 service endpoints), 3 NSGs, action group, 6 role assignments | — | swedencentral | no meter (service endpoints free) | 2026-11-08 15:50 | 20:50 | 0.00 | 2026-11-08 20:52 | |
| L2.1 | `vm-tdlab-legacy` (+ NIC, managed boot diagnostics) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-08 20:53 | 22:15 | 0.20 | 2026-11-08 23:04 | lift cycle L2 (azure-lift): rebuild from zero 20:53-21:13, cutover rehearsal 21:20-21:24; the agent session then stopped on a usage limit and the **scheduled reaper deleted the RG** when its ttl (22:52:56) passed: delete 23:00-23:04, 2.2 h in all |
| L2.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-08 20:53 | 22:15 | 0.01 | 2026-11-08 23:04 | |
| L2.3 | `pip-tdlab-legacy` | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-08 20:53 | 22:15 | 0.02 | 2026-11-08 23:04 | |
| L2.4 | `sttdlab36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month; writes $0.05/10K, reads $0.004/10K | 2026-11-08 20:53 | 22:15 | 0.01 | 2026-11-08 23:04 | |
| L2.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB | 2026-11-08 20:53 | 22:15 | 0.03 | 2026-11-08 23:04 | |
| L2.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month | 2026-11-08 20:53 | 22:15 | 0.50 | 2026-11-08 23:04 | counted as a full month again |
| L2.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | $0 first 5 GB/month, then $2.99/GB | 2026-11-08 20:53 | 22:15 | 0.30 | 2026-11-08 23:04 | recreated workspace: assume a fresh daily cap |
| L2.8 | `kv-tdlab-36d2d4` (+ secrets, certificate) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-08 20:53 | 22:15 | 0.03 | 2026-11-08 23:04 | |
| L2.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer | swedencentral | $0 within the free limit | 2026-11-08 20:53 | 22:15 | 0.00 | 2026-11-08 23:04 | |
| L2.10 | RG, VNet, subnets, NSGs, action group, role assignments | — | swedencentral | no meter | 2026-11-08 20:53 | 22:15 | 0.00 | 2026-11-08 23:04 | |
| R1.1 | `vm-tdlab-legacy` (+ NIC, managed boot diagnostics; system-assigned + `id-tdlab-sqldeploy` identities) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-09 02:28 | 10:30 | 0.75 | 2026-11-09 11:03 (reaper) | re-platform cycle R1 (azure-sql): SQL Express baseline, golden before, cutover rehearsal + measured cutover to Azure SQL, Query Store follow-ups, golden after, latency samples, certificate rotation; 6 h planned, extended to 8 h at 06:25 (the session stopped on a usage limit 02:40-06:22 while the VM ran); the session stopped again on a usage limit at ~07:00 and the scheduled reaper deleted the RG at ttl + 6 min (ttl 10:54:18, delete 11:00-11:03): 8.6 h in all |
| R1.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-09 02:28 | 10:30 | 0.02 | 2026-11-09 11:03 (reaper) | |
| R1.3 | `pip-tdlab-legacy` | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-09 02:28 | 10:30 | 0.04 | 2026-11-09 11:03 (reaper) | |
| R1.4 | `sttdlab36d2d4` (artifacts, backups, logs) + `sttdlabfiles36d2d4` (ticketdesk-attachments, shared key for the legacy SDK's account SAS) | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month stored; writes $0.05/10K, reads $0.004/10K | 2026-11-09 02:28 | 10:30 | 0.02 | 2026-11-09 11:03 (reaper) | two accounts, each under 1 GB and under 10K operations |
| R1.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-09 02:28 | 10:30 | 0.09 | 2026-11-09 11:03 (reaper) | used this time: the legacy app reaches Azure SQL through it (a few hundred MB processed at most) |
| R1.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-09 02:28 | 10:30 | 0.50 | 2026-11-09 11:03 (reaper) | counted as a full month |
| R1.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-09 02:28 | 10:30 | 0.30 | 2026-11-09 11:03 (reaper) | worst case: one full day at the cap beyond the free 5 GB |
| R1.8 | `kv-tdlab-36d2d4` (+ secrets incl. `td-remoteapp-api-key`, `td-attachments-connection`; certificate `td-iis-legacy` + one rotated version) | Key Vault Standard | swedencentral | $0.03 per 10K operations (certificate operations $3 per renewal request: none, imported versions only) | 2026-11-09 02:28 | 10:30 | 0.03 | 2026-11-09 11:03 (reaper) | one 10K block |
| R1.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` (Entra admin `id-tdlab-sqldeploy`) | GP_S_Gen5_2 free offer, AutoPause on exhaustion | swedencentral | $0 within the free limit (paid: $0.573934/vCore-h, $0.13685/GB-month — not billed with AutoPause) | 2026-11-09 02:28 | 10:30 | 0.00 | 2026-11-09 11:03 (reaper) | database replaced between rehearsal, measured run and golden (free offer each time) |
| R1.10 | RG, VNet, 3 subnets, 3 NSGs, action group, role assignments, `id-tdlab-sqldeploy` (user-assigned identity) | — | swedencentral | no meter (managed identities free) | 2026-11-09 02:28 | 10:30 | 0.00 | 2026-11-09 11:03 (reaper) | |
| R2.1 | `vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-10 00:57 | 05:00 | 0.44 | 2026-11-10 06:04 (reaper) | re-platform cycle R2 (azure-sql): timed rebuild from zero; the legacy app answered 500 on Azure SQL (EF 6.1 Database.Exists() probes master, which a contained user cannot log in to; fixed in the interceptor afterwards); the session then stopped on a usage limit and the scheduled reaper deleted the RG at ttl + 3 min (ttl 05:57:26, delete 06:00-06:04): 5.1 h |
| R2.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-10 00:57 | 05:00 | 0.01 | 2026-11-10 06:04 (reaper) | |
| R2.3 | `pip-tdlab-legacy` | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-10 00:57 | 05:00 | 0.03 | 2026-11-10 06:04 (reaper) | |
| R2.4 | `sttdlab36d2d4` + `sttdlabfiles36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month stored; writes $0.05/10K, reads $0.004/10K | 2026-11-10 00:57 | 05:00 | 0.02 | 2026-11-10 06:04 (reaper) | |
| R2.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-10 00:57 | 05:00 | 0.05 | 2026-11-10 06:04 (reaper) | |
| R2.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-10 00:57 | 05:00 | 0.50 | 2026-11-10 06:04 (reaper) | counted as a full month again |
| R2.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-10 00:57 | 05:00 | 0.30 | 2026-11-10 06:04 (reaper) | recovered workspace: assume a fresh daily cap |
| R2.8 | `kv-tdlab-36d2d4` (+ secrets, certificate + one rotated version) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-10 00:57 | 05:00 | 0.03 | 2026-11-10 06:04 (reaper) | |
| R2.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer, AutoPause on exhaustion | swedencentral | $0 within the free limit | 2026-11-10 00:57 | 05:00 | 0.00 | 2026-11-10 06:04 (reaper) | |
| R2.10 | RG, VNet, subnets, NSGs, action group, role assignments, `id-tdlab-sqldeploy` | — | swedencentral | no meter | 2026-11-10 00:57 | 05:00 | 0.00 | 2026-11-10 06:04 (reaper) | |
| R3.1 | `vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-10 07:35 | 10:45 | 0.09 | 2026-11-10 08:34 (session trap) | re-platform cycle R3 (azure-sql): rebuild from zero (sign-in 500: Identity V1-schema check, fixed and redeployed in place), then `replatform.sh session --after-rebuild`: SQL Express, latency, rehearsal, measured cutover, migrate, plan-tag, golden and latency on Azure SQL; certificate rotation failed (empty PFX, script bug); destroyed by the session at its end: 1.0 h |
| R3.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-10 07:35 | 10:45 | 0.01 | 2026-11-10 08:34 (session trap) | |
| R3.3 | `pip-tdlab-legacy` | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-10 07:35 | 10:45 | 0.01 | 2026-11-10 08:34 (session trap) | |
| R3.4 | `sttdlab36d2d4` + `sttdlabfiles36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month stored; writes $0.05/10K, reads $0.004/10K | 2026-11-10 07:35 | 10:45 | 0.02 | 2026-11-10 08:34 (session trap) | |
| R3.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-10 07:35 | 10:45 | 0.01 | 2026-11-10 08:34 (session trap) | |
| R3.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-10 07:35 | 10:45 | 0.50 | 2026-11-10 08:34 (session trap) | counted as a full month again |
| R3.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-10 07:35 | 10:45 | 0.30 | 2026-11-10 08:34 (session trap) | |
| R3.8 | `kv-tdlab-36d2d4` (+ secrets, certificate + one rotated version) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-10 07:35 | 10:45 | 0.03 | 2026-11-10 08:34 (session trap) | |
| R3.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer, AutoPause on exhaustion | swedencentral | $0 within the free limit | 2026-11-10 07:35 | 10:45 | 0.00 | 2026-11-10 08:34 (session trap) | |
| R3.10 | RG, VNet, subnets, NSGs, action group, role assignments, `id-tdlab-sqldeploy` | — | swedencentral | no meter | 2026-11-10 07:35 | 10:45 | 0.00 | 2026-11-10 08:34 (session trap) | |
| R4.1 | `vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h (not allocated: no compute billed) | 2026-11-10 17:24 | 18:00 | 0.00 | 2026-11-10 17:40 (make azure-down) | re-platform cycle R4 (azure-sql): clean rebuild + certificate rotation attempt; the VM create failed with AllocationFailed (no B2as_v2 capacity in swedencentral at 17:31Z); the stage-none destroy was blocked by the failed VM's NIC, so the RG was deleted with make azure-down (17:35-17:40); nothing else ran |
| R4.2 | `osdisk-tdlab-legacy` | S4 LRS 32 GiB Standard HDD | swedencentral | $1.536/month (+$0.0005 per 10K ops) | 2026-11-10 17:24 | 18:00 | 0.01 | 2026-11-10 17:40 (make azure-down) | |
| R4.3 | `pip-tdlab-legacy` | Standard IPv4 static | swedencentral | $0.005/h | 2026-11-10 17:24 | 18:00 | 0.01 | 2026-11-10 17:40 (make azure-down) | |
| R4.4 | `sttdlab36d2d4` + `sttdlabfiles36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month stored; writes $0.05/10K, reads $0.004/10K | 2026-11-10 17:24 | 18:00 | 0.01 | 2026-11-10 17:40 (make azure-down) | |
| R4.5 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | swedencentral (Global meter) | $0.01/h + $0.01/GB processed | 2026-11-10 17:24 | 18:00 | 0.01 | 2026-11-10 17:40 (make azure-down) | |
| R4.6 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month (first 25) | 2026-11-10 17:24 | 18:00 | 0.50 | 2026-11-10 17:40 (make azure-down) | counted as a full month again |
| R4.7 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | ingestion $0 first 5 GB/month, then $2.99/GB | 2026-11-10 17:24 | 18:00 | 0.30 | 2026-11-10 17:40 (make azure-down) | |
| R4.8 | `kv-tdlab-36d2d4` (+ secrets, certificate + one rotated version) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-10 17:24 | 18:00 | 0.03 | 2026-11-10 17:40 (make azure-down) | |
| R4.9 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk` | GP_S_Gen5_2 free offer, AutoPause on exhaustion | swedencentral | $0 within the free limit | 2026-11-10 17:24 | 18:00 | 0.00 | 2026-11-10 17:40 (make azure-down) | |
| R4.10 | RG, VNet, subnets, NSGs, action group, role assignments, `id-tdlab-sqldeploy` | — | swedencentral | no meter | 2026-11-10 17:24 | 18:00 | 0.00 | 2026-11-10 17:40 (make azure-down) | |
| P4.1 | Entra app `ticketdesk-lab-ado` + service principal + federated credential `ado-ticketdesk-lab-arm` (issuer Entra ID, subject = the Azure DevOps service connection `ticketdesk-lab-arm`) | app registration | University tenant | $0 | 2026-11-11 05:04 | project end | 0.00 | kept | round 4 pipelines; app 2 of max 2; no secret, no certificate, 0 role assignments when created (Contributor on `rg-ticketdesk-lab` + AKS RBAC Writer come from the aks session); delete at project end: `az ad app delete --id <client id>` and the service connection |
| K1.1 | `aks-tdlab-swedencentral` node pool (VMSS, 1 node) | Standard_B2as_v2 Linux PAYG | swedencentral | $0.0778/h | 2026-11-11 17:44 | 20:44 | 0.04 | 2026-11-11 18:17 (session end) | AKS round session, 3 h planned; AllocationFailed fallbacks B2als_v2 ($0.0389/h) / B2s_v2 ($0.0864/h); session session-20261111T174358Z; actual: AKS created 17:44-17:50, deleted with the group by 18:17; ~0.5 h |
| K1.2 | node OS disk (AKS-managed, 30 GB) | P4 LRS Premium SSD | swedencentral | $5.8072/month | 2026-11-11 17:44 | 20:44 | 0.01 | 2026-11-11 18:17 (session end) | user exception 2026-11-10 (AGENT-RULES banned list): AKS has no HDD OS disk for these sizes; session session-20261111T174358Z; actual: ~0.5 h |
| K1.3 | AKS cluster, Free tier | meter FreeTierInfrastructureCost Uptime SLA | swedencentral | $0.05/h | 2026-11-11 17:44 | 20:44 | 0.03 | 2026-11-11 18:17 (session end) | counted until shown not to apply; session session-20261111T174358Z; actual: ~0.5 h |
| K1.4 | AKS Standard load balancer (outbound + the app's service rule) | Standard, first 5 rules | Global | $0.025/h | 2026-11-11 17:44 | 20:44 | 0.02 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: outbound rule only, ~0.5 h |
| K1.5 | 2 public IPs (AKS outbound, the app's service `tdlab-aks-36d2d4`) | Standard IPv4 static | swedencentral | $0.005/h each | 2026-11-11 17:44 | 20:44 | 0.01 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: outbound IP only (the app's service was never deployed), ~0.5 h |
| K1.6 | `vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-11 17:44 | 20:44 | 0.00 | 2026-11-11 18:17 (session end) | 1.5 h planned: retired (deleted) before v2; session session-20261111T174358Z; actual: never allocated: AllocationFailed for Standard_B2as_v2 at 17:57Z, the fallback sizes were not tried (failed VM left in Azure, see session evidence); no compute billed |
| K1.7 | `osdisk-tdlab-legacy` + `pip-tdlab-legacy` | S4 LRS + Standard IPv4 static | swedencentral | $1.536/month + $0.005/h | 2026-11-11 17:44 | 20:44 | 0.01 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: public IP ~0.5 h, OS disk not created with the failed VM |
| K1.8 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | Global | $0.01/h + $0.01/GB | 2026-11-11 17:44 | 20:44 | 0.01 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: ~0.4 h |
| K1.9 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month | 2026-11-11 17:44 | 20:44 | 0.50 | 2026-11-11 18:17 (session end) | counted as a full month; session session-20261111T174358Z; actual: created, counted as a full month |
| K1.10 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | $0 first 5 GB/month, then $2.99/GB | 2026-11-11 17:44 | 20:44 | 0.30 | 2026-11-11 18:17 (session end) | worst case: one full day at the cap beyond the free 5 GB; session session-20261111T174358Z; actual: created, counted as one day at the cap |
| K1.11 | `acrtdlab36d2d4` | Container Registry Basic | swedencentral | $0.1666/day + $0.10/GB-month | 2026-11-11 17:44 | 20:44 | 0.17 | 2026-11-11 18:17 (session end) | this round only (brief); counted as a full day; session session-20261111T174358Z; actual: created 17:45 (no image pushed), counted as a full day |
| K1.12 | `kv-tdlab-36d2d4` (+ secrets, certificates `td-iis-legacy`, `td-aks-web` + rotated versions, key `td-dataprotection`) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-11 17:44 | 20:44 | 0.03 | 2026-11-11 18:17 (session end) | one 10K block; imported certificates, no CA-issued renewals; software key; session session-20261111T174358Z; actual: one 10K block |
| K1.13 | `sttdlab36d2d4` + `sttdlabfiles36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month; writes $0.05/10K | 2026-11-11 17:44 | 20:44 | 0.02 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: created, no data |
| K1.14 | `sb-tdlab-36d2d4` (queue `ticket-notifications`) | Service Bus Basic | swedencentral | $0.05 per million operations | 2026-11-11 17:44 | 20:44 | 0.00 | 2026-11-11 18:17 (session end) | local auth off; session session-20261111T174358Z; actual: namespace and queue created, no messages |
| K1.15 | `alert-tdlab-failed-requests` (+ action group `ag-tdlab-drill`, no receivers) | metric alert, 1 metric monitored | Global | $0.10/month (first 10 metrics $0) | 2026-11-11 17:44 | 20:44 | 0.10 | 2026-11-11 18:17 (session end) | counted as a full month; session session-20261111T174358Z; actual: created, counted as a full month |
| K1.16 | `sqldb-ticketdesk-pitr` (restore drill copy) | SQL Database Single Basic | swedencentral | $0.161/day | 2026-11-11 17:44 | 20:44 | 0.00 | 2026-11-11 18:17 (session end) | deleted right after the drill; counted as a full day; session session-20261111T174358Z; actual: never created (the session stopped at the first apply) |
| K1.17 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk`, identities `id-tdlab-app` / `id-tdlab-sqldeploy` + federated credentials, RG, VNet, subnets, NSGs, action groups, role assignments | GP_S_Gen5_2 free offer (AutoPause) + no meter | swedencentral | $0 | 2026-11-11 17:44 | 20:44 | 0.00 | 2026-11-11 18:17 (session end) | session session-20261111T174358Z; actual: also a hand-made placeholder smart detector rule 17:54-17:57Z (no meter) |
| K2.1 | `aks-tdlab-swedencentral` node pool (VMSS, 1 node) | Standard_B2as_v2 Linux PAYG | swedencentral | $0.0778/h | 2026-11-12 06:25 | 09:25 | 0.16 | 2026-11-12 08:40 (session end) | AKS round session, 3 h planned; AllocationFailed fallbacks B2als_v2 ($0.0389/h) / B2s_v2 ($0.0864/h); session session-20261112T062534Z; actual: AKS created 06:33-06:39 (4m56s), deleted with the group by 08:40; 2.12 h |
| K2.2 | node OS disk (AKS-managed, 30 GB) | P4 LRS Premium SSD | swedencentral | $5.8072/month | 2026-11-12 06:25 | 09:25 | 0.02 | 2026-11-12 08:40 (session end) | user exception 2026-11-10 (AGENT-RULES banned list): AKS has no HDD OS disk for these sizes; session session-20261112T062534Z; actual: 2.12 h |
| K2.3 | AKS cluster, Free tier | meter FreeTierInfrastructureCost Uptime SLA | swedencentral | $0.05/h | 2026-11-12 06:25 | 09:25 | 0.11 | 2026-11-12 08:40 (session end) | counted until shown not to apply; session session-20261112T062534Z; actual: 2.12 h |
| K2.4 | AKS Standard load balancer (outbound + the app's service rule) | Standard, first 5 rules | Global | $0.025/h | 2026-11-12 06:25 | 09:25 | 0.05 | 2026-11-12 08:40 (session end) | session session-20261112T062534Z; actual: 2.12 h |
| K2.5 | 2 public IPs (AKS outbound, the app's service `tdlab-aks-36d2d4`) | Standard IPv4 static | swedencentral | $0.005/h each | 2026-11-12 06:25 | 09:25 | 0.02 | 2026-11-12 08:40 (session end) | session session-20261112T062534Z; actual: 2.12 h |
| K2.6 | `vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`) | Standard_B2as_v2, Windows PAYG | swedencentral | $0.087/h | 2026-11-12 06:25 | 09:25 | 0.06 | 2026-11-12 08:40 (session end) | 1.6 h planned: allocated first (before anything with a fixed charge, ~0.1 h earlier), retired before v2; AllocationFailed fallbacks B2als_v2 ($0.0481/h), B2s_v2 ($0.0956/h); session session-20261112T062534Z; actual: Standard_B2as_v2 on the first try (no fallback), created ~06:28-06:30, deleted by retire-vm 07:09; 0.7 h |
| K2.7 | `osdisk-tdlab-legacy` + `pip-tdlab-legacy` | S4 LRS + Standard IPv4 static | swedencentral | $1.536/month + $0.005/h | 2026-11-12 06:25 | 09:25 | 0.01 | 2026-11-12 08:40 (session end) | 1.6 h; session session-20261112T062534Z; actual: disk 0.7 h, IP 0.73 h |
| K2.8 | `pe-sql-tdlab` (+ NIC) | Standard Private Endpoint | Global | $0.01/h + $0.01/GB | 2026-11-12 06:25 | 09:25 | 0.02 | 2026-11-12 08:40 (session end) | session session-20261112T062534Z; actual: 2.12 h |
| K2.9 | `privatelink.database.windows.net` (+ VNet link) | Private DNS zone | global | $0.50 per zone-month | 2026-11-12 06:25 | 09:25 | 0.50 | 2026-11-12 08:40 (session end) | counted as a full month; session session-20261112T062534Z; actual: created, counted as a full month |
| K2.10 | `log-tdlab-swedencentral` + `appi-tdlab-swedencentral` | PerGB2018, daily cap 0.1 GB | swedencentral | $0 first 5 GB/month, then $2.99/GB | 2026-11-12 06:25 | 09:25 | 0.30 | 2026-11-12 08:40 (session end) | worst case: one full day at the cap beyond the free 5 GB; session session-20261112T062534Z; actual: created, counted as one day at the cap |
| K2.11 | `acrtdlab36d2d4` | Container Registry Basic | swedencentral | $0.1666/day + $0.10/GB-month | 2026-11-12 06:25 | 09:25 | 0.17 | 2026-11-12 08:40 (session end) | this round only (brief); counted as a full day; session session-20261112T062534Z; actual: created, counted as a full day |
| K2.12 | `kv-tdlab-36d2d4` (+ secrets, certificates `td-iis-legacy`, `td-aks-web` + rotated versions, key `td-dataprotection`) | Key Vault Standard | swedencentral | $0.03 per 10K operations | 2026-11-12 06:25 | 09:25 | 0.03 | 2026-11-12 08:40 (session end) | one 10K block; imported certificates, no CA-issued renewals; software key; session session-20261112T062534Z; actual: one 10K block |
| K2.13 | `sttdlab36d2d4` + `sttdlabfiles36d2d4` | StorageV2 Standard LRS Hot | swedencentral | $0.0184/GB-month; writes $0.05/10K | 2026-11-12 06:25 | 09:25 | 0.02 | 2026-11-12 08:40 (session end) | session session-20261112T062534Z; actual: under 1 GB, under 10K ops |
| K2.14 | `sb-tdlab-36d2d4` (queue `ticket-notifications`) | Service Bus Basic | swedencentral | $0.05 per million operations | 2026-11-12 06:25 | 09:25 | 0.01 | 2026-11-12 08:40 (session end) | local auth off; session session-20261112T062534Z; actual: under 100K ops |
| K2.15 | `alert-tdlab-failed-requests` (+ action group `ag-tdlab-drill`, no receivers) | metric alert, 1 metric monitored | Global | $0.10/month (first 10 metrics $0) | 2026-11-12 06:25 | 09:25 | 0.10 | 2026-11-12 08:40 (session end) | counted as a full month; session session-20261112T062534Z; actual: created, counted as a full month |
| K2.16 | `sqldb-ticketdesk-pitr` (restore drill copy) | SQL Database Single Basic | swedencentral | $0.161/day | 2026-11-12 06:25 | 09:25 | 0.16 | 2026-11-12 08:40 (session end) | deleted right after the drill; counted as a full day; session session-20261112T062534Z; actual: restored 07:46-08:19, deleted with the group by 08:40 (the drill stopped before its own delete); counted as a full day |
| K2.17 | `sql-tdlab-36d2d4` + `sqldb-ticketdesk`, identities `id-tdlab-app` / `id-tdlab-sqldeploy` + federated credentials, RG, VNet, subnets, NSGs, action groups, role assignments | GP_S_Gen5_2 free offer (AutoPause) + no meter | swedencentral | $0 | 2026-11-12 06:25 | 09:25 | 0.00 | 2026-11-12 08:40 (session end) | session session-20261112T062534Z; actual: no meter |
<!-- ledger:end -->

Rows F1–F3 ran in **westus** (first lab region, Round 1 on 2026-11-08 00:34–01:25 UTC); rows S* run in
**swedencentral**, the lab region since 2026-11-08 (`docs/dev/AZURE-BUDGET.md`, "Why swedencentral").

**Status after the lift round (2026-11-09 01:41 UTC):** running estimate **$6.76** (conservative, not metered): cycles
L1 (15:50-20:52, provisioning, replay, backup drill, first cutover attempt) and L2 (20:53-23:04, rebuild from zero and the
cutover). L2 ended through the scheduled reaper (ttl 22:52:56, delete 23:00-23:04 UTC, `$W/logs/azure-reaper.log`; copy in
`docs/evidence/azure-lift-20261108/reaper-catch.txt`), its first real catch. Terraform state was reconciled afterwards
(refresh-only + stage `none`: "No changes"), the soft-deleted vault `kv-tdlab-36d2d4` purged; the soft-deleted workspace
`log-tdlab-swedencentral` is not billed and is recovered by the next foundation apply. Nothing billable is left; only the
$0 Entra app remains (`docs/evidence/azure-lift-20261108/state-reconcile.txt`).

**Status after the swedencentral cycles (2026-11-08 15:17 UTC):** running estimate **$4.29** (conservative, not
metered). Nothing billable is left: `rg-ticketdesk-lab` does not exist, no soft-deleted vault or workspace, only the
$0 Entra app remains (`docs/evidence/azure-foundation-20261108/end-state.txt`).

**Status after the westus cycles (2026-11-08 01:26 UTC):** running estimate **$2.53** (conservative, not metered — the cost API returns nothing here; cycles 2 and 3 lasted 11 and 10 minutes
from apply start to destroy end). Nothing
billable is left: `rg-ticketdesk-lab` does not exist, no soft-deleted vault, only the $0 Entra app remains
(`docs/evidence/azure-foundation-20261107/end-state.txt`).

## Unit prices used by the plan (swedencentral)

Source: Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`, `priceType eq 'Consumption'`,
USD), fetched 2026-11-08T07:28Z with `scripts/azure_prices.py <dir> swedencentral`; raw responses in
`docs/evidence/azure-foundation-20261108/prices/raw/`, flattened in `.../prices/prices.tsv`. Re-fetch with
`make azure-prices`. (The westus prices of 2026-11-07/08 stay in `docs/evidence/azure-foundation-20261107/prices/`.)

| Item | SKU / meter | Unit price | Used by |
|---|---|---|---|
| Windows VM | **Standard_B2as_v2** (2 vCPU, 8 GiB, x64, Basv2) | **$0.087/h** | lift stage |
| Windows VM | Standard_B2als_v2 (2/4) / B2s_v2 (2/8) / B4as_v2 (4/16) / D2as_v5 (2/8) | $0.0481 / $0.0956 / $0.174 / $0.184 per h | fallbacks |
| Linux node | **Standard_B2as_v2** | **$0.0778/h** | AKS nodes (×2) |
| Linux node | Standard_B2als_v2 / B2s_v2 / B4as_v2 / D2as_v5 | $0.0389 / $0.0864 / $0.156 / $0.092 per h | fallbacks |
| Managed disk | S4 LRS Standard HDD (32 GiB) | $1.536/month (+$0.0005 per 10K ops) | VM and node OS disks |
| Managed disk | S6 LRS Standard HDD (64 GiB) | $3.008/month | — |
| Load balancer | Standard, first 5 rules (Global meter) | $0.025/h; extra rule $0.01/h; $0.005/GB processed | AKS |
| Public IP | Standard IPv4 static | $0.005/h | VM, AKS outbound |
| AKS control plane | Free tier | no control-plane meter | AKS |
| AKS | meter "FreeTierInfrastructureCost Uptime SLA" (effective 2026-10-01) | $0.05/h | counted for AKS until shown not to apply |
| AKS | Standard tier "Standard Uptime SLA" | $0.10/h | banned (lab rule) |
| Private endpoint | Standard (Global meter) | $0.01/h + $0.01/GB in/out | SQL (Key Vault optional) |
| Private DNS | zone (first 25) / queries | $0.50 per zone-month / $0.40 per million | SQL (Key Vault optional) |
| Log Analytics | Analytics Logs ingestion | $0 first 5 GB/month, then $2.99/GB; retention beyond included $0.13/GB-month | monitoring |
| Application Insights | workspace-based | billed as Log Analytics ingestion | monitoring |
| Azure Monitor | e-mail notifications | $0 first 1,000/month (then $0.00002 each, westus row; e-mails are not region-priced) | action group |
| Key Vault | Standard operations | $0.03 per 10K | Key Vault |
| Azure SQL | GP serverless Gen5 free offer ("2 vCore - Free") | $0 (paid serverless $0.573934/vCore-h; storage $0.13685/GB-month) | SQL free DB with AutoPause |
| Service Bus | Basic operations | $0.05 per million | Round 3 option |
| Service Bus | Standard base unit + operations | $0.013441/h; first 13M ops/month $0, then $0.80/M | Round 3 option |
| Bandwidth | data transfer out (Microsoft network) | first 100 GB/month $0, then $0.087/GB | all |

Hourly burn of each stage as configured (Terraform output `est_usd_per_hour`, from `make azure-plan STAGE=…`,
evidence `docs/evidence/azure-foundation-20261108/plan-*.txt`): foundation **$0.0232/h**, lift **$0.1173/h**,
aks **$0.2630/h** (2 nodes), cutover **$0.3571/h**. Monthly-billed items (DNS zones) are additionally counted as a
full month per creation in the ledger rows.
