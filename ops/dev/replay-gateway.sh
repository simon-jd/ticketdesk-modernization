#!/usr/bin/env bash
# Coexistence rounds (1-3) only: since r4-retire TicketDesk.Web has no YARP fallback, no remote authentication and no shared
# cookie, so the gateway replays below need a checkout from before it (docs/dev/rollback.md). --serve still starts the new app
# alone with the lab settings (the Legacy__* ones are ignored); the replay of the new app alone is ops/dev/replay-modern.sh.
#
# Replay a recording through the new gateway (TicketDesk.Web: migrated routes per the registry, YARP fallback to the
# legacy app for the rest) and compare it with the recording made directly against the legacy app.
#
#   TD_LOCK_OWNER=<you> ops/dev/replay-gateway.sh --from GOLDEN.jsonl[.xz|.gz] --out-dir DIR [--no-reset]
#        [--registry FILE | --waves 0,1,...] [--remote-auth] [--shared-cookie] [--entra ENVFILE]
#   ops/dev/replay-gateway.sh --serve [--registry FILE | --waves ...] [--remote-auth]   only start the gateway (foreground)
#
# --registry: the migration registry file (default: none, i.e. nothing migrated, the transparency check). --waves: the
# shipped src/modern/TicketDesk.Web/migration-registry.json cut down to the routes of those waves (routes.csv). The
# summary (replay-summary.py) says which app answered each exchange, per wave. --remote-auth: System.Web adapters remote
# authentication against the legacy app (key from $TD_W/.secrets/legacy-remoteapp.env). --shared-cookie: the new app
# issues and reads the legacy sign-in cookie itself (key ring in the database it uses; the legacy app must be deployed
# with TD_SHARED_COOKIE_KEYRING=database on the same database, i.e. TD_MODERN_DB=vm).
# --entra ENVFILE: Entra ID sign-in on (TD_ENTRA_AUTHORITY, _CLIENT_ID, _CLIENT_SECRET, _TENANT_ID, _REQUIRE_HTTPS from the
# file: $TD_W/.secrets/entra-web.env for the real tenant, or a file pointing at tools/TestIdp).
# TD_MODERN_LOGIN=app (with TD_MODERN_DB=vm): the new app connects with the least-privilege login td_modern_app
# (ops/dev/td-db-app-login.sh) instead of sa.
# TD_MODERN_DB=vm points the new app at the VM's database (shared with the legacy app) instead of td-sql's TicketDesk copy.
#
# The gateway listens on https://localhost:5443 with the lab certificate, trusts the legacy certificate by its SHA-256
# pin and talks to td-sql database TicketDesk (same baseline-s data as the VM). Secrets stay in the process environment.
# The VM reset (snapshot baseline-s) needs the shared VM lock.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
CA="$TD_W/.secrets/legacy-lab-ca"
GW_URL="${TD_GATEWAY_URL:-https://localhost:5443/}"
FROM=""; OUT=""; RESET=1; SERVE=0; REGISTRY=""; WAVES=""; REMOTE=0; SHARED=0; ENTRA=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --from) FROM="$2"; shift 2 ;;
    --out-dir) OUT="$2"; shift 2 ;;
    --no-reset) RESET=0; shift ;;
    --serve) SERVE=1; shift ;;
    --registry) REGISTRY="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"; shift 2 ;;
    --waves) WAVES="$2"; shift 2 ;;
    --remote-auth) REMOTE=1; shift ;;
    --shared-cookie) SHARED=1; shift ;;
    --entra) ENTRA="$2"; shift 2 ;;
    *) sed -n '2,10p' "$0"; exit 2 ;;
  esac
done

dotnet build "$REPO/src/modern/TicketDesk.Web" -c Release -v quiet -nologo >/dev/null
if [[ -n "$WAVES" ]]; then
  REGISTRY="$(mktemp --suffix=.json)"
  python3 -I - "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$REPO/docs/migration/routes.csv" "$WAVES" > "$REGISTRY" <<'PY'
