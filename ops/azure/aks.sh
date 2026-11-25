#!/usr/bin/env bash
# aks.sh - Round 4 (AKS): the new app on AKS behind workload identity, cutover from the Azure Windows VM, the VM retired, the
# drills; one unattended session that destroys everything at its end. Driven from lab-host. docs/dev/azure-aks.md.
#
#   aks.sh cert                    lab CA certificate for the app endpoint tdlab-aks-<suffix>.swedencentral.cloudapp.azure.com
#   aks.sh images TAG [--push]     Angular build + ticketdesk-{web,worker,db}:TAG (linux/amd64); --push: to the session's ACR
#   aks.sh apply STAGE [TARGET..]  make azure-up-STAGE (cutover | aks | none), retrying AllocationFailed with the next size of
#                                  the node / VM fallback list (Standard_B2as_v2 -> B2als_v2 -> B2s_v2); TARGETs: -target=...
#                                  (the session's first apply: module.vm[0] alone, i.e. the VM before anything with a fixed
#                                  charge)
#   aks.sh kube                    kubeconfig for the cluster (Entra token from the az login through an exec credential)
#   aks.sh vm-data                 VM: scripts + legacy package + sql tools + baseline BACPAC staged; Run Command: IIS, BACPAC
#                                  import into Azure SQL, contained users (VM identity, AKS app identity in td_modern_app_role),
#                                  td-db migrate, legacy deploy with the shared key ring wrapped with the Key Vault key; smoke
#   aks.sh deploy-v2-cd            v2 through the Azure DevOps CD pipeline (ops/ado/queue-cd.sh, stage aks-lab, the user's
#                                  approval within TD_CD_TIMEOUT, default 1800 s); on any other outcome the run is cancelled and
#                                  finished, a pending Helm release rolled back, and v2 deployed by `deploy v2`; a probe (reads +
#                                  writes, 1/s) covers the rollout on either path; $TD_W/logs/aks-cd-approval.txt gets the UTC
#                                  time and run URL when the approval is requested (the coordinator alerts the user)
#   aks.sh cd-preflight            the single self-hosted agent free, nothing queued, Azure Repos modernize carries this
#                                  session's chart and CD script (identical files)
#   aks.sh deploy v1|v2            helm upgrade --install from the Terraform outputs (td-db migrate job first): v1 = image TAG_V1
#                                  with the YARP fallback to the VM over the VNet; v2 = image TAG_V2, no legacy, its pre-upgrade
#                                  job runs td-db migrate --contract --legacy-retired (C0001 breaks legacy writes: only once the
#                                  VM is gone; v1 pods keep working on the contracted schema until v2 has rolled out)
#   aks.sh smoke [NAME]            ops/k8s/app_smoke.py against https://<aks fqdn>
#   aks.sh cutover                 probe (reads + writes, 1/s) on the VM, switched to AKS after 30 s, 60 s more: failed requests
#   aks.sh golden TAG              golden replay through https://<aks fqdn>/ + compare
#   aks.sh rotate-vm-cert          replatform.sh's rotation (new version in Key Vault -> IIS) while the gateway's legacy check
#                                  is polled through AKS
#   aks.sh rotate-aks-cert         new version of td-aks-web in Key Vault: time until AKS serves it (CSI poll + Kestrel reload),
#                                  probe 1/s meanwhile
#   aks.sh retire-vm               apply stage aks: the VM, its disk, IP and ops storage are deleted (timed)
#   aks.sh alert-drill             fault (NetworkPolicy: web pods cannot reach Azure SQL, pods replaced) -> alert Fired ->
#                                  recover -> Resolved, times from the Alerts Management API, probe meanwhile
#   aks.sh slo                     availability and p95 latency SLOs over the session (KQL on AppRequests), per phase
#   aks.sh restore-drill           marker writes, Azure SQL point-in-time restore (Basic) to the moment they stop, the app
#                                  switched to the restored database: RTO (request -> app smoke) and RPO (restore point vs last
#                                  marker in the restored database); the restored database is deleted right after
#   aks.sh destroy                 stage none (Entra app only); the resource group itself if anything is left (timed)
#   aks.sh session [--dry-run]     all of the above in order (run it in tmux); trap EXIT -> destroy, whatever happened
#
# Cap: TD_LEDGER_CAP=15.00 for the rerun (user, plan/DECISIONS.md "2026-11-11, aks session failed; rerun with cap $15.00"; the
# first session ran with 13.70); scripts/azure_ledger.py check and
# every make azure-up-* of the session use it.
#
# Session deadline (coordinator 2026-11-11): the K2 rows plan start .. start + 180 min; the deadline is start + 165 min (180
# minus the 15-min destroy budget), so the destroy ends by the planned end. Checked before every step and inside the long
# waits (CD approval, PITR restore, golden replay, alert polls); steps before retire-vm stop 8 min earlier so the VM delete
# fits too; a step still running 2 min after the deadline is stopped. Past it every remaining measurement is recorded as
# "not measured: session deadline" ($SESSION_EV/not-measured.txt, steps.txt) and the trap destroys. retire-vm and destroy
# are never skipped.
#
# Env: TAG_V1 (default aks-v1), TAG_V2 (aks-v2b), TTL_HOURS (3.25; fractions allowed), TD_EVIDENCE
# (docs/evidence/aks-<UTC date>), TD_PROBE_TICKET (9993: Active, not in the golden recording), TD_CD_TIMEOUT (1800),
# TD_QUEUE_CD (ops/ado/queue-cd.sh); for the stub proofs only: TD_SESSION_DEADLINE_OVERRIDE (+SECONDS after the session start,
# or a UTC time), TD_RETIRE_BUDGET_S (480), TD_DEADLINE_GRACE_S (120), TD_RESTORE_MARKER_S (120).
# Secrets stay in Key Vault and $TD_W/.secrets; nothing here prints them.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
export TD_EVIDENCE="${TD_EVIDENCE:-$REPO/docs/evidence/aks-$(date -u +%Y%m%d)}"
export TD_LIFT_ARTIFACTS="${TD_LIFT_ARTIFACTS:-$TD_W/vm/artifacts/aks}"
# shellcheck source=ops/azure/replatform.sh
. "$HERE/replatform.sh"     # lift.sh + replatform.sh helpers: rp_stage, rp_provision, rp_rotate_cert, cmd_smoke, blob_up ...
OWNER_TAG="agent-aks"
export TD_LEDGER_CAP="${TD_LEDGER_CAP:-15.00}"
LEDGER_PREFIX="${TD_LEDGER_PREFIX:-K2}"   # the session's rows in docs/dev/azure-spend.md (ops/azure/aks_ledger_rows.py)
TAG_V1="${TAG_V1:-aks-v1}"; TAG_V2="${TAG_V2:-aks-v2b}"   # v2b: + td-db V0013 (snapshot reads, the deadlock fix)
NS=ticketdesk; RELEASE=ticketdesk   # the release name the CD stage (cd-aks.sh) upgrades
CHART="$REPO/ops/k8s/ticketdesk"
CA="$TD_W/.secrets/legacy-lab-ca"
RUN="$TD_W/logs/aks"; mkdir -p "$RUN"
KCFG="$TD_W/.tfstate/scratch/aks-kubeconfig"   # 0700 directory; the file holds no token (exec credential)
SIZES=(Standard_B2as_v2 Standard_B2als_v2 Standard_B2s_v2)
SIZES_ENV="$RUN/aks-sizes.env"                 # sizes the session ended up with (AllocationFailed fallbacks)
PROBE_TICKET="${TD_PROBE_TICKET:-9993}"   # probe and marker comments: an Active ticket the golden replay never reads
QUEUE_CD="${TD_QUEUE_CD:-$REPO/ops/ado/queue-cd.sh}"
CD_TIMEOUT="${TD_CD_TIMEOUT:-1800}"        # queueing -> approval -> deploy; the session's 3 h window holds it (docs/dev/azure-aks.md)
APPROVAL_FILE="$TD_W/logs/aks-cd-approval.txt"
AKS_LABEL_PREFIX="tdlab-aks"
PRICES="${TD_PRICES:-$(ls -1 "$REPO"/docs/evidence/aks-*/prices/prices.tsv 2>/dev/null | tail -1)}"   # this round's Retail Prices rows
# ttl and deadline (coordinator 2026-11-11, after the rerun prep): a dead host's reaper deletes at start + 3.25 h, not 4 h
TTL_HOURS="${TTL_HOURS:-3.25}"
SESSION_WINDOW_MIN=180                          # the K2 rows: start .. start + 3 h
DESTROY_BUDGET_MIN=15                           # 2026-11-11: the destroy took 666 s (aks-20261111/destroy-20261111T180623Z.txt)
RETIRE_BUDGET_S="${TD_RETIRE_BUDGET_S:-480}"    # retire-vm (stage aks, the VM deleted), planned ~6 min
DEADLINE_GRACE_S="${TD_DEADLINE_GRACE_S:-120}"  # a step still running this long after the deadline is stopped
SESSION_DEADLINE=""                             # epoch s, set by ak_session; empty = no deadline (a single command)
k() { kubectl --kubeconfig "$KCFG" "$@"; }
h() { helm --kubeconfig "$KCFG" "$@"; }
utc_ms() { date -u +%FT%T.%3NZ; }
secs_since() { python3 -I -c 'import sys, datetime as d; t = d.datetime.fromisoformat(sys.argv[1].replace("Z", "+00:00")); print(round((d.datetime.now(d.timezone.utc) - t).total_seconds(), 1))' "$1"; }
ttl_minutes() { python3 -I -c 'import sys; print(round(float(sys.argv[1]) * 60))' "$TTL_HOURS"; }        # `date -d` takes no "3.25 hours"
ttl_make_hours() { python3 -I -c 'import sys, math; print(math.ceil(float(sys.argv[1])))' "$TTL_HOURS"; }  # infra/azure.mk's integer (-var ttl_utc wins)

