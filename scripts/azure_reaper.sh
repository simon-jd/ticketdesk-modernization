#!/usr/bin/env bash
# Delete expired lab resources in rg-ticketdesk-lab (and nothing else).
#
#   scripts/azure_reaper.sh            delete what has expired
#   scripts/azure_reaper.sh --dry-run  only report
#
# Rules: a resource whose `ttl` tag (UTC, YYYY-MM-DDThh:mm:ssZ) is in the past is deleted.
# If the resource group's own ttl has passed, the whole group is deleted (this also removes
# Azure-created children without tags, such as private endpoint NICs). Resources without a ttl
# tag are listed as warnings. The resource group name is fixed; there is no option to point the
# reaper anywhere else (rg-mlinfra-westus and NetworkWatcherRG are never touched). The RG lives in
# swedencentral (lab region since 2026-11-08); the reaper works by RG name, not by region.
set -euo pipefail

RG="rg-ticketdesk-lab"
DRY=0
[[ "${1:-}" == "--dry-run" ]] && DRY=1

log() { printf '%s reaper: %s\n' "$(date -u +%FT%TZ)" "$*"; }
# Every az call has a time limit: on 2026-11-11 one `az group exists` hung for 18 h 50 min and held the flock, so every cron
# run after it exited at once and the lab had no reaper (found after the aks session). Deletes get longer.
AZ_TIMEOUT="${TD_REAPER_AZ_TIMEOUT:-300}"
AZ_BIN="$(type -P az)" || { log "ERROR az not on PATH"; exit 1; }
az() { timeout -k 10 "$AZ_TIMEOUT" "$AZ_BIN" "$@"; }
az_delete() { timeout -k 10 1800 "$AZ_BIN" "$@"; }
now=$(date -u +%s)
expired() { # $1 = ttl tag value; true if set and in the past
  [[ -n "$1" && "$1" != "None" && "$1" != "null" ]] || return 1
  local t
  t=$(date -u -d "$1" +%s 2>/dev/null) || { log "unparseable ttl '$1' (treated as expired)"; return 0; }
  ((t < now))
}

# an az failure (timeout, expired login, network) is an error, not "does not exist": exit 1 so the log shows it
if ! exists="$(az group exists -n "$RG")"; then
  log "ERROR az group exists failed or timed out (${AZ_TIMEOUT}s): cannot tell whether $RG exists"
  exit 1
fi
if [[ "$exists" != "true" ]]; then
  log "$RG does not exist: nothing to reap"
  exit 0
fi

rg_ttl=$(az group show -n "$RG" --query "tags.ttl" -o tsv)
if expired "$rg_ttl"; then
  log "$RG ttl $rg_ttl has passed: deleting the resource group"
  if ((DRY)); then log "dry run: would run az group delete -n $RG --yes"; exit 0; fi
  az_delete group delete -n "$RG" --yes
  log "$RG deleted"
  exit 0
fi
log "$RG ttl ${rg_ttl:-<none>} (not expired)"

# id <TAB> ttl <TAB> type, one per line
mapfile -t res < <(az resource list -g "$RG" --query "[].[id, tags.ttl, type]" -o tsv)
todo=()
for line in "${res[@]}"; do
  IFS=$'\t' read -r id ttl type <<<"$line"
  if [[ -z "$ttl" || "$ttl" == "None" ]]; then
    log "WARN no ttl tag: $type ${id##*/}"
  elif expired "$ttl"; then
    log "expired ($ttl): $type ${id##*/}"
    todo+=("$id")
  fi
done

if ((${#todo[@]} == 0)); then log "nothing expired"; exit 0; fi
if ((DRY)); then log "dry run: would delete ${#todo[@]} resource(s)"; exit 0; fi

# Dependencies (endpoint before subnet, VM before NIC, ...) are not known here: retry in passes.
for pass in 1 2 3 4; do
  left=()
  for id in "${todo[@]}"; do
    if az_delete resource delete --ids "$id" >/dev/null 2>&1; then log "deleted ${id##*/}"; else left+=("$id"); fi
  done
  todo=("${left[@]}")
  ((${#todo[@]} == 0)) && break
  log "pass $pass: ${#todo[@]} left, retrying"
done
if ((${#todo[@]})); then
  log "ERROR could not delete: ${todo[*]##*/}"
  exit 1
fi
log "done"
