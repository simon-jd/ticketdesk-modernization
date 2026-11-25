# shellcheck shell=bash
# Sourced by e2e.sh / replay.sh: start TicketDesk.Web (Release build output) alone - no legacy app behind it - on a fresh
# copy of td-sql's TicketDesk, HTTPS with a throw-away self-signed certificate, local attachment folder; stop it on exit.
#   ci_app_start NAME PORT OUTDIR    sets APP_URL, APP_DB, APP_WORK, APP_REGISTRY, APP_PID
ci_app_start() {
  local name="$1" port="$2" out="$3"
  APP_DB="TicketDesk_ci_$name"; APP_URL="https://localhost:$port"
  APP_WORK="$(mktemp -d "${AGENT_TEMPDIRECTORY:-/tmp}/td-ci-$name-XXXXXX")"
  APP_REGISTRY="$APP_WORK/migration-registry.json"
  cp "$CI_REPO/src/modern/TicketDesk.Web/migration-registry.json" "$APP_REGISTRY"
  mkdir -p "$APP_WORK/attachments" "$out"
  if ss -ltnH "sport = :$port" | grep -q .; then echo "port $port is in use" >&2; return 1; fi
  bash "$CI_REPO/ops/dev/e2e-db.sh" "$APP_DB" | tee "$out/db.txt"
  openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj /CN=localhost -addext subjectAltName=DNS:localhost \
    -keyout "$APP_WORK/key.pem" -out "$APP_WORK/cert.pem" 2>/dev/null
  local conn; conn="$(ci_sql_conn "$APP_DB")"
  ( cd "$CI_REPO/src/modern/TicketDesk.Web"
    ASPNETCORE_URLS="$APP_URL" ASPNETCORE_ENVIRONMENT=Production \
    Kestrel__Certificates__Default__Path="$APP_WORK/cert.pem" Kestrel__Certificates__Default__KeyPath="$APP_WORK/key.pem" \
    ConnectionStrings__TicketDesk="$conn" Migration__RegistryFile="$APP_REGISTRY" Legacy__Enabled=false \
    Attachments__RootPath="$APP_WORK/attachments" exec dotnet bin/Release/net10.0/TicketDesk.Web.dll ) > "$out/server.log" 2>&1 &
  APP_PID=$!
  trap ci_app_stop EXIT
  local i
  for i in $(seq 1 120); do curl -ksf "$APP_URL/health/live" >/dev/null 2>&1 && break; sleep 1; done
  curl -ksf "$APP_URL/health/live" >/dev/null || { echo "app did not start" >&2; tail -20 "$out/server.log" >&2; return 1; }
  echo "app up at $APP_URL after ~${i} s (pid $APP_PID, database $APP_DB)"
}

ci_app_stop() {
  [[ -n "${APP_PID:-}" ]] && { kill "$APP_PID" 2>/dev/null || true; wait "$APP_PID" 2>/dev/null || true; }
  [[ -n "${APP_DB:-}" ]] && bash "$CI_REPO/ops/dev/td-sql.sh" sql -Q "IF DB_ID(N'$APP_DB') IS NOT NULL BEGIN ALTER DATABASE [$APP_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$APP_DB]; END" >/dev/null 2>&1 || true
  [[ -n "${APP_WORK:-}" ]] && rm -rf "$APP_WORK"
  APP_PID=""; APP_DB=""; APP_WORK=""
}
