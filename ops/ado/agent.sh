#!/usr/bin/env bash
# Self-hosted Azure Pipelines agent for project TicketDesk: pool "lab-host", agent "lab-host-1", run as the
# current user (no sudo, no systemd) in tmux session td-ado-agent.
#
#   ops/ado/agent.sh configure   one time: register the agent (PAT from $TD_W/.secrets/ado.pat via VSTS_AGENT_INPUT_TOKEN,
#                                never on the command line; the agent keeps its own RSA credential, not the PAT)
#   ops/ado/agent.sh start       start it in tmux td-ado-agent (no-op if the session exists)
#   ops/ado/agent.sh stop        Ctrl-C in td-ado-agent (the listener finishes its shutdown), kill the session after 60 s
#   ops/ado/agent.sh status      tmux session + the pool's view of the agent (online/offline, version, busy job)
#   ops/ado/agent.sh remove      unregister (PAT again) - only when the lab is torn down
#   ops/ado/agent.sh run         foreground, what the tmux session runs
#
# Jobs get a clean environment: env -i with an explicit PATH (the lab toolchains in $TD_W/.tools), TD_W, and an empty
# AZURE_CONFIG_DIR ($AGENT_DIR/.azure-empty), no ARM_*/AZURE_* variables: a job can reach Azure only through a service
# connection (the CI asserts `az account show` fails). Everything else of the login shell (tmux server env, az login in
# ~/.azure) is not inherited.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$HERE/ado-lib.sh"
AGENT_DIR="${ADO_AGENT_DIR:-$TD_W/.tools/ado-agent}"
POOL="${ADO_POOL:-lab-host}"
NAME="${ADO_AGENT_NAME:-lab-host-1}"
SESSION=td-ado-agent
LOG="$TD_W/logs/ado-agent.log"

clean_env() {
  install -d -m 0700 "$AGENT_DIR/.azure-empty"
  local path="$TD_W/.tools/node/bin:$TD_W/.tools/dotnet:$HOME/.dotnet/tools:$TD_W/.tools/bin:/usr/local/bin:/usr/bin:/bin"
  CLEAN_ENV=(env -i HOME="$HOME" USER="$(id -un)" LOGNAME="$(id -un)" SHELL=/bin/bash LANG=C.UTF-8 TERM=dumb
    PATH="$path" TD_W="$TD_W" DOTNET_ROOT="$TD_W/.tools/dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
    AZURE_CONFIG_DIR="$AGENT_DIR/.azure-empty" AZURE_CORE_COLLECT_TELEMETRY=no
    TF_PLUGIN_CACHE_DIR="$TD_W/.tools/tf-plugin-cache" PLAYWRIGHT_BROWSERS_PATH="$HOME/.cache/ms-playwright")
}

agent_json() {
  local pool_id
  pool_id="$(ado_api GET "/_apis/distributedtask/pools?poolName=$POOL&api-version=7.1" | jq -r '.value[0].id')"
  ado_api GET "/_apis/distributedtask/pools/$pool_id/agents?agentName=$NAME&includeAssignedRequest=true&includeCapabilities=false&api-version=7.1" \
    | jq '{pool: "'"$POOL"'", agents: [.value[] | {id, name, version, status, enabled, busy: (.assignedRequest.planType // null)}]}'
}

case "${1:-}" in
  configure)
    clean_env
    cd "$AGENT_DIR"
    [[ -f .agent ]] && { echo "already configured ($(jq -r .agentName .agent) in $(jq -r .poolName .agent 2>/dev/null || echo "$POOL"))"; exit 0; }
    # the PAT is read by the clean bash itself and handed to config.sh as an environment variable (not argv)
    "${CLEAN_ENV[@]}" bash -c 'VSTS_AGENT_INPUT_TOKEN="$(cat "$1")" exec ./config.sh --unattended --url "$2" --auth pat \
        --pool "$3" --agent "$4" --work _work --replace' _ "$ADO_PAT_FILE" "$ADO_ORG_URL" "$POOL" "$NAME" \
      2>&1 | grep -v -i token
    ;;
  run)
    clean_env
    cd "$AGENT_DIR"
    exec "${CLEAN_ENV[@]}" ./run.sh
    ;;
  start)
    if tmux has-session -t "$SESSION" 2>/dev/null; then echo "$SESSION already running"; exit 0; fi
    mkdir -p "$(dirname "$LOG")"
    tmux new-session -d -s "$SESSION" -c "$AGENT_DIR" "bash '$HERE/agent.sh' run 2>&1 | tee -i -a '$LOG'"
    echo "started tmux $SESSION (log $LOG)"
    ;;
  stop)
    tmux has-session -t "$SESSION" 2>/dev/null || { echo "$SESSION not running"; exit 0; }
    tmux send-keys -t "$SESSION" C-c
    for _ in $(seq 1 60); do tmux has-session -t "$SESSION" 2>/dev/null || { echo "stopped"; exit 0; }; sleep 1; done
    tmux kill-session -t "$SESSION"; echo "killed $SESSION after 60 s"
    ;;
  status)
    tmux has-session -t "$SESSION" 2>/dev/null && echo "tmux $SESSION: running" || echo "tmux $SESSION: not running"
    agent_json
    ;;
  remove)
    "$0" stop || true
    clean_env
    cd "$AGENT_DIR"
    "${CLEAN_ENV[@]}" bash -c 'VSTS_AGENT_INPUT_TOKEN="$(cat "$1")" exec ./config.sh remove --unattended --auth pat' _ "$ADO_PAT_FILE" \
      2>&1 | grep -v -i token
    ;;
  *) sed -n '2,17p' "$0"; exit 2 ;;
esac
