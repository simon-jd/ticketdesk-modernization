#!/usr/bin/env bash
# ticket-db-dump.sh - the rows the ticket journeys write (tickets above 10000 and everything hanging off them) from the lab VM's
# database, normalized for comparing two runs of the golden journeys (legacy direct vs through the gateway): no rowversions,
# dates only as relations (AssignedDate = LastUpdateDate, CurrentStatusDate = LastUpdateDate), one JSON document per table.
#   ops/dev/ticket-db-dump.sh OUT.json          (read only; sa password from $TD_W/.secrets/legacy-lab.env, never printed)
#   ops/dev/ticket-db-dump.sh --diff A.json B.json
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
if [[ "${1:-}" == --diff ]]; then
  python3 -I - "$2" "$3" <<'PY'
import json, sys
a, b = (json.load(open(p)) for p in sys.argv[1:3])
bad = 0
for table in a:
    ra = [json.dumps(r, sort_keys=True, ensure_ascii=False) for r in a[table]]
    rb = [json.dumps(r, sort_keys=True, ensure_ascii=False) for r in b.get(table, [])]
    only_a = sorted(set(ra) - set(rb)); only_b = sorted(set(rb) - set(ra))
    status = "EQUAL" if not only_a and not only_b and len(ra) == len(rb) else "DIFFERENT"
    bad += status != "EQUAL"
    print(f"{table}: {len(ra)} vs {len(rb)} rows: {status}")
    for x in only_a[:8]: print(f"  only in A: {x[:400]}")
    for x in only_b[:8]: print(f"  only in B: {x[:400]}")
sys.exit(1 if bad else 0)
PY
  exit $?
fi
OUT="${1:?OUT.json}"
q() {
  SQLCMDPASSWORD="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")" \
    "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d TicketDesk -y 0 -Q "SET NOCOUNT ON; SELECT ($1 FOR JSON PATH, INCLUDE_NULL_VALUES) AS j" | python3 -I -c "
import sys, json; s = sys.stdin.read(); i = s.find('['); print(s[i:s.rindex(']') + 1] if i >= 0 else '[]')"
}
{
  echo '{"Tickets":'
  q "SELECT TicketId, ProjectId, TicketType, Category, Title, Details, IsHtml, TagList, CreatedBy, Owner, AssignedTo, TicketStatus, CurrentStatusSetBy,
       LastUpdateBy, Priority, AffectsCustomer, EstimatedDuration, ActualDuration, DueDate, TargetDate, ResolutionDate,
       CASE WHEN AssignedDate IS NULL THEN 'null' WHEN AssignedDate = LastUpdateDate THEN 'LastUpdateDate' WHEN AssignedDate = CreatedDate THEN 'CreatedDate' ELSE 'other' END AS AssignedDate,
       CASE WHEN CurrentStatusDate = LastUpdateDate THEN 'LastUpdateDate' ELSE 'other' END AS CurrentStatusDate
     FROM dbo.Tickets WHERE TicketId > 10000 ORDER BY TicketId"
  echo ',"TicketEvents":'
  q "SELECT TicketId, EventId, EventBy, EventDescription, Comment, IsHtml, ForActivity FROM dbo.TicketEvents WHERE TicketId > 10000 ORDER BY TicketId, EventId"
  echo ',"TicketSubscribers":'
  q "SELECT TicketId, SubscriberId FROM dbo.TicketSubscribers WHERE TicketId > 10000 ORDER BY TicketId, SubscriberId"
  echo ',"TicketEventNotifications":'
  q "SELECT TicketId, EventId, SubscriberId, IsNew, IsRead FROM dbo.TicketEventNotifications WHERE TicketId > 10000 ORDER BY TicketId, EventId, SubscriberId"
  echo ',"TicketTags":'
  q "SELECT TicketId, TagName, COUNT(*) AS Rows FROM dbo.TicketTags WHERE TicketId > 10000 GROUP BY TicketId, TagName ORDER BY TicketId, TagName"
  echo ',"PushNotificationItems":'
  q "SELECT COUNT(*) AS Items FROM notifications.PushNotificationItems"
  echo '}'
} > "$OUT"
python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print({k: len(v) for k, v in d.items()})" "$OUT"