import csv, json, sys
reg = json.load(open(sys.argv[1]))
waves = set(sys.argv[3].split(","))
wave_of = {r["id"]: r["wave"] for r in csv.DictReader(open(sys.argv[2], encoding="utf-8"))}
routes = {k: v for k, v in reg["Migration"]["Routes"].items() if wave_of.get(k) in waves}
print(json.dumps({"Migration": {"Routes": routes}}, indent=2))
PY
fi
gateway_env() {
  export ASPNETCORE_URLS="${GW_URL%/}" ASPNETCORE_ENVIRONMENT=Production
  export Kestrel__Certificates__Default__Path="$CA/server.pfx"
  export Kestrel__Certificates__Default__Password; Kestrel__Certificates__Default__Password="$(sed -n 's/^TD_CERT_PFX_PASSWORD=//p' "$TD_W/.secrets/legacy-lab-cert.env")"
  export Legacy__BaseUrl="${TD_LEGACY_URL:-https://localhost:8443/}"
  export Legacy__ServerCertificateSha256; Legacy__ServerCertificateSha256="$(openssl x509 -in "$CA/server.crt" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)"
  local db="${TD_MODERN_DB:-TicketDesk}"; [[ "$db" == vm ]] && db=TicketDesk
  export ConnectionStrings__TicketDesk; ConnectionStrings__TicketDesk="$("$HERE/td-sql.sh" conn "$db")"
  export Migration__RegistryFile="$REGISTRY"
  if (( REMOTE )); then
    export Legacy__RemoteApp__Enabled=true
    export Legacy__RemoteApp__ApiKey; Legacy__RemoteApp__ApiKey="$(sed -n 's/^TD_REMOTEAPP_API_KEY=//p' "$TD_W/.secrets/legacy-remoteapp.env")"
  fi
  if (( SHARED )); then export Authentication__SharedCookie__Enabled=true; fi
  if [[ -n "$ENTRA" ]]; then
    export Authentication__EntraId__Enabled=true
    export Authentication__EntraId__Authority; Authentication__EntraId__Authority="$(sed -n 's/^TD_ENTRA_AUTHORITY=//p' "$ENTRA")"
    export Authentication__EntraId__ClientId; Authentication__EntraId__ClientId="$(sed -n 's/^TD_ENTRA_CLIENT_ID=//p' "$ENTRA")"
    export Authentication__EntraId__ClientSecret; Authentication__EntraId__ClientSecret="$(sed -n 's/^TD_ENTRA_CLIENT_SECRET=//p' "$ENTRA")"
    export Authentication__EntraId__TenantId; Authentication__EntraId__TenantId="$(sed -n 's/^TD_ENTRA_TENANT_ID=//p' "$ENTRA")"
    export Authentication__EntraId__RequireHttpsMetadata; Authentication__EntraId__RequireHttpsMetadata="$(sed -n 's/^TD_ENTRA_REQUIRE_HTTPS=//p' "$ENTRA")"
  fi
  # attachments: the Blob store shared with the legacy app (Azurite in the lab) unless TD_ATTACHMENTS=local
  if [[ "${TD_ATTACHMENTS:-blob}" == blob && -f "$TD_W/.secrets/legacy-filestore.env" ]]; then
    export Attachments__Store=Blob
    export Attachments__BlobConnectionString; Attachments__BlobConnectionString="$(sed -n 's/^TD_AZURE_STORAGE_CONNECTION_HOST=//p' "$TD_W/.secrets/legacy-filestore.env")"
  fi
  if [[ "${TD_MODERN_DB:-}" == vm ]]; then
    local pw login=sa
    if [[ "${TD_MODERN_LOGIN:-sa}" == app ]]; then
      login=td_modern_app; pw="$(sed -n 's/^TD_MODERN_APP_SQL_PASSWORD=//p' "$TD_W/.secrets/td-modern-app.env")"
    else
      pw="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")"
    fi
    ConnectionStrings__TicketDesk="Server=127.0.0.1,11433;Database=TicketDesk;User Id=$login;Password=$pw;TrustServerCertificate=True;Encrypt=True"
  fi
}
DLL="$REPO/src/modern/TicketDesk.Web/bin/Release/net10.0/TicketDesk.Web.dll"
# run from the project folder so appsettings.json (log levels, Legacy defaults) is the content root
if (( SERVE )); then gateway_env; cd "$REPO/src/modern/TicketDesk.Web"; exec dotnet "$DLL"; fi

