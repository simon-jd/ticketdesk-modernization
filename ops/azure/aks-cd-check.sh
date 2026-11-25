#!/usr/bin/env bash
# aks-cd-check.sh - local proofs for the AKS session's v2 deploy through the Azure DevOps CD pipeline (no Azure resource):
#
#   aks-cd-check.sh roles OUT_DIR       the AKS RBAC Writer / Admin definitions (az role definition list, read-only) against the
#                                       object kinds the chart installs on the v2 deploy: which role covers them
#   aks-cd-check.sh template OUT_DIR    the session's v2 values (from fake ids, the real admin CIDR file) rendered by aks.sh
#                                       (ak_template = what `deploy v2` upgrades with) and by cd-aks.sh --template-out: identical?
#   aks-cd-check.sh vm-guard OUT_DIR    cd-aks.sh and aks.sh with a stub az that reports vm-tdlab-legacy: both refuse before
#                                       any docker push or helm call; without the VM cd-aks.sh goes on to the push
#   aks-cd-check.sh fallback OUT_DIR    on kind (ops/k8s/kind.sh): v1 deployed as release ticketdesk; aks.sh's real v2 step
#                                       (ak_deploy_v2_cd) with a stub queue-cd that leaves the release pending-upgrade and times
#                                       out (124) -> recovery (rollback) -> script deploy; then a stub that deploys and exits 0
#                                       -> path "cd"; the probe runs through both
#   aks-cd-check.sh apply-fallback OUT_DIR   ak_apply with stub make/az: AllocationFailed -> the failed VM, its disk and NIC
#                                       deleted -> next size; one ttl for every attempt; AKS node fallback; no retry on other errors
#   aks-cd-check.sh deadline OUT_DIR    the session deadline with stubs (TD_SESSION_DEADLINE_OVERRIDE moves it up): the whole
#                                       ak_session with stub steps and the real v2 step, the deadline passing while the CD run
#                                       waits for the approval; a hung step stopped by the watchdog; the real restore drill (PITR
#                                       not restorable / restore hanging) and alert drill (alert never fires) at the deadline
#   aks-cd-check.sh restore-values OUT_DIR   the restore drill's connection string through helm template: the values file
#                                       (fix) renders it whole, the old --set cut it at the comma of "tcp:<server>,1433"
# Outputs with the admin CIDR or ids stay outside the repository; OUT_DIR gets summaries and hashes.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
. "$TD_W/.tools/env.sh"
SCRATCH="$(mktemp -d)"; chmod 700 "$SCRATCH"; trap 'rm -rf "$SCRATCH"' EXIT
FAKE_ACR=acrtdlab36d2d4; FAKE_AKS=aks-tdlab-swedencentral; TAG=aks-v2b

fake_vars() {  # the variables ak_outputs would set, with made-up ids (no secret exists among them anyway)
  ACR="$FAKE_ACR.azurecr.io"; ACR_NAME="$FAKE_ACR"; AKS="$FAKE_AKS"; TENANT=00000000-0000-0000-0000-00000000000a
  APP_ID=00000000-0000-0000-0000-00000000000b; DEPLOY_ID=00000000-0000-0000-0000-00000000000c; KV=kv-tdlab-36d2d4
  CSI_ID=00000000-0000-0000-0000-00000000000d; SQLSRV=sql-tdlab-36d2d4.database.windows.net; SQLDB=sqldb-ticketdesk
  DP_KEY=https://kv-tdlab-36d2d4.vault.azure.net/keys/td-dataprotection; SB_NS=sb-tdlab-36d2d4.servicebus.windows.net
  SB_QUEUE=ticket-notifications; AKS_FQDN=tdlab-aks-36d2d4.swedencentral.cloudapp.azure.com
}

