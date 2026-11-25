#!/usr/bin/env bash
# repro-list-deadlock.sh - TAGX=<images> (aks-v2: before the fix, aks-v2b: after): kind, the case-ok sequence, logs under
# $TD_W/logs/aks/repro-500-<TAGX>/ (probe, web pod log, deadlock reports); docs/evidence/aks-20261111/ado-cd/deadlock/
# case-ok's sequence on kind, repeated: v2 deployed (contracted schema), probe 1/s (reads + writes on 9993) through
# (A) 4 min with nothing else, then (B) 20 helm upgrades with the same values (only the pre-upgrade td-db migrate job runs)
source $HOME/Desktop/ticketdesk-modernization/.tools/env.sh
TAGX=${TAGX:-aks-v2b}
cd $HOME/Desktop/ticketdesk-modernization/wt-aks
O=$HOME/Desktop/ticketdesk-modernization/logs/aks/repro-500-${TAGX}; rm -rf $O; mkdir -p $O
ts() { date -u +%FT%T.%3NZ; }
ops/k8s/kind.sh up > $O/up.txt 2>&1; TD_KIND_TAG=${TAGX} ops/k8s/kind.sh db > $O/db.txt 2>&1
ops/k8s/kind.sh images ${TAGX} > /dev/null 2>&1; ops/k8s/kind.sh secrets > /dev/null 2>&1
kc=$O/kc; kind get kubeconfig --name td-aks > $kc; chmod 600 $kc
v2=$O/v2.yaml; printf '%s\n' "image: { tag: ${TAGX}, pullPolicy: Never }" "database: { secretName: td-db, migrator: { secretName: td-db-migrator } }" "web:" "  attachments: { store: Local }" "  serviceBus: { secretName: td-sb }" "  tls: { source: secret, secretName: td-tls }" "  service: { type: NodePort, nodePort: 30443 }" "migrate:" "  args: [migrate, --contract, --legacy-retired]" > $v2
echo "snapshot_isolation_state: $(ops/dev/td-sql.sh sql -h -1 -W -Q "SET NOCOUNT ON; SELECT snapshot_isolation_state FROM sys.databases WHERE name = N'TicketDeskAks'")" > $O/si.txt
helm --kubeconfig $kc upgrade --install ticketdesk ops/k8s/ticketdesk -n ticketdesk -f $v2 --wait > $O/install.txt 2>&1
sleep 20
START=$(date -u +%FT%T); echo "start $START" > $O/marks.txt
web=$(kubectl --kubeconfig $kc -n ticketdesk get pod -l app.kubernetes.io/name=td-web -o jsonpath='{.items[0].metadata.name}'); echo "web pod $web" >> $O/marks.txt
d=$(mktemp -d); echo https://localhost:18443 > $d/t
python3 -I ops/azure/cutover_probe.py run --target-file $d/t --stop-file $d/s --out $O/probe.jsonl --write-ticket 9993 < /dev/null > /dev/null 2>&1 & pp=$!
echo "A control from $(ts)" >> $O/marks.txt; sleep 240; echo "A end $(ts)" >> $O/marks.txt
for i in $(seq 1 20); do
  echo "B$i upgrade $(ts)" >> $O/marks.txt
  helm --kubeconfig $kc upgrade ticketdesk ops/k8s/ticketdesk -n ticketdesk -f $v2 --wait >> $O/upgrades.txt 2>&1
  echo "B$i done $(ts) job=$(kubectl --kubeconfig $kc -n ticketdesk get pods -l app.kubernetes.io/name=td-db-migrate -o jsonpath='{.items[0].status.startTime}')" >> $O/marks.txt
  kubectl --kubeconfig $kc -n ticketdesk logs job/td-db-migrate --timestamps >> $O/jobs.log 2>&1
  sleep 8
done
touch $d/s; wait $pp
echo "end $(ts); web pod now $(kubectl --kubeconfig $kc -n ticketdesk get pod -l app.kubernetes.io/name=td-web -o jsonpath='{.items[0].metadata.name}')" >> $O/marks.txt
kubectl --kubeconfig $kc -n ticketdesk logs $web --timestamps > $O/web.log 2>&1
ops/dev/td-sql.sh sql -h -1 -W -s "|" -Q "SET NOCOUNT ON; WITH t AS (SELECT CAST(target_data AS xml) x FROM sys.dm_xe_session_targets st JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address WHERE s.name = N'system_health' AND st.target_name = N'ring_buffer')
SELECT e.value('@timestamp','varchar(30)'), e.value('(data/value/deadlock/process-list/process/@currentdbname)[1]','nvarchar(128)'), LEFT(REPLACE(REPLACE(e.value('(data/value/deadlock/process-list/process/inputbuf)[1]','nvarchar(max)'), CHAR(10), ' '), CHAR(13), ' '), 160), LEFT(REPLACE(REPLACE(e.value('(data/value/deadlock/process-list/process/inputbuf)[2]','nvarchar(max)'), CHAR(10), ' '), CHAR(13), ' '), 160)
FROM t CROSS APPLY x.nodes('RingBufferTarget/event[@name=\"xml_deadlock_report\"]') n(e) WHERE e.value('@timestamp','datetime2') > '$START'" > $O/deadlocks.txt 2>&1
python3 -I ops/azure/cutover_probe.py summarize $O/probe.jsonl > $O/probe-summary.json
ops/k8s/kind.sh down > /dev/null 2>&1
echo done >> $O/marks.txt
