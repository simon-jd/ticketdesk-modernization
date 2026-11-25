#!/usr/bin/env bash
# Dev/test SQL Server 2022 container for the TicketDesk modernization.
#
#   ops/dev/td-sql.sh up                  create (or start) td-sql on 127.0.0.1:14333
#   ops/dev/td-sql.sh status              container state + server version
#   ops/dev/td-sql.sh sql [sqlcmd args]   run sqlcmd inside the container as sa
#   ops/dev/td-sql.sh restore BAK [DB]    restore a .bak into DB (default TicketDesk)
#   ops/dev/td-sql.sh conn [DB]           print an ADO.NET connection string (contains the password; pipe it, don't log it)
#   ops/dev/td-sql.sh down                stop the container (data stays on the volume)
#   ops/dev/td-sql.sh destroy             remove container AND the data volume
#   ops/dev/td-sql.sh build-image         build the full-text image (ops/dev/td-sql-image: official image + mssql-server-fts)
#   ops/dev/td-sql.sh recreate            replace the container by one from $IMAGE: same name, port, volume (databases), env;
#                                         the old container is kept stopped as td-sql-previous until the next recreate
#
# The sa password lives in $TD_W/.secrets/td-sql.env (created on first `up`, mode 0600) and is
# never echoed. Override the image with TD_SQL_IMAGE=... (default since round 3: the full-text image built by
# build-image on top of the official mcr 2022 image; Full-Text Search is needed by td-db V0011 and the search module).
set -euo pipefail

TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
NAME="${TD_SQL_NAME:-td-sql}"
VOLUME="${TD_SQL_VOLUME:-td-sql-data}"
PORT="${TD_SQL_PORT:-14333}"
FTS_BASE="${TD_SQL_FTS_BASE:-mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1}"  # 2022 CU25 (16.0.4262.2)
FTS_VERSION="${TD_SQL_FTS_VERSION:-16.0.4262.2-1}"  # mssql-server-fts build = the base image's ProductVersion
IMAGE="${TD_SQL_IMAGE:-ticketdesk/td-sql:2022-fts-${FTS_VERSION%-*}}"
ENV_FILE="$TD_W/.secrets/td-sql.env"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

ensure_secret() {
  if [[ ! -f "$ENV_FILE" ]]; then
    install -d -m 0700 "$TD_W/.secrets"
    umask 077
    # SQL Server policy: 8+ chars from 3 of 4 classes. 32 random hex chars + a fixed suffix
    # that supplies the upper-case and symbol classes. (No `tr </dev/urandom | head`: with
    # pipefail the SIGPIPE would abort the script.)
    local pw
    pw="$(openssl rand -hex 16)Aa1!"
    printf 'ACCEPT_EULA=Y\nMSSQL_PID=Developer\nMSSQL_SA_PASSWORD=%s\n' "$pw" >"$ENV_FILE"
    chmod 0600 "$ENV_FILE"
    echo "created $ENV_FILE"
  fi
}

sa_password() { sed -n 's/^MSSQL_SA_PASSWORD=//p' "$ENV_FILE"; }

wait_ready() {
  local i
  for i in $(seq 1 90); do
    if SQLCMDPASSWORD="$(sa_password)" docker exec -e SQLCMDPASSWORD "$NAME" \
         "$SQLCMD" -S localhost -U sa -C -b -l 2 -Q "SELECT 1" >/dev/null 2>&1; then
      echo "td-sql ready after ~$((i * 2))s"; return 0
    fi
    sleep 2
  done
  echo "td-sql did not become ready" >&2; docker logs --tail 40 "$NAME" >&2; return 1
}

run_sql() {
  # stdin is attached only for `sql` with no -Q/-i (interactive or piped SQL); otherwise a caller's heredoc would be swallowed
  local tty=()
  if [[ "${TD_SQL_STDIN:-0}" == 1 ]]; then tty=(-i); fi
  SQLCMDPASSWORD="$(sa_password)" docker exec "${tty[@]}" -e SQLCMDPASSWORD "$NAME" \
    "$SQLCMD" -S localhost -U sa -C -b "$@"
}

