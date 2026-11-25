#!/usr/bin/env bash
# Notification coexistence check on the lab VM (round 3): which app sends what while both write tickets, and no double mail.
#
#   TD_LOCK_OWNER=<you> ops/dev/notification-coexistence.sh --out-dir DIR [--snapshot baseline-s-search] [--worker-image IMG]
#
# Rule under test: each app delivers the notifications of its own writes. The legacy app turns its writes into
# notifications.PushNotificationItems (BinaryFormatter) and sends them from its in-process timer; the new app's writes
# publish one Service Bus message each, delivered by TicketDesk.Worker. Neither reads the other's queue.
# Steps: reset the VM; push notifications on in the shared database (SMTP provider -> the host's sink, seen from the VM as
# 10.0.2.2:2525; consolidation off so the legacy app sends each event; new-ticket broadcast to one custom address;
# destinations for the ticket's owner and assignee); restart the legacy pool (it reads push settings at start); start the
# gateway on TD_GATEWAY_URL (default https://localhost:5444/, shipped registry) with Service Bus publishing, and the worker
# container (host network, least-privilege login, SMTP -> 127.0.0.1:2525); write through the gateway with the ticket
# writes on the new app; switch the ticket-write routes back to legacy in the registry file; write again (a new ticket and
# a comment on the first one); wait for both senders; analyze the sink. The VM is left at the scenario's end state: reset it.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
OUT=""; SNAP=baseline-s-search; IMAGE=ticketdesk-worker:r3-search-worker
while [[ $# -gt 0 ]]; do
  case "$1" in
    --out-dir) OUT="$2"; shift 2 ;; --snapshot) SNAP="$2"; shift 2 ;; --worker-image) IMAGE="$2"; shift 2 ;;
    *) sed -n '2,4p' "$0"; exit 2 ;;
  esac