cmd_roles() {
  local out="$1" f; mkdir -p "$out"
  for r in "Azure Kubernetes Service RBAC Writer" "Azure Kubernetes Service RBAC Admin"; do
    az role definition list --name "$r" -o json > "$out/$(tr ' ' '-' <<<"$r").json"
  done
  ( export TD_EVIDENCE="$SCRATCH/ev"; . "$HERE/aks.sh"; fake_vars; write_values v2 "$SCRATCH/v2.yaml"; ak_template "$SCRATCH/v2.yaml" "$SCRATCH/v2-render.yaml" )
  python3 -I - "$out" "$SCRATCH/v2-render.yaml" <<'PY' | tee "$out/coverage.txt"
import json, sys, re, fnmatch
out, render = sys.argv[1], sys.argv[2]
kinds = sorted(set(re.findall(r"^apiVersion: (\S+)\nkind: (\S+)", open(render).read(), re.M)))
# what helm itself reads and writes beside the manifests: release Secrets, hook cleanup, --wait watches
extra = [("v1", "Secret"), ("v1", "Pod"), ("apps/v1", "ReplicaSet"), ("v1", "Event")]
plural = {"Secret": "secrets", "Pod": "pods", "ReplicaSet": "replicasets", "Event": "events", "ServiceAccount": "serviceaccounts",
          "Service": "services", "Deployment": "deployments", "Job": "jobs", "SecretProviderClass": "secretproviderclasses", "ConfigMap": "configmaps"}
def action(api, kind, verb):
    group = api.split("/")[0] if "/" in api else ""
    return f"Microsoft.ContainerService/managedClusters/{group + '/' if group else ''}{plural[kind]}/{verb}"
for name in ("Azure-Kubernetes-Service-RBAC-Writer", "Azure-Kubernetes-Service-RBAC-Admin"):
    d = json.load(open(f"{out}/{name}.json"))[0]["permissions"][0]
    allowed, denied = d["dataActions"], d["notDataActions"]
    def ok(a): return any(fnmatch.fnmatchcase(a, p) for p in allowed) and not any(fnmatch.fnmatchcase(a, p) for p in denied)
    print(f"== {name.replace('-', ' ')}")
    missing = []
    for api, kind in kinds + extra:
        for verb in ("read", "write", "delete"):
            if not ok(action(api, kind, verb)):
                missing.append(action(api, kind, verb))
        print(f"  {api:42} {kind:22} {'read/write/delete' if all(ok(action(api, kind, v)) for v in ('read','write','delete')) else 'MISSING ' + ','.join(v for v in ('read','write','delete') if not ok(action(api, kind, v)))}")
    print(f"  -> {'covers every kind' if not missing else 'does not cover: ' + ', '.join(sorted(set(missing)))}")
print("object kinds of the v2 render: " + ", ".join(f"{k} ({a})" for a, k in kinds))
PY
}

cmd_template() {
  local out="$1"; mkdir -p "$out"
  ( export TD_EVIDENCE="$SCRATCH/ev"; . "$HERE/aks.sh"; fake_vars; write_values v2 "$SCRATCH/v2.yaml"; ak_template "$SCRATCH/v2.yaml" "$SCRATCH/session.yaml" )
  "$REPO/azure-pipelines/deploy/aks/cd-aks.sh" --acr "$FAKE_ACR" --aks "$FAKE_AKS" --tag "$TAG" --values "$SCRATCH/v2.yaml" --template-out "$SCRATCH/cd.yaml"
  {
    echo "# $(date -u +%FT%TZ) v2 render: aks.sh ak_template (helm template ticketdesk ops/k8s/ticketdesk -n ticketdesk -f values-v2.yaml, the arguments of"
    echo "# ak_deploy v2) vs cd-aks.sh --template-out (its upgrade arguments: --set image.registry/--set image.tag + -f the same host values file)"
    echo "session render: $(wc -l < "$SCRATCH/session.yaml") lines, sha256 $(sha256sum < "$SCRATCH/session.yaml" | cut -c1-16)"
    echo "CD render:      $(wc -l < "$SCRATCH/cd.yaml") lines, sha256 $(sha256sum < "$SCRATCH/cd.yaml" | cut -c1-16)"
    if diff -q "$SCRATCH/session.yaml" "$SCRATCH/cd.yaml" >/dev/null; then echo "identical"; else echo "DIFFERENT:"; diff -u "$SCRATCH/session.yaml" "$SCRATCH/cd.yaml" | grep -v 'loadBalancerSourceRanges' | head -40; fi
    echo "kinds: $(grep -c '^kind:' "$SCRATCH/session.yaml") objects ($(grep '^kind:' "$SCRATCH/session.yaml" | sort | uniq -c | awk '{print $3 "x" $1}' | tr '\n' ' '))"
    echo "contract job args: $(grep -A3 'args:' "$SCRATCH/session.yaml" | grep -o -- '- "*--contract"*' | head -1) ; Legacy__/SharedCookie settings: $(grep -cE 'Legacy__|SharedCookie' "$SCRATCH/session.yaml" || true)"
  } | tee "$out/template.txt"
  grep -q '^identical$' "$out/template.txt"
}

