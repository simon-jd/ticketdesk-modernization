#!/usr/bin/env bash
# entra-local.sh - run TicketDesk.Web (src/modern) with Entra ID (OpenID Connect) sign-in on https://localhost:5443, in a
# .NET SDK container (no .NET on the host needed). Kestrel listens on 127.0.0.1:5443 only (host network): reach it from
# another machine through an SSH tunnel, e.g.  ssh -L 5443:127.0.0.1:5443 <user>@<this-host>  and open https://localhost:5443/
#
#   TD_ENTRA_ENV=/path/to/entra.env ops/dev/entra-local.sh [up|down|logs]          (default: up)
#
# TD_ENTRA_ENV    Entra settings, see ops/dev/entra-local.env.example (default ops/dev/entra-local.env, git-ignored).
#                 Must not be readable by group/others; it is passed to the container through --env-file and never printed.
# TD_ENTRA_DB     database in td-sql (ops/dev/td-sql.sh up) for the signed-in session and the first-sign-in user row
#                 (default TicketDeskSynth, schema incl. the contract stage, see ops/dev/run-modern.sh).
#                 TD_ENTRA_DB=none: no database (key ring in the container): the sign-in page and the redirect to Entra
#                 work, the callback cannot create the user (check of the app registration only).
# TD_ENTRA_TLS    directory with cert.pem/key.pem for localhost (default ops/dev/entra-local-tls, git-ignored; a
#                 self-signed 30-day certificate is created when missing: the browser warns once).
# TD_ENTRA_NAME   container name (default td-entra-local); TD_ENTRA_CPUS (default 2).
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
NAME="${TD_ENTRA_NAME:-td-entra-local}"
ENVF="${TD_ENTRA_ENV:-$HERE/entra-local.env}"
TLS="${TD_ENTRA_TLS:-$HERE/entra-local-tls}"
DB="${TD_ENTRA_DB:-TicketDeskSynth}"
PORT=5443
SDK=mcr.microsoft.com/dotnet/sdk:10.0

case "${1:-up}" in
  down) docker rm -f "$NAME" >/dev/null 2>&1 || true; echo "removed $NAME"; exit 0;;
  logs) exec docker logs -f "$NAME";;
  up) ;;
  *) echo "usage: $0 [up|down|logs]" >&2; exit 2;;
esac

[[ -f "$ENVF" ]] || { echo "no Entra settings file: $ENVF (template: ops/dev/entra-local.env.example)" >&2; exit 1; }
[[ "$(stat -c %a "$ENVF")" =~ ^[0-7]00$ ]] || { echo "$ENVF must not be readable by group/others (chmod 600)" >&2; exit 1; }
for k in Authority ClientId ClientSecret TenantId; do
  grep -q "^Authentication__EntraId__$k=[^<]" "$ENVF" || { echo "$ENVF: Authentication__EntraId__$k is missing or still a placeholder" >&2; exit 1; }
done

if [[ ! -f "$TLS/cert.pem" || ! -f "$TLS/key.pem" ]]; then
  install -d -m 0700 "$TLS"
  (umask 077; openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=localhost" \
     -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" -keyout "$TLS/key.pem" -out "$TLS/cert.pem" 2>/dev/null)
  echo "created a self-signed localhost certificate in $TLS (30 days)"
fi

RUNENV="$(mktemp)"; chmod 600 "$RUNENV"; trap 'rm -f "$RUNENV"' EXIT
{
  grep -E '^Authentication__EntraId__[A-Za-z]+=' "$ENVF"
  echo "ASPNETCORE_ENVIRONMENT=Production"
  echo "Kestrel__Endpoints__Https__Url=https://127.0.0.1:$PORT"
  echo "Kestrel__Endpoints__Https__Certificate__Path=/tls/cert.pem"
  echo "Kestrel__Endpoints__Https__Certificate__KeyPath=/tls/key.pem"
  echo "DOTNET_CLI_TELEMETRY_OPTOUT=1"
  echo "DOTNET_NOLOGO=1"
  if [[ "$DB" == none ]]; then
    echo "DataProtection__KeyRing=Default"
    # the DbContexts need a connection string to be constructed; nothing listens there, so only pages that query fail
    echo "ConnectionStrings__TicketDesk=Server=127.0.0.1,1;Database=none;Integrated Security=false;Connect Timeout=2;TrustServerCertificate=True"
  else
    echo "ConnectionStrings__TicketDesk=$("$HERE/td-sql.sh" conn "$DB")"
  fi
} >"$RUNENV"

docker rm -f "$NAME" >/dev/null 2>&1 || true
docker run -d --name "$NAME" --network host --cpus="${TD_ENTRA_CPUS:-2}" --env-file "$RUNENV" \
  -v "$REPO":/ro:ro -v "$TLS":/tls:ro "$SDK" bash -c '
set -e
mkdir -p /src && cd /ro
tar --exclude=./.git --exclude=bin --exclude=obj --exclude=node_modules -cf - src/modern docs/migration TicketDesk/TicketDesk.Localization | tar -C /src -xf -
cd /src
exec dotnet run --project src/modern/TicketDesk.Web -c Release --no-launch-profile -p:TdBuildWeb=false' >/dev/null
echo "started $NAME (database: $DB); building and starting takes a few minutes"
for _ in $(seq 1 120); do
  if curl -ksf -o /dev/null --resolve "localhost:$PORT:127.0.0.1" "https://localhost:$PORT/health/live"; then
    echo "up: https://localhost:$PORT/  (sign-in page: https://localhost:$PORT/user/sign-in)"; exit 0
  fi
  docker inspect -f '{{.State.Running}}' "$NAME" 2>/dev/null | grep -q true || { echo "container stopped; docker logs $NAME" >&2; exit 1; }
  sleep 5
done
echo "not up after 10 minutes; docker logs $NAME" >&2; exit 1