done
[[ -n "$OUT" ]] || { sed -n '2,4p' "$0"; exit 2; }
"$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "take the VM lock first" >&2; exit 1; }
. "$TD_W/.tools/env.sh"
mkdir -p "$OUT/mail"
export TD_GATEWAY_URL="${TD_GATEWAY_URL:-https://localhost:5444/}" TD_MODERN_DB=vm TD_MODERN_LOGIN=app
log() { echo "$(date -u +%FT%TZ) $*" | tee -a "$OUT/run.txt"; }
SA="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")"
vm_sql() { SQLCMDPASSWORD="$SA" "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d TicketDesk -h -1 -W -b "$@"; }
SINK_PID=""; GW_PID=""; WORKER=td-sw-worker-$$; ENVF="$(mktemp)"; chmod 600 "$ENVF"; REG="$(mktemp --suffix=.json)"
cleanup() {
  docker logs "$WORKER" > "$OUT/worker.log" 2>&1 || true
  docker rm -f "$WORKER" >/dev/null 2>&1 || true
  [[ -z "$GW_PID" ]] || { kill "$GW_PID" 2>/dev/null || true; wait "$GW_PID" 2>/dev/null || true; }
  [[ -z "$SINK_PID" ]] || kill "$SINK_PID" 2>/dev/null || true
  rm -f "$ENVF" "$REG"
}
trap cleanup EXIT

log "reset the VM to $SNAP"
TD_BASELINE_SNAPSHOT="$SNAP" "$REPO/ops/onprem/baseline/baseline-s.sh" reset > "$OUT/reset.txt" 2>&1; tail -1 "$OUT/reset.txt" | tee -a "$OUT/run.txt"
"$HERE/td-db-app-login.sh" apply > "$OUT/app-login.txt" 2>&1; tail -1 "$OUT/app-login.txt"
python3 -I "$HERE/smtp-sink.py" --dir "$OUT/mail" --port 2525 > "$OUT/sink.log" 2>&1 & SINK_PID=$!
"$HERE/servicebus-emulator.sh" up | tee -a "$OUT/run.txt"

OWNER="$(vm_sql -Q "SET NOCOUNT ON; SELECT TOP 1 ur.UserId FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = 'TdInternalUsers' WHERE NOT EXISTS (SELECT 1 FROM dbo.IdentityUserRoles u2 JOIN dbo.IdentityRoles r2 ON r2.Id = u2.RoleId WHERE u2.UserId = ur.UserId AND r2.Name IN ('TdHelpDeskUsers','TdAdministrators')) ORDER BY ur.UserId" | tr -d '\r ')"
ASSIGNEE="$(vm_sql -Q "SET NOCOUNT ON; SELECT TOP 1 ur.UserId FROM dbo.IdentityUserRoles ur JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId AND r.Name = 'TdHelpDeskUsers' WHERE NOT EXISTS (SELECT 1 FROM dbo.IdentityUserRoles u2 JOIN dbo.IdentityRoles r2 ON r2.Id = u2.RoleId WHERE u2.UserId = ur.UserId AND r2.Name = 'TdAdministrators') ORDER BY ur.UserId" | tr -d '\r ')"
log "owner $OWNER, assignee $ASSIGNEE; push notifications on (SMTP 10.0.2.2:2525, consolidation off, broadcast to one address)"
vm_sql -Q "SET NOCOUNT ON;
UPDATE notifications.ApplicationPushNotificationSettings SET PushNotificationSettingsJson = N'{\"DeliveryProviderSettings\":[{\"ProviderAssemblyQualifiedName\":\"TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider, TicketDesk.PushNotifications, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\",\"IsEnabled\":true,\"ProviderConfigurationData\":{\"SmtpServer\":\"10.0.2.2\",\"SmtpPort\":2525,\"EnableSsl\":false,\"SmtpUserName\":null,\"SmtpPassword\":null,\"SmtpFromAddress\":\"ticketdesk@lab.test\",\"SmtpFromDisplayName\":\"TicketDesk\"}}],\"IsEnabled\":true,\"IsBackgroundQueueEnabled\":true,\"DeliveryIntervalMinutes\":1,\"RetryAttempts\":5,\"RetryIntervalMinutes\":1,\"AntiNoiseSettings\":{\"IsConsolidationEnabled\":false,\"InitialConsolidationDelayMinutes\":2,\"MaxConsolidationDelayMinutes\":10,\"ExcludeSubscriberEvents\":true},\"BroadcastSettings\":{\"BroadcastMode\":1,\"IsBroadcastEnabled\":true,\"SendToCustomEmailAddress\":\"helpdesk-broadcast@lab.test\",\"SendToCustomEmailDisplayName\":\"Help desk\"}}' WHERE ApplicationName = N'TicketDesk';
INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) SELECT v.Id, 1 FROM (VALUES (N'$OWNER'), (N'$ASSIGNEE')) v(Id) WHERE NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings s WHERE s.SubscriberId = v.Id);
INSERT notifications.PushNotificationDestinations (SubscriberName, DestinationAddress, DestinationType, SubscriberId) VALUES (N'Owner', N'owner@lab.test', N'email', N'$OWNER'), (N'Assignee', N'assignee@lab.test', N'email', N'$ASSIGNEE');
SELECT 'push settings and destinations written'" | tee -a "$OUT/run.txt"
"$REPO/ops/onprem/vm/vmctl.sh" ssh 'Import-Module WebAdministration; Restart-WebAppPool TicketDesk; "app pool restarted"' < /dev/null | tee -a "$OUT/run.txt"
"$REPO/ops/onprem/baseline/baseline-s.sh" wait-app | tee -a "$OUT/run.txt"

cp "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$REG"
SB="$("$HERE/servicebus-emulator.sh" conn)"
log "gateway on $TD_GATEWAY_URL (shipped registry, shared cookie, td_modern_app) publishing to Service Bus"
( export Notifications__ServiceBus__ConnectionString="$SB"; exec "$HERE/replay-gateway.sh" --serve --registry "$REG" --shared-cookie ) > "$OUT/gateway.log" 2>&1 & GW_PID=$!
for _ in $(seq 1 90); do curl -ksf "${TD_GATEWAY_URL}health/live" >/dev/null 2>&1 && break; sleep 1; done
APP_PW="$(sed -n 's/^TD_MODERN_APP_SQL_PASSWORD=//p' "$TD_W/.secrets/td-modern-app.env")"
{ echo "ConnectionStrings__TicketDesk=Server=127.0.0.1,11433;Database=TicketDesk;User Id=td_modern_app;Password=$APP_PW;TrustServerCertificate=True;Encrypt=True"
  echo "Notifications__ServiceBus__ConnectionString=$SB"
  echo "Worker__Mail__SmtpHost=127.0.0.1"; echo "Worker__Mail__SmtpPort=2525"; } > "$ENVF"