cmd_vm_guard() {
  local out="$1" stub="$SCRATCH/stub" rc; mkdir -p "$out" "$stub"
  # stubs: every call is logged; az answers the VM list from $STUB_VMS, docker/helm/kubectl only log
  printf '%s\n' '#!/usr/bin/env bash' 'echo "az $*" >> "$STUB_LOG"' \
    'case "$*" in' '  "vm list"*) [[ -n "${STUB_VMS:-}" ]] && echo "$STUB_VMS"; exit 0 ;;' '  "account show"*) echo stub ;;' '  *) exit 0 ;;' 'esac' > "$stub/az"
  for t in docker helm kubectl; do printf '%s\n' '#!/usr/bin/env bash' "echo \"$t \$*\" >> \"\$STUB_LOG\"" 'exit 0' > "$stub/$t"; done
  chmod +x "$stub"/*
  {
    echo "# $(date -u +%FT%TZ) cd-aks.sh with a stub az (logs every call), docker/helm/kubectl stubs"
    : > "$SCRATCH/calls"; rc=0
    STUB_LOG="$SCRATCH/calls" STUB_VMS=vm-tdlab-legacy PATH="$stub:$PATH" "$REPO/azure-pipelines/deploy/aks/cd-aks.sh" --acr x --aks y --tag t --out "$SCRATCH/o1" > "$SCRATCH/o1.txt" 2>&1 || rc=$?
    echo "VM present: exit $rc; calls: $(tr '\n' ';' < "$SCRATCH/calls"); $(grep -o 'vm-tdlab-legacy still exists[^;]*' "$SCRATCH/o1.txt" | head -1)"
    echo "  docker or helm called: $(grep -cE '^(docker|helm) ' "$SCRATCH/calls" || true)"
    : > "$SCRATCH/calls"; rc=0
    STUB_LOG="$SCRATCH/calls" STUB_VMS= PATH="$stub:$PATH" timeout 30 "$REPO/azure-pipelines/deploy/aks/cd-aks.sh" --acr x --aks y --tag t --out "$SCRATCH/o2" > "$SCRATCH/o2.txt" 2>&1 || rc=$?
    echo "VM absent: exit $rc (stubs end it later); the first docker call: $(grep -m1 '^docker ' "$SCRATCH/calls" || echo none)"
    echo "# aks.sh (the same rule in deploy v2 and in the CD step) with the same stub az"
    : > "$SCRATCH/calls"
    ( export TD_EVIDENCE="$SCRATCH/ev-guard"; . "$HERE/aks.sh"; ak_outputs() { fake_vars; }; set +e
      STUB_LOG="$SCRATCH/calls" STUB_VMS=vm-tdlab-legacy PATH="$stub:$PATH"; export STUB_LOG STUB_VMS PATH
      ( ak_deploy v2 ) > "$SCRATCH/a1.txt" 2>&1; echo "ak_deploy v2, VM present: exit $? ($(grep -o 'v2 contracts the schema[^)]*)' "$SCRATCH/a1.txt" | head -1))"
      ( ak_deploy_v2_cd ) > "$SCRATCH/a2.txt" 2>&1; echo "ak_deploy_v2_cd, VM present: exit $? ($(grep -o 'v2 contracts the schema[^)]*)' "$SCRATCH/a2.txt" | head -1))" )
    echo "  helm called by aks.sh: $(grep -c '^helm ' "$SCRATCH/calls" || true)"
  } | tee "$out/vm-guard.txt"
}

cmd_fallback() {
  local out="$1" kc="$SCRATCH/kind.kubeconfig" v1 v2; mkdir -p "$out"
  "$REPO/ops/k8s/kind.sh" up > "$SCRATCH/kind-up.txt" 2>&1
  TD_KIND_TAG=aks-v1 "$REPO/ops/k8s/kind.sh" db | tail -1
  "$REPO/ops/k8s/kind.sh" images aks-v1 > /dev/null; "$REPO/ops/k8s/kind.sh" images aks-v2 > /dev/null; "$REPO/ops/k8s/kind.sh" secrets | tail -1
  kind get kubeconfig --name td-aks > "$kc"; chmod 600 "$kc"
  v1="$SCRATCH/v1.yaml"; v2="$SCRATCH/v2.yaml"
  # ops/k8s/values-kind.yaml's content, one file per version (YAML cannot repeat a top-level key)
  kindv() { printf '%s\n' "image: { tag: $1, pullPolicy: Never }" "database: { secretName: td-db, migrator: { secretName: td-db-migrator } }" "web:" \
    "  aspnetcoreEnvironment: Production" "  attachments: { store: Local }" "  serviceBus: { secretName: td-sb }" \
    "  tls: { source: secret, secretName: td-tls }" "  service: { type: NodePort, nodePort: 30443 }" "${@:2}"; }
  kindv aks-v1 "  legacy: { enabled: false }" > "$v1"
  kindv aks-v2 "migrate:" "  args: [migrate, --contract, --legacy-retired]" > "$v2"
  helm --kubeconfig "$kc" upgrade --install ticketdesk "$REPO/ops/k8s/ticketdesk" -n ticketdesk -f "$v1" --wait --timeout 5m > /dev/null
  echo "v1 deployed as release ticketdesk: $(helm --kubeconfig "$kc" -n ticketdesk status ticketdesk -o json | jq -r .info.status)"
  # stub queue-cd: (timeout) a CD helm upgrade killed half-way, the run "cancelled", exit 124; (ok) a full upgrade, exit 0
  cat > "$SCRATCH/queue-cd-timeout" <<EOF
#!/usr/bin/env bash
echo "\$(date -u +%FT%TZ) queued cd run 999 (https://dev.azure.com/<ado-org>/TicketDesk/_build/results?buildId=999) branch modernize (stub)"
echo "\$(date -u +%FT%TZ) approval requested: waiting for the user"; sleep 8
helm --kubeconfig "$kc" upgrade ticketdesk "$REPO/ops/k8s/ticketdesk" -n ticketdesk -f "$v2" --wait --timeout 5m > /dev/null 2>&1 & hp=\$!
for i in \$(seq 1 60); do [[ "\$(helm --kubeconfig "$kc" -n ticketdesk status ticketdesk -o json 2>/dev/null | jq -r .info.status)" == pending-upgrade ]] && break; sleep 0.5; done
sleep 2; kill -9 \$hp; echo "\$(date -u +%FT%TZ) timeout after 1800s: cancelling run 999 (stub: the agent's helm was killed mid-upgrade)"; exit 124
EOF
  cat > "$SCRATCH/queue-cd-ok" <<EOF
#!/usr/bin/env bash
echo "\$(date -u +%FT%TZ) queued cd run 998 (https://dev.azure.com/<ado-org>/TicketDesk/_build/results?buildId=998) branch modernize (stub)"
echo "\$(date -u +%FT%TZ) approval requested: waiting for the user"; sleep 5
helm --kubeconfig "$kc" upgrade ticketdesk "$REPO/ops/k8s/ticketdesk" -n ticketdesk -f "$v2" --wait --timeout 5m > /dev/null 2>&1 || exit 1
echo "\$(date -u +%FT%TZ) run 998 completed: succeeded"; exit 0
EOF
  chmod +x "$SCRATCH"/queue-cd-*
  for case in timeout ok; do
    (
      export TD_EVIDENCE="$SCRATCH/ev-$case" TD_QUEUE_CD="$SCRATCH/queue-cd-$case" TAG_V2=aks-v2
      . "$HERE/aks.sh"
      KCFG="$kc"; APPROVAL_FILE="$SCRATCH/approval-$case.txt"
      ak_outputs() { fake_vars; AKS_FQDN=localhost:18443; }
      vm_absent() { true; }
      ak_values() { cp "$v2" "$2"; }
      ak_cd_preflight() { echo "preflight ok (stub)"; }
      ak_cd_finish() { echo "$(utc_ms) run $1: cancelled and completed (stub)"; }
      ak_deploy() { h upgrade --install "$RELEASE" "$CHART" -n "$NS" -f "$v2" --wait --timeout 10m; }   # deploy v2 without the AKS load balancer wait
      set +e; ( set -e; ak_deploy_v2_cd ); echo "ak_deploy_v2_cd ($case) exit $?"
      d="$(ls -d "$TD_EVIDENCE"/deploy-v2-* | head -1)"
      { echo "## case $case: stub queue-cd $(basename "$TD_QUEUE_CD")"; cat "$d/step.txt"; echo "-- recovery.txt"; cat "$d/recovery.txt" 2>/dev/null || echo "(none)"
        echo "-- approval file: $(cat "$APPROVAL_FILE" 2>/dev/null || echo missing)"
        echo "-- failed probe requests"; xz -dc "$d/probe.jsonl.xz" | grep '"ok": false' | cut -c1-300; } | sed "s|$SCRATCH|<scratch>|g" > "$out/case-$case.txt"
      cp "$d/probe.jsonl.xz" "$out/probe-$case.jsonl.xz"
    )
    tail -4 "$out/case-$case.txt"
  done
  "$REPO/ops/k8s/kind.sh" down > /dev/null 2>&1 || true
}

cmd_apply_fallback() {
  # ak_apply with stub make/az (shell functions shadow the commands): what each attempt asks Terraform for, what is deleted
  # between attempts, and when it gives up. Scenarios: VM fails twice then fits; VM fails on every size; AKS node fails
  # once; another error (no retry). Reproduces 2026-11-11 too: without the delete, attempt 2 would see "already exists".
  local out="$1"; mkdir -p "$out"
  local scen
  for scen in vm-2-fail vm-all-fail aks-1-fail other-error; do
    (
      export TD_EVIDENCE="$SCRATCH/ev-$scen"; . "$HERE/aks.sh"
      SIZES_ENV="$SCRATCH/sizes-$scen.env"; rm -f "$SIZES_ENV"
      calls="$SCRATCH/calls-$scen"; : > "$calls"; echo 0 > "$SCRATCH/n-$scen"; echo 0 > "$SCRATCH/vmgone-$scen"
      az() {
        case "$1 $2" in
          "group show") return 1 ;;   # the group does not exist yet (first apply)
          "vm delete") echo "az vm delete $*" >> "$calls"; echo 1 > "$SCRATCH/vmgone-$scen" ;;
          "disk delete"|"network nic") echo "az $1 $2 $3" >> "$calls" ;;
          *) return 0 ;;
        esac
      }
      make() {
        local n extra; n=$(( $(cat "$SCRATCH/n-$scen") + 1 )); echo $n > "$SCRATCH/n-$scen"
        extra="$(printf '%s\n' "$@" | sed -n 's/^AZ_EXTRA_ARGS=//p')"
        echo "make attempt $n: $(grep -o -- '-var vm_size=[^ ]*' <<<"$extra" | cut -d' ' -f2) $(grep -o 'aks_node_vm_size=[^ ]*' <<<"$extra") $(grep -o 'ttl_utc=[^ ]*' <<<"$extra") $(grep -o -- '-target=[^ ]*' <<<"$extra")" >> "$calls"
        if (( n > 1 )) && [[ "$scen" == vm-* ]] && [[ "$(cat "$SCRATCH/vmgone-$scen")" != 1 ]]; then
          echo 'Error: a resource with the ID ".../virtualMachines/vm-tdlab-legacy" already exists - to be managed via Terraform this resource needs to be imported'; return 1
        fi
        echo 0 > "$SCRATCH/vmgone-$scen"
        case "$scen:$n" in
          vm-2-fail:1|vm-2-fail:2|vm-all-fail:*)
            printf '%s\n' 'Error: creating Windows Virtual Machine (... "vm-tdlab-legacy"): polling failed:' 'Status: "AllocationFailed"' ; return 1 ;;
          aks-1-fail:1)
            printf '%s\n' 'Error: creating Kubernetes Cluster (... "aks-tdlab-swedencentral"): agentpool "system":' 'Code: "AllocationFailed"'; return 1 ;;
          other-error:1)
            echo 'Error: creating Key Vault: AuthorizationFailed'; return 1 ;;
          *) echo "Apply complete! Resources: 20 added, 0 changed, 0 destroyed."; return 0 ;;
        esac
      }
      set +e; ( set -e; ak_apply cutover 'module.vm[0]' ) > "$SCRATCH/out-$scen.txt" 2>&1; rc=$?
      { echo "## scenario $scen: ak_apply exit $rc"; cat "$calls"; grep -E "AllocationFailed: retry|no VM size left|no node size left|apply cutover failed|applied in" "$SCRATCH/out-$scen.txt" | sed "s|$SCRATCH|<scratch>|g"
        echo "ttl values across attempts: $(grep -o 'ttl_utc=[^ ]*' "$calls" | sort -u | wc -l) distinct"; } | tee "$out/$scen.txt"
    )
  done
}

# --- session deadline: stubs only (no Azure, ADO or ledger call; the ledger scripts are intercepted) -------------------------
dl_stubs() {  # dl_stubs SCRATCH_DIR, after `. aks.sh` in a subshell
  local sc="$1"
  RUN="$sc/run"; mkdir -p "$RUN" "$sc/bin"; SIZES_ENV="$RUN/aks-sizes.env"; APPROVAL_FILE="$sc/aks-cd-approval.txt"
  STUB_LOG="$sc/calls.txt"; : > "$STUB_LOG"
  ak_outputs() { fake_vars; }
  az() { echo "az $*" >> "$STUB_LOG"; case "$1 $2" in "group exists") echo false ;; "group show") echo 2026-11-12T05:15:00Z ;; esac; return 0; }
  docker() { return 0; }
  ak_cd_preflight() { echo "preflight ok (stub)"; }
  python3() {
    case "$*" in
      *aks_ledger_rows.py*|*azure_ledger.py*) echo "stub ledger, nothing written: ${*##*/azure/}" | tee -a "$STUB_LOG"; return 0 ;;
      *) command python3 "$@" ;;
    esac
  }
  k() { echo "kubectl $*" >> "$STUB_LOG"; }
  h() { echo "helm $*" >> "$STUB_LOG"; }
  probe_stub() {
    local stop="" out=""
    case "$1" in
      run) while (($#)); do case "$1" in --stop-file) stop="$2"; shift ;; --out) out="$2"; shift ;; esac; shift; done
           until [[ -f "$stop" ]]; do echo '{"t":"x","kind":"read","ok":true}' >> "$out"; sleep 1; done ;;
      summarize) echo '{"read_requests": 1, "failed_reads": 0, "write_requests": 0, "failed_writes": 0, "failure_windows": []}' ;;
    esac
  }
  PROBE=(probe_stub)
  ak_destroy() { echo "$(utc_ms) stub ak_destroy ran" | tee "$sc/destroyed"; }
}
dl_stub_steps() {  # every session step a 1 s stub, except those named
  local f
  for f in ak_cert ak_apply ak_kube ak_push ak_vm_data ak_deploy ak_smoke ak_golden ak_cutover ak_rotate_vm_cert ak_rotate_aks_cert \
    ak_retire_vm ak_deploy_v2_cd ak_alert_drill ak_slo ak_restore_drill; do
    [[ " $* " == *" $f "* ]] || eval "$f() { echo \"\$(utc_ms) stub $f \$*\"; sleep 1; }"
  done
}
dl_session_summary() {  # dl_session_summary SCRATCH OUT_FILE SESSION_RC
  local sc="$1" ev; ev="$(ls -d "$sc"/ev/session-* | head -1)"
  { echo "## ak_session exit $3; destroy stub ran: $(cat "$sc/destroyed" 2>/dev/null || echo NO)"
    echo "## deadline.txt"; cat "$ev/deadline.txt"
    echo "## steps.txt"; cat "$ev/steps.txt"
    echo "## not-measured.txt"; cat "$ev/not-measured.txt"
    echo "## session.log (SESSION lines)"; grep ' SESSION ' "$ev/session.log"
    echo "## ledger calls (intercepted)"; grep '^stub ledger' "$sc/calls.txt"
  } | sed "s|$sc|<scratch>|g; s|$REPO|<repo>|g" > "$2"
}
cmd_deadline() {
  local out="$1" sc rc; mkdir -p "$out"
  # 1. the whole session with stub steps and the real v2 step: the deadline (start + 45 s here) passes while the CD run waits
  #    for the approval; queue-cd (a stub that never ends, ignoring its --timeout) is stopped, the run cancelled, no fallback
  sc="$SCRATCH/cd-wait"; mkdir -p "$sc"
  cat > "$sc/queue-cd-stub.sh" <<'EOF'
#!/usr/bin/env bash
echo "stub queue-cd $*"
echo "queued cd run 4242 (https://dev.azure.com/org/project/_build/results?buildId=4242)"
echo "approval requested"
sleep 3601
EOF
  chmod +x "$sc/queue-cd-stub.sh"
  rc=0
  ( export TD_EVIDENCE="$sc/ev" TD_SESSION_DEADLINE_OVERRIDE=+45 TD_RETIRE_BUDGET_S=5 TD_DEADLINE_GRACE_S=90; . "$HERE/aks.sh"
    dl_stubs "$sc"; dl_stub_steps ak_deploy_v2_cd; QUEUE_CD="$sc/queue-cd-stub.sh"
    vm_absent() { return 0; }; helm_status() { echo deployed; }; v2_deployed() { return 1; }
    ak_values() { printf '%s\n' 'web:' '  service:' '    loadBalancerSourceRanges: [203.0.113.1/32]' > "$2"; }
    ado() {
      echo "ado $1 ${2%%\?*}" >> "$STUB_LOG"
      if [[ "$1" == PATCH ]]; then cat > /dev/null; : > "$sc/cancelled"; echo '{}'; return 0; fi
      if [[ -f "$sc/cancelled" ]]; then echo '{"status":"completed","result":"canceled"}'; else echo '{"status":"notStarted"}'; fi
    }
    set +e; ak_session ) > "$sc/out.txt" 2>&1 || rc=$?
  dl_session_summary "$sc" "$out/session-cd-wait.txt" "$rc"
  { echo "## deploy-v2 step.txt"; cat "$sc"/ev/deploy-v2-*/step.txt
    echo "## approval file"; cat "$sc/aks-cd-approval.txt"
    echo "## ADO calls"; grep '^ado ' "$sc/calls.txt"
    echo "## queue-cd or its sleep still running: $(pgrep -f 'sleep 3601' | wc -l)"
  } | sed "s|$sc|<scratch>|g; s|$REPO|<repo>|g" >> "$out/session-cd-wait.txt"
  cat "$out/session-cd-wait.txt"

  # 2. a step that hangs (golden-v1 sleeps an hour): the watchdog stops it 10 s after the deadline (+20 s; before retire-vm
  #    5 s earlier); the next measurements are dropped, retire-vm (take-down) still runs, then the destroy
  sc="$SCRATCH/hung"; mkdir -p "$sc"; rc=0
  ( export TD_EVIDENCE="$sc/ev" TD_SESSION_DEADLINE_OVERRIDE=+20 TD_RETIRE_BUDGET_S=5 TD_DEADLINE_GRACE_S=10; . "$HERE/aks.sh"
    dl_stubs "$sc"; dl_stub_steps ak_golden
    ak_golden() { echo "$(utc_ms) stub ak_golden $* (hangs)"; sleep 3602; }
    set +e; ak_session ) > "$sc/out.txt" 2>&1 || rc=$?
  dl_session_summary "$sc" "$out/session-hung-step.txt" "$rc"
  echo "## the hung step's sleep still running: $(pgrep -f 'sleep 3602' | wc -l)" >> "$out/session-hung-step.txt"
  cat "$out/session-hung-step.txt"

  # 3. the real restore drill at the deadline (15 s away, markers 3 s): PITR not restorable yet, then a restore that hangs
  local mode
  for mode in not-restorable hang; do
    sc="$SCRATCH/restore-$mode"; mkdir -p "$sc"; rc=0
    ( export TD_EVIDENCE="$sc/ev" TD_RESTORE_MARKER_S=3; . "$HERE/aks.sh"; dl_stubs "$sc"
      cat > "$sc/bin/az" <<EOF
#!/usr/bin/env bash
echo "az(bin) \$*" >> "$sc/calls.txt"
case "\$*" in
  "sql db restore"*) [[ "$mode" == hang ]] && exec sleep 3603; echo "stub: the point in time is not restorable yet" >&2; exit 1 ;;
esac
EOF
      chmod +x "$sc/bin/az"; PATH="$sc/bin:$PATH"
      SESSION_DEADLINE=$(( $(date +%s) + 15 )); VM_DOWN=1
      set +e; ( set -e; ak_restore_drill ) ) > "$sc/out.txt" 2>&1 || rc=$?
    { echo "## restore drill ($mode), deadline 15 s after its start: exit $rc"
      cat "$sc/ev/restore-drill/drill.txt"; echo "## not-measured.txt"; cat "$sc/ev/not-measured.txt"
      echo "## result.json written: $([[ -f "$sc/ev/restore-drill/result.json" ]] && echo yes || echo no); helm called: $(grep -c '^helm ' "$sc/calls.txt")"
      echo "## restore call still running: $(pgrep -f 'sleep 3603' | wc -l)"
    } | sed "s|$sc|<scratch>|g; s|$REPO|<repo>|g" > "$out/restore-$mode.txt"
    cat "$out/restore-$mode.txt"
  done

  # 4. the real alert drill at the deadline (40 s away): the alert never fires; the fault is removed, the probe stopped
  sc="$SCRATCH/alert"; mkdir -p "$sc"; rc=0
  ( export TD_EVIDENCE="$sc/ev"; . "$HERE/aks.sh"; dl_stubs "$sc"
    alert_state() { echo "Resolved 2026-11-11T00:00:00Z 2026-11-11T00:05:00Z"; }
    SESSION_DEADLINE=$(( $(date +%s) + 40 )); VM_DOWN=1
    set +e; ( set -e; ak_alert_drill ) ) > "$sc/out.txt" 2>&1 || rc=$?
  { echo "## alert drill, deadline 40 s after its start: exit $rc"; cat "$sc/ev/alert-drill/drill.txt" | grep -v '^ *$'
    echo "## not-measured.txt"; cat "$sc/ev/not-measured.txt"
    echo "## kubectl calls"; grep '^kubectl' "$sc/calls.txt"
    echo "## result.json written: $([[ -f "$sc/ev/alert-drill/result.json" ]] && echo yes || echo no)"
  } | sed "s|$sc|<scratch>|g; s|$REPO|<repo>|g" > "$out/alert-no-fire.txt"
  cat "$out/alert-no-fire.txt"
}