# --- session deadline ------------------------------------------------------------------------------
deadline_left() {  # seconds until the deadline that applies now; before retire-vm the VM delete's budget is kept free too
  [[ -n "$SESSION_DEADLINE" ]] || { echo 999999; return; }
  local d=$SESSION_DEADLINE; [[ -n "${VM_DOWN:-}" ]] || d=$((d - RETIRE_BUDGET_S))
  echo $((d - $(date +%s)))
}
past_deadline() { (( $(deadline_left) <= 0 )); }
# min(N, seconds left), at least 1 (`timeout 0` would mean no limit at all)
cap_s() { local l; l="$(deadline_left)"; ((l < $1)) && echo "$((l > 1 ? l : 1))" || echo "$1"; }
not_measured() {  # not_measured STEP [DETAIL] [STEP_EVIDENCE_DIR]: said in the session's list and in the step's own evidence
  local line; line="$(date -u +%FT%TZ) $1: not measured: session deadline${2:+ ($2)}"
  echo "$line" >> "${SESSION_EV:-$EVID}/not-measured.txt"; [[ -n "${3:-}" ]] && echo "$line" >> "$3/not-measured.txt"
  echo "$line"
}
kill_tree() {  # kill_tree PID: the process and everything under it, frozen first (no new children), TERM, KILL after 30 s
  local p c all=() todo=("$1") i
  while ((${#todo[@]})); do
    p="${todo[0]}"; todo=("${todo[@]:1}")
    kill -STOP "$p" 2>/dev/null || continue
    all+=("$p"); for c in $(pgrep -P "$p"); do todo+=("$c"); done
  done
  ((${#all[@]})) || return 0
  kill -TERM "${all[@]}" 2>/dev/null; kill -CONT "${all[@]}" 2>/dev/null
  for i in $(seq 1 30); do kill -0 "${all[@]}" 2>/dev/null || return 0; sleep 1; done
  kill -KILL "${all[@]}" 2>/dev/null || true
}

suffix() { printf '%s' "$(az account show --query id -o tsv)/$RG" | sha1sum | cut -c1-6; }
aks_fqdn() { echo "$AKS_LABEL_PREFIX-$(suffix).swedencentral.cloudapp.azure.com"; }

ak_outputs() {  # the AKS-side outputs (no VM needed)
  local j; j="$(terraform -chdir="$TF_DIR" output -json)"
  eval "$(python3 -I -c '
import json, shlex, sys
o = json.load(sys.stdin)
for k, n in (("key_vault_name", "KV"), ("sql_server_fqdn", "SQLSRV"), ("sql_database", "SQLDB"), ("sql_deploy_identity_client_id", "DEPLOY_ID"),
             ("aks_cluster", "AKS"), ("aks_csi_identity_client_id", "CSI_ID"), ("acr_login_server", "ACR"), ("acr_name", "ACR_NAME"),
             ("app_identity_name", "APP_ID_NAME"), ("app_identity_client_id", "APP_ID"), ("dataprotection_key_id", "DP_KEY"),
             ("servicebus_namespace", "SB_NS"), ("servicebus_queue", "SB_QUEUE"), ("vm_private_ip", "VM_IP"), ("vm_fqdn", "VM_FQDN"),
             ("tenant_id", "TENANT"), ("failed_requests_alert_id", "ALERT_ID"), ("log_analytics_workspace", "LAW"),
             ("aks_node_vm_size", "NODE_SIZE"), ("aks_subnet_cidr", "AKS_CIDR")):
    v = (o.get(k) or {}).get("value") or ""
    print(n + "=" + shlex.quote(str(v)))' <<<"$j")"
  [[ -n "$AKS" && -n "$APP_ID" && -n "$ACR" ]] || die "AKS outputs missing (is an AKS stage applied?)"
  AKS_FQDN="$(aks_fqdn)"
}

# ---------------------------------------------------------------------------------------------------
ak_cert() {
  local fqdn crt="$CA/aks-web.crt"; fqdn="$(aks_fqdn)"
  if [[ -f "$crt" ]] && openssl x509 -in "$crt" -noout -ext subjectAltName | grep -q "DNS:$fqdn" && openssl x509 -in "$crt" -noout -checkend $((30 * 86400)) >/dev/null; then
    log "certificate for $fqdn present"
  else
    TD_CERT_NAME=aks-web "$REPO/ops/onprem/new-lab-cert.sh" "$fqdn" localhost td-web.ticketdesk.svc
  fi
  openssl x509 -in "$crt" -noout -subject -issuer -serial -enddate -ext subjectAltName
}

ak_images() {
  local tag="${1:?tag}" push="${2:-}" n s
  ( cd "$REPO"
    s=$SECONDS; npm --prefix src/web ci --no-audit --no-fund >/dev/null 2>&1; npm --prefix src/web run build >/dev/null 2>&1; log "angular build $((SECONDS - s)) s"
    for n in web:TicketDesk.Web worker:TicketDesk.Worker db:TicketDesk.Database; do
      s=$SECONDS; docker build --platform linux/amd64 -f "src/modern/${n#*:}/Dockerfile" -t "ticketdesk-${n%%:*}:$tag" . > "$RUN/build-${n%%:*}-$tag.log" 2>&1 \
        || die "docker build ${n%%:*} failed ($RUN/build-${n%%:*}-$tag.log)"
      log "image ticketdesk-${n%%:*}:$tag $(docker image inspect "ticketdesk-${n%%:*}:$tag" --format '{{.Id}} {{.Size}}' | cut -c8-19,72-) in $((SECONDS - s)) s"
    done )
  [[ "$push" == --push ]] || return 0
  ak_outputs
  az acr login -n "$ACR_NAME" --only-show-errors >/dev/null
  for n in web worker db; do
    s=$SECONDS; docker tag "ticketdesk-$n:$tag" "$ACR/ticketdesk-$n:$tag"; docker push -q "$ACR/ticketdesk-$n:$tag" >/dev/null
    log "pushed $ACR/ticketdesk-$n:$tag in $((SECONDS - s)) s ($(docker image inspect "$ACR/ticketdesk-$n:$tag" --format '{{index .RepoDigests 0}}' | cut -d@ -f2 | cut -c1-19))"
  done
}

# apply a stage; on AllocationFailed retry with the next node size (AKS) or VM size (Windows), the sizes stay for later applies
ak_apply() {  # ak_apply STAGE [TARGET...]
  local stage="${1:?stage}" out="$EVID/apply-$1${2:+-targeted}-$(date -u +%Y%m%dT%H%M%SZ).txt" rc i node vm t0=$SECONDS att ttl_utc targets="" tgt
  shift
  for tgt in "$@"; do targets+=" -target=$tgt"; done
  [[ -f "$SIZES_ENV" ]] && . "$SIZES_ENV"
  node="${NODE_SIZE_CHOSEN:-${SIZES[0]}}"; vm="${VM_SIZE_CHOSEN:-${SIZES[0]}}"
  # one absolute ttl for every attempt: the group's ttl if it exists, else the session's (start + TTL_HOURS), else now +
  # TTL_HOURS (session of 2026-11-11: every retry moved the ttl and re-tagged all 70 resources, ~4 min per attempt)
  ttl_utc="$(az group show -n "$RG" --query tags.ttl -o tsv 2>/dev/null || true)"
  [[ -n "$ttl_utc" ]] || ttl_utc="${SESSION_TTL_UTC:-$(date -u -d "+$(ttl_minutes) minutes" +%Y-%m-%dT%H:%M:%SZ)}"
  for i in 1 2 3 4; do
    rc=0; att="$out.attempt$i"
    make -C "$REPO" --no-print-directory "azure-up-$stage" AZ_OWNER="$OWNER_TAG" TTL_HOURS="$(ttl_make_hours)" \
      AZ_EXTRA_ARGS="-var aks_node_vm_size=$node -var vm_size=$vm -var ttl_utc=$ttl_utc -no-color$targets" > "$att" 2>&1 || rc=$?
    cat "$att" >> "$out"; rm -f "$att.prev"; mv "$att" "$att.prev"
    ((rc == 0)) && break
    # only this attempt's errors count (2026-11-11: the cumulative log made an "already exists" look like AllocationFailed)
    if grep -q "AllocationFailed\|ZonalAllocationFailed\|SkuNotAvailable" "$att.prev"; then
      if grep -B5 -A5 "AllocationFailed\|SkuNotAvailable" "$att.prev" | grep -qi "kubernetes\|agentpool\|aks"; then
        node="$(next_size "$node")" || die "no node size left after AllocationFailed (see $out)"; log "AKS AllocationFailed: retry with node size $node"
      else
        vm="$(next_size "$vm")" || die "no VM size left after AllocationFailed (see $out)"; log "VM AllocationFailed: retry with VM size $vm"
        # Azure keeps a VM whose allocation failed (provisioningState Failed) outside Terraform's state: the next create
        # would stop on "already exists". Delete it, its OS disk if one was made, and its NIC (Terraform's: the refresh of the
        # retry sees it gone and creates it again, unbound) before the next size
        az vm delete -g "$RG" -n vm-tdlab-legacy --yes --only-show-errors >> "$out" 2>&1 || true
        az disk delete -g "$RG" -n osdisk-tdlab-legacy --yes --only-show-errors >> "$out" 2>&1 || true
        az network nic delete -g "$RG" -n nic-tdlab-legacy --only-show-errors >> "$out" 2>&1 || true
        echo "---- deleted the failed VM, its OS disk and NIC before the retry ($(utc_ms))" >> "$out"
      fi
      printf 'NODE_SIZE_CHOSEN=%s\nVM_SIZE_CHOSEN=%s\n' "$node" "$vm" > "$SIZES_ENV"
      echo "---- retry $i with node $node, VM $vm" >> "$out"
    else
      tail -25 "$att.prev"; die "apply $stage failed (see $out)"
    fi
  done
  rm -f "$out".attempt*.prev
  ((rc == 0)) || die "apply $stage failed after the fallbacks (see $out)"
  "$REPO/scripts/azure_ttl_sync.sh" >> "$out" 2>&1 || true
  printf 'NODE_SIZE_CHOSEN=%s\nVM_SIZE_CHOSEN=%s\n' "$node" "$vm" > "$SIZES_ENV"
  log "stage $stage${targets:+ ($targets )} applied in $((SECONDS - t0)) s (node $node, VM $vm): $(grep -ao 'Apply complete![^.]*' "$out" | tail -1)"
}
next_size() { local i; for i in "${!SIZES[@]}"; do [[ "${SIZES[$i]}" == "$1" && $((i + 1)) -lt ${#SIZES[@]} ]] && { echo "${SIZES[$((i + 1))]}"; return 0; }; done; return 1; }

ak_kube() {
  ak_outputs
  ( umask 077; az aks get-credentials -g "$RG" -n "$AKS" --file "$KCFG" --overwrite-existing --only-show-errors >/dev/null )
  # no kubelogin: an exec credential that asks the az login for a token of the AKS Entra server application
  k config set-credentials td-aks-az --exec-api-version=client.authentication.k8s.io/v1 --exec-command="$HERE/aks-token.sh" \
    --exec-interactive-mode=Never >/dev/null
  k config set-context "$(k config current-context)" --user=td-aks-az >/dev/null
  local i; for i in $(seq 1 30); do k get nodes >/dev/null 2>&1 && break; sleep 10; done
  k get nodes -o wide
  k describe node | sed -n '/Allocatable:/,/System Info:/p;/Allocated resources:/,/Events:/p' | grep -v "^ *$"
}

ak_ca_configmap() {
  local hsh; hsh="$(openssl x509 -hash -noout -in "$CA/ca.crt")"
  k get ns "$NS" >/dev/null 2>&1 || k create ns "$NS" >/dev/null
  k -n "$NS" create configmap td-lab-ca --from-file="$hsh.0=$CA/ca.crt" --dry-run=client -o yaml | k apply -f - >/dev/null
}

ak_values() { ak_outputs; write_values "$@"; }   # ak_values v1|v2 FILE: Helm values from the Terraform outputs
write_values() {  # the same from the variables ak_outputs sets (no secret in them; the admin CIDR is redacted in evidence)
  local which="$1" f="$2" tag home
  tag="$TAG_V1"; [[ "$which" == v1 ]] || tag="$TAG_V2"
  home="$(sed -n 's/^admin_source_cidrs *= *\[\(.*\)\]/\1/p' "$TD_W/.secrets/azure-lab.tfvars")"
  cat > "$f" <<EOF
image: { registry: "$ACR", tag: "$tag", dbTag: "$TAG_V2", pullPolicy: IfNotPresent }
azure:
  workloadIdentity: true
  tenantId: "$TENANT"
  appClientId: "$APP_ID"
  migratorClientId: "$DEPLOY_ID"
  keyVault: { enabled: true, name: "$KV", csiIdentityClientId: "$CSI_ID" }
database:
  connectionString: "Server=tcp:$SQLSRV,1433;Database=$SQLDB;Authentication=Active Directory Workload Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=30;Application Name=TicketDesk.Web"
  migrator:
    connectionString: "Server=tcp:$SQLSRV,1433;Database=$SQLDB;Authentication=Active Directory Workload Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;Application Name=td-db"
web:
  sharedCookie: $([[ "$which" == v1 ]] && echo true || echo false)
  keyVaultKeyId: "$DP_KEY"
  attachments: { store: Blob }
  serviceBus: { fullyQualifiedNamespace: "$SB_NS", queue: "$SB_QUEUE" }
  tls: { source: csi }
  service:
    type: LoadBalancer
    dnsLabel: "$AKS_LABEL_PREFIX-$(suffix)"
    loadBalancerSourceRanges: [$home]
EOF
  if [[ "$which" == v2 ]]; then
    # the pre-upgrade job contracts the schema (v2 needs it; it breaks the legacy app's writes: only once the VM is gone)
    printf '%s\n' "migrate:" "  args: [migrate, --contract, --legacy-retired]" >> "$f"
  fi
  if [[ "$which" == v1 ]]; then
    [[ -n "$VM_IP" && -n "$VM_FQDN" ]] || die "v1 needs the VM (stage cutover)"
    cat >> "$f" <<EOF
  legacy:
    enabled: true
    baseUrl: "https://$VM_FQDN/"
    hostAlias: { ip: "$VM_IP", hostname: "$VM_FQDN" }
    caConfigMap: td-lab-ca
EOF
  fi
}

ak_deploy() {
  local which="${1:?v1|v2}" vf extra=() t0=$SECONDS ev
  [[ "$which" == contract ]] && which=v2
  vf="$RUN/values-$which.yaml"
  ev="$EVID/deploy-$which-$(date -u +%Y%m%dT%H%M%SZ)"; mkdir -p "$ev"
  ak_outputs
  case "$which" in
    v1) ak_values v1 "$vf"; ak_ca_configmap ;;
    v2)
      # the contract (C0001 drops the dual-write trigger, adds CK_Tickets_AssignedDate) breaks the legacy app's writes
      vm_absent || die "v2 contracts the schema: retire the VM first (aks.sh retire-vm)"
      ak_values v2 "$vf" ;;
  esac
  sed -E 's/^(    loadBalancerSourceRanges: ).*/\1[<admin_source_cidrs>]/' "$vf" > "$ev/values.yaml"   # no home IP in the evidence
  echo "deploy $which start $(utc_ms)" > "$ev/deploy.txt"
  h upgrade --install "$RELEASE" "$CHART" -n "$NS" --create-namespace -f "$vf" "${extra[@]}" --wait --timeout 15m >> "$ev/deploy.txt" 2>&1 \
    || { k -n "$NS" get pods -o wide >> "$ev/deploy.txt" 2>&1; k -n "$NS" logs job/td-db-migrate --tail=50 >> "$ev/deploy.txt" 2>&1 || true; die "helm $which failed (see $ev)"; }
  echo "deploy $which end $(utc_ms) elapsed_s=$((SECONDS - t0))" >> "$ev/deploy.txt"
  { k -n "$NS" get pods,svc,jobs -o wide; k -n "$NS" logs job/td-db-migrate --tail=30 2>/dev/null || true; } >> "$ev/deploy.txt" 2>&1
  local i ip; for i in $(seq 1 60); do ip="$(k -n "$NS" get svc td-web -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null)"; [[ -n "$ip" ]] && break; sleep 5; done
  for i in $(seq 1 60); do getent hosts "$AKS_FQDN" >/dev/null && break; sleep 5; done
  log "deploy $which: $((SECONDS - t0)) s, service IP $ip, $AKS_FQDN resolves to $(getent hosts "$AKS_FQDN" | awk '{print $1}')" | tee -a "$ev/deploy.txt"
}

vm_absent() { [[ -z "$(az vm list -g "$RG" --query "[?name=='vm-tdlab-legacy'].name" -o tsv)" ]]; }

# what the session renders for a deploy, without a cluster: the template-equivalence proof against cd-aks.sh --template-out
ak_template() {  # ak_template VALUES_FILE OUT
  helm template "$RELEASE" "$CHART" -n "$NS" -f "$1" > "$2"
}

ado() { ( . "$REPO/ops/ado/ado-lib.sh"; ado_api "$@" ); }

ak_cd_preflight() {
  local pool agents remote paths=(ops/k8s/ticketdesk azure-pipelines/deploy/aks/cd-aks.sh azure-pipelines/cd.yml)
  pool="$(ado GET "/_apis/distributedtask/pools?poolName=lab-host&api-version=7.1" | jq -r '.value[0].id // empty')"
  [[ -n "$pool" ]] || { echo "preflight: no agent pool lab-host"; return 1; }
  agents="$(ado GET "/_apis/distributedtask/pools/$pool/agents?includeAssignedRequest=true&api-version=7.1" \
    | jq -r '.value[] | "\(.name) \(.status) enabled=\(.enabled) job=\(.assignedRequest.requestId // "none")"')"
  echo "agents: $agents"
  grep -q " online enabled=true job=none" <<<"$agents" || { echo "preflight: no free online agent (one self-hosted agent: it must be idle)"; return 1; }
  # runs not started or in progress: one that waits for an approval (kind-local run 16 may) holds no agent and is left alone;
  # one that waits for the agent or runs on it would delay the session's CD run
  local id st blocking="" waiting=""
  while read -r id st; do
    [[ -n "$id" ]] || continue
    if ado GET "_apis/build/builds/$id/timeline?api-version=7.1" | jq -e '[.records[] | select(.type == "Checkpoint.Approval" and .state == "inProgress")] | length > 0' >/dev/null 2>&1 \
       && ! ado GET "_apis/build/builds/$id/timeline?api-version=7.1" | jq -e '[.records[] | select(.type == "Job" and .state == "inProgress")] | length > 0' >/dev/null 2>&1; then
      waiting+="$id($st) "
    else
      blocking+="$id($st) "
    fi
  done < <(ado GET "_apis/build/builds?statusFilter=notStarted,inProgress&api-version=7.1" | jq -r '.value[] | "\(.id) \(.status)"')
  echo "runs waiting for an approval (left alone): ${waiting:-none}; runs queued for or running on the agent: ${blocking:-none}"
  [[ -z "$blocking" ]] || { echo "preflight: the single agent is (about to be) busy: $blocking"; return 1; }
  remote="$(ado GET "_apis/git/repositories/TicketDesk/refs?filter=heads/modernize&api-version=7.1" | jq -r '.value[0].objectId // empty')"
  [[ -n "$remote" ]] || { echo "preflight: no modernize on Azure Repos"; return 1; }
  if ! git -C "$REPO" cat-file -e "$remote^{commit}" 2>/dev/null; then
    ( . "$REPO/ops/ado/ado-lib.sh"; ado_git -C "$REPO" fetch -q "$ADO_GIT_URL" modernize ) || true
  fi
  git -C "$REPO" cat-file -e "$remote^{commit}" 2>/dev/null || { echo "preflight: Azure Repos modernize $remote is not in this repository"; return 1; }
  if ! git -C "$REPO" diff --quiet "$remote" HEAD -- "${paths[@]}"; then
    echo "preflight: Azure Repos modernize ${remote:0:8} differs from this session's ${paths[*]} (sync ws/aks first)"; return 1
  fi
  echo "preflight ok: agent free, nothing queued, Azure Repos modernize ${remote:0:8} has this session's chart and CD script"
}

helm_status() { h -n "$NS" status "$RELEASE" -o json 2>/dev/null | jq -r '.info.status // "none"' || echo none; }

# a release left pending-* (a CD run cancelled while helm ran) blocks every later upgrade: roll back to the last good revision
helm_recover() {
  local st rev
  st="$(helm_status)"; echo "$(utc_ms) release $RELEASE: $st"
  case "$st" in
    pending-*)
      rev="$(h -n "$NS" history "$RELEASE" -o json | jq -r '[.[] | select(.status == "deployed" or .status == "superseded")] | max_by(.revision) | .revision // empty')"
      [[ -n "$rev" ]] || { echo "no earlier good revision to roll back to"; return 1; }
      h -n "$NS" rollback "$RELEASE" "$rev" --wait --timeout 10m
      echo "$(utc_ms) rolled back to revision $rev: $(helm_status)" ;;
  esac
}

# the run must be finished (cancelled if need be) before the session deploys v2 itself: a late approval must not deploy
ak_cd_finish() {  # ak_cd_finish RUN [TRIES]: 15 s apart (40 = 10 min; 4 at the session deadline)
  local run="$1" i st
  [[ -n "$run" ]] || { echo "no run was queued"; return 0; }
  for i in $(seq 1 "${2:-40}"); do
    st="$(ado GET "_apis/build/builds/$run?api-version=7.1" | jq -r '"\(.status) \(.result // "-")"')" || st="unknown -"
    [[ "$st" == completed* ]] && { echo "$(utc_ms) run $run: $st"; return 0; }
    printf '{"status":"cancelling"}' | ado PATCH "_apis/build/builds/$run?api-version=7.1" - >/dev/null 2>&1 || true
    sleep 15
  done
  echo "$(utc_ms) run $run still not completed after $((${2:-40} * 15)) s ($st)"; return 1
}

v2_deployed() {
  [[ "$(helm_status)" == deployed ]] && [[ "$(k -n "$NS" get deploy td-web -o jsonpath='{.spec.template.spec.containers[0].image}')" == *":$TAG_V2" ]]
}

ak_deploy_v2_cd() {
  ak_outputs
  local ev="$EVID/deploy-v2-$(date -u +%Y%m%dT%H%M%SZ)" vf="$RUN/values-v2.yaml" rc=0 path="" reason="" run="" qp pp tf stop noted="" url
  local dl="" cd_timeout
  mkdir -p "$ev"; tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  vm_absent || die "v2 contracts the schema: retire the VM first (aks.sh retire-vm)"
  ak_values v2 "$vf"
  sed -E 's/^(    loadBalancerSourceRanges: ).*/\1[<admin_source_cidrs>]/' "$vf" > "$ev/values.yaml"
  # the probe covers the whole step: the wait for the approval, the CD (or the script) rollout, 20 s after it
  echo "https://$AKS_FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" --write-ticket "$PROBE_TICKET" > /dev/null 2>&1 &
  pp=$!
  echo "v2 step start $(utc_ms): release $RELEASE $(helm_status), image $(k -n "$NS" get deploy td-web -o jsonpath='{.spec.template.spec.containers[0].image}')" > "$ev/step.txt"
  if ak_cd_preflight > "$ev/preflight.txt" 2>&1; then
    # the CD timeout never reaches past the session deadline (queue-cd cancels the run itself at its timeout)
    cd_timeout="$(cap_s "$CD_TIMEOUT")"; echo "$(utc_ms) queue-cd --timeout $cd_timeout (TD_CD_TIMEOUT $CD_TIMEOUT, deadline in $(deadline_left) s)" >> "$ev/step.txt"
    "$QUEUE_CD" --acr "$ACR_NAME" --aks "$AKS" --tag "$TAG_V2" --values "$vf" --branch modernize --timeout "$cd_timeout" \
      --out "$ev/queue-cd" > "$ev/queue-cd.log" 2>&1 &
    qp=$!
    while kill -0 "$qp" 2>/dev/null; do
      if [[ -z "$noted" ]] && grep -q "approval requested" "$ev/queue-cd.log"; then
        url="$(grep -o 'https://dev.azure.com/[^ )]*buildId=[0-9]*' "$ev/queue-cd.log" | head -1)"
        printf '%s approval requested for the v2 deploy (aks-lab): %s\n' "$(date -u +%FT%TZ)" "${url:-run URL not found}" > "$APPROVAL_FILE"
        echo "$(utc_ms) approval requested, $APPROVAL_FILE written" >> "$ev/step.txt"; noted=1
      fi
      if past_deadline; then
        dl=1; echo "$(utc_ms) session deadline: queue-cd stopped" >> "$ev/step.txt"; kill_tree "$qp"; break
      fi
      sleep 5
    done
    wait "$qp" || rc=$?
    run="$(grep -o 'queued cd run [0-9]*' "$ev/queue-cd.log" | grep -o '[0-9]*$' | head -1)"
    echo "$(utc_ms) queue-cd exit $rc (run ${run:-none})" >> "$ev/step.txt"
    if ((rc == 0)) && v2_deployed; then
      path="cd (run $run, the user's approval)"
    elif [[ -n "$dl" ]] || past_deadline; then
      # the deadline: the run cancelled (a late approval must not deploy), no rollback and no script deploy (the destroy follows)
      dl=1; reason="session deadline (queue-cd exit $rc)"
      ak_cd_finish "$run" 4 >> "$ev/recovery.txt" 2>&1 || echo "$(utc_ms) run ${run:-none} not completed yet (see recovery.txt)" >> "$ev/step.txt"
      [[ -n "$noted" ]] && printf '%s run %s cancelled at the session deadline: no approval needed any more\n' "$(date -u +%FT%TZ)" "${run:-?}" >> "$APPROVAL_FILE"
    else
      reason="queue-cd exit $rc"; ((rc == 0)) && reason="queue-cd exit 0, but $RELEASE is $(helm_status) without image $TAG_V2"
      { ak_cd_finish "$run"; helm_recover; } >> "$ev/recovery.txt" 2>&1 || echo "$(utc_ms) recovery incomplete (see recovery.txt)" >> "$ev/step.txt"
    fi
  else
    reason="$(grep '^preflight' "$ev/preflight.txt" | tail -1)"
  fi
  if [[ -n "$dl" ]]; then
    path="none: $reason"; not_measured deploy-v2 "${noted:+approval requested, }run ${run:-none} cancelled, no script fallback" "$ev" >> "$ev/step.txt"
  elif [[ -z "$path" ]]; then
    echo "$(utc_ms) fallback: aks.sh deploy v2 ($reason)" >> "$ev/step.txt"
    if ak_deploy v2 >> "$ev/fallback.txt" 2>&1 && v2_deployed; then path="script (aks.sh deploy v2; $reason)"; else path="none: the fallback failed too ($reason)"; fi
  fi
  echo "$(utc_ms) v2 deployed by: $path" | tee -a "$ev/step.txt" > "$ev/path.txt"
  [[ -n "$dl" ]] || sleep 20; touch "$stop"; wait "$pp" || true; rm -f "$stop" "$tf"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" > "$ev/probe-summary.json"; xz -9f "$ev/probe.jsonl"
  python3 -I -c 'import json, sys; s = json.load(open(sys.argv[1])); print("probe through the v2 step: reads %d (%d failed), writes %d (%d failed); failure windows %s" % (s["read_requests"], s["failed_reads"], s["write_requests"], s["failed_writes"], json.dumps(s["failure_windows"])))' \
    "$ev/probe-summary.json" | tee -a "$ev/step.txt"
  { h -n "$NS" history "$RELEASE" --max 6; k -n "$NS" get pods -o wide; } >> "$ev/step.txt" 2>&1
  [[ "$path" != none* ]]
}

ak_smoke() {  # ak_smoke NAME [--read-only]
  ak_outputs
  local name="${1:-$(date -u +%Y%m%dT%H%M%SZ)}" rc=0 i
  for i in $(seq 1 30); do curl -fs -o /dev/null --cacert "$CA/ca.crt" "https://$AKS_FQDN/health/live" && break; sleep 5; done
  python3 -I "$REPO/ops/k8s/app_smoke.py" --base "https://$AKS_FQDN" ${2:+"$2"} --json "$EVID/aks-smoke-$name.json" > "$EVID/aks-smoke-$name.txt" 2>&1 || rc=$?
  tail -1 "$EVID/aks-smoke-$name.txt"; return $rc
}

ak_cutover() {
  ak_outputs; load_outputs   # VM_FQDN (Terraform) and FQDN (lift.sh) are the same name
  local ev="$EVID/cutover-aks-$(date -u +%Y%m%dT%H%M%SZ)" tf stop pp
  mkdir -p "$ev"; tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  declare -A M; mark() { M[$1]="$(utc_ms)"; log "MARK $1 ${M[$1]}"; }
  echo "https://$FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" --write-ticket "$PROBE_TICKET" > /dev/null 2>&1 &
  pp=$!
  mark probe_start; sleep 30
  echo "https://$AKS_FQDN" > "$tf"; mark switched   # the clients' switch (DNS/traffic manager in production): one write
  sleep 60; touch "$stop"; wait "$pp" || true; mark probe_end
  printf '{"probe_start":"%s","switched":"%s","probe_end":"%s","source":"https://%s (legacy, Azure VM)","target":"https://%s (new app, AKS, YARP fallback to the VM)"}\n' \
    "${M[probe_start]}" "${M[switched]}" "${M[probe_end]}" "$FQDN" "$AKS_FQDN" > "$ev/marks.json"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" --marks "$ev/marks.json" > "$ev/probe-summary.json"
  xz -9f "$ev/probe.jsonl"; rm -f "$tf"
  python3 -I -c 'import json, sys; s = json.load(open(sys.argv[1])); print("cutover VM -> AKS: reads %d (%d failed), writes %d (%d failed), sign-ins %d (%d failed); failure windows %s" % (s["read_requests"], s["failed_reads"], s["write_requests"], s["failed_writes"], s["signins"], s["failed_signins"], json.dumps(s["failure_windows"])))' "$ev/probe-summary.json" | tee "$ev/summary.txt"
}

ak_golden() {
  ak_outputs
  local tag="${1:?tag}" ev bin base rc=0 left stopped=""
  ev="$EVID/golden-$tag"; mkdir -p "$ev"
  dotnet build "$REPO/tools/Replay/TicketDesk.Replay.Cli" -c Release -v quiet -nologo >/dev/null
  bin="$REPO/tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay"
  left="$(deadline_left)"; ((left > 60)) || { not_measured "golden-$tag" "under 60 s left before the replay" "$ev"; return 1; }
  base="$(mktemp --suffix=.jsonl)"; xz -dc "$GOLDEN" > "$base"
  { echo "# $(now) td-replay replay against https://$AKS_FQDN/ (AKS, $tag)"; echo "# golden: $(sha256sum "$GOLDEN" | cut -c1-16)  git: $(git -C "$REPO" rev-parse --short HEAD)"; } > "$ev/replay.txt"
  # the replay ends at the session deadline at the latest (a partial candidate is not compared)
  timeout -k 20 "$(cap_s 999999)" "$bin" replay --from "$base" --target "https://$AKS_FQDN/" --insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" \
    --secrets-file "$TD_W/.secrets/td-seed.env" --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes "$REPO/docs/migration/routes.csv" \
    --out "$ev/candidate.jsonl" --run-id "aks-$tag" >> "$ev/replay.txt" 2>&1 || rc=$?
  if ((rc == 124 || rc == 137)) && past_deadline; then
    stopped=1; not_measured "golden-$tag" "replay stopped at the deadline, partial candidate kept, not compared" "$ev"
  else
    "$bin" compare --baseline "$base" --candidate "$ev/candidate.jsonl" --out-dir "$ev/compare" > "$ev/compare.txt" 2>&1 || true
  fi
  rm -f "$base"; [[ -f "$ev/candidate.jsonl" ]] && xz -9f "$ev/candidate.jsonl"
  ! grep -rlF "$(envval "$TD_W/.secrets/legacy-lab.env" TD_ADMIN_PASSWORD)" "$ev" || die "admin password found in $ev"
  [[ -z "$stopped" ]] || return 1
  tail -1 "$ev/compare.txt"
}

# background poller of the gateway's legacy reachability (its /health/ready "legacy" entry) and of a page, 1/s
poll_start() {  # poll_start OUT_JSONL STOPFILE
  ( while [[ ! -f "$2" ]]; do
      t="$(utc_ms)"; r="$(curl -s --max-time 5 --cacert "$CA/ca.crt" -w ' %{http_code}' "https://$AKS_FQDN/health/ready" || echo ' 000')"
      printf '{"t":"%s","code":"%s","legacy":"%s"}\n' "$t" "${r##* }" "$(grep -o '"name":"legacy","status":"[A-Za-z]*' <<<"$r" | sed 's/.*status":"//')"
      sleep 1
    done ) > "$1" 2>/dev/null &
  echo $!
}
poll_summary() { python3 -I -c '
import json, sys
rows = [json.loads(l) for l in open(sys.argv[1]) if l.strip()]
bad = [r for r in rows if r["code"] != "200"]
leg = [r for r in rows if r.get("legacy") and r["legacy"] != "Healthy"]
span = " (%s .. %s)" % (leg[0]["t"], leg[-1]["t"]) if leg else ""
print("%d polls of /health/ready through AKS, %d not 200, %d with the legacy check not Healthy%s" % (len(rows), len(bad), len(leg), span))' "$1"; }

ak_rotate_vm_cert() {
  ak_outputs
  local ev="$EVID/cert-rotation" stop pp; mkdir -p "$ev"; stop="$ev/.poll-stop"; rm -f "$stop"
  pp="$(poll_start "$ev/gateway-poll.jsonl" "$stop")"
  rp_rotate_cert || true
  sleep 10; touch "$stop"; wait "$pp" 2>/dev/null || true; rm -f "$stop"
  poll_summary "$ev/gateway-poll.jsonl" | tee "$ev/gateway-poll.txt"
}

served_serial() { echo | timeout 10 openssl s_client -connect "$1:443" -servername "$1" 2>/dev/null | openssl x509 -noout -serial 2>/dev/null | cut -d= -f2; }

ak_rotate_aks_cert() {
  ak_outputs
  local ev="$EVID/aks-cert-rotation" old new nopass t0 tf stop pp i rc=0
  mkdir -p "$ev"; tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  old="$(served_serial "$AKS_FQDN")"
  TD_CERT_NAME=aks-web-rotated "$REPO/ops/onprem/new-lab-cert.sh" "$AKS_FQDN" localhost td-web.ticketdesk.svc > "$ev/new-cert.txt" 2>&1
  nopass="$(mktemp --suffix=.pfx)"; chmod 600 "$nopass"
  openssl pkcs12 -export -inkey "$CA/aks-web-rotated.key" -in "$CA/aks-web-rotated.crt" -certfile "$CA/ca.crt" -name "TicketDesk lab" -passout pass: -out "$nopass"
  echo "https://$AKS_FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" > /dev/null 2>&1 &
  pp=$!; sleep 10
  t0="$(utc_ms)"; echo "rotation start $t0, served serial before: $old" > "$ev/rotation.txt"
  az keyvault certificate import --vault-name "$KV" -n td-aks-web -f "$nopass" --query '{version: id, thumbprint: x509ThumbprintHex, notBefore: attributes.notBefore}' -o json >> "$ev/rotation.txt" 2>&1 || rc=$?
  rm -f "$nopass"; ((rc == 0)) || { touch "$stop"; wait "$pp" || true; die "import failed (see $ev/rotation.txt)"; }
  echo "new version in Key Vault after $(secs_since "$t0") s" >> "$ev/rotation.txt"
  for i in $(seq 1 600); do new="$(served_serial "$AKS_FQDN")"; [[ -n "$new" && "$new" != "$old" ]] && break; sleep 1; done
  echo "served serial after: $new, picked up $(secs_since "$t0") s after the import (CSI rotation poll 2m + Tls:ReloadInterval 10 s)" >> "$ev/rotation.txt"
  sleep 15; touch "$stop"; wait "$pp" || true; rm -f "$stop" "$tf"
  printf '{"rotation_start":"%s"}\n' "$t0" > "$ev/marks.json"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" --marks "$ev/marks.json" > "$ev/probe-summary.json"; xz -9f "$ev/probe.jsonl"
  python3 -I -c 'import json, sys; s = json.load(open(sys.argv[1])); print("probe during the rotation: reads %d (%d failed)" % (s["read_requests"], s["failed_reads"]))' "$ev/probe-summary.json" >> "$ev/rotation.txt"
  { echo "# new certificate"; openssl x509 -in "$CA/aks-web-rotated.crt" -noout -serial -enddate; } >> "$ev/rotation.txt"
  cat "$ev/rotation.txt"
}

ak_retire_vm() {
  local ev="$EVID/retire-vm" t0=$SECONDS; mkdir -p "$ev"
  echo "retire start $(utc_ms)" > "$ev/retire.txt"
  ak_apply aks >> "$ev/retire.txt" 2>&1
  echo "retire end $(utc_ms) elapsed_s=$((SECONDS - t0)); VM exists: $(az vm show -g "$RG" -n vm-tdlab-legacy --query name -o tsv 2>/dev/null || echo no)" >> "$ev/retire.txt"
  tail -2 "$ev/retire.txt"
}

alerts_json() { az rest --method get --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/providers/Microsoft.AlertsManagement/alerts?api-version=2019-05-05-preview&targetResourceGroup=$RG&timeRange=1d" -o json; }
alert_state() {  # newest alert instance of the failed-requests rule: "condition start resolved"
  alerts_json | python3 -I -c '
import json, sys
rule = sys.argv[1].lower()
v = [a["properties"]["essentials"] for a in json.load(sys.stdin).get("value", []) if (a["properties"]["essentials"].get("alertRule") or "").lower() == rule]
v.sort(key=lambda e: e.get("startDateTime") or "")
e = v[-1] if v else {}
print(e.get("monitorCondition", "none"), e.get("startDateTime", "-"), e.get("monitorConditionResolvedDateTime", "-"))' "$ALERT_ID"
}

ak_alert_drill() {
  ak_outputs
  local ev="$EVID/alert-drill" tf stop pp st fault_at recover_at fired resolved i
  mkdir -p "$ev"; tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"
  local pe_cidr="10.42.2.0/24"   # snet-pe: the Azure SQL private endpoint
  cat > "$ev/deny-sql.yaml" <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata: { name: td-drill-deny-sql, namespace: $NS }
spec:
  podSelector: { matchLabels: { app.kubernetes.io/name: td-web } }
  policyTypes: [Egress]
  egress:
    - to: [{ ipBlock: { cidr: 0.0.0.0/0, except: ["$pe_cidr"] } }]
EOF
  echo "before: $(alert_state)" > "$ev/drill.txt"
  echo "https://$AKS_FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/probe.jsonl" > /dev/null 2>&1 &
  pp=$!; sleep 30
  fault_at="$(utc_ms)"; echo "fault $fault_at: NetworkPolicy deny egress to $pe_cidr for td-web, pods replaced (pooled connections)" >> "$ev/drill.txt"
  k apply -f "$ev/deny-sql.yaml" >> "$ev/drill.txt" 2>&1
  k -n "$NS" delete pod -l app.kubernetes.io/name=td-web --wait=false >> "$ev/drill.txt" 2>&1
  local dl=""
  for i in $(seq 1 120); do
    past_deadline && { dl="before the alert was seen Fired"; break; }
    st="$(alert_state)"; [[ "$st" == Fired* && "$(cut -d' ' -f2 <<<"$st")" > "${fault_at%.*}" ]] && break; sleep "$(cap_s 15)"
  done
  fired="$(utc_ms)"
  if [[ -z "$dl" ]]; then echo "alert seen Fired at $fired (polling 15 s): $st"; else echo "session deadline at $fired: alert not seen Fired (last state: ${st:-none})"; fi >> "$ev/drill.txt"
  recover_at="$(utc_ms)"; k delete -f "$ev/deny-sql.yaml" >> "$ev/drill.txt" 2>&1; echo "recover $recover_at: policy removed" >> "$ev/drill.txt"
  for i in $(seq 1 160); do
    [[ -z "$dl" ]] || break
    past_deadline && { dl="alert Fired, not seen Resolved"; echo "session deadline at $(utc_ms): alert not seen Resolved (last state: $st)" >> "$ev/drill.txt"; break; }
    st="$(alert_state)"; [[ "$st" == Resolved* ]] && break; sleep "$(cap_s 15)"
  done
  resolved="$(utc_ms)"; [[ -n "$dl" ]] || echo "alert seen Resolved at $resolved: $st" >> "$ev/drill.txt"
  touch "$stop"; wait "$pp" || true; rm -f "$stop" "$tf"
  if [[ -n "$dl" ]]; then   # the fault is removed (above), the probe stopped; no result.json: the times are not measured
    xz -9f "$ev/probe.jsonl" 2>/dev/null || true; not_measured alert-drill "$dl" "$ev" >> "$ev/drill.txt"; return 1
  fi
  alerts_json > "$ev/alerts.json" 2>/dev/null || true
  printf '{"fault":"%s","recover":"%s"}\n' "$fault_at" "$recover_at" > "$ev/marks.json"
  "${PROBE[@]}" summarize "$ev/probe.jsonl" --marks "$ev/marks.json" > "$ev/probe-summary.json"; xz -9f "$ev/probe.jsonl"
  python3 -I - "$ev" "$fault_at" "$recover_at" "$st" <<'PY' | tee -a "$ev/drill.txt"
import json, sys, datetime as d
ev, fault, recover, st = sys.argv[1:5]
p = lambda s: d.datetime.fromisoformat(s.replace("Z", "+00:00"))
cond, start, res = (st.split() + ["-", "-", "-"])[:3]
s = json.load(open(f"{ev}/probe-summary.json"))
out = {"fault": fault, "recover": recover, "alert_fired_utc": start, "alert_resolved_utc": res,
       "fault_to_fired_s": round((p(start) - p(fault)).total_seconds(), 1) if start != "-" else None,
       "recover_to_resolved_s": round((p(res) - p(recover)).total_seconds(), 1) if res != "-" else None,
       "fired_to_resolved_s": round((p(res) - p(start)).total_seconds(), 1) if "-" not in (start, res) else None,
       "probe_reads": s["read_requests"], "probe_failed_reads": s["failed_reads"], "failure_windows": s["failure_windows"]}
json.dump(out, open(f"{ev}/result.json", "w"), indent=1)
print(json.dumps(out))
PY
}

ak_kql() {  # ak_kql QUERY -> table
  local cid; cid="$(az monitor log-analytics workspace show -g "$RG" -n "$LAW" --query customerId -o tsv)"
  az monitor log-analytics query -w "$cid" --analytics-query "$1" -o table
}
ak_slo() {
  ak_outputs
  local ev="$EVID/slo" m="$RUN/session-marks.env" from to; mkdir -p "$ev"
  [[ -f "$m" ]] && . "$m"
  from="${MARK_V1:-$(date -u -d '-3 hours' +%FT%TZ)}"; to="${MARK_DRILL:-$(date -u +%FT%TZ)}"
  # availability: share of non-health requests that succeeded (5xx = failed); latency: p95 of their server duration
  local q="AppRequests | where TimeGenerated between (datetime($from) .. datetime($to)) | where AppRoleName == 'ticketdesk-web' and Name !has '/health/'
| extend phase = case(TimeGenerated < datetime(${MARK_V2:-$to}), 'v1 (gateway + VM)', 'v2 (no legacy)')
| summarize requests = count(), failed = countif(Success == false), p50_ms = round(percentile(DurationMs, 50), 1), p95_ms = round(percentile(DurationMs, 95), 1) by phase
| extend availability_pct = round(100.0 * (requests - failed) / requests, 3), slo_availability_99_5 = iff(100.0 * (requests - failed) / requests >= 99.5, 'met', 'missed'), slo_p95_500ms = iff(p95_ms <= 500, 'met', 'missed')"
  { echo "# $(now) KQL over the workspace (AppRequests of the new app, health probes excluded), $from .. $to"
    echo "# SLOs (lab): availability >= 99.5 % of requests, p95 server duration <= 500 ms"
    echo "$q"; echo; ak_kql "$q"
    echo; echo "# the drill window (expected to miss)"; ak_kql "AppRequests | where TimeGenerated >= datetime(${MARK_DRILL:-$to}) | where AppRoleName == 'ticketdesk-web' | summarize requests = count(), failed = countif(Success == false) by bin(TimeGenerated, 1m) | order by TimeGenerated asc"
  } > "$ev/slo.txt" 2>&1
  cat "$ev/slo.txt"
}

# the restored database's override as a values file: `--set` splits the connection string at the comma of
# "tcp:<server>,1433" (session of 2026-11-12: the pod got "Server=tcp:<server>" alone, "Login failed for user ''")
pitr_values() {  # pitr_values DATABASE FILE (SQLSRV from ak_outputs); JSON, which is YAML
  python3 -I - "$SQLSRV" "$1" > "$2" <<'PY'
import json, sys
srv, db = sys.argv[1:3]
cs = (f"Server=tcp:{srv},1433;Database={db};Authentication=Active Directory Workload Identity;Encrypt=True;"
      "TrustServerCertificate=False;Connect Timeout=30;Application Name=TicketDesk.Web")
print(json.dumps({"migrate": {"enabled": False}, "database": {"connectionString": cs}}, indent=1))
PY
}
ak_restore_drill() {
  ak_outputs
  local ev="$EVID/restore-drill" tf stop pp t_disaster t_req t_acc t_online t_smoke pitr="sqldb-ticketdesk-pitr" i rc out srv dl=""
  mkdir -p "$ev"; tf="$ev/.target"; stop="$ev/.stop"; rm -f "$stop"; srv="${SQLSRV%%.*}"
  # price first (Retail Prices API, this round's evidence): Basic single database
  awk -F'\t' '$1=="sql-basic"' "$PRICES" > "$ev/price-basic.tsv" 2>/dev/null || true
  echo "restored database: Basic ($(cut -f9,10 "$ev/price-basic.tsv" | head -1 | tr '\t' ' ')), deleted right after the drill" > "$ev/drill.txt"
  # markers: one probe comment per second on ticket $PROBE_TICKET for 2 minutes, then the "disaster" (stop)
  echo "https://$AKS_FQDN" > "$tf"
  "${PROBE[@]}" run --target-file "$tf" --stop-file "$stop" --out "$ev/markers.jsonl" --write-ticket "$PROBE_TICKET" > /dev/null 2>&1 &
  pp=$!; sleep "$(cap_s "${TD_RESTORE_MARKER_S:-120}")"; touch "$stop"; wait "$pp" || true; rm -f "$stop" "$tf"
  t_disaster="$(date -u +%FT%TZ)"; echo "disaster (marker writes stopped) $t_disaster" >> "$ev/drill.txt"
  t_req="$(utc_ms)"; echo "restore requested $t_req (point in time $t_disaster)" >> "$ev/drill.txt"
  for i in $(seq 1 30); do
    past_deadline && { dl="point in time not restored by the deadline (attempt $i)"; break; }
    # the restore call itself ends at the deadline at the latest (the copy it may leave is deleted first by the destroy)
    rc=0; out="$(timeout -k 30 "$(cap_s 999999)" az sql db restore -g "$RG" -s "$srv" -n "$SQLDB" --dest-name "$pitr" --time "$t_disaster" --edition Basic --service-objective Basic \
      --backup-storage-redundancy Local --tags project=ticketdesk owner="$OWNER_TAG" ttl="$(az group show -n "$RG" --query tags.ttl -o tsv)" est_usd_per_hour=0.0067 \
      --query '{name: name, status: status, sku: currentServiceObjectiveName}' -o json 2>&1)" || rc=$?
    echo "attempt $i $(utc_ms) rc=$rc: $(tr '\n' ' ' <<<"$out" | cut -c1-400)" >> "$ev/drill.txt"
    ((rc == 0)) && break
    ((rc == 124 || rc == 137)) && past_deadline && { dl="restore still running at the deadline (attempt $i)"; break; }
    # only "the point in time is not restorable yet" is worth waiting for (log backups every 5-10 min); anything else stops
    grep -qiE "point in time|restore point|restorable|not available|later than|too recent|backup" <<<"$out" || break
    sleep "$(cap_s 60)"
  done
  if [[ -z "$dl" ]] && ((rc == 0)) && past_deadline; then dl="restored database online, the app not switched to it"; fi
  if [[ -n "$dl" ]]; then
    xz -9f "$ev/markers.jsonl" 2>/dev/null || true; not_measured restore-drill "$dl; RTO and RPO not measured" "$ev" >> "$ev/drill.txt"; return 1
  fi
  ((rc == 0)) || die "restore failed (see $ev/drill.txt)"
  t_online="$(utc_ms)"; echo "restored database online $t_online" >> "$ev/drill.txt"
  # the app on the restored database (migrate job off: it is a copy of a migrated database), from a values file
  pitr_values "$pitr" "$RUN/values-pitr.yaml"; cp "$RUN/values-pitr.yaml" "$ev/values-pitr.yaml"
  h upgrade "$RELEASE" "$CHART" -n "$NS" --reuse-values -f "$RUN/values-pitr.yaml" \
    --wait --timeout "$(cap_s 600)s" >> "$ev/drill.txt" 2>&1 || {
      past_deadline || return 1
      not_measured restore-drill "the app on the restored database not ready by the deadline; RTO and RPO not measured" "$ev" >> "$ev/drill.txt"; return 1; }
  python3 -I "$REPO/ops/k8s/app_smoke.py" --base "https://$AKS_FQDN" --marker-ticket "$PROBE_TICKET" --json "$ev/smoke-restored.json" > "$ev/smoke-restored.txt" 2>&1 || true
  t_smoke="$(utc_ms)"; echo "app smoke on the restored database $t_smoke: $(tail -1 "$ev/smoke-restored.txt")" >> "$ev/drill.txt"
  # back to the original database (not past the deadline: the destroy follows), restored copy deleted at once (always)
  if past_deadline; then
    echo "$(utc_ms) switch back to $SQLDB skipped: session deadline (the destroy follows)" >> "$ev/drill.txt"
  else
    h upgrade "$RELEASE" "$CHART" -n "$NS" -f "$RUN/values-v2.yaml" --wait --timeout "$(cap_s 600)s" >> "$ev/drill.txt" 2>&1 || true   # back (the job: nothing to apply)
  fi
  az sql db delete -g "$RG" -s "$srv" -n "$pitr" --yes >> "$ev/drill.txt" 2>&1; echo "restored database deleted $(utc_ms)" >> "$ev/drill.txt"
  xz -9f "$ev/markers.jsonl"
  python3 -I - "$ev" "$t_disaster" "$t_req" "$t_online" "$t_smoke" <<'PY' | tee -a "$ev/drill.txt"
import json, lzma, sys, datetime as d
ev, disaster, req, online, smoke = sys.argv[1:6]
p = lambda s: d.datetime.fromisoformat(s.replace("Z", "+00:00"))
sm = json.load(open(f"{ev}/smoke-restored.json"))
writes = [json.loads(l) for l in lzma.open(f"{ev}/markers.jsonl.xz", "rt") if '"write"' in l]
last_ok = max((w["t"] for w in writes if w.get("ok")), default=None)
last_in_db = sm.get("last_marker_utc")
out = {"restore_point": disaster, "restore_requested": req, "restored_online": online, "app_smoke_ok": sm.get("ok"), "app_smoke_done": smoke,
       "rto_s (request -> app smoke on the restored database)": round((p(smoke) - p(req)).total_seconds(), 1),
       "last_marker_written_ok": last_ok, "last_marker_in_restored_db": last_in_db,
       "rpo_s (restore point - last marker in the restored database)": round((p(disaster) - p(last_in_db)).total_seconds(), 1) if last_in_db else None,
       "markers_lost (written ok after the last restored one)": sum(1 for w in writes if w.get("ok") and last_in_db and w["t"] > last_in_db)}
json.dump(out, open(f"{ev}/result.json", "w"), indent=1)
print(json.dumps(out))
PY
}

ak_destroy() {
  local ev="$EVID/destroy-$(date -u +%Y%m%dT%H%M%SZ).txt" t0=$SECONDS
  echo "destroy start $(utc_ms)" > "$ev"
  az sql db delete -g "$RG" -s "$(terraform -chdir="$TF_DIR" output -raw sql_server_fqdn 2>/dev/null | cut -d. -f1)" -n sqldb-ticketdesk-pitr --yes >> "$ev" 2>&1 || true
  # azure-down-foundation = stage none without the ledger check (a destroy must never be blocked by the cap)
  make -C "$REPO" --no-print-directory azure-down-foundation AZ_OWNER="$OWNER_TAG" >> "$ev" 2>&1 || echo "stage none failed" >> "$ev"
  if [[ "$(az group exists -n "$RG")" == true ]]; then
    echo "stage none left the group: make azure-down $(utc_ms)" >> "$ev"
    make -C "$REPO" --no-print-directory azure-down AZ_OWNER="$OWNER_TAG" >> "$ev" 2>&1
    make -C "$REPO" --no-print-directory azure-down-foundation AZ_OWNER="$OWNER_TAG" >> "$ev" 2>&1 || true
  fi
  echo "destroy end $(utc_ms) elapsed_s=$((SECONDS - t0)); RG exists=$(az group exists -n "$RG"); node RG exists=$(az group exists -n "$RG-aksnodes")" >> "$ev"
  tail -1 "$ev"
}

# ---------------------------------------------------------------------------------------------------
SESSION_STEPS=(
  "ledger        rows K2.* into docs/dev/azure-spend.md (before anything exists); total <= \$TD_LEDGER_CAP (15.00) or no start"
  "cert          lab CA certificate of the app endpoint"
  "apply-vm      -target=module.vm[0]: group, network, Key Vault, deploy identity, the VM (fallback sizes; stop if none)  ~6 min"
  "apply         the rest of stage cutover: SQL + DNS zone, monitoring, ACR, Service Bus, AKS, alert, roles   ~10 min"
  "kube          kubeconfig, node allocatable vs requests                                               ~1 min"
  "images        push ticketdesk-{web,worker,db}:\$TAG_V1 and :\$TAG_V2 (built before the session)      ~4 min"
  "vm-data       BACPAC -> Azure SQL, contained users, migrate, legacy deploy on the VM, legacy smoke   ~12 min"
  "deploy-v1     helm: migrate job, web (gateway to the VM over the VNet), worker; app smoke (= rebuild from zero)  ~5 min"
  "golden-v1     golden replay through AKS (before any session write: read-only smoke)                   ~6 min"
  "cutover       probe on the VM, switched to AKS: failed requests                                      ~2 min"
  "vm-cert       VM certificate rotation (R3 path) with the gateway's legacy check polled               ~4 min"
  "aks-cert      AKS certificate rotation from Key Vault: time to pick up, failed requests              ~4 min"
  "retire-vm     stage aks: the VM deleted (legacy retired; v1 keeps serving, its fallback unused)      ~6 min"
  "deploy-v2     CD pipeline (queue-cd.sh, the user's approval, timeout \$TD_CD_TIMEOUT 1800 s or the time to the deadline) or the script; pre-upgrade job td-db migrate --contract --legacy-retired, then v2   ~5-37 min"
  "golden-v2     golden replay on v2                                                                    ~6 min"
  "alert-drill   fault -> fired -> recover -> resolved                                                  ~18 min"
  "slo           availability and p95 latency KQL over the session                                      ~3 min"
  "restore-drill point-in-time restore: RTO, RPO; restored database deleted                             ~25 min"
  "destroy       stage none / RG delete (trap: also on any failure, stop or the deadline)               ~15 min"
  "deadline      start + 165 min (rows' 180 - destroy 15); steps before retire-vm until start + 157 min; a step still running 2 min after it is stopped; past it: \"not measured: session deadline\", destroy (retire-vm and destroy never skipped)"
)

SESSION_RUNS=(cert apply-vm apply kube push-v1 push-v2 vm-data deploy-v1 smoke-v1 golden-v1 cutover vm-cert aks-cert retire-vm
  deploy-v2 smoke-v2 golden-v2 alert-drill slo restore-drill)   # the names ak_session runs, in order (steps.txt)

ak_session() {
  # globals, not locals: the EXIT trap runs after this function returns (replatform.sh's lesson of 2026-11-10)
  SESSION_EV="$EVID/session-$(date -u +%Y%m%dT%H%M%SZ)"; mkdir -p "$SESSION_EV"
  sl="$SESSION_EV/session.log"; MARKS="$RUN/session-marks.env"; : > "$MARKS"
  SESSION_PID=$BASHPID; STEP_PIDF="$RUN/.session-step.pid"; DEADLINE_HIT=""; WD_PID=""; VM_DOWN=""
  step() { echo "$(utc_ms) SESSION $*" | tee -a "$sl"; }
  smark() { echo "MARK_$1=$(date -u +%FT%TZ)" >> "$MARKS"; }
  # a step: dropped once the deadline has passed; a watchdog stops it if it still runs DEADLINE_GRACE_S after the deadline
  run() {
    local name="$1" rc=0; shift
    if past_deadline; then DEADLINE_HIT=1; step "skip $name: session deadline"; not_measured "$name" "not started" >> "$sl"; return 1; fi
    step "start $name"; rm -f "$STEP_PIDF" "$STEP_PIDF.stopping"
    watchdog "$name" & WD_PID=$!
    ( echo "$BASHPID" > "$STEP_PIDF"; set -e; "$@" ) >> "$sl" 2>&1 || rc=$?
    stop_watchdog
    if ((rc == 0)); then step "ok $name"; else step "FAILED $name (rc $rc)"; return 1; fi
  }
  # a step that only takes things down (the VM delete): never dropped, never stopped
  down() {
    local name="$1" rc=0; shift
    step "start $name (take-down: runs whatever the deadline)"
    ( set -e; "$@" ) >> "$sl" 2>&1 || rc=$?
    if ((rc == 0)); then step "ok $name"; else step "FAILED $name (rc $rc)"; return 1; fi
  }
  watchdog() {
    local pid
    while kill -0 "$SESSION_PID" 2>/dev/null; do
      if (( $(deadline_left) <= -DEADLINE_GRACE_S )); then
        pid="$(cat "$STEP_PIDF" 2>/dev/null)"
        # only the step's own subshell (a child of the session), never a recycled pid
        [[ -n "$pid" && "$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ')" == "$SESSION_PID" ]] || return 0
        : > "$STEP_PIDF.stopping"
        step "deadline + ${DEADLINE_GRACE_S} s: $1 still running, its processes are stopped"
        not_measured "$1" "still running ${DEADLINE_GRACE_S} s after the deadline, stopped" >> "$sl"
        kill_tree "$pid"; return 0
      fi
      sleep 5
    done
  }
  stop_watchdog() {
    [[ -n "$WD_PID" ]] || return 0
    [[ -f "$STEP_PIDF.stopping" ]] || kill "$WD_PID" 2>/dev/null
    wait "$WD_PID" 2>/dev/null; WD_PID=""; rm -f "$STEP_PIDF" "$STEP_PIDF.stopping"
  }
  # steps.txt: every planned step's outcome; a step dropped or stopped at the deadline is also in not-measured.txt
  session_report() {
    local n out stopped nm="$SESSION_EV/not-measured.txt"
    # a step stopped at the deadline (or a failure after it) ends the session too: what follows was dropped by the deadline
    if [[ -s "$nm" ]] || past_deadline; then DEADLINE_HIT=1; fi
    stopped="$(grep -o ' SESSION FAILED [a-z0-9-]*' "$sl" | head -1 | awk '{print $3}')"
    for n in "${SESSION_RUNS[@]}"; do
      if grep -q " SESSION ok $n\$" "$sl"; then out=ok
      elif grep -q "^[^ ]* $n: not measured" "$nm" 2>/dev/null; then out="$(grep "^[^ ]* $n: not measured" "$nm" | head -1 | cut -d' ' -f3-)"
      elif grep -q " SESSION FAILED $n (" "$sl"; then out=FAILED
      elif [[ -n "$DEADLINE_HIT" ]]; then out="not measured: session deadline (not reached)"; echo "$(date -u +%FT%TZ) $n: $out" >> "$nm"
      else out="not run (the session stopped at ${stopped:-?})"; fi
      printf '%-14s %s\n' "$n" "$out"
    done > "$SESSION_EV/steps.txt"
    step "steps: $(grep -c ' ok$' "$SESSION_EV/steps.txt") ok, $(grep -c 'not measured' "$SESSION_EV/steps.txt") not measured, $(grep -c ' FAILED$' "$SESSION_EV/steps.txt") failed, $(grep -c 'not run' "$SESSION_EV/steps.txt") not run (steps.txt)"
  }
  finish() {
    set +eu; stop_watchdog; session_report
    step "destroy"; ak_destroy >> "$sl" 2>&1; step "destroyed: RG exists=$(az group exists -n "$RG")"
    python3 -I "$HERE/aks_ledger_rows.py" close "$LEDGER_PREFIX" "$(date -u '+%F %H:%M') (session end)" "session $(basename "$SESSION_EV")" | tee -a "$sl"
    cp "$MARKS" "$SESSION_EV/marks.env" 2>/dev/null
  }
  set +e
  [[ "$(az group exists -n "$RG")" == false ]] || { step "$RG exists: a session starts from the empty resource group"; return 1; }
  docker image inspect "ticketdesk-web:$TAG_V1" "ticketdesk-web:$TAG_V2" >/dev/null 2>&1 || { step "images $TAG_V1/$TAG_V2 missing (aks.sh images TAG)"; return 1; }
  # the CD path needs the single agent and Azure Repos modernize = this session's chart: checked before anything exists
  ak_cd_preflight | tee -a "$sl"; [[ "${PIPESTATUS[0]}" == 0 ]] || { step "CD preflight failed: no session (sync ws/aks, free the agent)"; return 1; }
  # one start time for the rows, the ttl and the deadline
  local t0e o; t0e="$(date +%s)"
  SESSION_END=$((t0e + SESSION_WINDOW_MIN * 60))
  SESSION_TTL_UTC="$(date -u -d "@$((t0e + $(ttl_minutes) * 60))" +%Y-%m-%dT%H:%M:%SZ)"
  # the ledger rows first (AGENT-RULES: before creating anything), then the cap check ($15.00 for the rerun)
  python3 -I "$HERE/aks_ledger_rows.py" add "$LEDGER_PREFIX" "$(date -u -d "@$t0e" '+%F %H:%M')" "$(date -u -d "@$SESSION_END" '+%H:%M')" | tee -a "$sl" || return 1
  if ! python3 -I "$REPO/scripts/azure_ledger.py" check | tee -a "$sl"; then
    python3 -I "$HERE/aks_ledger_rows.py" close "$LEDGER_PREFIX" "not created" "over the cap, session not started" | tee -a "$sl"; return 1
  fi
  SESSION_DEADLINE=$((SESSION_END - DESTROY_BUDGET_MIN * 60))
  o="${TD_SESSION_DEADLINE_OVERRIDE:-}"
  case "$o" in "") ;; +*) SESSION_DEADLINE=$((t0e + ${o#+})) ;; *) SESSION_DEADLINE="$(date -u -d "$o" +%s)" ;; esac
  {
    echo "start            $(date -u -d "@$t0e" +%FT%TZ)"
    echo "deadline         $(date -u -d "@$SESSION_DEADLINE" +%FT%TZ)  start + $(( (SESSION_DEADLINE - t0e) / 60 )) min $(( (SESSION_DEADLINE - t0e) % 60 )) s${o:+ (TD_SESSION_DEADLINE_OVERRIDE=$o)}; the rows' $SESSION_WINDOW_MIN min - destroy budget $DESTROY_BUDGET_MIN min"
    echo "before retire-vm $(date -u -d "@$((SESSION_DEADLINE - RETIRE_BUDGET_S))" +%FT%TZ)  (the VM delete's $RETIRE_BUDGET_S s kept free)"
    echo "watchdog         a step still running ${DEADLINE_GRACE_S} s after the deadline is stopped"
    echo "planned end      $(date -u -d "@$SESSION_END" +%FT%TZ)  (rows $LEDGER_PREFIX.*: destroy finished)"
    echo "ttl              $SESSION_TTL_UTC  (TTL_HOURS $TTL_HOURS: the reaper's time if the trap never runs)"
  } > "$SESSION_EV/deadline.txt"
  step "deadline $(date -u -d "@$SESSION_DEADLINE" +%FT%TZ) (start + $(( (SESSION_DEADLINE - t0e) / 60 )) min${o:+, override $o}), planned end $(date -u -d "@$SESSION_END" +%FT%TZ), ttl $SESSION_TTL_UTC (deadline.txt)"
  trap finish EXIT
  run cert ak_cert || return 1
  rm -f "$SIZES_ENV"   # sizes start from the first of the list (a previous session's fallback is not this one's)
  smark T0; step "rebuild from zero: start (empty resource group)"
  # the VM first (rerun decision 2026-11-11): only it and what it needs (group, network, Key Vault, the deploy identity), none
  # with a fixed charge; if every size fails AllocationFailed the session stops here, close to $0
  # (a deadline in a build step ends the session at once: the destroy deletes the VM with the rest)
  run apply-vm ak_apply cutover 'module.vm[0]' || return 1
  run apply ak_apply cutover || return 1
  run kube ak_kube || return 1
  run push-v1 ak_push "$TAG_V1" || return 1
  run push-v2 ak_push "$TAG_V2"
  run vm-data ak_vm_data || return 1
  smark V1; run deploy-v1 ak_deploy v1 || return 1
  # read-only smoke, then the golden first: the database is still exactly the imported baseline (no session write yet)
  run smoke-v1 ak_smoke v1 --read-only && { smark REBUILT; step "rebuild from zero: app smoke passed on AKS"; }
  run golden-v1 ak_golden v1
  run cutover ak_cutover
  run vm-cert ak_rotate_vm_cert
  run aks-cert ak_rotate_aks_cert
  # coordinator 2026-11-11: v2 needs the contracted schema and the contract breaks legacy writes, so the VM goes first
  down retire-vm ak_retire_vm || return 1
  VM_DOWN=1   # from here the deadline itself applies (no VM delete left to budget for)
  # the user's decision 2026-11-11: v2 through the Azure DevOps CD pipeline (approval), the script as the fallback
  smark V2; run deploy-v2 ak_deploy_v2_cd && run smoke-v2 ak_smoke v2
  run golden-v2 ak_golden v2
  smark DRILL; run alert-drill ak_alert_drill
  run slo ak_slo
  run restore-drill ak_restore_drill
  if [[ -s "$SESSION_EV/not-measured.txt" ]]; then step "session complete (session deadline reached: not-measured.txt)"; else step "session complete"; fi
}
ak_push() { ak_outputs; local tag="$1" n s; az acr login -n "$ACR_NAME" --only-show-errors >/dev/null
  for n in web worker db; do s=$SECONDS; docker tag "ticketdesk-$n:$tag" "$ACR/ticketdesk-$n:$tag"; docker push -q "$ACR/ticketdesk-$n:$tag" >/dev/null; log "pushed ticketdesk-$n:$tag in $((SECONDS - s)) s"; done; }
ak_vm_data() {
  ak_outputs
  local bacpac="$EXPORTS/azure-sql/ticketdesk-baseline-s-shared.bacpac"
  [[ -s "$bacpac" ]] || rp_bacpac
  ls "$ART"/td-sqltools-*.zip >/dev/null 2>&1 || rp_tools
  rp_stage --bacpac "$bacpac"
  . "$ART/staged.env"
  rp_provision "fetch+prereqs+site+sqltools+import+azsql-users+migrate+aks-sql-user+deploy+verify" "Database=azure" \
    "BacpacBlob=$BACPAC_BLOB" "BacpacSha256=$BACPAC_SHA" "SharedCookieKeyRing=database" "DataProtectionKeyId=$DP_KEY" \
    "AksAppIdentityName=$APP_ID_NAME" "AksAppClientId=$APP_ID"
  cmd_smoke "vm-azure-sql"
}

ak_dry_run() {
  echo "aks.sh session --dry-run: nothing is created. Steps, in order (TAG_V1=$TAG_V1, TAG_V2=$TAG_V2, TTL_HOURS=$TTL_HOURS):"
  printf '  %s\n' "${SESSION_STEPS[@]}"
  echo "checks:"
  printf '  ledger total now: %s USD; %s\n' "$(python3 -I "$REPO/scripts/azure_ledger.py" total)" "$(python3 -I "$REPO/scripts/azure_ledger.py" check)"
  local rows now; rows="$(python3 -I "$HERE/aks_ledger_rows.py" add "$LEDGER_PREFIX" start end --dry-run | tail -1 | awk '{print $3}')"; now="$(python3 -I "$REPO/scripts/azure_ledger.py" total)"
  printf '  rows %s.* the session adds first: %s USD -> total %s vs cap %s: %s\n' "$LEDGER_PREFIX" "$rows" "$(python3 -c "print(f'{$now + $rows:.2f}')")" "$TD_LEDGER_CAP" \
    "$(python3 -c "print('fits' if round($now + $rows, 2) <= $TD_LEDGER_CAP else 'OVER: the session would not start')")"
  printf '  window: rows %s.* start .. start + %s min; deadline start + %s min; before retire-vm start + %s min; ttl start + %s min (TTL_HOURS %s)\n' \
    "$LEDGER_PREFIX" "$SESSION_WINDOW_MIN" "$((SESSION_WINDOW_MIN - DESTROY_BUDGET_MIN))" "$(( ((SESSION_WINDOW_MIN - DESTROY_BUDGET_MIN) * 60 - RETIRE_BUDGET_S) / 60 ))" \
    "$(ttl_minutes)" "$TTL_HOURS"
  python3 -I "$HERE/aks_estimate.py" "$PRICES" --ttl-hours "$TTL_HOURS" | sed -n '/^worst case/,$p' | sed 's/^/  /' 
  printf '  resource group %s exists: %s\n' "$RG" "$(az group exists -n "$RG")"
  local t; for t in "$TAG_V1" "$TAG_V2"; do printf '  images :%s: %s\n' "$t" "$(docker image inspect "ticketdesk-web:$t" "ticketdesk-worker:$t" "ticketdesk-db:$t" >/dev/null 2>&1 && echo present || echo MISSING)"; done
  printf '  app certificate: %s\n' "$(openssl x509 -in "$CA/aks-web.crt" -noout -subject -enddate 2>/dev/null | tr '\n' ' ')"
  printf '  legacy package: %s\n' "$(ls -t "$ART"/td-legacy-publish-*.zip 2>/dev/null | head -1)"
  printf '  BACPAC: %s\n' "$(ls -la "$EXPORTS/azure-sql/ticketdesk-baseline-s-shared.bacpac" 2>/dev/null | awk '{print $5, $9}')"
  printf '  CD path: %s\n' "$(ak_cd_preflight 2>&1 | tail -1)"
  printf '  quota swedencentral: %s\n' "$(az vm list-usage -l swedencentral --query "[?name.value=='cores' || name.value=='standardBasv2Family'].[name.value, currentValue, limit]" -o tsv | tr '\t\n' ' /')"
  printf '  helm template v1/v2: '; local f; f="$(mktemp)"
  printf '%s\n' 'image: { registry: "acrtdlab000000.azurecr.io", tag: "x" }' 'azure: { workloadIdentity: true, tenantId: t, appClientId: a, migratorClientId: m, keyVault: { enabled: true, name: kv, csiIdentityClientId: c } }' \
    'database: { connectionString: "Server=tcp:s,1433;Database=d;Authentication=Active Directory Workload Identity", migrator: { connectionString: "x" } }' \
    'web: { sharedCookie: true, keyVaultKeyId: "https://kv.vault.azure.net/keys/td-dataprotection", attachments: { store: Blob }, serviceBus: { fullyQualifiedNamespace: sb.servicebus.windows.net }, tls: { source: csi }, service: { type: LoadBalancer, dnsLabel: x }, legacy: { enabled: true, baseUrl: "https://vm/", hostAlias: { ip: 10.42.1.4, hostname: vm }, caConfigMap: td-lab-ca } }' > "$f"
  helm template td "$CHART" -f "$f" >/dev/null && echo -n "v1 ok " || echo -n "v1 FAILED "
  helm template td "$CHART" -f "$f" --set web.legacy.enabled=null --set web.sharedCookie=false --set 'migrate.args={migrate,--contract,--legacy-retired}' > "$f.v2" \
    && ! grep -qE "Legacy__|SharedCookie" "$f.v2" && echo "v2 ok (no Legacy__/SharedCookie settings, contract job)" || echo "v2 FAILED"
  rm -f "$f.v2"
  rm -f "$f"
}

[[ "${BASH_SOURCE[0]}" == "$0" ]] || return 0
[[ $# -gt 0 ]] || { sed -n '2,40p' "$0"; exit 2; }
c="$1"; shift
case "$c" in
  cert) ak_cert ;;
  images) ak_images "$@" ;;
  apply) ak_apply "$@" ;;
  kube) ak_kube ;;
  vm-data) ak_vm_data ;;
  deploy) ak_deploy "$@" ;;
  deploy-v2-cd) ak_deploy_v2_cd ;;
  cd-preflight) ak_cd_preflight ;;
  smoke) ak_smoke "$@" ;;
  cutover) ak_cutover ;;
  golden) ak_golden "$@" ;;
  rotate-vm-cert) ak_rotate_vm_cert ;;
  rotate-aks-cert) ak_rotate_aks_cert ;;
  retire-vm) ak_retire_vm ;;
  alert-drill) ak_alert_drill ;;
  slo) ak_slo ;;
  restore-drill) ak_restore_drill ;;
  destroy) ak_destroy ;;
  session) if [[ "${1:-}" == --dry-run ]]; then ak_dry_run; else ak_session; fi ;;
  *) sed -n '2,40p' "$0"; exit 2 ;;
esac
