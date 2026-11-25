#!/usr/bin/env bash
# Seed the td-sql dev database with deterministic synthetic TicketDesk data.
#
#   ops/dev/seed.sh run --scale S [--seed 42]     reset + load (see `td-seed --help`)
#   ops/dev/seed.sh verify | stats | reset
#   TD_SEED_DB=TicketDeskSynth ops/dev/seed.sh schema --create-database   (EF6-implied schema in a new DB)
#
# Connection string and the synthetic users' password stay in $TD_W/.secrets (never echoed).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
DB="${TD_SEED_DB:-TicketDesk}"
SEED_ENV="$TD_W/.secrets/td-seed.env"
. "$TD_W/.tools/env.sh"

if [[ ! -f "$SEED_ENV" ]]; then
  umask 077
  printf 'TD_SEED_USER_PASSWORD=Seed-%s\n' "$(openssl rand -hex 12)" >"$SEED_ENV"
  chmod 0600 "$SEED_ENV"
  echo "created $SEED_ENV (password of every synthetic user)"
fi
export TD_SQL_CONN; TD_SQL_CONN="$("$HERE/td-sql.sh" conn "$DB")"
export TD_SEED_USER_PASSWORD; TD_SEED_USER_PASSWORD="$(sed -n 's/^TD_SEED_USER_PASSWORD=//p' "$SEED_ENV")"
BIN="$REPO/tools/Seed/TicketDesk.Seed.Cli/bin/Release/net10.0/td-seed"
if [[ ! -x "$BIN" || -n "${TD_SEED_REBUILD:-}" ]]; then
  dotnet build "$REPO/tools/Seed/TicketDesk.Seed.Cli" -c Release -v quiet -nologo >/dev/null
fi
exec "$BIN" "$@"