cmd_restore_values() {
  # the restore drill's override of the connection string: the values file of the fix and the old --set (2026-11-12),
  # each rendered with helm template on top of the session's v2 values (fake ids)
  local out="$1"; mkdir -p "$out"
  ( export TD_EVIDENCE="$SCRATCH/ev"; . "$HERE/aks.sh"; fake_vars
    local want="Server=tcp:$SQLSRV,1433;Database=sqldb-ticketdesk-pitr;Authentication=Active Directory Workload Identity;Encrypt=True;TrustServerCertificate=False;Connect Timeout=30;Application Name=TicketDesk.Web"
    write_values v2 "$SCRATCH/v2.yaml"; pitr_values sqldb-ticketdesk-pitr "$SCRATCH/pitr.yaml"
    helm template ticketdesk "$CHART" -n ticketdesk -f "$SCRATCH/v2.yaml" -f "$SCRATCH/pitr.yaml" > "$SCRATCH/fixed.yaml"
    helm template ticketdesk "$CHART" -n ticketdesk -f "$SCRATCH/v2.yaml" --set migrate.enabled=false \
      --set database.connectionString="$want" > "$SCRATCH/old.yaml" 2>&1 || true
    { echo "# $(date -u +%FT%TZ) helm $(helm version --short); connection string wanted (fake server):"; echo "#   $want"
      echo "## fix: -f values-v2.yaml -f values-pitr.yaml (aks.sh pitr_values):"; cat "$SCRATCH/pitr.yaml"
      echo "rendered ConnectionStrings__TicketDesk values:"; grep -A1 'name: ConnectionStrings__TicketDesk' "$SCRATCH/fixed.yaml" | grep 'value:' | sort | uniq -c
      echo "full string rendered: $(grep -cF "value: \"$want\"" "$SCRATCH/fixed.yaml") time(s); Job objects: $(grep -c '^kind: Job' "$SCRATCH/fixed.yaml")"
      echo "## before: --set migrate.enabled=false --set database.connectionString=\"<the same string>\" (session of 2026-11-12):"
      echo "rendered ConnectionStrings__TicketDesk values:"; grep -A1 'name: ConnectionStrings__TicketDesk' "$SCRATCH/old.yaml" | grep 'value:' | sort | uniq -c
      echo "full string rendered: $(grep -cF "value: \"$want\"" "$SCRATCH/old.yaml") time(s)"
    } | tee "$out/restore-values.txt" )
  grep -q '^full string rendered: [1-9]' "$out/restore-values.txt"
}

c="${1:-}"; shift || true
case "$c" in
  roles) cmd_roles "$@" ;; template) cmd_template "$@" ;; vm-guard) cmd_vm_guard "$@" ;; fallback) cmd_fallback "$@" ;;
  apply-fallback) cmd_apply_fallback "$@" ;; deadline) cmd_deadline "$@" ;; restore-values) cmd_restore_values "$@" ;;
  *) sed -n '2,22p' "$0"; exit 2 ;;
esac
