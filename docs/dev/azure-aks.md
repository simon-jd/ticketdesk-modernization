# AKS round (Round 4): the new app on AKS, the VM retired, drills

Brief: `plan/briefs/r4-aks.md`. Phase A (local preparation, no Azure resources) is done on `ws/aks`; Phase B is one
unattended Azure session (`ops/azure/aks.sh session`, tmux) after the coordinator's go and after `retire` is merged into
`modernize` (image v2). Numbers below come from commands run in Phase A; evidence `docs/evidence/aks-20261111/`.

## Design

| Part | Choice | Why |
|---|---|---|
| Cluster | AKS Free tier, 1 node `Standard_B2as_v2`, fallbacks `B2als_v2` then `B2s_v2` on `AllocationFailed` (`aks.sh apply`), no upgrade channels, Container Insights off | 2 + 2 vCPU next to the VM leaves a surge node inside the 6 vCPU cap; agents of Container Insights would take ~0.2 vCPU of the one node and the workspace's 0.1 GB/day |
| Images | ACR Basic (this round only), pushed from lab-host with `az acr login`; kubelet identity AcrPull | GHCR is out (nothing is pushed publicly) |
| Identities | `id-tdlab-app` (web + worker, federated with `ticketdesk/td-web`, `ticketdesk/td-worker`): Key Vault Crypto User on `td-dataprotection` only, Service Bus Data Sender + Receiver on the queue only, Azure SQL contained user in `td_modern_app_role` (VM step `aks-sql-user`); `id-tdlab-sqldeploy` (Entra admin of the server) federated with `ticketdesk/td-migrator` for the td-db job | no keys or passwords anywhere on AKS; the migrate job keeps an admin path into the private-endpoint-only server after the VM is gone |
| Key ring | `dbo.DataProtectionKeys` wrapped with the Key Vault key `td-dataprotection` (RSA 2048, wrap/unwrap): new app `DataProtection:KeyVaultKeyId` (workload identity), legacy `ticketdesk:DataProtectionKeyVaultKeyId` (VM identity over IMDS), both Azure.Extensions.AspNetCore.DataProtection.Keys **1.6.1** (its netstandard2.0 build keeps the legacy app's DataProtection 8.0.22; one version on both sides because each key names its decryptor type with the assembly version, which net48 cannot load newer than its bin: a test pins it) | DECISIONS 2026-11-10 (Key Vault key through managed identity on both apps). The baseline has no key yet, so the first key of the session is created wrapped |
| Legacy key ring on Azure SQL | `SqlKeyRingRepository` asks `ManagedIdentitySqlTokenInterceptor.ApplyAccessToken` before `Open()` | its raw SqlConnections had no token (azure-sql handoff) |
| TLS | Kestrel terminates TLS (`Tls:CertificatePath`, PEM re-read when its content changes, checked every 10 s on a handshake); the certificate `td-aks-web` (lab CA) comes from Key Vault through the Secrets Store CSI add-on (rotation poll 2 min); the Azure load balancer forwards TCP 443 (Service `LoadBalancer`, DNS label `tdlab-aks-<suffix>`, source range = home IP, NSG 443 from home only) | no ingress controller fits: the AKS application routing add-on's managed NGINX is out of support after 2026-11-30, its Gateway API successor runs Istio (too heavy for one B2as_v2 node), and no ingress controller image is on the lab's download list |
| Gateway (v1) | `Legacy:BaseUrl=https://<vm fqdn>/` with a host alias to the VM's private IP (VNet, NSG 443 from `snet-aks`), certificate validated normally: the lab CA as a ConfigMap (`<subject hash>.0`) in `SSL_CERT_DIR` | survives the VM certificate rotation (a SHA-256 pin would not) |
| Service Bus | Basic namespace, local auth off, queue `ticket-notifications`; `Notifications:ServiceBus:FullyQualifiedNamespace` | queues are all the worker uses (Basic: no topics/sessions/transactions) |
| Schema | `td-db migrate` as a Helm pre-install/pre-upgrade job (`ticketdesk-db` image); `deploy contract` runs `--contract --legacy-retired` as the pre-upgrade job | DECISIONS 2026-11-08 |
| Telemetry, alert, SLOs | the web app sends requests to Application Insights (Azure Monitor OpenTelemetry 1.6.0, no sampling, warnings only); metric alert `requests/failed` count > 2 per minute, action group without receivers; fired/resolved times from the Alerts Management API; SLO KQL on `AppRequests` | the drill and the SLOs need request telemetry; the daily cap holds |
| kubectl | `aks.sh kube`: `az aks get-credentials` + an exec credential (`ops/azure/aks-token.sh`) that asks the az login for an AKS server-app token | kubelogin is not on the download list |

## Rerun after the failed session of 2026-11-11 (cap $15.00)

The first session stopped at its first apply (VM `Standard_B2as_v2` AllocationFailed, the fallback sizes never tried:
`session-20261111T174358Z/summary.txt`). For the rerun:

- **VM first**: the session's first apply is `-target=module.vm[0]` of stage cutover (`plan-cutover-vm-first.txt`, 21
  resources): the group, the network (VNet, 3 subnets, 3 NSGs + associations), Key Vault (+ the admin's role and the
  RBAC wait), the Azure SQL deploy identity, and the VM with its NIC, public IP, admin-password secret and Key Vault role.
  Nothing with a fixed charge: the private DNS zone, Log Analytics/App Insights, ACR, the metric alert and AKS come with the
  second apply (the rest of stage cutover). If every VM size fails, the session stops there and destroys, close to $0. The
  VM runs ~0.1 h longer than in the first plan (the second apply, ~10 min, no longer runs beside the VM's creation; the
  steps after it start ~4-6 min later): ~$0.009, in row K2.6 (1.6 h).
