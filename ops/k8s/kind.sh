#!/usr/bin/env bash
# kind.sh - the ops/k8s/ticketdesk chart on a local kind cluster against td-sql (AKS round, Phase A dry run).
#
#   kind.sh up                 kind cluster td-aks on its own Docker network td-aks-kind (node port 30443 -> 127.0.0.1:18443),
#                              the TCP relay gateway:14334 -> td-sql (127.0.0.1:14333), a Service Bus emulator on that network
#   kind.sh db                 TicketDeskAks on td-sql from the shared baseline .bak, td-db migrate (the db image), login
#                              td_aks_app in td_modern_app_role (password in .secrets, 0600, never printed)
#   kind.sh images TAG         kind load docker-image ticketdesk-{web,worker,db}:TAG
#   kind.sh secrets [CRT]      Kubernetes secrets td-db (td_aks_app), td-db-migrator (sa), td-sb (emulator), td-tls (CRT + its .key,
#                              default the lab CA's aks-web certificate, chain appended as the CSI driver delivers it on AKS)
#   kind.sh deploy TAG [ARGS]  helm upgrade --install (migrate job first, then web + worker), --wait; extra helm args pass through
#   kind.sh smoke [NAME]       readiness, then legacy_smoke.py against https://localhost:18443 (lab CA), saved under $KIND_EV
#   kind.sh rotate-cert        a new lab certificate into td-tls; time until the served certificate changes, probe 1/s meanwhile
#   kind.sh netpol-drill       NetworkPolicy denying the web pods' egress to the database + new pods, until readiness fails;
#                              then removed, until ready again (the fault of the Azure alert drill)
#   kind.sh legacy-tls         the v1 gateway's trust of the lab CA (SSL_CERT_DIR + ConfigMap): web with Legacy:BaseUrl on an
#                              HTTPS stand-in (lab CA certificate, host alias to the gateway IP), with and without the CA
#   kind.sh legacy-gone TAG SECONDS LABEL   v1 with Legacy:BaseUrl on an address whose packets are dropped (the VM after
#                              retire-vm): the web pod's Ready condition and the service every 2 s (CHART_DIR: another chart)
#   kind.sh down               delete the cluster, stop the relay (PID file), remove the emulator, drop TicketDeskAks + the login
#   kind.sh status
#
# Everything here is this agent's own: cluster td-aks, network td-aks-kind, containers td-aks-sb-*, the relay (PID file),
# database TicketDeskAks, login td_aks_app. Shared containers (td-sql) are only connected to, never changed.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
# shellcheck disable=SC1091
. "$TD_W/.tools/env.sh"
CLUSTER=td-aks; NET=td-aks-kind; NS=ticketdesk; HOST_PORT=18443; NODE_PORT=30443
DB=TicketDeskAks; LOGIN=td_aks_app; FWD_PORT=14334
RUN="$TD_W/logs/aks"; FWD_PID="$RUN/kind-sql-forward.pid"
APP_ENV="$TD_W/.secrets/td-aks-app.env"; SB_ENV="$TD_W/.secrets/td-aks-sb.env"
CA="$TD_W/.secrets/legacy-lab-ca"
KIND_EV="${KIND_EV:-$REPO/docs/evidence/aks-$(date -u +%Y%m%d)/kind}"
SB_EMU=td-aks-sb-emulator; SB_SQL=td-aks-sb-sql
SB_IMAGE="mcr.microsoft.com/azure-messaging/servicebus-emulator@sha256:5a96d893b245031740f7d46e0fe5ff282d24b78c4b7d761dd57590f3f010a9b3"
SB_SQL_IMAGE="mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1"
mkdir -p "$RUN"
log() { echo "$(date -u +%FT%TZ) $*"; }
die() { echo "kind.sh: $*" >&2; exit 1; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
kc() { kubectl --context "kind-$CLUSTER" "$@"; }
gw() { docker network inspect "$NET" -f '{{range .IPAM.Config}}{{.Gateway}} {{end}}' | tr ' ' '\n' | grep -m1 -E '^[0-9.]+$'; }   # the IPv4 gateway
sql() { "$REPO/ops/dev/td-sql.sh" sql -b "$@"; }
secret_file() { [[ -f "$1" ]] || { (umask 077; printf '%s=%sAa1!\n' "$2" "$(openssl rand -hex 16)" > "$1"); chmod 600 "$1"; }; }

cmd_up() {
  if ! kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then
    local cfg; cfg="$(mktemp)"
    printf '%s\n' "kind: Cluster" "apiVersion: kind.x-k8s.io/v1alpha4" "nodes:" "  - role: control-plane" "    extraPortMappings:" \
      "      - { containerPort: $NODE_PORT, hostPort: $HOST_PORT, listenAddress: \"127.0.0.1\", protocol: TCP }" > "$cfg"
    KIND_EXPERIMENTAL_DOCKER_NETWORK="$NET" kind create cluster --name "$CLUSTER" --config "$cfg" --wait 180s
    rm -f "$cfg"
  fi
  kc get nodes -o wide
  if [[ -f "$FWD_PID" ]] && kill -0 "$(cat "$FWD_PID")" 2>/dev/null; then log "relay running (pid $(cat "$FWD_PID"))"; else
    nohup python3 -I "$HERE/tcp_forward.py" "$(gw):$FWD_PORT" "127.0.0.1:14333" > "$RUN/kind-sql-forward.log" 2>&1 &
    echo $! > "$FWD_PID"; sleep 1; kill -0 "$(cat "$FWD_PID")" || die "relay did not start ($(cat "$RUN/kind-sql-forward.log"))"
    log "relay $(gw):$FWD_PORT -> td-sql (pid $(cat "$FWD_PID"))"
  fi
  secret_file "$SB_ENV" MSSQL_SA_PASSWORD
  docker container inspect "$SB_SQL" >/dev/null 2>&1 || docker run -d --name "$SB_SQL" --network "$NET" --network-alias td-aks-sb-sql \
    -e ACCEPT_EULA=Y --env-file "$SB_ENV" -e MSSQL_MEMORY_LIMIT_MB=2048 --memory 3g "$SB_SQL_IMAGE" >/dev/null
  docker container inspect "$SB_EMU" >/dev/null 2>&1 || docker run -d --name "$SB_EMU" --network "$NET" --network-alias td-aks-sb \
    -e ACCEPT_EULA=Y -e SQL_SERVER=td-aks-sb-sql --env-file "$SB_ENV" -e SQL_WAIT_INTERVAL=15 \
    -v "$REPO/ops/dev/servicebus-emulator/Config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro" "$SB_IMAGE" >/dev/null
  docker start "$SB_SQL" "$SB_EMU" >/dev/null
  log "Service Bus emulator $SB_EMU on $NET (AMQP td-aks-sb:5672)"
}

cmd_db() {
  local bak="$TD_W/vm/exports/ticketdesk-baseline-s-shared.bak" pw sqlf
  "$REPO/ops/dev/td-sql.sh" restore "$bak" "$DB" | tail -2
  # the role's grants name tables of the current schema (V0010 key ring...): migrate once with the td-db image first (the
  # chart's job then finds nothing to do on install, and applies the contract scripts on the v2 upgrade)
  local envf; envf="$(mktemp)"; chmod 600 "$envf"
  printf 'TD_DB_CONN=Server=127.0.0.1,14333;Database=%s;User ID=sa;Password=%s;TrustServerCertificate=True;Encrypt=True\n' "$DB" \
    "$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" > "$envf"
  docker run --rm --network host --env-file "$envf" "ticketdesk-db:${TD_KIND_TAG:-aks-dev}" migrate | tail -3; rm -f "$envf"
  secret_file "$APP_ENV" TD_AKS_APP_PASSWORD
  pw="$(envval "$APP_ENV" TD_AKS_APP_PASSWORD)"
  sqlf="$(mktemp)"; chmod 600 "$sqlf"
  {
    printf "IF SUSER_ID(N'%s') IS NULL EXEC (N'CREATE LOGIN [%s] WITH PASSWORD = N''%s'', CHECK_POLICY = OFF');\n" "$LOGIN" "$LOGIN" "$pw"
    printf "ELSE EXEC (N'ALTER LOGIN [%s] WITH PASSWORD = N''%s''');\nGO\nUSE [%s];\nGO\n" "$LOGIN" "$pw" "$DB"
    cat "$REPO/ops/db/td_modern_app_role.sql"; printf "\nGO\n"
    printf "IF USER_ID(N'%s') IS NULL CREATE USER [%s] FOR LOGIN [%s];\n" "$LOGIN" "$LOGIN" "$LOGIN"
    printf "IF IS_ROLEMEMBER(N'td_modern_app_role', N'%s') = 0 ALTER ROLE td_modern_app_role ADD MEMBER [%s];\nGO\n" "$LOGIN" "$LOGIN"
  } > "$sqlf"
  TD_SQL_STDIN=1 "$REPO/ops/dev/td-sql.sh" sql -b < "$sqlf" > /dev/null; rm -f "$sqlf"
  sql -d "$DB" -h -1 -W -Q "SET NOCOUNT ON; SELECT N'$LOGIN in td_modern_app_role: ' + CAST(IS_ROLEMEMBER(N'td_modern_app_role', N'$LOGIN') AS nvarchar(5)) + N'; key ring rows: ' + CAST((SELECT COUNT(*) FROM dbo.DataProtectionKeys) AS nvarchar(10))"
}

cmd_images() {
  local tag="${1:?tag}" n
  for n in web worker db; do kind load docker-image --name "$CLUSTER" "ticketdesk-$n:$tag" 2>&1 | tail -1; done
}

cmd_secrets() {
  local crt="${1:-$CA/aks-web.crt}" key gwip sapw apppw base chain
  key="${crt%.crt}.key"; gwip="$(gw)"
  sapw="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)"; apppw="$(envval "$APP_ENV" TD_AKS_APP_PASSWORD)"
  kc get ns "$NS" >/dev/null 2>&1 || kc create ns "$NS" >/dev/null
  base="Server=$gwip,$FWD_PORT;Database=$DB;TrustServerCertificate=True;Encrypt=True;Connect Timeout=15"
  kc -n "$NS" create secret generic td-db --from-literal=connection-string="$base;User ID=$LOGIN;Password=$apppw" --dry-run=client -o yaml | kc apply -f - >/dev/null
  kc -n "$NS" create secret generic td-db-migrator --from-literal=connection-string="$base;User ID=sa;Password=$sapw" --dry-run=client -o yaml | kc apply -f - >/dev/null
  kc -n "$NS" create secret generic td-sb --from-literal=connection-string="Endpoint=sb://td-aks-sb:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;" --dry-run=client -o yaml | kc apply -f - >/dev/null
  chain="$(mktemp)"; cat "$crt" "$CA/ca.crt" > "$chain"
  kc -n "$NS" create secret tls td-tls --cert="$chain" --key="$key" --dry-run=client -o yaml | kc apply -f - >/dev/null
  rm -f "$chain"
  log "secrets td-db, td-db-migrator, td-sb, td-tls ($(openssl x509 -in "$crt" -noout -serial)) in $NS"
}

cmd_deploy() {
  local tag="${1:?tag}" t0=$SECONDS; shift
  helm --kube-context "kind-$CLUSTER" upgrade --install td "$HERE/ticketdesk" -n "$NS" -f "$HERE/values-kind.yaml" \
    --set image.tag="$tag" --wait --timeout 10m "$@"
  log "helm upgrade --install done in $((SECONDS - t0)) s"
  kc -n "$NS" get pods -o wide
  kc -n "$NS" logs job/td-db-migrate --tail=20 2>/dev/null || true
}

served_serial() { echo | timeout 5 openssl s_client -connect "127.0.0.1:$HOST_PORT" -servername localhost 2>/dev/null | openssl x509 -noout -serial 2>/dev/null | cut -d= -f2; }

cmd_smoke() {
  local name="${1:-kind}" ev; ev="$KIND_EV/smoke-$name"
  mkdir -p "$ev"
  curl -s --cacert "$CA/ca.crt" "https://localhost:$HOST_PORT/health/ready" | tee "$ev/ready.json"; echo
  python3 -I "$REPO/ops/onprem/smoke/legacy_smoke.py" smoke --base "https://localhost:$HOST_PORT" --json "$ev/smoke.json" > "$ev/smoke.txt" 2>&1 || true
  tail -3 "$ev/smoke.txt"
}

# certificate rotation dry run: a new certificate into the mounted secret; the kubelet rewrites the volume, Kestrel re-reads it
cmd_rotate_cert() {
  local ev="$KIND_EV/cert-rotation" t0 old new stop pp i
  mkdir -p "$ev"
  old="$(served_serial)"
  TD_CERT_NAME=aks-web-kind-rotated "$REPO/ops/onprem/new-lab-cert.sh" localhost td-web.ticketdesk.svc > "$ev/new-cert.txt" 2>&1
  stop="$ev/.stop"; rm -f "$stop"
  ( n=0; f=0; while [[ ! -f "$stop" ]]; do c="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 --cacert "$CA/ca.crt" "https://localhost:$HOST_PORT/health/live" || true)"; n=$((n+1)); [[ "$c" == 200 ]] || f=$((f+1)); sleep 1; done; echo "probe GET /health/live 1/s during the rotation: $n requests, $f failed" ) > "$ev/probe.txt" &
  pp=$!
  t0=$(date +%s.%N); echo "rotation start $(date -u +%FT%T.%3NZ), served serial before: $old" > "$ev/rotation.txt"
  cmd_secrets "$CA/aks-web-kind-rotated.crt" >> "$ev/rotation.txt"
  for i in $(seq 1 300); do new="$(served_serial)"; [[ -n "$new" && "$new" != "$old" ]] && break; sleep 1; done
  echo "served serial after: $new, picked up $(python3 -c "import time; print(round(time.time() - $t0, 1))") s after the secret update (kubelet volume sync + Tls:ReloadInterval 10 s)" >> "$ev/rotation.txt"
  sleep 5; touch "$stop"; wait "$pp" || true; rm -f "$stop"
  cat "$ev/rotation.txt" "$ev/probe.txt"
}

# alert-drill dry run: the fault the Azure drill injects (web pods cannot reach the database), as a NetworkPolicy
cmd_netpol_drill() {
  local ev="$KIND_EV/netpol-drill" t0 i c
  mkdir -p "$ev"
  printf '%s\n' "apiVersion: networking.k8s.io/v1" "kind: NetworkPolicy" "metadata: { name: td-drill-deny-db, namespace: $NS }" "spec:" \
    "  podSelector: { matchLabels: { app.kubernetes.io/name: td-web } }" "  policyTypes: [Egress]" "  egress:" \
    "    - to: [{ ipBlock: { cidr: 0.0.0.0/0, except: [\"$(gw)/32\"] } }]" > "$ev/deny-db.yaml"
  code() { curl -s -o /dev/null -w '%{http_code}' --max-time 20 --cacert "$CA/ca.crt" "https://localhost:$HOST_PORT/health/ready" || true; }
  {
    echo "before: $(code)"; t0=$SECONDS; kc apply -f "$ev/deny-db.yaml"
    # pooled connections are established flows a new policy does not cut: replace the pods so they must connect again
    kc -n "$NS" delete pod -l app.kubernetes.io/name=td-web --wait=false
    for i in $(seq 1 60); do c="$(code)"; [[ "$c" != 200 ]] && break; sleep 2; done; echo "fault visible after $((SECONDS - t0)) s: /health/ready $c"
    sleep "${DRILL_HOLD_S:-60}"   # the Azure drill holds the fault until the alert fires
    echo "after the hold: /health/ready $(code); web pods: $(kc -n "$NS" get pods -l app.kubernetes.io/name=td-web --no-headers | awk '{print $2}' | tr '\n' ' ')"
    t0=$SECONDS; kc delete -f "$ev/deny-db.yaml"
    for i in $(seq 1 60); do c="$(code)"; [[ "$c" == 200 ]] && break; sleep 2; done; echo "recovered after $((SECONDS - t0)) s: /health/ready $c"
  } | tee "$ev/drill.txt"
}

# the v1 gateway reaches IIS by the VM's name (host alias -> private IP) and validates its lab-CA certificate like any other:
# the lab CA goes in a ConfigMap as <subject hash>.0 and SSL_CERT_DIR lists it after the system roots
cmd_legacy_tls() {
  local ev="$KIND_EV/legacy-tls" gwip hsh pid ca
  mkdir -p "$ev"; gwip="$(gw)"; hsh="$(openssl x509 -hash -noout -in "$CA/ca.crt")"
  printf '%s\n' 'import http.server, ssl, sys' 'class H(http.server.BaseHTTPRequestHandler):' \
    '    def do_GET(self):' '        self.send_response(200); self.send_header("Content-Type", "text/html"); self.end_headers(); self.wfile.write(b"<html>TicketDesk stand-in</html>")' \
    '    def log_message(self, *a): pass' 's = http.server.HTTPServer((sys.argv[1], int(sys.argv[2])), H)' \
    'ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); ctx.load_cert_chain(sys.argv[3], sys.argv[4])' \
    's.socket = ctx.wrap_socket(s.socket, server_side=True); s.serve_forever()' > "$RUN/https-stand-in.py"
  python3 -I "$RUN/https-stand-in.py" "$gwip" 9443 "$CA/aks-web.crt" "$CA/aks-web.key" > "$RUN/https-stand-in.log" 2>&1 &
  pid=$!; sleep 1
  kc -n "$NS" create configmap td-lab-ca --from-file="$hsh.0=$CA/ca.crt" --dry-run=client -o yaml | kc apply -f - >/dev/null
  {
    echo "# $(date -u +%FT%TZ) web with Legacy:BaseUrl=https://td-web.ticketdesk.svc:9443/ (host alias -> $gwip, stand-in with the lab CA's aks-web certificate)"
    for ca in td-lab-ca ""; do
      helm --kube-context "kind-$CLUSTER" upgrade --install td "$HERE/ticketdesk" -n "$NS" -f "$HERE/values-kind.yaml" --set image.tag="${TD_KIND_TAG:-aks-dev}" \
        --set web.legacy.enabled=true --set web.legacy.baseUrl=https://td-web.ticketdesk.svc:9443/ --set web.legacy.hostAlias.ip="$gwip" \
        --set web.legacy.hostAlias.hostname=td-web.ticketdesk.svc --set web.legacy.caConfigMap="$ca" --wait --timeout 5m > /dev/null
      sleep 3
      echo "caConfigMap='$ca': /health/ready $(curl -s --cacert "$CA/ca.crt" "https://localhost:$HOST_PORT/health/ready")"
    done
  } | tee "$ev/legacy-tls.txt"
  kill "$pid" 2>/dev/null || true
  helm --kube-context "kind-$CLUSTER" upgrade --install td "$HERE/ticketdesk" -n "$NS" -f "$HERE/values-kind.yaml" --set image.tag="${TD_KIND_TAG:-aks-dev}" --wait --timeout 5m > /dev/null
}

cmd_down() {
  kind delete cluster --name "$CLUSTER" 2>&1 | tail -1 || true
  if [[ -f "$FWD_PID" ]]; then kill "$(cat "$FWD_PID")" 2>/dev/null || true; rm -f "$FWD_PID"; fi
  docker rm -f "$SB_EMU" "$SB_SQL" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  sql -Q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END; IF SUSER_ID(N'$LOGIN') IS NOT NULL DROP LOGIN [$LOGIN];" >/dev/null
  log "kind cluster, relay, emulator, $DB and $LOGIN removed"
}

cmd_status() {
  if kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then kc -n "$NS" get pods,svc,jobs -o wide 2>/dev/null || true; else echo "no cluster $CLUSTER"; fi
  [[ -f "$FWD_PID" ]] && echo "relay pid $(cat "$FWD_PID")"
  docker ps --filter "name=^/td-aks-" --format '{{.Names}} {{.Status}}'
}

[[ $# -gt 0 ]] || { sed -n '2,22p' "$0"; exit 2; }
c="$1"; shift
# v1 after retire-vm: its legacy fallback points at an address whose packets are dropped (the deleted VM: every connect
# times out), so /health/ready waits for the legacy check's 5 s client timeout. Watches the web pod's Ready condition and
# the service (node port) every 2 s for SECONDS. LABEL names the evidence; CHART_DIR (default the chart) compares charts.
cmd_legacy_gone() {  # legacy-gone TAG SECONDS LABEL [HELM ARGS]
  local tag="${1:?tag}" secs="${2:-300}" label="${3:-run}" chart="${CHART_DIR:-$HERE/ticketdesk}" ev pod pf t_end i ready code lat
  shift 3 || true
  ev="$KIND_EV/legacy-gone-$label"; mkdir -p "$ev"
  docker exec "$CLUSTER-control-plane" sh -c 'iptables -C FORWARD -d 192.0.2.10 -j DROP 2>/dev/null || iptables -I FORWARD -d 192.0.2.10 -j DROP'
  helm --kube-context "kind-$CLUSTER" uninstall td -n "$NS" --wait > /dev/null 2>&1 || true
  kc -n "$NS" wait --for=delete pod -l app.kubernetes.io/name=td-web --timeout=120s > /dev/null 2>&1 || true   # no old pod
  helm --kube-context "kind-$CLUSTER" upgrade --install td "$chart" -n "$NS" -f "$HERE/values-kind.yaml" --set image.tag="$tag" \
    --set web.legacy.enabled=true --set web.legacy.baseUrl=https://legacy-gone.lab/ \
    --set web.legacy.hostAlias.ip=192.0.2.10 --set web.legacy.hostAlias.hostname=legacy-gone.lab "$@" > "$ev/deploy.txt" 2>&1
  for i in $(seq 1 90); do
    pod="$(kc -n "$NS" get pod -l app.kubernetes.io/name=td-web -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)"
    [[ -n "$pod" && "$(kc -n "$NS" get pod "$pod" -o jsonpath='{.status.containerStatuses[0].started}' 2>/dev/null)" == true ]] && break
    sleep 2
  done
  { echo "# $(date -u +%FT%TZ) chart $chart, image $tag, Legacy:BaseUrl https://legacy-gone.lab/ -> 192.0.2.10 (dropped in the node)"
    echo "readinessProbe: $(kc -n "$NS" get deploy td-web -o jsonpath='{.spec.template.spec.containers[0].readinessProbe}')"
    echo "legacy check client timeout: 5 s (image $tag, Hosting/HealthChecks.cs)"; } > "$ev/summary.txt"
  for i in 1 2 3; do   # one port-forward per request (a forward can drop after a slow response)
    kc -n "$NS" port-forward "pod/$pod" 18080:8080 > /dev/null 2>&1 & pf=$!; sleep 3
    curl -s -o "$ev/ready-$i.json" -w "/health/ready from the pod: HTTP %{http_code} in %{time_total} s" --max-time 20 http://127.0.0.1:18080/health/ready >> "$ev/summary.txt" || true
    echo " ($(python3 -I -c 'import json, sys; d = json.load(open(sys.argv[1])); print(d["status"], [(c["name"], c["status"]) for c in d["checks"]])' "$ev/ready-$i.json" 2>/dev/null))" >> "$ev/summary.txt"
    kill "$pf" 2>/dev/null || true; wait "$pf" 2>/dev/null || true
  done
  t_end=$((SECONDS + secs)); : > "$ev/samples.tsv"
  while ((SECONDS < t_end)); do
    ready="$(kc -n "$NS" get pod "$pod" -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null || true)"
    code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 --cacert "$CA/ca.crt" "https://localhost:$HOST_PORT/health/live" 2>/dev/null || true)"
    printf '%s\t%s\t%s\n' "$(date -u +%T)" "${ready:-?}" "${code:-000}" >> "$ev/samples.tsv"
    sleep 2
  done
  python3 -I - "$ev/samples.tsv" >> "$ev/summary.txt" <<'PY'
import sys
rows = [l.rstrip("\n").split("\t") for l in open(sys.argv[1]) if l.strip()]
ready = sum(1 for r in rows if r[1] == "True")
served = sum(1 for r in rows if r[2] == "200")
flips = sum(1 for a, b in zip(rows, rows[1:]) if a[1] == "True" and b[1] != "True")
print(f"{len(rows)} samples over {rows[0][0]}..{rows[-1][0]}: pod Ready {ready} ({100 * ready / len(rows):.0f} %), "
      f"service answered {served} ({100 * served / len(rows):.0f} %), Ready -> NotReady {flips} time(s)")
PY
  echo "readiness probe failures (events): $(kc -n "$NS" get events --field-selector "involvedObject.name=$pod,reason=Unhealthy" -o json | python3 -I -c 'import json, sys; print(sum(e.get("count", 1) for e in json.load(sys.stdin)["items"] if "Readiness" in e["message"]))')" >> "$ev/summary.txt"
  cat "$ev/summary.txt"
}

case "$c" in
  up) cmd_up ;; db) cmd_db ;; images) cmd_images "$@" ;; secrets) cmd_secrets "$@" ;; deploy) cmd_deploy "$@" ;;
  smoke) cmd_smoke "$@" ;; rotate-cert) cmd_rotate_cert ;; netpol-drill) cmd_netpol_drill ;; legacy-tls) cmd_legacy_tls ;; legacy-gone) cmd_legacy_gone "$@" ;;
  down) cmd_down ;; status) cmd_status ;;
  *) sed -n '2,22p' "$0"; exit 2 ;;
esac
