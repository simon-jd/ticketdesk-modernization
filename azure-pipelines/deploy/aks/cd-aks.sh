#!/usr/bin/env bash
# CD stage aks_lab (cd.yml), run by AzureCLI@2 signed in as the service connection ticketdesk-lab-arm (Entra app
# ticketdesk-lab-ado, workload identity federation). Needs on the session's resources: Contributor on rg-ticketdesk-lab
# (ACR push, AKS user credentials) and "Azure Kubernetes Service RBAC Admin" on the cluster's namespace ticketdesk
# (Kubernetes API, Azure RBAC; Writer has no dataActions for the chart's SecretProviderClass, infra/terraform root module).
#   cd-aks.sh --acr NAME --aks NAME --tag TAG [--rg RG] [--values "FILES"] [--set "k=v,..."] [--out DIR] [--template-out FILE]
# FILES: repository paths, or absolute paths on the agent host (the aks session's rendered values, which hold the session's
# resource ids and the admin CIDR and so never go into the repository or a pipeline parameter of their own).
# Steps: refuse while the legacy VM vm-tdlab-legacy exists (v2 contracts the schema; same rule as ops/azure/aks.sh deploy v2)
# -> docker push ticketdesk-{web,worker[,db]}:TAG to NAME.azurecr.io (job-private DOCKER_CONFIG) -> kubeconfig with an
# access token for the AKS Entra server app (no kubelogin; not on the lab's download list) -> helm upgrade --install of
# ops/k8s/ticketdesk (image.registry/tag set) --wait -> rollout status -> pods. Tokens are masked, never printed.
# --template-out FILE: only `helm template` with exactly the arguments the upgrade would get (no Azure call, no push), so the
# aks session can prove that this path renders what its own `aks.sh deploy v2` renders.
set -euo pipefail
ACR=""; AKS=""; TAG=""; RG="${TD_ARM_RESOURCE_GROUP:-rg-ticketdesk-lab}"; VALUES=""; SETS=""; OUT="${AGENT_TEMPDIRECTORY:-/tmp}/cd-aks"
TEMPLATE_OUT=""
LEGACY_VM="vm-tdlab-legacy"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --acr) ACR="$2"; shift 2 ;; --aks) AKS="$2"; shift 2 ;; --tag) TAG="$2"; shift 2 ;; --rg) RG="$2"; shift 2 ;;
    --values) VALUES="$2"; shift 2 ;; --set) SETS="$2"; shift 2 ;; --out) OUT="$2"; shift 2 ;;
    --template-out) TEMPLATE_OUT="$2"; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done
[[ "$ACR" != none && -n "$ACR" && "$AKS" != none && -n "$AKS" && -n "$TAG" ]] || { echo "--acr, --aks and --tag are required (queue with ops/ado/queue-cd.sh)" >&2; exit 2; }
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
CHART="$REPO/ops/k8s/ticketdesk"
[[ -f "$CHART/Chart.yaml" ]] || { echo "##vso[task.logissue type=error]no Helm chart at ops/k8s/ticketdesk on this commit (the aks round's chart must be on the branch)"; exit 1; }
# the Helm arguments, built once: the template-only mode renders with exactly these
args=(upgrade --install ticketdesk "$CHART" -n ticketdesk --create-namespace --wait --timeout 10m
      --set "image.registry=$ACR.azurecr.io" --set "image.tag=$TAG")