- **AllocationFailed fallback** (`ak_apply`): only the attempt's own output decides; on a VM AllocationFailed the failed VM,
  its OS disk and its NIC are deleted before the next size (B2as_v2 -> B2als_v2 -> B2s_v2); one absolute ttl for every
  attempt; the chosen sizes of a previous session are cleared at the start. Proven with stub make/az
  (`apply-fallback/`): VM fails twice then fits on B2s_v2 (deletes before each retry, never "already exists"), fails on
  every size (stops after 3), AKS node fails once (node fallback, no VM delete), another error (no retry); 1 ttl value
  across attempts in every scenario.
- `disable_generated_rule = false` (no wait for a Failure Anomalies rule Azure may not generate).
- Cap `TD_LEDGER_CAP=15.00`, rows **K2.*** (1.98) -> **12.98 + 1.98 = 14.96 <= 15.00** (margin 0.04). The reaper is fixed
  on modernize a90ce7c (az time limits).
- The approval request comes ~62 min after the start (steps before deploy-v2: ~60 min).
- **Session deadline** (coordinator 2026-11-11): the K2 rows plan start .. start + 180 min; the deadline is **start + 165
  min** (180 minus the 15-min destroy budget; the 11-11 destroy took 666 s), so the destroy ends by the planned end. It is
  checked before every step and inside the long waits: the CD approval poll (queue-cd's `--timeout` is also capped at the
  time left; at the deadline queue-cd is stopped and the run cancelled, no rollback, no script fallback), the PITR restore
  (each attempt and the restore call itself), the golden replay (`timeout`), the alert drill's polls. Steps before
  retire-vm stop 8 min earlier (start + 157 min) so the VM delete fits too; a step still running 2 min after the deadline
  (a hung `az` call, say) is stopped by a watchdog (its processes frozen, then TERM, KILL after 30 s). Past the deadline
  every remaining step is recorded as "not measured: session deadline" (`session-*/not-measured.txt`, outcome of every
  step in `steps.txt`), then the trap destroys. retire-vm and the destroy are never skipped; a deadline inside a build step
  (apply .. deploy-v1) ends the session at once and the destroy deletes the VM with the rest. If time runs short, the steps
  drop from the end: restore-drill first, then slo, alert-drill, golden-v2. Proven with stubs (`deadline/`,
  `ops/azure/aks-cd-check.sh deadline`, `TD_SESSION_DEADLINE_OVERRIDE` moves it up): the deadline passing in the CD
  approval wait, a hung step stopped by the watchdog (retire-vm still runs), the real restore and alert drills at the
  deadline; the destroy ran every time.
