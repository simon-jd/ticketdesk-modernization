#!/usr/bin/env bash
# capacity.sh - Round 6 load: capacity of the legacy app (lab VM: IIS + SQL Express) vs the new app (host: Kestrel + a SQL
# Express container), k6 open model (tests/perf/ticketdesk-mix.js, constant-arrival-rate), stepped rates, repeats, resource
# samples. Both systems get 4 cores of the same kind (E-core clusters of the i9-13900K: legacy vCPUs 28-31, new app + its SQL
# 24-27), k6 on the E-core cluster 16-19, the VM's emulator threads (user-mode network, disk) on $VM_EMU_CPUS.
#
#   TD_LOCK_OWNER=load capacity.sh prepare-vm   COPY_ONLY backup of the VM's live TicketDesk (on the VM + a copy in $SCRATCH):
#                                               the start state of every legacy curve and the data of the new app's database
#   capacity.sh prepare-new                     scratch container td-load-sql (SQL Server 2022 Express, cpuset $SUT_CPUS, 8 GB)
#                                               with that backup, td-db migrate --contract --legacy-retired, td_modern_app, one
#                                               sign-in per seeded help-desk user (V2 -> V3 rehash), backup new-base.bak
#   TD_LOCK_OWNER=load capacity.sh pin|unpin    qemu vCPU i -> CPU 28+i, its other threads -> $VM_EMU_CPUS | back to the saved masks
#   capacity.sh reset legacy|new                start state: legacy = RESTORE of the VM backup + app pool recycle + wait;
#                                               new = app stopped, RESTORE of new-base, app started (taskset $SUT_CPUS)
#   capacity.sh step legacy|new RATE DIR        one k6 step (warm-up $WARMUP_S + measure $MEASURE_S) with samples
#   capacity.sh curve legacy|new DIR RATE...    reset, then the steps in order; stops after a step far past breaking
#   capacity.sh stop-new | down                 stop the app | + remove td-load-sql and its volume
# Env: TD_LOAD_WARMUP_S (30), TD_LOAD_MEASURE_S (120), TD_LOAD_DB_BAK (new-base.bak; another base for scale L).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
# shellcheck disable=SC1091
. "$TD_W/.tools/env.sh"
SCRATCH="$TD_W/scratch-load"; RUN="$TD_W/logs/load"; mkdir -p "$SCRATCH" "$RUN"; chmod 700 "$SCRATCH"
OWNER="${TD_LOCK_OWNER:-}"
SUT_CPUS=24-27; VM_VCPU_BASE=28; VM_EMU_CPUS=20-23; K6_CPUS="${TD_LOAD_K6_CPUS:-16-19}"; AUX_CPUS=0-1
SQL_NAME=td-load-sql; SQL_VOL=td-load-sql-data; SQL_IMAGE=ticketdesk/td-sql:2022-fts-16.0.4262.2
NEW_PORT=5493; NEW_URL="https://localhost:$NEW_PORT"; LEGACY_URL=https://localhost:8443
VM_BAK=r6-load-pre.bak; NEW_BAK="${TD_LOAD_DB_BAK:-new-base.bak}"
WARMUP_S="${TD_LOAD_WARMUP_S:-30}"; MEASURE_S="${TD_LOAD_MEASURE_S:-120}"
APP_PID_FILE="$RUN/new-app.pid"; QEMU_PID="$(cat "$TD_W/vm/run/qemu.pid")"
V="$REPO/ops/onprem/vm/vmctl.sh"; SQLCMD="$TD_W/.tools/bin/sqlcmd"
log() { echo "$(date -u +%FT%TZ) $*"; }
die() { echo "capacity.sh: $*" >&2; exit 1; }
need_lock() { "$REPO/ops/onprem/vm/vmlock.sh" check "$OWNER" || die "take the VM lock first (TD_LOCK_OWNER=$OWNER)"; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
vm_sql() {  # vm_sql DB SQL: the VM's SQL Express through the 11433 forward as sa (password from the environment only)
  SQLCMDPASSWORD="$(envval "$TD_W/.secrets/legacy-lab.env" TD_SQL_SA_PASSWORD)" "$SQLCMD" -S 127.0.0.1,11433 -U sa -C -b -d "$1" -h -1 -W -Q "$2"
}
new_ip() { docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$SQL_NAME"; }
new_sql() {  # new_sql DB SQL: td-load-sql as sa (sqlcmd inside the container)
  SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -e SQLCMDPASSWORD "$SQL_NAME" \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d "$1" -h -1 -W -Q "$2"
}
vcpu_tids() { printf 'info cpus\n' | socat -t 3 - "UNIX-CONNECT:$TD_W/vm/run/monitor.sock" | tr -d '\r' | sed -n 's/.*thread_id=\([0-9]*\).*/\1/p'; }

WAITS_Q="SET NOCOUNT ON; SELECT wait_type + '|' + CAST(waiting_tasks_count AS varchar(20)) + '|' + CAST(wait_time_ms AS varchar(20)) + '|' + CAST(signal_wait_time_ms AS varchar(20)) FROM sys.dm_os_wait_stats WHERE wait_time_ms > 0 AND wait_type NOT IN (N'SLEEP_TASK', N'LAZYWRITER_SLEEP', N'SQLTRACE_INCREMENTAL_FLUSH_SLEEP', N'XE_TIMER_EVENT', N'XE_DISPATCHER_WAIT', N'REQUEST_FOR_DEADLOCK_SEARCH', N'LOGMGR_QUEUE', N'CHECKPOINT_QUEUE', N'BROKER_TO_FLUSH', N'BROKER_TASK_STOP', N'BROKER_EVENTHANDLER', N'DIRTY_PAGE_POLL', N'HADR_FILESTREAM_IOMGR_IOCOMPLETION', N'SP_SERVER_DIAGNOSTICS_SLEEP', N'QDS_PERSIST_TASK_MAIN_LOOP_SLEEP', N'QDS_ASYNC_QUEUE', N'QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP', N'FT_IFTS_SCHEDULER_IDLE_WAIT', N'FT_IFTSHC_MUTEX', N'WAITFOR', N'SOS_WORK_DISPATCHER', N'PREEMPTIVE_OS_FLUSHFILEBUFFERS', N'PVS_PREALLOCATE', N'SLEEP_DBSTARTUP', N'SLEEP_DCOMSTARTUP', N'SLEEP_MASTERDBREADY', N'SLEEP_MASTERMDREADY', N'SLEEP_MASTERUPGRADED', N'SLEEP_MSDBSTARTUP', N'SLEEP_SYSTEMTASK', N'SLEEP_TEMPDBSTARTUP', N'ONDEMAND_TASK_QUEUE', N'KSOURCE_WAKEUP', N'CLR_AUTO_EVENT', N'CLR_MANUAL_EVENT', N'SQLTRACE_BUFFER_FLUSH', N'FT_IFTS_RWLOCK', N'XE_LIVE_TARGET_TVF', N'PWAIT_EXTENSIBILITY_CLEANUP_TASK', N'PARALLEL_REDO_WORKER_WAIT_WORK', N'PARALLEL_REDO_DRAIN_WORKER', N'PARALLEL_REDO_LOG_CACHE', N'PARALLEL_REDO_TRAN_LIST', N'PARALLEL_REDO_WORKER_SYNC', N'WAIT_XTP_OFFLINE_CKPT_NEW_LOG', N'SOS_SCHEDULER_YIELD_DUMMY', N'STARTUP_DEPENDENCY_MANAGER', N'XE_FILE_TARGET_TVF')"
waits() { case "$1" in legacy) vm_sql master "$WAITS_Q" ;; new) new_sql master "$WAITS_Q" ;; esac; }

host_load() {
  echo "## $(date -u +%FT%TZ) $1"
  uptime; free -g | head -2
  echo "# per-CPU busy % over 3 s (0-31)"
  python3 -I -c '
import time
def s():
    d={}
    for l in open("/proc/stat"):
        if l.startswith("cpu") and l[3].isdigit():
            p=l.split(); v=list(map(int,p[1:9])); d[int(p[0][3:])]=(sum(v),v[3]+v[4])
    return d
a=s(); time.sleep(3); b=s()
print(" ".join(f"{c}:{100*(1-(b[c][1]-a[c][1])/max(1,b[c][0]-a[c][0])):.0f}" for c in sorted(a)))'
  echo "# top processes by CPU"
  ps -eo pcpu,psr,pid,comm --sort=-pcpu | head -8
}

cmd_prepare_vm() {
  need_lock
  local ev="$1" path
  mkdir -p "$ev"
  path="$(vm_sql master "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260))" | tr -d '\r')"
  {
    echo "# $(date -u +%FT%TZ) VM live database before Round 6 load"
    vm_sql TicketDesk "SET NOCOUNT ON; SELECT s.name + '.' + t.name + '|' + CAST(SUM(p.rows) AS varchar(20)) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1) GROUP BY s.name, t.name ORDER BY 1"
    vm_sql master "SET NOCOUNT ON; BACKUP DATABASE TicketDesk TO DISK = N'$path\\$VM_BAK' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'$path\\$VM_BAK' WITH CHECKSUM;" 2>&1
  } | tee "$ev/vm-before.txt"
  "$V" ps > /dev/null <<PS