docker run -d --name "$WORKER" --network host --env-file "$ENVF" "$IMAGE" >/dev/null
log "worker container $WORKER from $IMAGE: $(docker top "$WORKER" -eo pid,uid,args | tail -n +2 | head -1)"

# the "new ticket broadcast" destinations and the legacy worker processes at each phase (the legacy app appends the custom
# broadcast address at every start of its push notifications, so each app start can add a destination row)
probe() {
  { echo "== $(date -u +%FT%TZ) $1"
    vm_sql -s '|' -Q "SET NOCOUNT ON; SELECT DestinationId, SubscriberId, DestinationAddress FROM notifications.PushNotificationDestinations WHERE SubscriberId = N'new ticket broadcast' ORDER BY DestinationId"
    "$REPO/ops/onprem/vm/vmctl.sh" ssh 'Get-Process w3wp -ErrorAction SilentlyContinue | ForEach-Object { "w3wp " + $_.Id + " started " + $_.StartTime.ToUniversalTime().ToString("o") }' < /dev/null
  } >> "$OUT/probes.txt" 2>&1
}
probe "before phase 1"
DRIVER=(python3 -I "$HERE/notification-coexistence.py")
log "phase 1: ticket writes answered by the new app"
"${DRIVER[@]}" write --base "$TD_GATEWAY_URL" --secrets "$TD_W/.secrets/legacy-lab.env" --owner "$OWNER" --assignee "$ASSIGNEE" --phase new --out "$OUT/writes-new.json" | tee -a "$OUT/run.txt"
probe "after phase 1"
FIRST="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["writes"][0]["ticket"])' "$OUT/writes-new.json")"
log "switch ticket writes back to legacy (registry file; ticket.new and the activity group)"
python3 -I - "$REG" <<'PY'
import json, sys
p = sys.argv[1]; reg = json.load(open(p))
for k in list(reg["Migration"]["Routes"]):
    if k.startswith("activity.") or k.startswith("ticket.new."):
        reg["Migration"]["Routes"][k] = "Legacy"
json.dump(reg, open(p + ".tmp", "w"), indent=2)
import os; os.replace(p + ".tmp", p)
PY
sleep 3
log "phase 2: ticket writes answered by the legacy app (new ticket, comment on ticket $FIRST)"
"${DRIVER[@]}" write --base "$TD_GATEWAY_URL" --secrets "$TD_W/.secrets/legacy-lab.env" --owner "$OWNER" --assignee "$ASSIGNEE" --phase legacy --ticket "$FIRST" --out "$OUT/writes-legacy.json" | tee -a "$OUT/run.txt"
probe "after phase 2"
log "waiting for both senders (legacy timer: every minute)"
last=-1; stable=0
for _ in $(seq 1 60); do
  n=$(ls "$OUT/mail" | grep -c '\.eml$' || true)
  if [[ "$n" == "$last" ]]; then stable=$((stable + 5)); else stable=0; last=$n; fi
  (( n >= 10 && stable >= 75 )) && break
  sleep 5
done
log "$(ls "$OUT/mail" | grep -c '\.eml$') mails in the sink"
vm_sql -s '|' -Q "SET NOCOUNT ON; SELECT 'push items: ' + CAST(DeliveryStatus AS varchar(4)) + ' x ' + CAST(COUNT(*) AS varchar(8)) FROM notifications.PushNotificationItems GROUP BY DeliveryStatus;
SELECT 'push items per ticket: ' + CAST(ContentSourceId AS varchar(12)) + ' ' + ContentSourceType + ' x ' + CAST(COUNT(*) AS varchar(8)) FROM notifications.PushNotificationItems GROUP BY ContentSourceId, ContentSourceType ORDER BY 1;" | tee "$OUT/push-items.txt" | tee -a "$OUT/run.txt"
probe "end"
"${DRIVER[@]}" analyze --mail "$OUT/mail" --writes "$OUT/writes-new.json" "$OUT/writes-legacy.json" \
  --expect-recipients owner@lab.test,assignee@lab.test --broadcast helpdesk-broadcast@lab.test --out "$OUT/result.json" | tee -a "$OUT/run.txt"
