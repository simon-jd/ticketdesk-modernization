#!/usr/bin/env bash
# td-db-app-login.sh - the new app's least-privilege database principal on the lab VM's SQL Server (instead of sa).
#
#   ops/dev/td-db-app-login.sh apply    create/refresh login td_modern_app (password in $TD_W/.secrets/td-modern-app.env,
#                                       generated on first use, never printed), user td_modern_app in TicketDesk, member of
#                                       td_modern_app_role (grants: ops/db/td_modern_app_role.sql). Idempotent.
#   ops/dev/td-db-app-login.sh check    connect as td_modern_app: what it can do (reads, writes it needs, in a rolled-back
#                                       transaction) and what it cannot (DDL, td-db journal, role changes, other tables)
#   ops/dev/td-db-app-login.sh conn     print the app's connection string (contains the password; pipe it, don't log it)
#
# Target: the VM SQL Express through 127.0.0.1:11433 (sa from legacy-lab.env) unless TD_APP_LOGIN_SERVER/_SA_ENV say
# otherwise. A VM snapshot reset drops the login (it lives in master), so replay-gateway.sh runs `apply` after its reset
# when TD_MODERN_LOGIN=app. Azure SQL: no login and no password: the same role script, then
#   CREATE USER [<app managed identity>] FROM EXTERNAL PROVIDER; ALTER ROLE td_modern_app_role ADD MEMBER [<identity>];
# run by the Entra admin (docs/dev/least-privilege-sql.md).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
SERVER="${TD_APP_LOGIN_SERVER:-127.0.0.1,11433}"
DB="${TD_APP_LOGIN_DB:-TicketDesk}"
ENV_FILE="$TD_W/.secrets/td-modern-app.env"
SQLCMD="$TD_W/.tools/bin/sqlcmd"
sa_pw() { sed -n 's/^\(TD_SQL_SA_PASSWORD\|MSSQL_SA_PASSWORD\)=//p' "${TD_APP_LOGIN_SA_ENV:-$TD_W/.secrets/legacy-lab.env}"; }
app_pw() { sed -n 's/^TD_MODERN_APP_SQL_PASSWORD=//p' "$ENV_FILE"; }
as_sa() { SQLCMDPASSWORD="$(sa_pw)" "$SQLCMD" -S "$SERVER" -U sa -C -b -h -1 -W "$@"; }
as_app() { SQLCMDPASSWORD="$(app_pw)" "$SQLCMD" -S "$SERVER" -U td_modern_app -C -h -1 -W -d "$DB" "$@"; }

ensure_secret() {
  [[ -f "$ENV_FILE" ]] && return
  umask 077
  local hex; hex="$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')"
  printf 'TD_MODERN_APP_SQL_PASSWORD=%s\n' "${hex}Td-App-1" > "$ENV_FILE"
  chmod 600 "$ENV_FILE"
}

cmd_apply() {
  ensure_secret
  local pw; pw="$(app_pw)"
  # the password goes in as a sqlcmd variable from the environment, not on the command line
  SQLCMDPASSWORD="$(sa_pw)" TD_APP_PW="$pw" "$SQLCMD" -S "$SERVER" -U sa -C -b -h -1 -W -d master -v TD_APP_DB="$DB" \
    -i "$REPO/ops/db/td_modern_app_login.sql"
  as_sa -d "$DB" -Q "SET NOCOUNT ON; IF DATABASE_PRINCIPAL_ID(N'td_modern_app') IS NULL CREATE USER td_modern_app FOR LOGIN td_modern_app; ELSE ALTER USER td_modern_app WITH LOGIN = td_modern_app; PRINT 'user td_modern_app ready';"
  as_sa -d "$DB" -i "$REPO/ops/db/td_modern_app_role.sql"
  as_sa -d "$DB" -Q "SET NOCOUNT ON; IF IS_ROLEMEMBER(N'td_modern_app_role', N'td_modern_app') = 0 ALTER ROLE td_modern_app_role ADD MEMBER td_modern_app; SELECT 'grants: ' + CAST(COUNT(*) AS varchar(10)) + ' on ' + CAST(COUNT(DISTINCT major_id) AS varchar(10)) + ' objects; fixed roles of td_modern_app: ' + CAST((SELECT COUNT(*) FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id = m.role_principal_id WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'td_modern_app') AND r.is_fixed_role = 1) AS varchar(10)) FROM sys.database_permissions WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'td_modern_app_role')"
}