Copy-Item -LiteralPath '$path\\$VM_BAK' -Destination 'C:\\Users\\Administrator\\$VM_BAK' -Force
PS
  "$V" scp "vm:$VM_BAK" "$SCRATCH/$VM_BAK" >/dev/null
  "$V" ps > /dev/null <<PS
Remove-Item -LiteralPath 'C:\\Users\\Administrator\\$VM_BAK' -Force
PS
  chmod 600 "$SCRATCH/$VM_BAK"
  echo "backup $path\\$VM_BAK on the VM, copy $SCRATCH/$VM_BAK ($(stat -c %s "$SCRATCH/$VM_BAK") bytes, sha256 $(sha256sum "$SCRATCH/$VM_BAK" | cut -c1-16))" | tee -a "$ev/vm-before.txt"
  echo "$path\\$VM_BAK" > "$RUN/vm-bak-path.txt"
}

sql_up() {
  if ! docker container inspect "$SQL_NAME" >/dev/null 2>&1; then
    docker volume create "$SQL_VOL" >/dev/null
    # Express like the VM (buffer pool capped at 1410 MB, 4 cores); same sa as td-sql (env file, never printed)
    docker run -d --name "$SQL_NAME" --env-file "$TD_W/.secrets/td-sql.env" -e MSSQL_PID=Express \
      --cpuset-cpus "$SUT_CPUS" --memory 8g -v "$SQL_VOL:/var/opt/mssql" "$SQL_IMAGE" >/dev/null
  fi
  docker start "$SQL_NAME" >/dev/null
  local i; for i in $(seq 1 60); do new_sql master "SELECT 1" >/dev/null 2>&1 && break; sleep 2; done
  new_sql master "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(64)) + ' ' + CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32)) + ', cpu_count ' + CAST(cpu_count AS varchar(5)) + ', schedulers ' + CAST(scheduler_count AS varchar(5)) + ', max memory MB ' + CAST((SELECT value_in_use FROM sys.configurations WHERE name = 'max server memory (MB)') AS varchar(12)) FROM sys.dm_os_sys_info"
}

cmd_prepare_new() {
  local ev="$1" ip pw
  mkdir -p "$ev"
  [[ -f "$SCRATCH/$VM_BAK" ]] || die "no $SCRATCH/$VM_BAK (prepare-vm)"
  {
    echo "# $(date -u +%FT%TZ) td-load-sql"; sql_up
    TD_SQL_NAME="$SQL_NAME" "$REPO/ops/dev/td-sql.sh" restore "$SCRATCH/$VM_BAK" TicketDesk | tail -2
    ip="$(new_ip)"; pw="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)"
    dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
    TD_DB_CONN="Server=$ip,1433;Database=TicketDesk;User Id=sa;Password=$pw;TrustServerCertificate=True;Encrypt=True" \
      "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate --contract --legacy-retired 2>&1 | tail -4
    TD_APP_LOGIN_SERVER="$ip,1433" TD_APP_LOGIN_SA_ENV="$TD_W/.secrets/td-sql.env" "$REPO/ops/dev/td-db-app-login.sh" apply | tail -2
    new_sql TicketDesk "SET NOCOUNT ON; SELECT 'tickets ' + CAST(COUNT(*) AS varchar(12)) FROM dbo.Tickets; SELECT 'snapshot=' + snapshot_isolation_state_desc COLLATE DATABASE_DEFAULT + ' rcsi=' + CAST(is_read_committed_snapshot_on AS varchar(1)) + ' recovery=' + recovery_model_desc COLLATE DATABASE_DEFAULT FROM sys.databases WHERE name = N'TicketDesk'"
    cmd_start_new
    echo "# one sign-in per seeded help-desk user (V2 hash -> V3 at the first sign-in, as after the switch)"
    python3 -I "$HERE/signin_all.py" --base "$NEW_URL" --users "$REPO/tests/perf/users-helpdesk.json" --password-env "$TD_W/.secrets/td-seed.env:TD_SEED_USER_PASSWORD"
    cmd_stop_new
    new_sql master "SET NOCOUNT ON; BACKUP DATABASE TicketDesk TO DISK = N'/var/opt/mssql/backup/new-base.bak' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backup/new-base.bak' WITH CHECKSUM;" 2>&1
  } 2>&1 | tee "$ev/new-prepare.txt"
}

