#!/usr/bin/env bash
# Pipeline runs of project TicketDesk through the REST API (PAT from $TD_W/.secrets/ado.pat, ops/ado/ado-lib.sh).
#
#   ops/ado/runs.sh queue PIPELINE [--branch REF] [--param NAME=VALUE]... [--stage-skip STAGE]...   prints the run id
#   ops/ado/runs.sh wait RUN [--timeout SECONDS]      poll until completed; exit 0 succeeded, 1 otherwise, 124 timeout
#   ops/ado/runs.sh show RUN                          result + one line per stage/job/task (state, result, seconds)
#   ops/ado/runs.sh log RUN TASK-NAME                 the raw log of the first timeline record with that name
#   ops/ado/runs.sh list [PIPELINE] [TOP]             recent runs (id, pipeline, branch, result, minutes, reason)
# PIPELINE is the name (ci, cd) or the id.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"

pipeline_id() {
  [[ "$1" =~ ^[0-9]+$ ]] && { echo "$1"; return; }
  ado_api GET "_apis/pipelines?api-version=7.1" | jq -r --arg n "$1" '.value[] | select(.name == $n) | .id' | head -1
}

cmd_queue() {
  local pid; pid="$(pipeline_id "$1")"; shift
  local branch="refs/heads/modernize" params="{}" skip="[]"
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --branch) branch="$2"; [[ "$branch" == refs/* ]] || branch="refs/heads/$branch"; shift 2 ;;
      --param) params="$(jq -c --arg k "${2%%=*}" --arg v "${2#*=}" '. + {($k): $v}' <<<"$params")"; shift 2 ;;
      --stage-skip) skip="$(jq -c --arg s "$2" '. + [$s]' <<<"$skip")"; shift 2 ;;
      *) echo "unknown option $1" >&2; exit 2 ;;
    esac
  done
  jq -n --arg b "$branch" --argjson p "$params" --argjson s "$skip" \
    '{resources: {repositories: {self: {refName: $b}}}, templateParameters: $p, stagesToSkip: $s}' \
    | ado_api POST "_apis/pipelines/$pid/runs?api-version=7.1" - | jq -r '.id'
}

build_json() { ado_api GET "_apis/build/builds/$1?api-version=7.1"; }

cmd_wait() {
  local run="$1" timeout=3600; shift
  [[ "${1:-}" == --timeout ]] && timeout="$2"
  local start; start=$(date +%s)
  while :; do
    local b; b="$(build_json "$run")"
    if [[ "$(jq -r .status <<<"$b")" == completed ]]; then
      echo "run $run $(jq -r '.buildNumber + " " + .result' <<<"$b")"
      [[ "$(jq -r .result <<<"$b")" == succeeded ]]; return
    fi
    (( $(date +%s) - start < timeout )) || { echo "run $run: timeout after ${timeout}s ($(jq -r .status <<<"$b"))"; return 124; }
    sleep 20
  done
}

cmd_show() {
  local run="$1" b
  b="$(build_json "$run")"
  jq -r '"run \(.id) \(.buildNumber) \(.definition.name) \(.sourceBranch) \(.sourceVersion[0:8]) status=\(.status) result=\(.result // "-") reason=\(.reason) queued=\(.queueTime) start=\(.startTime // "-") finish=\(.finishTime // "-")"' <<<"$b"
  ado_api GET "_apis/build/builds/$run/timeline?api-version=7.1" | jq -r '
    def secs: if .startTime and .finishTime then (((.finishTime|sub("\\.[0-9]+";"")|fromdate) - (.startTime|sub("\\.[0-9]+";"")|fromdate))|tostring) + " s" else "-" end;
    [.records[] | select(.type == "Stage" or .type == "Job" or .type == "Task" or .type == "Checkpoint" or .type == "Checkpoint.Approval")]
    | sort_by(.startTime // "9") | .[]
    | "\(.type|.[0:10]|.+"          "|.[0:10]) \(.state|.+"          "|.[0:10]) \((.result // "-")|.+"            "|.[0:12]) \(secs|.+"        "|.[0:8]) \(.name)\(if .issues then "  ! " + ([.issues[] | select(.type=="error") | .message[0:160]] | join(" | ")) else "" end)"'
}

cmd_log() {
  local run="$1" name="$2" url
  url="$(ado_api GET "_apis/build/builds/$run/timeline?api-version=7.1" | jq -r --arg n "$name" '[.records[] | select(.name == $n and .log != null)][0].log.url // empty')"
  [[ -n "$url" ]] || { echo "no log for '$name'" >&2; return 1; }
  ado_api GET "$url" | jq -r '.value[]' 2>/dev/null || ado_api GET "$url"
}

cmd_list() {
  local def="" top="${2:-20}"
  [[ -n "${1:-}" ]] && def="&definitions=$(pipeline_id "$1")"
  ado_api GET "_apis/build/builds?api-version=7.1&\$top=$top$def" | jq -r '.value[] |
    "\(.id)\t\(.definition.name)\t\(.buildNumber)\t\(.sourceBranch|sub("refs/heads/";""))\t\(.status)/\(.result // "-")\t\(if .startTime and .finishTime then ((((.finishTime|sub("\\.[0-9]+";"")|fromdate) - (.startTime|sub("\\.[0-9]+";"")|fromdate))/60*10|floor)/10|tostring) + " min" else "-" end)\t\(.reason)"'
}

case "${1:-}" in
  queue) shift; cmd_queue "$@" ;;
  wait) shift; cmd_wait "$@" ;;
  show) shift; cmd_show "$@" ;;
  log) shift; cmd_log "$@" ;;
  list) shift; cmd_list "$@" ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac
