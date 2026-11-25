#!/usr/bin/env bash
# The CD pipeline's local environment "kind-local": kind cluster td-cd on the agent host, the new app (web + worker) against
# td-sql through a relay, its own Service Bus emulator. Everything here belongs to the pipelines workstream:
# cluster td-cd (kubeconfig $TD_W/.tools/ado-agent/kube/td-cd.kubeconfig, not ~/.kube/config), Docker network td-cd-kind,
# containers td-cd-sb-sql / td-cd-sb-emulator, tmux session td-pipelines-cd-relay, database TicketDesk_cd + login td_cd_app
# on td-sql (password in $TD_W/.secrets/td-cd-app.env, generated, never printed). Shared containers are only connected to.
#
#   cd-kind.sh up            cluster, relay, Service Bus emulator (idempotent)
#   cd-kind.sh db            TicketDesk_cd (first time: copy of td-sql TicketDesk), td-db migrate, login/user in td_modern_app_role
#   cd-kind.sh deploy TAG    kind load ticketdesk-{web,worker}:TAG, secrets, apply manifests, wait for the rollout
#   cd-kind.sh smoke [DIR]   readiness, sign-in page, Angular bundle, worker running; writes DIR/smoke.txt
#   cd-kind.sh status | down (down removes all of the above, incl. the database and the login)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
CLUSTER=td-cd; NET=td-cd-kind; NS=ticketdesk; HOST_PORT=18480; NODE_PORT=30480
NODE_IMAGE="${TD_CD_NODE_IMAGE:-kindest/node:v1.37.0@sha256:a1ed56cfb0e7b93589bdf97c8cd566405a265939e3620fc4f5de89adff580ae5}"
DB=TicketDesk_cd; LOGIN=td_cd_app; FWD_PORT=14335
RELAY=td-pipelines-cd-relay
SB_SQL=td-cd-sb-sql; SB_EMU=td-cd-sb-emulator; SB_ALIAS=td-cd-sb
SB_IMAGE="mcr.microsoft.com/azure-messaging/servicebus-emulator@sha256:5a96d893b245031740f7d46e0fe5ff282d24b78c4b7d761dd57590f3f010a9b3"
SB_SQL_IMAGE="mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1"
APP_ENV="$TD_W/.secrets/td-cd-app.env"; SB_ENV="$TD_W/.secrets/td-cd-sb.env"
export KUBECONFIG="$TD_W/.tools/ado-agent/kube/td-cd.kubeconfig"
mkdir -p "$(dirname "$KUBECONFIG")"
log() { echo "$(date -u +%FT%TZ) $*"; }
kc() { kubectl --context "kind-$CLUSTER" "$@"; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
gw() { docker network inspect "$NET" -f '{{range .IPAM.Config}}{{.Gateway}} {{end}}' | tr ' ' '\n' | grep -m1 -E '^[0-9.]+$'; }
sa() { SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,14333 -U sa -C -b -h -1 -W "$@"; }
secret_file() { [[ -f "$1" ]] || { (umask 077; printf '%s=%sTd-Cd-1\n' "$2" "$(openssl rand -hex 16)" > "$1"); }; chmod 600 "$1"; }
mask() { [[ -n "${TF_BUILD:-}" ]] && printf '##vso[task.setsecret]%s\n' "$1"; return 0; }

cmd_up() {
  if ! kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then
    local cfg; cfg="$(mktemp)"
    printf '%s\n' "kind: Cluster" "apiVersion: kind.x-k8s.io/v1alpha4" "nodes:" "  - role: control-plane" "    extraPortMappings:" \
      "      - { containerPort: $NODE_PORT, hostPort: $HOST_PORT, listenAddress: \"127.0.0.1\", protocol: TCP }" > "$cfg"
    KIND_EXPERIMENTAL_DOCKER_NETWORK="$NET" kind create cluster --name "$CLUSTER" --image "$NODE_IMAGE" --config "$cfg" --wait 180s
    rm -f "$cfg"
  fi
  kind export kubeconfig --name "$CLUSTER" >/dev/null
  kc get nodes -o wide --no-headers
  if tmux has-session -t "$RELAY" 2>/dev/null; then log "relay running (tmux $RELAY)"; else
    install -D -m 0644 "$HERE/tcp_relay.py" "$TD_W/.tools/ado-agent/kube/tcp_relay.py"
    tmux new-session -d -s "$RELAY" "python3 -I '$TD_W/.tools/ado-agent/kube/tcp_relay.py' '$(gw):$FWD_PORT=127.0.0.1:14333' 2>&1 | tee -i -a '$TD_W/logs/td-cd-relay.log'"
    sleep 1; tmux has-session -t "$RELAY" 2>/dev/null || { echo "relay did not start" >&2; exit 1; }
    log "relay $(gw):$FWD_PORT -> td-sql (tmux $RELAY)"
  fi
  secret_file "$SB_ENV" MSSQL_SA_PASSWORD
  docker container inspect "$SB_SQL" >/dev/null 2>&1 || docker run -d --name "$SB_SQL" --network "$NET" --network-alias "$SB_SQL" \
    -e ACCEPT_EULA=Y --env-file "$SB_ENV" -e MSSQL_MEMORY_LIMIT_MB=2048 --memory 3g --restart unless-stopped "$SB_SQL_IMAGE" >/dev/null
  install -D -m 0644 "$REPO/ops/dev/servicebus-emulator/Config.json" "$TD_W/.tools/ado-agent/kube/sb-config.json"
  docker container inspect "$SB_EMU" >/dev/null 2>&1 || docker run -d --name "$SB_EMU" --network "$NET" --network-alias "$SB_ALIAS" \
    -e ACCEPT_EULA=Y -e SQL_SERVER="$SB_SQL" --env-file "$SB_ENV" -e SQL_WAIT_INTERVAL=15 --restart unless-stopped \
    -v "$TD_W/.tools/ado-agent/kube/sb-config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro" "$SB_IMAGE" >/dev/null
  docker start "$SB_SQL" "$SB_EMU" >/dev/null
  log "Service Bus emulator $SB_EMU on $NET (alias $SB_ALIAS:5672)"
}

cmd_db() {
  if [[ "$(sa -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DB') IS NULL THEN 0 ELSE 1 END")" != 1 ]]; then
    bash "$REPO/ops/dev/e2e-db.sh" "$DB"
  else log "$DB exists (kept across deployments)"; fi
  dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -nologo -v quiet >/dev/null
  local sapw; sapw="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)"; mask "$sapw"
  TD_DB_CONN="Server=127.0.0.1,14333;Database=$DB;User Id=sa;Password=$sapw;TrustServerCertificate=True;Encrypt=True" \
    "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate | tail -3
  secret_file "$APP_ENV" TD_CD_APP_PASSWORD
  local pw; pw="$(envval "$APP_ENV" TD_CD_APP_PASSWORD)"; mask "$pw"
  # the password goes in as a sqlcmd variable from the environment, never on a command line
  TD_APP_PW="$pw" sa -Q "SET NOCOUNT ON; DECLARE @pw nvarchar(256) = REPLACE(N'\$(TD_APP_PW)', N'''', N''''''), @s nvarchar(max);
    IF SUSER_ID(N'$LOGIN') IS NULL SET @s = N'CREATE LOGIN $LOGIN WITH PASSWORD = N''' + @pw + N''', DEFAULT_DATABASE = [$DB], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
    ELSE SET @s = N'ALTER LOGIN $LOGIN WITH PASSWORD = N''' + @pw + N'''';
    EXEC (@s);"
  sa -d "$DB" -i "$REPO/ops/db/td_modern_app_role.sql" >/dev/null
  sa -d "$DB" -Q "SET NOCOUNT ON; IF USER_ID(N'$LOGIN') IS NULL CREATE USER $LOGIN FOR LOGIN $LOGIN; ALTER ROLE td_modern_app_role ADD MEMBER $LOGIN;"
  log "$DB migrated; $LOGIN in td_modern_app_role"
}

cmd_deploy() {
  local tag="${1:?image tag}" tmp pw
  for app in web worker; do
    docker image inspect "ticketdesk-$app:$tag" >/dev/null 2>&1 && continue
    local have; have="$(docker image ls "ticketdesk-$app" --format '{{.Tag}}' | grep -E '^ci-[0-9]+$' | sort -t- -k2 -n | tr '\n' ' ')"
    msg="image ticketdesk-$app:$tag is not on the agent host (pruned after 30 h by azure-pipelines/scripts/image-prune.sh, or never built). Queue the CD with a CI run that still has its images (imageTag=ci-<run id>; present: ${have:-none}) or run the CI again."
    [[ -n "${TF_BUILD:-}" ]] && echo "##vso[task.logissue type=error]$msg"
    echo "$msg" >&2; exit 1
  done
  kind load docker-image "ticketdesk-web:$tag" "ticketdesk-worker:$tag" --name "$CLUSTER" 2>&1 | tail -2
  kc create namespace "$NS" --dry-run=client -o yaml | kc apply -f - >/dev/null
  pw="$(envval "$APP_ENV" TD_CD_APP_PASSWORD)"; mask "$pw"
  tmp="$(mktemp -d)"; chmod 700 "$tmp"; trap 'rm -rf "$tmp"' RETURN
  printf 'connection=Server=%s,%s;Database=%s;User Id=%s;Password=%s;TrustServerCertificate=True;Encrypt=True\n' "$(gw)" "$FWD_PORT" "$DB" "$LOGIN" "$pw" > "$tmp/db.env"
  printf 'connection=Endpoint=sb://%s;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;\n' "$SB_ALIAS" > "$tmp/sb.env"
  kc -n "$NS" create secret generic td-db --from-env-file="$tmp/db.env" --dry-run=client -o yaml | kc apply -f - >/dev/null
  kc -n "$NS" create secret generic td-sb --from-env-file="$tmp/sb.env" --dry-run=client -o yaml | kc apply -f - >/dev/null
  sed -e "s/__TAG__/$tag/g" -e "s/__DEPLOYED__/$(date -u +%Y%m%dT%H%M%SZ)/g" "$HERE/manifests.yaml" | kc apply -f -
  kc -n "$NS" rollout status deployment/td-web --timeout=300s
  kc -n "$NS" rollout status deployment/td-worker --timeout=300s
  kc -n "$NS" get pods -o wide --no-headers
}

cmd_smoke() {
  local out="${1:-$(mktemp -d)}" base="http://127.0.0.1:$HOST_PORT" fail=0 r wp; mkdir -p "$out"
  : > "$out/smoke.txt"
  say() { echo "$*" | tee -a "$out/smoke.txt"; }
  say "# $(date -u +%FT%TZ) kind cluster $CLUSTER, $base"
  kc -n "$NS" get deploy -o custom-columns=NAME:.metadata.name,READY:.status.readyReplicas,IMAGE:.spec.template.spec.containers[0].image --no-headers | tee -a "$out/smoke.txt"
  # right after a rollout the node port can still route a request to the terminating pod (CD run 23: curl exit 56 after
  # 136 s): every request has a 5 s limit and each check is retried for up to 60 s; a curl error is a result, not an abort
  local check path want n
  for check in "/health/ready 200" "/user/sign-in 200" "/tickets 302"; do
    path="${check% *}"; want="${check#* }"
    for n in $(seq 1 12); do
      r="$(curl -s --max-time 5 -o /dev/null -w '%{http_code} %{time_total}s %{redirect_url}' "$base$path" 2>/dev/null || echo "000 curl-exit-$?")"
      [[ "$r" == "$want "* ]] && break; sleep 5
    done
    say "GET $path -> $r (expected $want, attempt $n)"; [[ "$r" == "$want "* ]] || fail=1
  done
  r="$(curl -s --max-time 5 -D - -o /dev/null "$base/user/sign-in" 2>/dev/null | tr -d '\r' | grep -i '^x-ticketdesk-backend' || true)"
  say "backend header on /user/sign-in: ${r:-none}"; [[ "$r" == *modern* ]] || fail=1
  wp="$(kc -n "$NS" get pods -l app.kubernetes.io/name=td-worker -o jsonpath='{.items[0].status.phase} restarts={.items[0].status.containerStatuses[0].restartCount}')"
  say "worker: $wp"; [[ "$wp" == Running* ]] || fail=1
  say "smoke: $([[ $fail == 0 ]] && echo passed || echo FAILED)"
  return $fail
}

cmd_status() {
  kind get clusters 2>/dev/null | grep -x "$CLUSTER" && kc -n "$NS" get deploy,pods -o wide 2>/dev/null || echo "no cluster $CLUSTER"
  tmux has-session -t "$RELAY" 2>/dev/null && echo "relay: tmux $RELAY" || echo "relay: not running"
  docker ps -a --filter "name=td-cd-sb" --format '{{.Names}} {{.Status}}'
}

cmd_down() {
  kind delete cluster --name "$CLUSTER" 2>&1 | tail -1 || true
  tmux kill-session -t "$RELAY" 2>/dev/null || true
  docker rm -f "$SB_EMU" "$SB_SQL" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  sa -Q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END; IF SUSER_ID(N'$LOGIN') IS NOT NULL DROP LOGIN $LOGIN;" || true
  log "td-cd environment removed"
}

case "${1:-}" in
  up) cmd_up ;; db) cmd_db ;; deploy) shift; cmd_deploy "$@" ;; smoke) shift; cmd_smoke "$@" ;; status) cmd_status ;; down) cmd_down ;;
  *) sed -n '2,13p' "$0"; exit 2 ;;
esac