cmd_pin() {
  need_lock
  local f="$RUN/qemu-affinity-before.txt" t i=0
  [[ -f "$f" ]] || { for t in /proc/"$QEMU_PID"/task/*; do echo "$(basename "$t") $(taskset -p "$(basename "$t")" | awk '{print $NF}')"; done > "$f"; }
  for t in /proc/"$QEMU_PID"/task/*; do taskset -pc "$VM_EMU_CPUS" "$(basename "$t")" >/dev/null; done
  for t in $(vcpu_tids); do taskset -pc "$((VM_VCPU_BASE + i))" "$t" >/dev/null; i=$((i + 1)); done
  echo "# $(date -u +%FT%TZ) qemu $QEMU_PID pinned (vCPU tid -> CPU; other threads -> $VM_EMU_CPUS)"
  for t in /proc/"$QEMU_PID"/task/*; do echo "$(basename "$t") $(taskset -pc "$(basename "$t")" | awk -F': ' '{print $2}')"; done
}
cmd_unpin() {
  need_lock
  local f="$RUN/qemu-affinity-before.txt" tid mask
  [[ -f "$f" ]] || die "no saved masks"
  while read -r tid mask; do taskset -p "$mask" "$tid" >/dev/null 2>&1 || true; done < "$f"
  for t in /proc/"$QEMU_PID"/task/*; do taskset -p "$(basename "$t")" | awk '{print $NF}'; done | sort | uniq -c
  rm -f "$f"
}

cmd_start_new() {
  [[ -f "$APP_PID_FILE" ]] && kill -0 "$(cat "$APP_PID_FILE")" 2>/dev/null && return 0
  local work="$RUN/new-app"; rm -rf "$work"; mkdir -p "$work/attachments"
  python3 -I -c 'import json, sys
reg = json.load(open(sys.argv[1]))
reg["Migration"]["Routes"] = {k: "New" for k in reg["Migration"]["Routes"]}  # server pages: the mix reads their HTML
print(json.dumps(reg, indent=2))' "$REPO/src/modern/TicketDesk.Web/migration-registry.json" > "$work/migration-registry.json"
  taskset -c "$SUT_CPUS" "$REPO/ops/dev/serve-modern.sh" --url "$NEW_URL" --registry "$work/migration-registry.json" \
    --attachments "$work/attachments" --app-server "$(new_ip),1433" > "$RUN/new-app.log" 2>&1 &
  echo $! > "$APP_PID_FILE"
  local i; for i in $(seq 1 90); do curl -ksf "$NEW_URL/health/ready" >/dev/null 2>&1 && break; sleep 1; done
  curl -ksf "$NEW_URL/health/ready" >/dev/null || die "new app not ready ($(tail -5 "$RUN/new-app.log"))"
  echo "new app pid $(cat "$APP_PID_FILE"): $(taskset -pc "$(cat "$APP_PID_FILE")" | awk -F': ' '{print "cpus " $2}'), ready"
}
cmd_stop_new() {
  [[ -f "$APP_PID_FILE" ]] || return 0
  local p; p="$(cat "$APP_PID_FILE")"
  kill "$p" 2>/dev/null || true
  local i; for i in $(seq 1 30); do kill -0 "$p" 2>/dev/null || break; sleep 1; done
  kill -9 "$p" 2>/dev/null || true; rm -f "$APP_PID_FILE"
}

cmd_reset() {
  local sys="$1" t0=$SECONDS
  case "$sys" in
    legacy)
      need_lock
      local path; path="$(cat "$RUN/vm-bak-path.txt")"
      vm_sql master "SET NOCOUNT ON; ALTER DATABASE TicketDesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE TicketDesk FROM DISK = N'$path' WITH REPLACE; ALTER DATABASE TicketDesk SET MULTI_USER;" >/dev/null
      "$V" ps >/dev/null <<'PS'
Import-Module WebAdministration; Restart-WebAppPool -Name TicketDesk
PS
      "$REPO/ops/onprem/baseline/baseline-s.sh" wait-app >/dev/null
      echo "legacy reset: database restored from $VM_BAK, app pool recycled, app ready ($((SECONDS - t0)) s); tickets $(vm_sql TicketDesk 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Tickets' | tr -d '\r')" ;;
    new)
      cmd_stop_new
      new_sql master "SET NOCOUNT ON; ALTER DATABASE TicketDesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE TicketDesk FROM DISK = N'/var/opt/mssql/backup/$NEW_BAK' WITH REPLACE; ALTER DATABASE TicketDesk SET MULTI_USER;" >/dev/null
      cmd_start_new >/dev/null
      echo "new reset: database restored from $NEW_BAK, app restarted ($((SECONDS - t0)) s); tickets $(new_sql TicketDesk 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Tickets' | tr -d '\r')" ;;
    *) die "legacy|new" ;;
  esac
}

cmd_step() {
  local sys="$1" rate="$2" dir="$3" url vus pw k6pid sp vp="" args=() tids total
  mkdir -p "$dir"; total=$((WARMUP_S + MEASURE_S))
  case "$sys" in legacy) url="$LEGACY_URL" ;; new) url="$NEW_URL" ;; *) die "legacy|new" ;; esac
  # open model: every VU is allocated up front (no VU is added later, so an overloaded step shows up as dropped iterations);
  # setup() signs each of the 37 users in once and the VUs reuse those sessions (TD_SETUP_SIGNIN=1): no password checks
  # inside the step
  vus=$((rate * 3 + 20)); ((vus > 1500)) && vus=1500
  pw="$(envval "$TD_W/.secrets/td-seed.env" TD_SEED_USER_PASSWORD)"
  host_load "before" > "$dir/host-load.txt" 2>&1
  waits "$sys" > "$dir/waits-before.txt" 2>&1 || true
  echo "\$ taskset -c $K6_CPUS k6 run -e TD_TARGET=$url -e TD_RATE=$rate -e TD_WARMUP_S=$WARMUP_S -e TD_MEASURE_S=$MEASURE_S -e TD_PRE_VUS=$vus -e TD_MAX_VUS=$vus -e TD_REQ_TIMEOUT=30s -e TD_SETUP_SIGNIN=1 --out csv=k6.csv.gz tests/perf/ticketdesk-mix.js" > "$dir/k6.txt"
  K6_CSV_TIME_FORMAT=unix_milli TD_PASSWORD="$pw" taskset -c "$K6_CPUS" k6 run --no-color --out "csv=$dir/k6.csv.gz" \
    -e TD_TARGET="$url" -e TD_USERS_FILE="$REPO/tests/perf/users-helpdesk.json" -e TD_SUMMARY="$dir/summary.json" \
    -e TD_RATE="$rate" -e TD_WARMUP_S="$WARMUP_S" -e TD_MEASURE_S="$MEASURE_S" -e TD_PRE_VUS="$vus" -e TD_MAX_VUS="$vus" \
    -e TD_REQ_TIMEOUT=30s -e TD_SETUP_SIGNIN=1 "$REPO/tests/perf/ticketdesk-mix.js" >> "$dir/k6.txt" 2>&1 &
  k6pid=$!
  args=(--out "$dir/samples.jsonl" --duration "$((total + 5))" --interval 2 --cpus "k6=$K6_CPUS" --proc "k6=$k6pid")
  if [[ "$sys" == legacy ]]; then
    tids="$(vcpu_tids | paste -sd,)"
    args+=(--cpus "sut=$VM_VCPU_BASE-$((VM_VCPU_BASE + 3))" --cpus "emu=$VM_EMU_CPUS" --proc "vcpu=$QEMU_PID:$tids" --proc-rest "emu=$QEMU_PID:$tids"
           --sql "sql=127.0.0.1,11433:$TD_W/.secrets/legacy-lab.env:TD_SQL_SA_PASSWORD:TicketDesk")
    sed -e "s/__SAMPLES__/$(( (total + 5) / 5 ))/" -e 's/__INTERVAL__/5/' "$HERE/vm-counters.ps1" | "$V" ps > "$dir/vm-counters.csv" 2>&1 &
    vp=$!
  else
    args+=(--cpus "sut=$SUT_CPUS" --proc "app=$(cat "$APP_PID_FILE")"
           --cgroup "sql=/sys/fs/cgroup/system.slice/docker-$(docker inspect -f '{{.Id}}' "$SQL_NAME").scope"
           --sql "sql=$(new_ip),1433:$TD_W/.secrets/td-sql.env:MSSQL_SA_PASSWORD:TicketDesk")
  fi
  taskset -c "$AUX_CPUS" python3 -I "$HERE/sample.py" "${args[@]}" > "$dir/sampler.log" 2>&1 &
  sp=$!
  wait "$k6pid" || true
  wait "$sp" || true
  [[ -n "$vp" ]] && { wait "$vp" || true; }
  waits "$sys" > "$dir/waits-after.txt" 2>&1 || true
  host_load "after" >> "$dir/host-load.txt" 2>&1
  python3 -I "$HERE/timeline.py" "$dir/k6.csv.gz" "$dir" "$WARMUP_S" > /dev/null 2>&1 || true
  python3 -I -c 'import json, sys
s = json.load(open(sys.argv[1])); a = s["all"]; d = (s.get("dropped_iterations") or {}).get("count", 0)
print("%s rate %s: p50 %.1f p95 %.1f p99 %.1f ms, %d req, failed %.2f%%, dropped %d" % (sys.argv[2], s["rate_per_s"], a["duration_ms"]["med"], a["duration_ms"]["p(95)"], a["duration_ms"]["p(99)"], a["duration_ms"]["count"], 100 * a["failed"]["rate"], d))' "$dir/summary.json" "$sys" 2>/dev/null || echo "$sys rate $rate: no summary"
}

cmd_curve() {
  local sys="$1" dir="$2"; shift 2
  mkdir -p "$dir"
  cmd_reset "$sys" | tee "$dir/reset.txt"
  # prime: 60 s at 10 ops/s after the restart (JIT, view compilation, pools), not measured
  TD_LOAD_WARMUP_S=0 TD_LOAD_MEASURE_S=60 "$0" step "$sys" 10 "$dir/prime" > "$dir/prime.txt" 2>&1 || true
  rm -f "$dir/prime/k6.csv.gz" "$dir/prime/requests.csv.xz"
  local r stop=""
  for r in "$@"; do
    if [[ -n "$stop" ]]; then echo "rate $r not run: $stop" >> "$dir/not-run.txt"; continue; fi
    cmd_step "$sys" "$r" "$dir/rate-$r" | tee -a "$dir/steps.txt"
    # far past breaking: the higher rates would only measure timeouts
    if python3 -I -c 'import csv, json, sys
s = json.load(open(sys.argv[1])); a = s["all"]
d = sum(int(x["dropped_iterations"] or 0) for x in csv.DictReader(open(sys.argv[2])) if x["phase"] == "measure")
sys.exit(0 if a["duration_ms"]["p(95)"] > 5000 or a["failed"]["rate"] > 0.2 or d > 0.2 * s["rate_per_s"] * s["measure_s"] else 1)' "$dir/rate-$r/summary.json" "$dir/rate-$r/timeline.csv" 2>/dev/null; then
      stop="step $r was far past breaking (p95 > 5 s, or > 20 % failed or dropped)"
    fi
  done
}

cmd_down() {
  cmd_stop_new
  docker rm -f "$SQL_NAME" >/dev/null 2>&1 || true
  docker volume rm "$SQL_VOL" >/dev/null 2>&1 || true
  log "td-load-sql and its volume removed"
}

[[ $# -gt 0 ]] || { sed -n '2,23p' "$0"; exit 2; }
c="$1"; shift
case "$c" in
  prepare-vm) cmd_prepare_vm "$@" ;; prepare-new) cmd_prepare_new "$@" ;; pin) cmd_pin ;; unpin) cmd_unpin ;;
  sql-up) sql_up ;; start-new) cmd_start_new ;; stop-new) cmd_stop_new ;; reset) cmd_reset "$@" ;;
  step) cmd_step "$@" ;; curve) cmd_curve "$@" ;; down) cmd_down ;;
  host-load) host_load "${1:-now}" ;; waits) waits "$@" ;;
  *) sed -n '2,23p' "$0"; exit 2 ;;
esac