cmd_up() {
  ensure_secret
  if docker container inspect "$NAME" >/dev/null 2>&1; then
    docker start "$NAME" >/dev/null
  else
    docker volume create "$VOLUME" >/dev/null
    docker run -d --name "$NAME" \
      --env-file "$ENV_FILE" \
      -e MSSQL_MEMORY_LIMIT_MB="${TD_SQL_MEMORY_MB:-8192}" \
      -p "127.0.0.1:${PORT}:1433" \
      -v "${VOLUME}:/var/opt/mssql" \
      --memory "${TD_SQL_CONTAINER_MEM:-10g}" \
      --restart unless-stopped \
      "$IMAGE" >/dev/null
  fi
  wait_ready
}

cmd_status() {
  docker ps -a --filter "name=^/${NAME}$" --format '{{.Names}}  {{.Status}}  {{.Ports}}  {{.Image}}'
  run_sql -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(SERVERPROPERTY('ProductUpdateLevel') AS nvarchar(16)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(64)); SELECT name FROM sys.databases WHERE database_id > 4;"
}

cmd_restore() {
  local bak="${1:?usage: restore BAK [DB]}" db="${2:-TicketDesk}"
  [[ -f "$bak" ]] || { echo "no such file: $bak" >&2; exit 1; }
  docker exec "$NAME" mkdir -p /var/opt/mssql/backup
  docker cp "$bak" "$NAME:/var/opt/mssql/backup/$(basename "$bak")"
  docker exec -u 0 "$NAME" chown mssql /var/opt/mssql/backup/"$(basename "$bak")" 2>/dev/null || true
  local remote="/var/opt/mssql/backup/$(basename "$bak")"
  # Build MOVE clauses from the backup's own file list so any logical names work.
  local moves
  moves="$(run_sql -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$remote';" \
    | awk -F'|' -v db="$db" 'NF>3 && $1!="" {
        ext = ($3=="L") ? "_log.ldf" : (n++ ? "_" n ".ndf" : ".mdf");
        printf "%sMOVE N'\''%s'\'' TO N'\''/var/opt/mssql/data/%s%s'\''", (c++ ? ", " : ""), $1, db, ext }')"
  [[ -n "$moves" ]] || { echo "could not read file list from $bak" >&2; exit 1; }
  run_sql -Q "IF DB_ID(N'$db') IS NOT NULL ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$db] FROM DISK = N'$remote' WITH REPLACE, $moves, STATS = 25;
ALTER DATABASE [$db] SET MULTI_USER;"
}

cmd_build_image() {
  local here; here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  docker build --build-arg BASE="$FTS_BASE" --build-arg FTS_VERSION="$FTS_VERSION" -t "$IMAGE" "$here/td-sql-image"
}

cmd_recreate() {
  ensure_secret
  docker image inspect "$IMAGE" >/dev/null 2>&1 || { echo "no image $IMAGE (run build-image)" >&2; exit 1; }
  if docker container inspect "$NAME" >/dev/null 2>&1; then
    docker rm -f "${NAME}-previous" >/dev/null 2>&1 || true
    docker stop -t 60 "$NAME" >/dev/null        # clean shutdown: checkpoint every database on the volume
    docker rename "$NAME" "${NAME}-previous"
    docker update --restart no "${NAME}-previous" >/dev/null
    echo "previous container kept (stopped) as ${NAME}-previous"
  fi
  cmd_up
  # the full-text package must be the engine's own build (td-sql-image/Dockerfile)
  local check
  check="$(run_sql -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ' ' + CAST(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS varchar(1));" | tr -d '\r')"
  echo "engine ${check% *}, full-text installed: ${check##* }"
  [[ "$check" == "${FTS_VERSION%-*} 1" ]] || { echo "expected engine ${FTS_VERSION%-*} with full-text" >&2; exit 1; }
}

cmd_conn() {
  local db="${1:-TicketDesk}"
  printf 'Server=127.0.0.1,%s;Database=%s;User Id=sa;Password=%s;TrustServerCertificate=True;Encrypt=True\n' \
    "$PORT" "$db" "$(sa_password)"
}

case "${1:-}" in
  up) cmd_up ;;
  status) cmd_status ;;
  sql) shift; if [[ " $* " != *" -Q "* && " $* " != *" -i "* ]]; then TD_SQL_STDIN=1; fi; run_sql "$@" ;;
  restore) shift; cmd_restore "$@" ;;
  conn) shift; cmd_conn "$@" ;;
  down) docker stop "$NAME" ;;
  destroy) docker rm -f "$NAME" 2>/dev/null || true; docker volume rm "$VOLUME" ;;
  build-image) cmd_build_image ;;
  recreate) cmd_recreate ;;
  *) sed -n '2,19p' "$0"; exit 2 ;;
esac
