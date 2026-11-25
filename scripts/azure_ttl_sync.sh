#!/usr/bin/env bash
# Give every ttl-tagged resource in rg-ticketdesk-lab the resource group's ttl (and nothing else).
#
#   scripts/azure_ttl_sync.sh            re-tag resources whose ttl differs from the group's
#   scripts/azure_ttl_sync.sh --dry-run  only report
#
# Why: Azure creates some resources itself (the VM's OS disk, a private endpoint's NIC). They copy tags once, at
# creation, and Terraform does not manage them, so when an apply extends the group's ttl they keep the old one and the
# reaper tries to delete them at that time (2026-11-09: osdisk-tdlab-legacy and nic-pe-sql-tdlab, refused only because
# they were in use). Run it after every apply that changes the ttl (ops/azure/replatform.sh does). Untagged resources
# (e.g. the server's master database) are left alone, like the reaper does.
set -euo pipefail
RG="rg-ticketdesk-lab"
DRY=0; [[ "${1:-}" == "--dry-run" ]] && DRY=1
log() { printf '%s ttl-sync: %s\n' "$(date -u +%FT%TZ)" "$*"; }
[[ "$(az group exists -n "$RG")" == "true" ]] || { log "$RG does not exist"; exit 0; }
ttl="$(az group show -n "$RG" --query tags.ttl -o tsv)"
[[ -n "$ttl" && "$ttl" != "None" ]] || { log "$RG has no ttl tag: nothing to sync"; exit 1; }
mapfile -t res < <(az resource list -g "$RG" --query "[?tags.ttl != null && tags.ttl != '$ttl'].[id, tags.ttl]" -o tsv)
n=0
for line in "${res[@]}"; do
  IFS=$'\t' read -r id old <<<"$line"; [[ -n "$id" ]] || continue
  if ((DRY)); then log "would re-tag ${id##*/}: ttl $old -> $ttl"; continue; fi
  if az resource tag --is-incremental --ids "$id" --tags "ttl=$ttl" -o none 2>/dev/null; then
    log "re-tagged ${id##*/}: ttl $old -> $ttl"; n=$((n + 1))
  else
    log "WARN could not re-tag ${id##*/} (ttl $old)"
  fi
done
log "group ttl $ttl; ${n} resource(s) re-tagged"
