# Lab VM: Full-Text Search on SQL Express and snapshot baseline-s-search (2026-11-10)

`TD_LOCK_OWNER=search-worker ops/onprem/baseline/baseline-s.sh fulltext` (`fulltext.txt`): reset to baseline-s-identity;
`provision-legacy.sh copy-ops sql-fulltext` downloaded the Express Advanced media (27 s) and added feature FullText to
`.\SQLEXPRESS` (setup exit 0, 26 s; SQL Server 16.0.1000.6 Express, full-text 1); `td-db migrate` applied 4 scripts on the VM
database (V0006, V0011, V0012, R__usp_ticket_list); both full-text indexes populated (10,000 tickets, 45,649 events, 0 failed);
snapshot `baseline-s-search` saved. Whole step 8 min 11 s.

Incident: SQL Server setup stopped IIS (W3SVC "entered the stopped state" 18:54:56Z, during the feature install) and left it
stopped, so the app pool restart after the migration had no listener; W3SVC was started by hand at ~19:00Z and the step went
on (the population wait and the snapshot came after that, so the snapshot has IIS running). Fixed for the next run in
`Install-TdSqlExpress.ps1` (`21bea85`: starts W3SVC again when setup stopped it). The wait also means the 8 min above include
~5 min of that outage.