[[ -n "$FROM" && -n "$OUT" ]] || { sed -n '2,10p' "$0"; exit 2; }
mkdir -p "$OUT"
BASELINE="$FROM"
case "$FROM" in
  *.xz) BASELINE="$OUT/baseline.jsonl"; xz -dc "$FROM" > "$BASELINE" ;;
  *.gz) BASELINE="$OUT/baseline.jsonl"; gunzip -c "$FROM" > "$BASELINE" ;;
esac

( gateway_env; cd "$REPO/src/modern/TicketDesk.Web"; exec dotnet "$DLL" ) > "$OUT/gateway.log" 2>&1 &
GW_PID=$!
trap 'kill $GW_PID 2>/dev/null || true; wait $GW_PID 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do curl -ksf "${GW_URL}health/live" >/dev/null 2>&1 && break; sleep 1; done
curl -ksf "${GW_URL}health/live" >/dev/null || { echo "gateway did not start" >&2; tail -20 "$OUT/gateway.log" >&2; exit 1; }

{
  echo "# date: $(date -u +%FT%TZ)  git: $(git -C "$REPO" rev-parse --short HEAD)  gateway: $GW_URL -> ${TD_LEGACY_URL:-https://localhost:8443/}"
  echo "# baseline recording: $FROM ($(sha256sum "$FROM" | cut -c1-64))  reset: $RESET (snapshot baseline-s)"
  echo "# registry: ${REGISTRY:-none (nothing migrated)}${WAVES:+ (waves $WAVES)}  remote auth: $REMOTE  shared cookie: $SHARED  new-app db: ${TD_MODERN_DB:-td-sql TicketDesk} (login ${TD_MODERN_LOGIN:-sa})  snapshot: ${TD_BASELINE_SNAPSHOT:-baseline-s}"
} > "$OUT/environment.txt"
[[ -z "$REGISTRY" ]] || cp "$REGISTRY" "$OUT/registry.json"
curl -ksf "${GW_URL}api/migration/routes" > "$OUT/migration-status.json" || true
if (( RESET )); then "$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$OUT/reset.txt" 2>&1; tail -1 "$OUT/reset.txt"; fi
# the snapshot reset drops server logins: make sure the app's least-privilege login exists (idempotent)
if [[ "${TD_MODERN_DB:-}" == vm && "${TD_MODERN_LOGIN:-sa}" == app ]]; then "$HERE/td-db-app-login.sh" apply > "$OUT/app-login.txt" 2>&1; tail -1 "$OUT/app-login.txt"; fi
BIN="$REPO/tools/Replay/TicketDesk.Replay.Cli/bin/Release/net10.0/td-replay"
dotnet build "$REPO/tools/Replay/TicketDesk.Replay.Cli" -c Release -v quiet -nologo >/dev/null
COMMON=(--target "$GW_URL" --insecure --secrets-file "$TD_W/.secrets/legacy-lab.env" --secrets-file "$TD_W/.secrets/td-seed.env"
        --secret-alias TD_ADMIN_USER=TD_ADMIN_EMAIL --routes "$REPO/docs/migration/routes.csv")
echo "\$ td-replay replay --from $FROM ${COMMON[*]} --out $OUT/gateway.jsonl --run-id gateway" > "$OUT/replay.txt"
"$BIN" replay --from "$BASELINE" "${COMMON[@]}" --out "$OUT/gateway.jsonl" --run-id gateway >> "$OUT/replay.txt" 2>&1 || true
tail -1 "$OUT/replay.txt"
echo "\$ td-replay compare --baseline $FROM --candidate $OUT/gateway.jsonl --out-dir $OUT/compare-golden-gateway" > "$OUT/compare.txt"
"$BIN" compare --baseline "$BASELINE" --candidate "$OUT/gateway.jsonl" --out-dir "$OUT/compare-golden-gateway" >> "$OUT/compare.txt" 2>&1 || true
tail -1 "$OUT/compare.txt"
python3 -I "$HERE/replay-summary.py" --candidate "$OUT/gateway.jsonl" --report "$OUT/compare-golden-gateway/report.json" \
  --routes "$REPO/docs/migration/routes.csv" --explain "$REPO/docs/migration/replay-explanations.json" \
  ${REGISTRY:+--registry "$REGISTRY"} --out "$OUT/summary.md" || true
[[ "$BASELINE" == "$FROM" ]] || rm -f "$BASELINE"