for f in $VALUES; do
  if [[ "$f" == /* ]]; then [[ -f "$f" ]] || { echo "values file $f not found on this host" >&2; exit 2; }; args+=(-f "$f")
  else args+=(-f "$REPO/$f"); fi
done
[[ -n "$SETS" && "$SETS" != none ]] && args+=(--set "$SETS")
if [[ -n "$TEMPLATE_OUT" ]]; then
  tpl=(template ticketdesk "$CHART" -n ticketdesk "${args[@]:10}")   # args minus "upgrade --install NAME CHART -n NS --create-namespace --wait --timeout 10m"
  helm "${tpl[@]}" > "$TEMPLATE_OUT"
  exit 0
fi

mkdir -p "$OUT"; WORK="$(mktemp -d)"; chmod 700 "$WORK"; trap 'rm -rf "$WORK"' EXIT
export DOCKER_CONFIG="$WORK/docker" KUBECONFIG="$WORK/kubeconfig"
log() { echo "$(date -u +%FT%TZ) $*" | tee -a "$OUT/cd-aks.txt"; }
log "subscription: $(az account show --query name -o tsv), principal type $(az account show --query user.type -o tsv); rg $RG, acr $ACR, aks $AKS, tag $TAG"

# the aks session's safety rule (plan/DECISIONS.md 2026-11-11): v2's pre-upgrade job contracts the schema (C0001 drops the
# dual-write trigger, adds CK_Tickets_AssignedDate), which breaks the legacy app's writes: never while its VM exists
vms="$(az vm list -g "$RG" --query "[?name=='$LEGACY_VM'].name" -o tsv)" || { echo "##vso[task.logissue type=error]cannot list the VMs of $RG" >&2; exit 1; }
if [[ -n "$vms" ]]; then
  echo "##vso[task.logissue type=error]$LEGACY_VM still exists in $RG: retire it first (ops/azure/aks.sh retire-vm); nothing pushed, nothing deployed"
  log "refused: $LEGACY_VM exists"; exit 1
fi

echo "##[section]images -> $ACR.azurecr.io"
az acr login -n "$ACR" --only-show-errors >/dev/null
for app in web worker db; do
  docker image inspect "ticketdesk-$app:$TAG" >/dev/null 2>&1 || { [[ $app == db ]] && { log "no ticketdesk-db:$TAG (skipped)"; continue; }; echo "missing ticketdesk-$app:$TAG" >&2; exit 1; }
  docker tag "ticketdesk-$app:$TAG" "$ACR.azurecr.io/ticketdesk-$app:$TAG"
  s=$(date +%s); docker push -q "$ACR.azurecr.io/ticketdesk-$app:$TAG" >/dev/null
  log "pushed ticketdesk-$app:$TAG in $(( $(date +%s) - s )) s"
done

echo "##[section]kubeconfig (AKS Entra server app token)"
az aks get-credentials -g "$RG" -n "$AKS" --file "$KUBECONFIG" --overwrite-existing --only-show-errors
TD_AKS_TOKEN="$(az account get-access-token --resource 6dae42f8-4368-4678-94ff-3960e28e3630 --query accessToken -o tsv)"
printf '##vso[task.setsecret]%s\n' "$TD_AKS_TOKEN"
TD_AKS_TOKEN="$TD_AKS_TOKEN" python3 -I - "$KUBECONFIG" <<'PY'
import os, sys, yaml  # PyYAML is part of the host's python3 (az depends on it as well)
p = sys.argv[1]
c = yaml.safe_load(open(p))
for u in c.get("users", []):
    u["user"] = {"token": os.environ["TD_AKS_TOKEN"]}
open(p, "w").write(yaml.safe_dump(c))
os.chmod(p, 0o600)
PY
kubectl version -o json 2>/dev/null | python3 -I -c 'import json,sys; v=json.load(sys.stdin)["serverVersion"]; print("server", v["gitVersion"])' | tee -a "$OUT/cd-aks.txt"

echo "##[section]helm upgrade --install"
s=$(date +%s)
helm "${args[@]}" 2>&1 | tee -a "$OUT/helm.txt"
log "helm upgrade done in $(( $(date +%s) - s )) s"
kubectl -n ticketdesk get deploy,pods -o wide | tee -a "$OUT/cd-aks.txt"
helm -n ticketdesk history ticketdesk --max 5 | tee -a "$OUT/cd-aks.txt"