probe() {  # probe LABEL EXPECT(ok|denied) SQL
  local out rc
  out="$(as_app -Q "SET NOCOUNT ON; $3" 2>&1)" && rc=0 || rc=$?
  if [[ $rc -eq 0 && ! "$out" =~ (Msg\ [0-9]+|denied|permission) ]]; then got=ok; else got=denied; fi
  printf '%-7s %-7s %s\n' "$([[ $got == "$2" ]] && echo PASS || echo FAIL)" "$got" "$1"
  [[ $got == "$2" ]]
}

cmd_check() {
  local fails=0
  echo "login: $(as_app -Q "SET NOCOUNT ON; SELECT SUSER_SNAME() + ' in ' + DB_NAME() + ', sysadmin=' + CAST(IS_SRVROLEMEMBER('sysadmin') AS varchar(2)) + ', db_owner=' + CAST(IS_MEMBER('db_owner') AS varchar(2))" | tr -d '\r')"
  probe "read tickets"                       ok     "SELECT COUNT(*) FROM dbo.Tickets" || fails=$((fails+1))
  probe "read identity users"                ok     "SELECT COUNT(*) FROM dbo.IdentityUsers" || fails=$((fails+1))
  probe "read key ring"                      ok     "SELECT COUNT(*) FROM dbo.DataProtectionKeys" || fails=$((fails+1))
  probe "write user settings (rolled back)"  ok     "BEGIN TRAN; UPDATE TOP (1) dbo.UserSettings SET SelectedProjectId = SelectedProjectId; ROLLBACK" || fails=$((fails+1))
  probe "add a key (rolled back)"            ok     "BEGIN TRAN; INSERT dbo.DataProtectionKeys (FriendlyName, Xml) VALUES (N'probe', N'<probe/>'); ROLLBACK" || fails=$((fails+1))
  probe "run usp_TicketList"                 ok     "IF OBJECT_ID(N'dbo.usp_TicketList') IS NULL SELECT 1 ELSE SELECT HAS_PERMS_BY_NAME(N'dbo.usp_TicketList', N'OBJECT', N'EXECUTE')" || fails=$((fails+1))
  probe "create a table"                     denied "CREATE TABLE dbo.td_probe (id int)" || fails=$((fails+1))
  probe "alter a table"                      denied "ALTER TABLE dbo.Projects ADD td_probe int NULL" || fails=$((fails+1))
  probe "read the td-db journal"             denied "SELECT COUNT(*) FROM dbo.__ModernSchemaHistory" || fails=$((fails+1))
  probe "read the EF6 journal"               denied "SELECT COUNT(*) FROM dbo.__MigrationHistory" || fails=$((fails+1))
  probe "delete tickets"                     denied "BEGIN TRAN; DELETE TOP (1) FROM dbo.Tickets; ROLLBACK" || fails=$((fails+1))
  probe "update or delete key ring rows"     denied "BEGIN TRAN; DELETE TOP (1) FROM dbo.DataProtectionKeys; ROLLBACK" || fails=$((fails+1))
  probe "read the notification queue"        denied "SELECT COUNT(*) FROM notifications.PushNotificationItems" || fails=$((fails+1))
  probe "join db_owner"                      denied "ALTER ROLE db_owner ADD MEMBER td_modern_app" || fails=$((fails+1))
  probe "grant itself more"                  denied "GRANT DELETE ON dbo.Tickets TO td_modern_app" || fails=$((fails+1))
  probe "read server state (DMVs)"           denied "SELECT TOP (1) wait_type FROM sys.dm_os_wait_stats" || fails=$((fails+1))
  echo "failed checks: $fails"
  [[ $fails -eq 0 ]]
}

case "${1:-}" in
  apply) cmd_apply ;;
  check) cmd_check ;;
  conn) printf 'Server=%s;Database=%s;User Id=td_modern_app;Password=%s;TrustServerCertificate=True;Encrypt=True\n' "$SERVER" "$DB" "$(app_pw)" ;;
  *) sed -n '2,19p' "$0"; exit 2 ;;
esac
