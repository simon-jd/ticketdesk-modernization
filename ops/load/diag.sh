#!/usr/bin/env bash
# diag.sh - Round 6 load: two diagnostic variants that test the knee causes (not the as-deployed configurations).
#   TD_LOCK_OWNER=load diag.sh legacy-threads-on | legacy-threads-off
#       legacy: machine.config <processModel autoConfig="true"/> -> + minWorkerThreads="50" minIoThreads="50" (per CPU: 200
#       each on the 4 vCPUs), app pool recycled; -off puts the saved file back (hash checked) and recycles
#   diag.sh new-rcsi-base     new app: new-base.bak + READ_COMMITTED_SNAPSHOT ON (Azure SQL Database's default), saved as
#                             new-base-rcsi.bak in td-load-sql (then: TD_LOAD_DB_BAK=new-base-rcsi.bak capacity.sh curve new ...)
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"; REPO="$(cd "$HERE/../.." && pwd)"
V="$REPO/ops/onprem/vm/vmctl.sh"
MC='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config'
MC_HASH=68E0DC6FEF3FF0DF4284C47B935ACCF4315DFC8228FBA809CB181258C78CB5E1   # the file before Round 6 (read 2026-11-14 01:42Z)
need_lock() { "$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "take the VM lock first" >&2; exit 1; }; }
envval() { sed -n "s/^$2=//p" "$1" | head -1; }
case "${1:-}" in
  legacy-threads-on) need_lock; "$V" ps <<PS
\$f = '$MC'; \$b = 'C:\Windows\Temp\machine.config.r6-before'
if ((Get-FileHash \$f -Algorithm SHA256).Hash -ne '$MC_HASH') { throw 'machine.config is not the saved state' }
Copy-Item -LiteralPath \$f -Destination \$b -Force
\$t = [IO.File]::ReadAllText(\$f)
\$n = \$t.Replace('<processModel autoConfig="true"/>', '<processModel autoConfig="true" minWorkerThreads="50" minIoThreads="50"/>')
if (\$n -eq \$t) { throw 'processModel element not found' }
[IO.File]::WriteAllText(\$f, \$n)
Import-Module WebAdministration; Restart-WebAppPool -Name TicketDesk
Select-String -Path \$f -Pattern '<processModel ' | ForEach-Object { 'now: ' + \$_.Line.Trim() }
PS
  ;;
  legacy-threads-off) need_lock; "$V" ps <<PS
\$f = '$MC'; \$b = 'C:\Windows\Temp\machine.config.r6-before'
if (-not (Test-Path \$b)) { throw 'no saved machine.config' }
Copy-Item -LiteralPath \$b -Destination \$f -Force
if ((Get-FileHash \$f -Algorithm SHA256).Hash -ne '$MC_HASH') { throw 'restored machine.config hash differs' }
Remove-Item -LiteralPath \$b -Force
Import-Module WebAdministration; Restart-WebAppPool -Name TicketDesk
'machine.config restored, sha256 ' + (Get-FileHash \$f -Algorithm SHA256).Hash
Select-String -Path \$f -Pattern '<processModel ' | ForEach-Object { 'now: ' + \$_.Line.Trim() }
PS
  ;;
  new-rcsi-base)
    s() { SQLCMDPASSWORD="$(envval "$TD_W/.secrets/td-sql.env" MSSQL_SA_PASSWORD)" docker exec -e SQLCMDPASSWORD td-load-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -Q "$1"; }
    "$HERE/capacity.sh" stop-new
    s "SET NOCOUNT ON; ALTER DATABASE TicketDesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE TicketDesk FROM DISK = N'/var/opt/mssql/backup/new-base.bak' WITH REPLACE; ALTER DATABASE TicketDesk SET MULTI_USER; ALTER DATABASE TicketDesk SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE; BACKUP DATABASE TicketDesk TO DISK = N'/var/opt/mssql/backup/new-base-rcsi.bak' WITH COPY_ONLY, INIT, CHECKSUM;" | tail -1
    s "SET NOCOUNT ON; SELECT 'rcsi=' + CAST(is_read_committed_snapshot_on AS varchar(1)) + ' snapshot=' + snapshot_isolation_state_desc COLLATE DATABASE_DEFAULT FROM sys.databases WHERE name = N'TicketDesk'" ;;
  *) sed -n '2,8p' "$0"; exit 2 ;;
esac
