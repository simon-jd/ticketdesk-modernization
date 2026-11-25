#!/usr/bin/env bash
# Azure Service Bus emulator for the notification worker (round 3): mcr.microsoft.com/azure-messaging/servicebus-emulator
# with its own SQL Server 2022 container as the emulator's store (not td-sql: the emulator creates its own databases).
#
#   ops/dev/servicebus-emulator.sh up       network td-sb, containers td-sb-sql + td-sb-emulator, AMQP on 127.0.0.1:5673,
#                                           health on 127.0.0.1:5301; queues from ops/dev/servicebus-emulator/Config.json
#   ops/dev/servicebus-emulator.sh status   containers + emulator health
#   ops/dev/servicebus-emulator.sh conn     the emulator connection string (the emulator's fixed, public development key)
#   ops/dev/servicebus-emulator.sh down     remove both containers (queues are re-created from Config.json on the next up)
#
# The store's sa password is generated into $TD_W/.secrets/td-sb.env (0600) and never echoed. Images: the emulator
# image pinned by digest, the store the same official mcr 2022 image td-sql is built on.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NET=td-sb
SQL=td-sb-sql
EMU=td-sb-emulator
AMQP_PORT="${TD_SB_AMQP_PORT:-5673}"
HEALTH_PORT="${TD_SB_HEALTH_PORT:-5301}"
EMU_IMAGE="${TD_SB_IMAGE:-mcr.microsoft.com/azure-messaging/servicebus-emulator@sha256:5a96d893b245031740f7d46e0fe5ff282d24b78c4b7d761dd57590f3f010a9b3}"
SQL_IMAGE="${TD_SB_SQL_IMAGE:-mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1}"
ENV_FILE="$TD_W/.secrets/td-sb.env"

ensure_secret() {
  [[ -f "$ENV_FILE" ]] && return
  install -d -m 0700 "$TD_W/.secrets"; umask 077
  printf 'MSSQL_SA_PASSWORD=%sAa1!\n' "$(openssl rand -hex 16)" > "$ENV_FILE"; chmod 0600 "$ENV_FILE"
}

cmd_up() {
  ensure_secret
  docker network inspect "$NET" >/dev/null 2>&1 || docker network create "$NET" >/dev/null
  if ! docker container inspect "$SQL" >/dev/null 2>&1; then
    docker run -d --name "$SQL" --network "$NET" --network-alias sb-sql -e ACCEPT_EULA=Y --env-file "$ENV_FILE" \
      -e MSSQL_MEMORY_LIMIT_MB=2048 --memory 3g "$SQL_IMAGE" >/dev/null
  else docker start "$SQL" >/dev/null; fi
  if ! docker container inspect "$EMU" >/dev/null 2>&1; then
    docker run -d --name "$EMU" --network "$NET" -e ACCEPT_EULA=Y -e SQL_SERVER=sb-sql --env-file "$ENV_FILE" -e SQL_WAIT_INTERVAL=15 \
      -v "$HERE/servicebus-emulator/Config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro" \
      -p "127.0.0.1:${AMQP_PORT}:5672" -p "127.0.0.1:${HEALTH_PORT}:5300" "$EMU_IMAGE" >/dev/null
  else docker start "$EMU" >/dev/null; fi
  local i
  for i in $(seq 1 90); do
    if curl -fsS "http://127.0.0.1:${HEALTH_PORT}/health" >/dev/null 2>&1; then echo "emulator healthy after ~$((i * 2)) s"; return 0; fi
    sleep 2
  done
  echo "emulator not healthy" >&2; docker logs --tail 40 "$EMU" >&2; exit 1
}

cmd_status() {
  docker ps -a --filter "name=^/td-sb-" --format '{{.Names}}  {{.Status}}  {{.Ports}}'
  curl -fsS "http://127.0.0.1:${HEALTH_PORT}/health" && echo || echo "health: unreachable"
}

case "${1:-}" in
  up) cmd_up ;;
  status) cmd_status ;;
  conn) echo "Endpoint=sb://localhost:${AMQP_PORT};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;" ;;
  down) docker rm -f "$EMU" "$SQL" >/dev/null 2>&1 || true; docker network rm "$NET" >/dev/null 2>&1 || true; echo removed ;;
  *) sed -n '2,13p' "$0"; exit 2 ;;
esac
