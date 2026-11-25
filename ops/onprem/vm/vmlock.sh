#!/usr/bin/env bash
# vmlock.sh - the shared legacy VM lock (plan/AGENT-RULES.md, "Shared legacy VM").
#
#   vmlock.sh acquire OWNER PURPOSE MINUTES   mkdir the lock (atomic); exit 1 and show the holder if it is taken
#   vmlock.sh release OWNER                   remove the lock, only if OWNER holds it
#   vmlock.sh check OWNER                     exit 0 if OWNER holds the lock
#   vmlock.sh status                          show the holder (or "free")
#
# The lock is a directory ($TD_W/plan/locks/vm.lock) with one line in info: "owner purpose start-UTC expected-minutes".
# Never break someone else's lock: wait and retry.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
LOCK="${TD_VM_LOCK:-$TD_W/plan/locks/vm.lock}"

holder() { [[ -f "$LOCK/info" ]] && cut -d' ' -f1 "$LOCK/info" || echo "?"; }

case "${1:-status}" in
  acquire)
    owner="${2:?owner}"; purpose="${3:?purpose}"; minutes="${4:?expected minutes}"
    mkdir -p "$(dirname "$LOCK")"
    if mkdir "$LOCK" 2>/dev/null; then
      echo "$owner ${purpose// /-} $(date -u +%FT%TZ) $minutes" > "$LOCK/info"
      echo "acquired: $(cat "$LOCK/info")"
    else
      echo "held: $(cat "$LOCK/info" 2>/dev/null || echo '(no info yet)')" >&2; exit 1
    fi ;;
  release)
    owner="${2:?owner}"
    [[ -d "$LOCK" ]] || { echo "not held"; exit 0; }
    [[ "$(holder)" == "$owner" ]] || { echo "held by $(holder), not $owner: not releasing" >&2; exit 1; }
    rm -rf "$LOCK"; echo "released by $owner" ;;
  check)
    [[ -d "$LOCK" && "$(holder)" == "${2:?owner}" ]] ;;
  status)
    if [[ -d "$LOCK" ]]; then echo "held: $(cat "$LOCK/info" 2>/dev/null)"; else echo "free"; fi ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac
