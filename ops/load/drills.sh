#!/usr/bin/env bash
# drills.sh - Round 6 load: failure drills on kind under steady load, and the restore drill on kind against td-sql.
#
#   drills.sh up                 kind cluster td-load (network td-load-kind, node port 30443 -> 127.0.0.1:18453; node cpuset
#                                $KIND_CPUS), td-load-sql joined to that network (cpuset $SQL_CPUS), a Service Bus emulator on
#                                it, the SMTP sink on the network gateway (headers only), the relay gateway:14336 -> td-sql
#   drills.sh db                 TicketDesk on td-load-sql = the capacity base (new-base.bak) + push notifications on (SMTP,
#                                consolidation and broadcast off, the help-desk users get an e-mail destination)
#   drills.sh deploy [ARGS]      chart with images ci-42 (9675f3e = this branch's app code), migrate job off (the database is
#                                migrated), every page a server page (the k6 mix reads their HTML), SMTP to the sink; ARGS to helm
#   drills.sh drill NAME RATE    steady k6 load at RATE ops/s; at +120 s the fault NAME (pod-kill | worker-kill | db-egress |
#                                sql-restart), recovery, load until +LOAD_S; pod Ready/events every second; evidence under
#                                $EV/drill-NAME[-SUFFIX]
#   drills.sh reset              before a drill: database back to the capacity base + push on, web and worker restarted, primed
#   drills.sh restore-drill N [RATE]   restore drill run N against td-sql (see cmd_restore_drill)
#   drills.sh down               cluster, emulator, sink, relay, scratch databases on td-sql, td-load-sql off the network
# Everything here is this agent's own; td-sql is only connected to (scratch databases TicketDesk_load_rd*, login td_load_app).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
# shellcheck disable=SC1091
. "$TD_W/.tools/env.sh"
CLUSTER=td-load; NET=td-load-kind; NS=ticketdesk; HOST_PORT=18453; NODE_PORT=30443; URL="https://localhost:$HOST_PORT"
KIND_CPUS="${TD_DRILL_KIND_CPUS:-24-27}"; SQL_CPUS="${TD_DRILL_SQL_CPUS:-20-23}"; K6_CPUS=16-19; AUX_CPUS=0-1
SQL_NAME=td-load-sql; SB_EMU=td-load-sb; SB_SQL=td-load-sb-sql; TAG="${TD_DRILL_TAG:-ci-42}"
SB_IMAGE="mcr.microsoft.com/azure-messaging/servicebus-emulator@sha256:5a96d893b245031740f7d46e0fe5ff282d24b78c4b7d761dd57590f3f010a9b3"
SB_SQL_IMAGE="mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1"
RUN="$TD_W/logs/load"; SINK_PORT=2526; FWD_PORT=14336
EV="${TD_DRILL_EV:-$REPO/docs/evidence/load-20261114}"
CA="$TD_W/.secrets/legacy-lab-ca"; SB_ENV="$TD_W/.secrets/td-load-sb.env"; RD_ENV="$TD_W/.secrets/td-load-rd-app.env"
LOAD_S="${TD_DRILL_LOAD_S:-360}"; FAULT_AT="${TD_DRILL_FAULT_AT:-120}"
mkdir -p "$RUN"
log() { echo "$(date -u +%FT%T.%3NZ) $*"; }
die() { echo "drills.sh: $*" >&2; exit 1; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
kc() { kubectl --context "kind-$CLUSTER" "$@"; }
h() { helm --kube-context "kind-$CLUSTER" "$@"; }
gw() { docker network inspect "$NET" -f '{{range .IPAM.Config}}{{.Gateway}} {{end}}' | tr ' ' '\n' | grep -m1 -E '^[0-9.]+$'; }
sql_ip() { docker inspect -f "{{(index .NetworkSettings.Networks \"$NET\").IPAddress}}" "$SQL_NAME"; }
lsql() {  # lsql DB SQL: td-load-sql as sa
  SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -e SQLCMDPASSWORD "$SQL_NAME" \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1" -h -1 -W -Q "$2"
}
tsql() { "$REPO/ops/dev/td-sql.sh" sql -b -h -1 -W "$@"; }   # the shared td-sql (scratch databases only)
secret_file() { [[ -f "$1" ]] || { (umask 077; printf '%s=%sAa1!\n' "$2" "$(openssl rand -hex 16)" > "$1"); chmod 600 "$1"; }; }
pidfile_up() { [[ -f "$1" ]] && kill -0 "$(cat "$1")" 2>/dev/null; }
utc_ms() { date -u +%FT%T.%3NZ; }

cmd_up() {
  if ! kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then
    local cfg; cfg="$(mktemp)"
    printf '%s\n' "kind: Cluster" "apiVersion: kind.x-k8s.io/v1alpha4" "nodes:" "  - role: control-plane" "    extraPortMappings:" \
      "      - { containerPort: $NODE_PORT, hostPort: $HOST_PORT, listenAddress: \"127.0.0.1\", protocol: TCP }" > "$cfg"
    KIND_EXPERIMENTAL_DOCKER_NETWORK="$NET" kind create cluster --name "$CLUSTER" --config "$cfg" --wait 180s
    rm -f "$cfg"
  fi
  docker update --cpuset-cpus "$KIND_CPUS" "$CLUSTER-control-plane" >/dev/null
  docker network inspect "$NET" -f '{{range $k, $v := .Containers}}{{$v.Name}} {{end}}' | grep -qw "$SQL_NAME" || docker network connect "$NET" "$SQL_NAME"
  docker update --cpuset-cpus "$SQL_CPUS" "$SQL_NAME" >/dev/null
  secret_file "$SB_ENV" MSSQL_SA_PASSWORD
  docker container inspect "$SB_SQL" >/dev/null 2>&1 || docker run -d --name "$SB_SQL" --network "$NET" --network-alias "$SB_SQL" \
    -e ACCEPT_EULA=Y --env-file "$SB_ENV" -e MSSQL_MEMORY_LIMIT_MB=2048 --memory 3g --cpuset-cpus "$AUX_CPUS" "$SB_SQL_IMAGE" >/dev/null
  docker container inspect "$SB_EMU" >/dev/null 2>&1 || docker run -d --name "$SB_EMU" --network "$NET" --network-alias "$SB_EMU" \
    -e ACCEPT_EULA=Y -e SQL_SERVER="$SB_SQL" --env-file "$SB_ENV" -e SQL_WAIT_INTERVAL=15 --cpuset-cpus "$AUX_CPUS" \
    -v "$REPO/ops/dev/servicebus-emulator/Config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro" "$SB_IMAGE" >/dev/null
  docker start "$SB_SQL" "$SB_EMU" >/dev/null
  if ! pidfile_up "$RUN/td-sql-forward.pid"; then
    nohup python3 -I "$REPO/ops/k8s/tcp_forward.py" "$(gw):$FWD_PORT" "127.0.0.1:14333" > "$RUN/td-sql-forward.log" 2>&1 &
    echo $! > "$RUN/td-sql-forward.pid"
  fi
  kc get nodes -o wide | tail -1
  log "kind $CLUSTER (node cpuset $KIND_CPUS), $SQL_NAME at $(sql_ip) (cpuset $SQL_CPUS), emulator $SB_EMU, relay $(gw):$FWD_PORT -> td-sql"
}

sink_start() {  # sink_start DIR [HOLDFILE]
  sink_stop
  mkdir -p "$1"
  taskset -c "$AUX_CPUS" python3 -I "$REPO/ops/dev/smtp-sink.py" --dir "$1" --host "$(gw)" --port "$SINK_PORT" --headers-only ${2:+--hold-file "$2"} \
    > "$1.log" 2>&1 &
  echo $! > "$RUN/sink.pid"; sleep 1; pidfile_up "$RUN/sink.pid" || die "sink did not start ($(cat "$1.log"))"
}
sink_stop() { if [[ -f "$RUN/sink.pid" ]]; then kill "$(cat "$RUN/sink.pid")" 2>/dev/null || true; rm -f "$RUN/sink.pid"; fi; }

PUSH_JSON='{"DeliveryProviderSettings":[{"ProviderAssemblyQualifiedName":"TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider, TicketDesk.PushNotifications, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null","IsEnabled":true,"ProviderConfigurationData":{"SmtpServer":"__GW__","SmtpPort":__PORT__,"EnableSsl":false,"SmtpUserName":null,"SmtpPassword":null,"SmtpFromAddress":"ticketdesk@lab.test","SmtpFromDisplayName":"TicketDesk"}}],"IsEnabled":true,"IsBackgroundQueueEnabled":true,"DeliveryIntervalMinutes":1,"RetryAttempts":5,"RetryIntervalMinutes":1,"AntiNoiseSettings":{"IsConsolidationEnabled":false,"InitialConsolidationDelayMinutes":2,"MaxConsolidationDelayMinutes":10,"ExcludeSubscriberEvents":true},"BroadcastSettings":{"BroadcastMode":1,"IsBroadcastEnabled":false,"SendToCustomEmailAddress":null,"SendToCustomEmailDisplayName":null}}'

push_on() {  # push_on SQLFN DB: push notifications to the sink; one e-mail destination per seeded help-desk user
  local fn="$1" db="$2" users
  users="$(python3 -I -c 'import json, sys; print(",".join("N'"'"'" + u + "'"'"'" for u in json.load(open(sys.argv[1]))))' "$REPO/tests/perf/users-helpdesk.json")"
  "$fn" "$db" "SET NOCOUNT ON;
UPDATE notifications.ApplicationPushNotificationSettings SET PushNotificationSettingsJson = N'$(sed -e "s/__GW__/$(gw)/" -e "s/__PORT__/$SINK_PORT/" <<<"$PUSH_JSON")' WHERE ApplicationName = N'TicketDesk';
INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) SELECT u.Id, 1 FROM dbo.IdentityUsers u WHERE u.Email IN ($users) AND NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings s WHERE s.SubscriberId = u.Id);
UPDATE s SET IsEnabled = 1 FROM notifications.SubscriberPushNotificationSettings s JOIN dbo.IdentityUsers u ON u.Id = s.SubscriberId WHERE u.Email IN ($users);
INSERT notifications.PushNotificationDestinations (SubscriberName, DestinationAddress, DestinationType, SubscriberId) SELECT u.DisplayName, N'drill+' + CAST(u.Id AS nvarchar(128)) + N'@lab.test', N'email', u.Id FROM dbo.IdentityUsers u WHERE u.Email IN ($users) AND NOT EXISTS (SELECT 1 FROM notifications.PushNotificationDestinations d WHERE d.SubscriberId = u.Id AND d.DestinationAddress LIKE N'drill+%');
SELECT 'push on; destinations: ' + CAST(COUNT(*) AS varchar(10)) FROM notifications.PushNotificationDestinations WHERE DestinationAddress LIKE N'drill+%';"
}

cmd_db() {
  lsql master "SET NOCOUNT ON; IF DB_ID(N'TicketDesk') IS NOT NULL ALTER DATABASE TicketDesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE TicketDesk FROM DISK = N'/var/opt/mssql/backup/new-base.bak' WITH REPLACE; ALTER DATABASE TicketDesk SET MULTI_USER;" >/dev/null
  push_on lsql TicketDesk
}

registry_json() {
  python3 -I -c 'import json, sys
reg = json.load(open(sys.argv[1])); reg["Migration"]["Routes"] = {k: "New" for k in reg["Migration"]["Routes"]}
print(json.dumps(reg))' "$REPO/src/modern/TicketDesk.Web/migration-registry.json"
}

cmd_secrets() {  # td-db -> td-load-sql as td_modern_app; td-sb; td-tls
  local apppw chain
  apppw="$(envval "$TD_W/.secrets/td-modern-app.env" TD_MODERN_APP_SQL_PASSWORD)"
  kc get ns "$NS" >/dev/null 2>&1 || kc create ns "$NS" >/dev/null
  kc -n "$NS" create secret generic td-db --from-literal=connection-string="Server=$SQL_NAME,1433;Database=TicketDesk;User ID=td_modern_app;Password=$apppw;TrustServerCertificate=True;Encrypt=True;Connect Timeout=15" --dry-run=client -o yaml | kc apply -f - >/dev/null
  kc -n "$NS" create secret generic td-db-migrator --from-literal=connection-string="unused" --dry-run=client -o yaml | kc apply -f - >/dev/null
  kc -n "$NS" create secret generic td-sb --from-literal=connection-string="Endpoint=sb://$SB_EMU:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;" --dry-run=client -o yaml | kc apply -f - >/dev/null
  chain="$(mktemp)"; cat "$CA/aks-web.crt" "$CA/ca.crt" > "$chain"
  kc -n "$NS" create secret tls td-tls --cert="$chain" --key="$CA/aks-web.key" --dry-run=client -o yaml | kc apply -f - >/dev/null
  rm -f "$chain"
}

cmd_deploy() {
  local vals="$RUN/values-drill.yaml" t0=$SECONDS
  for n in web worker; do kind load docker-image --name "$CLUSTER" "ticketdesk-$n:$TAG" 2>&1 | tail -1; done
  cmd_secrets
  python3 -I - "$(registry_json)" "$(gw)" "$SINK_PORT" > "$vals" <<'PY'
import json, sys
reg, gw, port = sys.argv[1:4]
print(json.dumps({"migrate": {"enabled": False}, "web": {"migrationRegistry": reg},
                  "worker": {"mailPickup": False, "extraEnv": [{"name": "Worker__Mail__SmtpHost", "value": gw},
                                                               {"name": "Worker__Mail__SmtpPort", "value": port}]}}, indent=1))
PY
  h upgrade --install td "$REPO/ops/k8s/ticketdesk" -n "$NS" -f "$REPO/ops/k8s/values-kind.yaml" -f "$vals" --set image.tag="$TAG" \
    --wait --timeout 10m "$@"
  log "deployed $TAG in $((SECONDS - t0)) s"
  kc -n "$NS" get pods -o wide
}

watch_pods() {  # watch_pods OUT STOPFILE: every second, each pod's Ready condition and restarts; the service's endpoints
  local out="$1" stop="$2"
  while [[ ! -f "$stop" ]]; do
    printf '%s\t%s\t%s\n' "$(utc_ms)" \
      "$(kc -n "$NS" get pods -o jsonpath='{range .items[*]}{.metadata.name}={.status.conditions[?(@.type=="Ready")].status}/{.status.containerStatuses[0].restartCount} {end}' 2>/dev/null)" \
      "$(kc -n "$NS" get endpointslices -l kubernetes.io/service-name=td-web -o jsonpath='{range .items[*].endpoints[*]}{.addresses[0]}:{.conditions.ready} {end}' 2>/dev/null)" >> "$out"
    sleep 1
  done
}

k6_start() {  # k6_start RATE SECONDS DIR -> K6PID
  local rate="$1" secs="$2" dir="$3" vus=$(( $1 * 3 + 20 )) pw
  pw="$(envval "$TD_W/.secrets/td-seed.env" TD_SEED_USER_PASSWORD)"
  echo "\$ taskset -c $K6_CPUS k6 run -e TD_TARGET=$URL -e TD_RATE=$rate -e TD_WARMUP_S=0 -e TD_MEASURE_S=$secs -e TD_PRE_VUS=$vus -e TD_MAX_VUS=$vus -e TD_REQ_TIMEOUT=30s -e TD_SETUP_SIGNIN=1 --out csv=k6.csv.gz tests/perf/ticketdesk-mix.js" > "$dir/k6.txt"
  K6_CSV_TIME_FORMAT=unix_milli TD_PASSWORD="$pw" taskset -c "$K6_CPUS" k6 run --no-color --out "csv=$dir/k6.csv.gz" \
    -e TD_TARGET="$URL" -e TD_USERS_FILE="$REPO/tests/perf/users-helpdesk.json" -e TD_SUMMARY="$dir/summary.json" \
    -e TD_RATE="$rate" -e TD_WARMUP_S=0 -e TD_MEASURE_S="$secs" -e TD_PRE_VUS="$vus" -e TD_MAX_VUS="$vus" -e TD_REQ_TIMEOUT=30s \
    -e TD_SETUP_SIGNIN=1 "$REPO/tests/perf/ticketdesk-mix.js" >> "$dir/k6.txt" 2>&1 &
  K6PID=$!
}

web_pod() { kc -n "$NS" get pods -l app.kubernetes.io/name=td-web -o jsonpath='{.items[0].metadata.name}'; }
# the worker's dotnet process as the node sees it (not this sh itself, whose command line holds the pattern too); no pipe, so
# set -o pipefail cannot end the drill on a SIGPIPE
worker_pid_in_node() { docker exec "$CLUSTER-control-plane" sh -c 'for p in /proc/[0-9]*; do c=$(tr "\0" " " < $p/cmdline 2>/dev/null); case "$c" in "dotnet TicketDesk.Worker.dll"*) echo ${p#/proc/}; exit 0;; esac; done; exit 0'; }
web_ips() { kc -n "$NS" get pods -l app.kubernetes.io/name=td-web -o jsonpath='{range .items[*]}{.status.podIP} {end}'; }

fault() {  # fault NAME DIR: inject, wait, recover; marks in DIR/marks.txt
  local name="$1" dir="$2" m="$2/marks.txt" pod pid ip i
  case "$name" in
    pod-kill)
      pod="$(web_pod)"; echo "fault $(utc_ms) kubectl delete pod $pod --grace-period=0 --force" >> "$m"
      kc -n "$NS" delete pod "$pod" --grace-period=0 --force >> "$dir/fault.txt" 2>&1
      for i in $(seq 1 120); do [[ "$(kc -n "$NS" get pods -l app.kubernetes.io/name=td-web -o jsonpath='{.items[*].status.conditions[?(@.type=="Ready")].status}')" == *True* ]] && break; sleep 0.5; done
      echo "ready $(utc_ms) $(web_pod)" >> "$m" ;;
    worker-kill)
      local hold="$dir/hold" k
      for k in 1 2 3; do
        touch "$hold"; echo "hold-on $(utc_ms) (kill $k)" >> "$m"
        for i in $(seq 1 100); do grep -q '^held' "$dir/mail.log" 2>/dev/null && [[ "$(grep -c '^held' "$dir/mail.log")" -ge "$k" ]] && break; sleep 0.2; done
        pid="$(worker_pid_in_node)"
        echo "fault $(utc_ms) kill -9 worker (pid $pid in the node; a mail of its message accepted by the sink, reply held: $(grep '^held' "$dir/mail.log" | tail -1))" >> "$m"
        docker exec "$CLUSTER-control-plane" kill -9 "$pid"
        sleep 1; rm -f "$hold"; echo "hold-off $(utc_ms)" >> "$m"
        sleep 60
      done ;;
    db-egress)
      ip="$(sql_ip)"
      echo "fault $(utc_ms) iptables DROP web pods ($(web_ips)) -> $ip:1433 in the node (established connections included)" >> "$m"
      for pod in $(web_ips); do docker exec "$CLUSTER-control-plane" iptables -I FORWARD -s "$pod" -d "$ip" -p tcp --dport 1433 -j DROP; done
      sleep "${TD_DRILL_HOLD_S:-60}"
      for pod in $(web_ips); do docker exec "$CLUSTER-control-plane" iptables -D FORWARD -s "$pod" -d "$ip" -p tcp --dport 1433 -j DROP 2>/dev/null || true; done
      docker exec "$CLUSTER-control-plane" sh -c "iptables -S FORWARD | grep -c -- '-d $ip/32' || true" >> "$dir/fault.txt"
      echo "recover $(utc_ms) rules removed" >> "$m" ;;
    sql-restart)
      echo "fault $(utc_ms) docker restart $SQL_NAME" >> "$m"
      docker restart -t 30 "$SQL_NAME" >> "$dir/fault.txt" 2>&1
      echo "container-up $(utc_ms)" >> "$m"
      for i in $(seq 1 120); do lsql master "SELECT 1" >/dev/null 2>&1 && break; sleep 0.5; done
      echo "sql-accepting $(utc_ms)" >> "$m" ;;
    *) die "unknown fault $name" ;;
  esac
}

cmd_drill() {  # drill NAME RATE [SUFFIX]
  local name="$1" rate="$2" dir="$EV/drill-$1${3:+-$3}" stop
  mkdir -p "$dir"; stop="$dir/.stop"; rm -f "$stop" "$dir/marks.txt"
  [[ "$name" == worker-kill ]] && sink_start "$dir/mail" "$dir/hold" || sink_start "$dir/mail"
  "$HERE/capacity.sh" host-load before > "$dir/host-load.txt" 2>&1
  lsql TicketDesk "SET NOCOUNT ON; SELECT MAX(EventId) FROM dbo.TicketEvents" | tr -d '\r ' > "$dir/base-event-id.txt"
  kc -n "$NS" get deploy td-web -o jsonpath='{.spec.template.spec.containers[0].readinessProbe}' > "$dir/readiness-probe.json"
  watch_pods "$dir/pods.tsv" "$stop" & local wp=$!
  # if the drill dies half-way, take its load, watcher and the sink's hold with it (run of 2026-11-14 05:30Z: an orphaned
  # k6 and watcher kept the plan's pipe open)
  trap 'kill ${wp:-} ${K6PID:-} 2>/dev/null; rm -f "${dir:-/nonexistent}/hold"; sink_stop' EXIT
  echo "start $(utc_ms) rate $rate for $LOAD_S s, fault $name at +$FAULT_AT s" > "$dir/marks.txt"
  k6_start "$rate" "$LOAD_S" "$dir"
  sleep "$FAULT_AT"
  fault "$name" "$dir"
  wait "$K6PID" || true
  echo "end $(utc_ms)" >> "$dir/marks.txt"
  if [[ "$name" == worker-kill ]]; then  # let the queue drain (lock expiry 60 s, then the backlog)
    local last=-1 n stable=0 i
    for i in $(seq 1 120); do n="$(find "$dir/mail" -name '*.eml' | wc -l)"; if [[ "$n" == "$last" ]]; then stable=$((stable + 5)); else stable=0; last="$n"; fi; ((stable >= 90)) && break; sleep 5; done
    echo "mail drained $(utc_ms): $n mails" >> "$dir/marks.txt"
  fi
  touch "$stop"; wait "$wp" || true; rm -f "$stop"
  kc -n "$NS" get events --sort-by=.lastTimestamp -o custom-columns=TIME:.lastTimestamp,REASON:.reason,OBJ:.involvedObject.name,COUNT:.count,MSG:.message > "$dir/events.txt" 2>&1 || true
  kc -n "$NS" logs deploy/td-worker --tail=400 > "$dir/worker.log" 2>&1 || true
  kc -n "$NS" logs deploy/td-worker --previous --tail=200 > "$dir/worker-previous.log" 2>&1 || true
  kc -n "$NS" logs deploy/td-web --tail=300 > "$dir/web.log" 2>&1 || true
  "$HERE/capacity.sh" host-load after >> "$dir/host-load.txt" 2>&1
  python3 -I "$HERE/timeline.py" "$dir/k6.csv.gz" "$dir" 0 > /dev/null
  if [[ "$name" == worker-kill ]]; then
    SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -e SQLCMDPASSWORD "$SQL_NAME" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -C -b -d TicketDesk -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT n.TicketId, n.EventId, d.DestinationAddress FROM dbo.TicketEventNotifications n JOIN notifications.SubscriberPushNotificationSettings s ON s.SubscriberId = n.SubscriberId AND s.IsEnabled = 1 JOIN notifications.PushNotificationDestinations d ON d.SubscriberId = n.SubscriberId AND d.DestinationType = N'email' AND d.DestinationAddress LIKE N'drill+%' WHERE n.EventId > $(cat "$dir/base-event-id.txt") AND n.IsRead = 0 ORDER BY 1, 2, 3" > "$dir/expected-mails.txt"
  fi
  sink_stop
  python3 -I "$HERE/drill_report.py" "$dir" | tee "$dir/result.txt"
  rm -rf "$dir/k6.csv.gz" "$dir/mail" "$dir/hold" "$dir/.stop"   # the mails are kept as delivered.csv.xz
}

# values file for the app's database (the fixed drill path of the AKS session: `--set` cut the connection string at the
# comma of "server,port"); JSON is YAML. The file holds a password: 0600 under logs/, a redacted copy in the evidence.
db_values() {  # db_values DATABASE FILE
  local pw; pw="$(envval "$RD_ENV" TD_LOAD_RD_APP_PASSWORD)"
  ( umask 077; python3 -I - "$(gw),$FWD_PORT" "$1" "$pw" > "$2" <<'PY2'
import json, sys
srv, db, pw = sys.argv[1:4]
cs = f"Server={srv};Database={db};User ID=td_load_app;Password={pw};TrustServerCertificate=True;Encrypt=True;Connect Timeout=15"
print(json.dumps({"migrate": {"enabled": False}, "database": {"connectionString": cs}}, indent=1))
PY2
  )
}
redact() { sed -E 's/Password=[^;"]*/Password=<redacted>/g' "$1"; }

rd_source() {  # rd_source DB: the capacity base restored on td-sql as DB, FULL recovery, login/user td_load_app
  local bak=/var/opt/mssql/backup/new-base.bak pw
  docker exec td-sql test -f "$bak" || { docker cp "$SQL_NAME:$bak" "$RUN/new-base.bak"; docker cp "$RUN/new-base.bak" "td-sql:$bak"; docker exec -u 0 td-sql chown mssql "$bak"; rm -f "$RUN/new-base.bak"; }
  secret_file "$RD_ENV" TD_LOAD_RD_APP_PASSWORD; pw="$(envval "$RD_ENV" TD_LOAD_RD_APP_PASSWORD)"
  tsql -Q "SET NOCOUNT ON; IF DB_ID(N'$1') IS NOT NULL ALTER DATABASE [$1] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$1] FROM DISK = N'$bak' WITH REPLACE, MOVE N'TicketDesk' TO N'/var/opt/mssql/data/$1.mdf', MOVE N'TicketDesk_log' TO N'/var/opt/mssql/data/$1_log.ldf';
ALTER DATABASE [$1] SET MULTI_USER; ALTER DATABASE [$1] SET RECOVERY FULL;" >/dev/null
  local f; f="$(mktemp)"; chmod 600 "$f"
  printf "IF SUSER_ID(N'td_load_app') IS NULL EXEC (N'CREATE LOGIN td_load_app WITH PASSWORD = N''%s'', CHECK_POLICY = OFF');\nGO\nUSE [%s];\nIF USER_ID(N'td_load_app') IS NULL CREATE USER td_load_app FOR LOGIN td_load_app;\nALTER ROLE td_modern_app_role ADD MEMBER td_load_app;\nGO\n" "$pw" "$1" > "$f"
  TD_SQL_STDIN=1 "$REPO/ops/dev/td-sql.sh" sql -b < "$f" >/dev/null; rm -f "$f"
}

# restore drill run N: the app on a fresh source database on td-sql (FULL recovery) under the k6 mix; full backup, then log
# backups every RD_LOG_S s; the database is lost (all sessions killed, last committed write read, DROP DATABASE); restored
# at once to the point of the loss (full + logs, STOPAT); the app switched to the restored database through a values file
# (helm upgrade --reuse-values -f); app smoke. RTO = loss -> smoke passed. RPO = last write committed before the loss -
# last write in the restored database (TicketEvents.EventDate), and the writes that are gone.
cmd_restore_drill() {
  local n="${1:?run number}" rate="${2:-40}" src="TicketDesk_load_rd$1" pitr="TicketDesk_load_rd$1_pitr" dir="$EV/restore-drill/run-$1"
  local stop i k t_full t_loss t_rs t_re t_switch t_smoke logs=() bk=/var/opt/mssql/backup
  mkdir -p "$dir"; : > "$dir/drill.txt"
  "$HERE/capacity.sh" host-load before > "$dir/host-load.txt" 2>&1
  rd_source "$src"
  db_values "$src" "$RUN/values-src.yaml"
  h upgrade td "$REPO/ops/k8s/ticketdesk" -n "$NS" --reuse-values -f "$RUN/values-src.yaml" --wait --timeout 5m > "$dir/helm-source.txt" 2>&1
  log "app on $src (values file)" | tee -a "$dir/drill.txt"
  stop="$dir/.stop"; rm -f "$stop"
  watch_pods "$dir/pods.tsv" "$stop" & local wp=$!
  k6_start "$rate" "${RD_LOAD_S:-420}" "$dir"
  sleep 20
  t_full="$(utc_ms)"; tsql -Q "BACKUP DATABASE [$src] TO DISK = N'$bk/load-rd-$n-full.bak' WITH INIT, CHECKSUM" > "$dir/backup-full.txt" 2>&1
  echo "full $t_full -> $(utc_ms)" >> "$dir/drill.txt"
  for k in 1 2 3 4; do
    sleep "${RD_LOG_S:-30}"
    tsql -Q "BACKUP LOG [$src] TO DISK = N'$bk/load-rd-$n-log-$k.trn' WITH INIT, CHECKSUM" > "$dir/backup-log-$k.txt" 2>&1
    echo "log $k $(utc_ms)" >> "$dir/drill.txt"; logs+=("$bk/load-rd-$n-log-$k.trn")
  done
  sleep "${RD_AFTER_LOG_S:-17}"
  # the loss: every session killed (in-flight transactions roll back), the last committed write read, the database dropped
  t_loss="$(utc_ms)"
  tsql -s '|' -Q "SET NOCOUNT ON; ALTER DATABASE [$src] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; USE [$src];
SELECT 'last committed event|' + CAST(MAX(EventId) AS varchar(12)) + '|' + CONVERT(varchar(40), MAX(EventDate), 127) FROM dbo.TicketEvents;
SELECT 'last committed ticket|' + CAST(MAX(TicketId) AS varchar(12)) + '|' + CONVERT(varchar(40), MAX(CreatedDate), 127) FROM dbo.Tickets;
SELECT 'events|' + CAST(COUNT(*) AS varchar(12)) FROM dbo.TicketEvents; USE master; DROP DATABASE [$src];" > "$dir/loss.txt" 2>&1
  echo "loss $t_loss (dropped $(utc_ms))" >> "$dir/drill.txt"
  # restore to the point of the loss
  t_rs="$(utc_ms)"
  { tsql -Q "RESTORE DATABASE [$pitr] FROM DISK = N'$bk/load-rd-$n-full.bak' WITH NORECOVERY, REPLACE, MOVE N'TicketDesk' TO N'/var/opt/mssql/data/$pitr.mdf', MOVE N'TicketDesk_log' TO N'/var/opt/mssql/data/$pitr""_log.ldf'"
    for i in "${logs[@]}"; do tsql -Q "RESTORE LOG [$pitr] FROM DISK = N'$i' WITH NORECOVERY, STOPAT = N'${t_loss%Z}'" || true; done
    tsql -Q "IF DATABASEPROPERTYEX(N'$pitr', 'Status') = N'RESTORING' RESTORE DATABASE [$pitr] WITH RECOVERY"
    tsql -h -1 -Q "SET NOCOUNT ON; SELECT 'state ' + CAST(DATABASEPROPERTYEX(N'$pitr', 'Status') AS nvarchar(20))"
  } > "$dir/restore.txt" 2>&1
  t_re="$(utc_ms)"; echo "restore $t_rs -> $t_re" >> "$dir/drill.txt"
  # read before the switch: afterwards the app's new writes reuse the lost identity range
  tsql -s '|' -d "$pitr" -Q "SET NOCOUNT ON; SELECT 'restored last event|' + CAST(MAX(EventId) AS varchar(12)) + '|' + CONVERT(varchar(40), MAX(EventDate), 127) FROM dbo.TicketEvents;
SELECT 'restored last ticket|' + CAST(MAX(TicketId) AS varchar(12)) + '|' + CONVERT(varchar(40), MAX(CreatedDate), 127) FROM dbo.Tickets;
SELECT 'restored events|' + CAST(COUNT(*) AS varchar(12)) FROM dbo.TicketEvents;" > "$dir/restored.txt" 2>&1
  db_values "$pitr" "$RUN/values-pitr.yaml"; redact "$RUN/values-pitr.yaml" > "$dir/values-pitr.redacted.yaml"
  h upgrade td "$REPO/ops/k8s/ticketdesk" -n "$NS" --reuse-values -f "$RUN/values-pitr.yaml" --wait --timeout 5m > "$dir/helm-pitr.txt" 2>&1
  t_switch="$(utc_ms)"; echo "app switched $t_switch" >> "$dir/drill.txt"
  # the drill's app serves server pages (the mix reads their HTML), so the smoke is the server-page one (legacy_smoke.py:
  # home, sign in, new ticket through the form, view, comment; 9 checks); app_smoke.py expects the Angular new-ticket page
  # until it passes (every 2 s, at most 60 attempts): right after `helm upgrade --wait` the old pod (on the lost database)
  # still serves for its preStop sleep, and a request can reach it (run 1 of 2026-11-14: the first smoke got its 500)
  local att=0
  while :; do
    att=$((att + 1))
    python3 -I "$REPO/ops/onprem/smoke/legacy_smoke.py" smoke --base "$URL" --json "$dir/smoke.json" > "$dir/smoke.txt" 2>&1 && break
    cp "$dir/smoke.txt" "$dir/smoke-failed-$att.txt"; ((att >= 60)) && break; sleep 2
  done
  t_smoke="$(utc_ms)"; echo "smoke $t_smoke (attempt $att): $(tail -1 "$dir/smoke.txt")" >> "$dir/drill.txt"
  wait "$K6PID" || true
  touch "$stop"; wait "$wp" || true; rm -f "$stop"
  "$HERE/capacity.sh" host-load after >> "$dir/host-load.txt" 2>&1
  python3 -I "$HERE/timeline.py" "$dir/k6.csv.gz" "$dir" 0 > /dev/null; rm -f "$dir/k6.csv.gz"
  python3 -I - "$dir" "$t_full" "$t_loss" "$t_rs" "$t_re" "$t_switch" "$t_smoke" "${RD_LOG_S:-30}" <<'PY2' | tee "$dir/result.txt"
import json, re, sys, datetime as d
ev, full, loss, rs, re_, sw, sm, logs = sys.argv[1:9]
p = lambda s: d.datetime.fromisoformat(s.replace("Z", "+00:00"))
def grab(f, key):
    for line in open(f"{ev}/{f}"):
        if line.startswith(key + "|"):
            return line.strip().split("|")[1:]
    return None
lc, lt, rc, rt = grab("loss.txt", "last committed event"), grab("loss.txt", "last committed ticket"), grab("restored.txt", "restored last event"), grab("restored.txt", "restored last ticket")
ne, nr = grab("loss.txt", "events"), grab("restored.txt", "restored events")
sm_ok = json.load(open(f"{ev}/smoke.json")).get("ok") if __import__("os").path.exists(f"{ev}/smoke.json") else None
marks = [l.split() for l in open(f"{ev}/drill.txt")]
lastlog = [m[2] for m in marks if m and m[0] == "log"][-1]
def tz(s):  # SQL style 127: 7 fraction digits, Z or an offset
    m = re.match(r"([0-9T:-]+)(?:\.(\d+))?(Z|[+-]\d\d:\d\d)?$", s.strip())
    frac = (m.group(2) or "0")[:6].ljust(6, "0")
    return d.datetime.fromisoformat(f"{m.group(1)}.{frac}" + ("+00:00" if m.group(3) in (None, "Z") else m.group(3)))
attempt = next((int(m.group(1)) for m in [re.search(r"smoke .*\(attempt (\d+)\)", l) for l in open(f"{ev}/drill.txt")] if m), 1)
out = {"full_backup": full, "log_backup_every_s": int(logs), "smoke_attempts": attempt, "last_log_backup": lastlog, "loss": loss, "restore_start": rs, "restore_end": re_,
       "app_switched": sw, "smoke_passed_at": sm, "smoke_ok": sm_ok,
       "rto_s (loss -> smoke passed)": round((p(sm) - p(loss)).total_seconds(), 1),
       "restore_s": round((p(re_) - p(rs)).total_seconds(), 1), "switch_s (helm upgrade --wait)": round((p(sw) - p(re_)).total_seconds(), 1),
       "last_committed_event": lc, "last_restored_event": rc, "last_committed_ticket": lt, "last_restored_ticket": rt,
       "rpo_s (last committed - last restored write)": round((tz(lc[1]) - tz(rc[1])).total_seconds(), 1) if lc and rc else None,
       "loss_after_last_log_backup_s": round((p(loss) - p(lastlog)).total_seconds(), 1),
       "events_committed": int(ne[0]) if ne else None, "events_restored": int(nr[0]) if nr else None,
       "events_lost": (int(ne[0]) - int(nr[0])) if ne and nr else None}
json.dump(out, open(f"{ev}/result.json", "w"), indent=1)
print(json.dumps(out, indent=1))
PY2
}

cmd_reset() {  # the drills' start state: database from the capacity base + push settings, web and worker restarted
  cmd_db >/dev/null
  kc -n "$NS" rollout restart deploy/td-web deploy/td-worker >/dev/null
  kc -n "$NS" rollout status deploy/td-web --timeout 180s >/dev/null; kc -n "$NS" rollout status deploy/td-worker --timeout 180s >/dev/null
  # the restarted pods' first requests (JIT, pools) are not part of a drill: 30 s at a low rate first
  local d; d="$(mktemp -d)"; k6_start 10 30 "$d"; wait "$K6PID" || true; rm -rf "$d"
  log "reset: TicketDesk restored + push on, web and worker restarted and primed"
}

cmd_down() {
  kind delete cluster --name "$CLUSTER" 2>&1 | tail -1 || true
  sink_stop
  if [[ -f "$RUN/td-sql-forward.pid" ]]; then kill "$(cat "$RUN/td-sql-forward.pid")" 2>/dev/null || true; rm -f "$RUN/td-sql-forward.pid"; fi
  docker rm -f "$SB_EMU" "$SB_SQL" >/dev/null 2>&1 || true
  docker network disconnect "$NET" "$SQL_NAME" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  tsql -Q "SET NOCOUNT ON; DECLARE @s nvarchar(max) = N''; SELECT @s += N'ALTER DATABASE [' + name + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [' + name + N'];' FROM sys.databases WHERE name LIKE N'TicketDesk[_]load[_]rd%'; EXEC (@s); IF SUSER_ID(N'td_load_app') IS NOT NULL DROP LOGIN td_load_app;" >/dev/null
  docker exec td-sql sh -c 'rm -f /var/opt/mssql/backup/load-rd-* /var/opt/mssql/backup/new-base.bak' 2>/dev/null || true
  rm -f "$SB_ENV" "$RD_ENV" "$RUN/values-drill.yaml" "$RUN/values-pitr.yaml"
  log "kind $CLUSTER, emulator, sink, relay, td-sql scratch databases and login removed"
}

[[ $# -gt 0 ]] || { sed -n '2,18p' "$0"; exit 2; }
c="$1"; shift
case "$c" in
  up) cmd_up ;; db) cmd_db ;; secrets) cmd_secrets ;; deploy) cmd_deploy "$@" ;; drill) cmd_drill "$@" ;;
  restore-drill) cmd_restore_drill "$@" ;; reset) cmd_reset ;; down) cmd_down ;;
  *) sed -n '2,18p' "$0"; exit 2 ;;
esac
