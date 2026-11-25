#!/usr/bin/env bash
# For the aks agent's Azure session: queue the CD pipeline's aks-lab stage, wait for the user's approval and the deployment
# with a timeout, report. The kind stage is skipped (deployKind=false); the run deploys the commit at the tip of BRANCH.
#
#   ops/ado/queue-cd.sh --acr ACR --aks AKS --tag TAG [--values "ops/k8s/values-aks.yaml ..."] [--set "k=v,..."]
#                       [--branch modernize] [--timeout 2400] [--out DIR]
#
# Prints the run's web URL first (give it to the user: Azure DevOps > Pipelines > cd > the run > "Review" > Approve).
# Exit 0 = deployed, 1 = failed or rejected, 124 = timeout: then the run is cancelled, so a late approval cannot deploy
# into a cluster that the session has already destroyed. DIR/queue-cd.txt gets the timeline, approval wait and stage times.
# Preconditions: the images ticketdesk-{web,worker[,db]}:TAG exist in the agent host's Docker; BRANCH on Azure Repos has
# ops/k8s/ticketdesk; the identity ticketdesk-lab-ado has Contributor on the RG and AKS RBAC Writer on the cluster.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
ACR=""; AKS=""; TAG=""; VALUES="ops/k8s/ticketdesk/values.yaml"; SETS="none"; BRANCH=modernize; TIMEOUT=2400; OUT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --acr) ACR="$2"; shift 2 ;; --aks) AKS="$2"; shift 2 ;; --tag) TAG="$2"; shift 2 ;; --values) VALUES="$2"; shift 2 ;;
    --set) SETS="$2"; shift 2 ;; --branch) BRANCH="$2"; shift 2 ;; --timeout) TIMEOUT="$2"; shift 2 ;; --out) OUT="$2"; shift 2 ;;
    *) sed -n '2,15p' "$0"; exit 2 ;;
  esac
done
[[ -n "$ACR" && -n "$AKS" && -n "$TAG" ]] || { sed -n '2,15p' "$0"; exit 2; }
OUT="${OUT:-$TD_W/logs/queue-cd-$(date -u +%Y%m%dT%H%M%SZ)}"; mkdir -p "$OUT"
log() { echo "$(date -u +%FT%TZ) $*" | tee -a "$OUT/queue-cd.txt"; }
run="$("$HERE/runs.sh" queue cd --branch "$BRANCH" --param deployKind=false --param deployAks=true --param "acrName=$ACR" \
  --param "aksName=$AKS" --param "imageTag=$TAG" --param "helmValues=$VALUES" --param "helmSet=$SETS")"
log "queued cd run $run ($ADO_ORG_URL/$ADO_PROJECT/_build/results?buildId=$run) branch $BRANCH tag $TAG acr $ACR aks $AKS"
log "waiting for the user's approval of environment aks-lab (timeout ${TIMEOUT}s)"
start=$(date +%s); seen_approval=""; approved_at=""
while :; do
  b="$(ado_api GET "_apis/build/builds/$run?api-version=7.1")"
  tl="$(ado_api GET "_apis/build/builds/$run/timeline?api-version=7.1" || echo '{"records":[]}')"
  ap="$(jq -r '[.records[] | select(.type == "Checkpoint.Approval")][0] | "\(.state // "")|\(.result // "")"' <<<"$tl")"
  if [[ "$ap" == inProgress* && -z "$seen_approval" ]]; then seen_approval=1; log "approval requested: waiting for the user"; fi
  if [[ "$ap" == completed* && -z "$approved_at" ]]; then approved_at=$(date +%s); log "approval ${ap#*|} after $(( approved_at - start )) s from queueing"; fi
  if [[ "$(jq -r .status <<<"$b")" == completed ]]; then
    "$HERE/runs.sh" show "$run" | tee -a "$OUT/queue-cd.txt" >/dev/null
    log "run $run completed: $(jq -r .result <<<"$b")"
    [[ "$(jq -r .result <<<"$b")" == succeeded ]]; exit $?
  fi
  if (( $(date +%s) - start >= TIMEOUT )); then
    log "timeout after ${TIMEOUT}s: cancelling run $run"
    printf '{"status":"cancelling"}' | ado_api PATCH "_apis/build/builds/$run?api-version=7.1" - >/dev/null || true
    "$HERE/runs.sh" show "$run" | tee -a "$OUT/queue-cd.txt" >/dev/null || true
    exit 124
  fi
  sleep 15
done