- **ttl 3.25 h** (`TTL_HOURS=3.25`, start + 195 min; `make azure-aks-session` sets it): if the host dies and the trap never
  runs, the reaper deletes the group then instead of at 4 h. Worst case (trap dead, VM never retired, reaper at the ttl):
  **2.18 -> 15.16** (was 2.39 -> 15.37 at 4 h); with the reaper's lag (next run within 15 min, ~11 min to delete) 2.30 ->
  15.28.

## Session (`aks.sh session`, ~2.5 h planned, deadline start + 165 min, ttl 3.25 h)

`aks.sh session --dry-run` prints the steps and the local checks. Order (coordinator 2026-11-11: v2 needs the contracted
schema, and C0001 drops the dual-write trigger and adds `CK_Tickets_AssignedDate`, which would break the legacy app's
writes, so the VM goes before v2): ledger rows `K1.*` into `docs/dev/azure-spend.md` and the check against the session's
cap **$13.70** (`TD_LEDGER_CAP`, user decision) → certificate → apply `cutover` from the empty group → kubeconfig → push
v1 and v2 → VM: BACPAC import, contained users (VM identity, app identity), `td-db migrate` (expand), legacy deploy with
the shared key ring wrapped by the Key Vault key, legacy smoke → helm v1 (migrate job, web with the gateway, worker) →
app smoke on AKS (**rebuild from zero = T0 → this smoke**) → cutover probe VM → AKS → golden through AKS → VM
certificate rotation (gateway's legacy check polled) → AKS certificate rotation from Key Vault (time to pick up, failed
requests) → apply `aks` (**VM deleted, legacy retired**; v1 keeps serving) → helm v2, whose pre-upgrade job runs
`td-db migrate --contract --legacy-retired` on Azure SQL (`deploy v2` refuses while a VM exists) → golden on v2 → alert
drill (NetworkPolicy denies the web pods' egress to the SQL private endpoint + pods replaced → Fired → policy removed →
Resolved) → SLO KQL → point-in-time restore drill (Basic copy, app switched to it, smoke, RPO from the last probe marker
in the copy; copy deleted) → destroy (also the EXIT trap), which fills the rows' "Actual delete". Every step writes under
`docs/evidence/aks-<date>/`.

## v2 through the Azure DevOps CD pipeline (user decision 2026-11-11)

`aks.sh deploy-v2-cd` (session step deploy-v2, after the VM is deleted):

1. Guard: no `vm-tdlab-legacy` in the group (v2's pre-upgrade job contracts the schema). `cd-aks.sh` checks the same before
   any push or helm call.
2. Values: `aks.sh` renders `values-v2.yaml` from the Terraform outputs into `$TD_W/logs/aks/` (the session's ids and the admin
   CIDR: never in the repository or a pipeline parameter) with the contract args inside; the CD run gets that host path
   (`--values`, the agent runs on lab-host), so both paths render the same manifests: `helm template` of `aks.sh`
   (`ak_template`) and of `cd-aks.sh --template-out` are **identical** (373 lines, 8 objects; sha256 `5f399df570d51e1e` with `aks-v2b`, since the readiness timeout of 8 s).
   Release name `ticketdesk` on both paths (v1 is installed under it too).
3. A probe (reads + writes, 1/s, ticket 9993) runs through the whole step, whichever path deploys.
4. `ops/ado/queue-cd.sh --acr --aks --tag aks-v2 --values <host file> --branch modernize --timeout 1800`; when it logs
   "approval requested", `$TD_W/logs/aks-cd-approval.txt` gets the UTC time and the run URL.
5. Exit 0 and the release `deployed` with image `:aks-v2` → path `cd`. Anything else: the run is cancelled and polled until
   completed (a late approval must not deploy), a `pending-*` release is rolled back to its last good revision, then
   `aks.sh deploy v2`; `deploy-v2-*/path.txt` records which path deployed v2.

Approval timeout **1800 s** from queueing (queue + approval + deploy). Planned steps before it: ~56 min, so the approval
request comes **~58 min after the session start**; the worst case of the step (timeout, cancel, rollback, script deploy) is
~37 min instead of ~5, and the session still ends inside its 3 h (rows unchanged; queue-cd's timeout never reaches past the
session deadline). Preflight (session start,
hard; again at the v2 step, else fallback): the single agent online, enabled and idle, nothing queued (runs waiting for an
approval, like kind-local run 16, are listed and allowed), Azure Repos `modernize` has this session's chart, `cd-aks.sh` and
`cd.yml` (identical files).

Roles of `ticketdesk-lab-ado` (Terraform, stages with AKS only, gone with the group): Contributor on the group; **Azure
Kubernetes Service RBAC Admin on the namespace `ticketdesk`**. RBAC Writer would be less, but its dataActions have no
`secrets-store.csi.x-k8s.io`: helm could not read or update the chart's SecretProviderClass (`ado-cd/role/coverage.txt`:
Writer misses secretproviderclasses read/write/delete, Admin covers every kind of the v2 render plus the release Secrets,
Pods, ReplicaSets, Events); the namespace scope keeps Admin out of kube-system and the other namespaces (the release exists,
so the upgrade never creates the namespace).

