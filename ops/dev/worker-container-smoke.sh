#!/usr/bin/env bash
# worker-container-smoke.sh - run the TicketDesk.Worker image as built (user from the image, UID 1654) against td-sql and the
# Service Bus emulator, and check that it delivers one notification batch to the SMTP sink. No VM needed.
#   ops/dev/worker-container-smoke.sh IMAGE --out-dir DIR
# Uses a throw-away copy of td-sql's TicketDesk (push notifications switched on there, two e-mail destinations), queue
# ticket-notifications-test; the sink listens on 127.0.0.1:2527. Secrets go to the container through a 0600 env file.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IMAGE="${1:?image}"; OUT="${3:?--out-dir DIR}"
. "$TD_W/.tools/env.sh"
mkdir -p "$OUT/mail"; DB=td_worker_smoke_$$; NAME=td-sw-worker-smoke-$$; ENVF="$(mktemp)"; chmod 600 "$ENVF"; SINK=""
cleanup() { docker logs "$NAME" > "$OUT/worker.log" 2>&1 || true; docker rm -f "$NAME" >/dev/null 2>&1 || true; [[ -z "$SINK" ]] || kill "$SINK" 2>/dev/null || true
  "$HERE/td-sql.sh" sql -Q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END" >/dev/null 2>&1 || true
  docker exec td-sql rm -f "/var/opt/mssql/backup/$DB.bak"; rm -f "$ENVF"; }
trap cleanup EXIT
"$HERE/td-sql.sh" sql -Q "BACKUP DATABASE [TicketDesk] TO DISK = N'/var/opt/mssql/backup/$DB.bak' WITH COPY_ONLY, INIT" >/dev/null
moves=$("$HERE/td-sql.sh" sql -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/$DB.bak';" | awk -F'|' -v db="$DB" 'NF>3 && $1!="" { ext = ($3=="L") ? "_log.ldf" : ".mdf"; printf "%sMOVE N'\''%s'\'' TO N'\''/var/opt/mssql/data/%s%s'\''", (c++ ? ", " : ""), $1, db, ext }')
"$HERE/td-sql.sh" sql -Q "RESTORE DATABASE [$DB] FROM DISK = N'/var/opt/mssql/backup/$DB.bak' WITH $moves" >/dev/null
read -r TICKET EVENT < <("$HERE/td-sql.sh" sql -d "$DB" -h -1 -W -s ' ' -Q "SET NOCOUNT ON; SELECT TOP 1 n.TicketId, n.EventId FROM dbo.TicketEventNotifications n WHERE n.IsRead = 0 GROUP BY n.TicketId, n.EventId HAVING COUNT(*) = 2 ORDER BY n.EventId DESC" | tr -d '\r')
"$HERE/td-sql.sh" sql -d "$DB" -Q "SET NOCOUNT ON;
UPDATE notifications.ApplicationPushNotificationSettings SET PushNotificationSettingsJson = N'{\"DeliveryProviderSettings\":[{\"ProviderAssemblyQualifiedName\":\"TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider, TicketDesk.PushNotifications, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\",\"IsEnabled\":true,\"ProviderConfigurationData\":{\"SmtpServer\":\"127.0.0.1\",\"SmtpPort\":2527,\"EnableSsl\":false,\"SmtpFromAddress\":\"ticketdesk@lab.test\",\"SmtpFromDisplayName\":\"TicketDesk\"}}],\"IsEnabled\":true,\"AntiNoiseSettings\":{\"ExcludeSubscriberEvents\":true},\"BroadcastSettings\":{\"IsBroadcastEnabled\":false}}' WHERE ApplicationName = N'TicketDesk';
INSERT notifications.SubscriberPushNotificationSettings (SubscriberId, IsEnabled) SELECT n.SubscriberId, 1 FROM dbo.TicketEventNotifications n WHERE n.EventId = $EVENT AND NOT EXISTS (SELECT 1 FROM notifications.SubscriberPushNotificationSettings s WHERE s.SubscriberId = n.SubscriberId);
DELETE notifications.PushNotificationDestinations WHERE SubscriberId IN (SELECT SubscriberId FROM dbo.TicketEventNotifications WHERE EventId = $EVENT);
INSERT notifications.PushNotificationDestinations (SubscriberName, DestinationAddress, DestinationType, SubscriberId) SELECT N'Subscriber', LEFT(n.SubscriberId, 8) + N'@lab.test', N'email', n.SubscriberId FROM dbo.TicketEventNotifications n WHERE n.EventId = $EVENT;" >/dev/null
python3 -I "$HERE/smtp-sink.py" --dir "$OUT/mail" --port 2527 > "$OUT/sink.log" 2>&1 & SINK=$!
SB="$("$HERE/servicebus-emulator.sh" conn)"
{ echo "ConnectionStrings__TicketDesk=$("$HERE/td-sql.sh" conn "$DB")"; echo "Notifications__ServiceBus__ConnectionString=$SB"; echo "Notifications__ServiceBus__QueueName=ticket-notifications-test"; } > "$ENVF"
docker run -d --name "$NAME" --network host --env-file "$ENVF" "$IMAGE" >/dev/null
sleep 5
TD_SB_CONN="$SB" dotnet run "$HERE/servicebus-send.cs" -- ticket-notifications-test "$TICKET" "$EVENT" > "$OUT/send.txt" 2>&1
for _ in $(seq 1 60); do [[ $(ls "$OUT/mail" | grep -c '\.eml$' || true) -ge 2 ]] && break; sleep 1; done
sleep 3
{
  echo "image: $IMAGE ($(docker image inspect "$IMAGE" --format '{{.Size}} bytes, configured user {{.Config.User}}'))"
  echo "process (pid uid args): $(docker top "$NAME" -eo pid,uid,args | tail -n +2 | head -1)"
  echo "batch: ticket $TICKET, event $EVENT (two subscribers with unread notifications)"
  echo "mails in the sink: $(ls "$OUT/mail" | grep -c '\.eml$' || true)"
  for f in "$OUT"/mail/*.eml; do echo "  $(basename "$f"): $(grep -m1 '^X-Sink-Rcpt-To:' "$f" | cut -d' ' -f2-) | $(grep -m1 '^Subject:' "$f") | $(grep -m1 '^X-TicketDesk-Sender:' "$f")"; done
} | tee "$OUT/smoke.txt"