Order change in the same commit: the v1 smoke is read-only and the golden v1 runs before the cutover probe, so the golden
replays against the imported baseline before any session write (the probes' comments go to ticket 9993, which the golden
never reads). Golden v2 still runs after the session's writes (golden v1's own writes, the probes): expect data-drift
differences there.

Local proof (`ops/azure/aks-cd-check.sh`, evidence `ado-cd/`): template equivalence; VM guard (stub az: `cd-aks.sh` exit 1
with 0 docker/helm calls, `aks.sh deploy v2` and `deploy-v2-cd` exit 1 with 0 helm calls; without the VM `cd-aks.sh` goes on
to the push); on kind with the real `ak_deploy_v2_cd` and stub queue-cd: timeout with a helm upgrade killed half-way →
`pending-upgrade` → rollback to revision 1 → script deploy, path `script`, probe 86 reads / 43 writes, 0 failed;
success stub → path `cd`, probe 62 reads (1 HTTP 500 of `/api/tickets`: the deadlock below) / 31 writes, 0 failed; approval
file written both times.

**The 500 (fixed, `ado-cd/deadlock/`)**: SQL deadlock 1205 between the list read and the probe's comment write, not the
migrate job. `usp_TicketList` (READ COMMITTED) held S on an `IX_Tickets_LastUpdateDate` key and asked for S on the ticket's
`PK_dbo.Tickets` key (key lookup) while the write held X on that PK key and asked for X on the `IX_Tickets_LastUpdateDate` key;
the reader was the victim, the app logged the SqlException and answered 500. All 5 deadlock graphs of `TicketDeskAks` in
`system_health` have this shape, case-ok's among them (15:42:39.277Z, the 500 at .295Z). The same sequence repeated (4 min
probe only + 20 helm upgrades, same web pod): before the fix 954 reads / 2 failed (one in the probe-only phase), 2 deadlocks;
after it (images **`aks-v2b`**, `6e08728`): **962 reads / 0 failed, 481 writes / 0 failed, 0 deadlocks, 0 error lines**. Fix:
td-db **V0013** `ALLOW_SNAPSHOT_ISOLATION ON` (no exclusive access, no killed connection, Azure SQL's default) and
`usp_TicketList` switches itself to SNAPSHOT (outside a caller's transaction, where V0013 ran): the list reads committed
versions and takes no S locks; READ_COMMITTED_SNAPSHOT, writers and the legacy app unchanged. Test: the list answers within
5 s while a ticket write holds its locks and shows the committed title. Because the VM's td-db (from the repo) applies V0013
before v1's migrate job runs, the chart's `image.dbTag` makes every migrate job run the newest td-db (`TAG_V2`): an older
td-db refuses a journal with a script it does not know.

Contract on Azure SQL during the v2 step: C0001 runs in one transaction under a schema-modification lock (DROP TRIGGER,
two repair UPDATEs, `ALTER TABLE ... WITH CHECK ADD CONSTRAINT`, DROP INDEX): every Tickets read waits for it (SNAPSHOT
readers too: Sch-M blocks Sch-S), measured 0.04 s at scale S and 0.05-0.16 s on kind, so a latency blip, not errors. If a
long read held the table, the ALTER would wait up to td-db's LOCK_TIMEOUT (10 s, then retry) and new reads would queue
behind it: a probe read could then pass its 10 s timeout; nothing at scale S runs that long. The repair UPDATEs touch rows
without the LastUpdateDate index, so they cannot form the deadlock above. Not provable without Azure: the CD run itself (agent, service connection,
the two role assignments, the approval), the run cancel against a real run.

## Dry runs (Phase A, and after the merge of `modernize` 3de52a4)

| What | Result | Evidence |
|---|---|---|
| Tests | Phase A 754 passed, 3 skipped, 0 failed; after the merge **742 passed, 3 skipped, 0 failed (745)** — the first run after the merge had one timing failure (full-text catch-up over 60 s with the suite loading td-sql), SearchTests then 12/12 three times | `tests/` |
| Legacy package | `td-legacy-publish-b4c1428.zip` (897 files), build 0 errors, the usual 4 CS0618 + 4 MSB3245; binding redirects 27 needed, 0 problems (the 920e185 build found 3 missing, fixed in b4c1428); local VM: deploy with the shared key ring, smoke 9/9, reset to `baseline-s-search` | `legacy-package/` |
| Terraform | `terraform validate` ok; plans from the empty state: `cutover` 73 to add (est $0.2951/h), `aks` 51 to add (est $0.2010/h); landed on `modernize` as `da705a9`, `7f758ec` | `plan-*.txt` |
| kind, Phase A | v0.33.0, Kubernetes 1.37.0: install 7 s; app smoke 9/9; web → queue → worker; TLS rotation picked up 31.1 s after the secret update, 0/36 probe requests failed; database egress fault: pods not ready until the policy goes, ready 2 s after; contract job applied C0001; gateway trusts the lab CA through `SSL_CERT_DIR` (legacy check Healthy; without the ConfigMap: "legacy unreachable") | `kind/` |
| kind, v1 → v2 | v1 (`aks-v1`, 344e992) then `helm upgrade` to v2 (`aks-v2`, 0e5cbd1) with the contract as the pre-upgrade job (C0001 applied 0.05 s): probe through the rollout **86 reads, 0 failed; 43 writes, 0 failed**; v2 app smoke 9/9 right after; v2 web has no `Legacy__*`/`Authentication__SharedCookie*` setting; readiness = database only. Found on the way: a request routed to the old pod as it exited hung until the client timeout → web `preStop` sleep 10 s | `kind/v1-to-v2/` |
| Images | `ticketdesk-{web,worker,db}:aks-v1` from `344e992` (gateway build) and `:aks-v2` from `0e5cbd1` (after retire; web 364 MB, worker 367 MB, db 301 MB), local only | `session-dry-run.txt` |
| Session script | `--dry-run`: all checks pass; rows K1.* 1.97 → 13.70 vs cap 13.70: fits | `session-dry-run.txt` |

Not dry-runnable without Azure: workload identity token exchange, the CSI Key Vault provider, the Key Vault key wrap
itself (tested against a local RSA key behind the same resolver interface), the Alerts Management and Log Analytics
queries, `az sql db restore`.

## Estimate (`ops/azure/aks_estimate.py docs/evidence/aks-20261111/prices/prices.tsv`, `estimate.txt`)

Session $1.97 (AKS 3 h, VM 1.5 h; DNS zone, alert rule as a whole month, ACR and the Basic restore copy as a whole day,
Log Analytics one day at the cap; unchanged by the new order): ledger rows 1.97 → **11.73 + 1.97 = 13.70 = the session's
cap** (user decision 2026-11-10, plan unchanged; zero margin in the conservative ledger). Worst case (session stops,
reaper at ttl = start + 4 h, VM never retired): $2.39 → 14.12, inside the $15 budget. Rerun (rows K2, VM 1.6 h, ttl 3.25
h): 1.98 → 14.96; worst case 2.18 → 15.16 (see the rerun section).

## Decisions taken (plan/DECISIONS.md "aks Phase A review")

Cap $13.70 for this session; the AKS node's P4 Premium OS disk as a one-time exception (AGENT-RULES); TLS in Kestrel
behind the Azure load balancer with the Key Vault certificate through the CSI driver, no ingress controller; key-wrap
package 1.6.1 on both apps. The plan's side effect (Microsoft.ServiceBus and Microsoft.AlertsManagement registered) is
recorded there; the infra landed before any apply.
