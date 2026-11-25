#!/usr/bin/env bash
# export-schema.sh - schema exports of the legacy TicketDesk database (run from lab-host).
#
#   export-schema.sh [OUT_DIR]     default OUT_DIR: $TD_W/vm/exports
#
# Writes ticketdesk-clean.dacpac (sqlpackage Extract of the live database on the lab VM) and
# ticketdesk-schema.sql (sqlpackage Script: the full CREATE script a new, empty database would get;
# SQLCMD mode; it never drops anything: it stops if the database already exists). Run it with
#   sqlcmd -b -v DatabaseName=TicketDesk DefaultFilePrefix=TicketDesk DefaultDataPath=<dir/> DefaultLogPath=<dir/> \
#          -i ticketdesk-schema.sql      (e.g. /var/opt/mssql/data/ in the SQL Server Linux container)
# Users, logins and permissions are left out (the legacy app's Windows login does not exist elsewhere).
# Connects to the VM's SQL Express through the forwarded port as sa; the password stays in a 0600
# response file so it never appears on a command line.
set -euo pipefail
shopt -s inherit_errexit 2>/dev/null || true
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
OUT="${1:-$TD_W/vm/exports}"
SQLPACKAGE="${SQLPACKAGE:-$TD_W/.tools/sqlpackage/sqlpackage}"
SERVER="${TD_SQL_SERVER:-127.0.0.1,11433}"
DB="${TD_SQL_DB:-TicketDesk}"
PW="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")"
[[ -n "$PW" ]] || { echo "no TD_SQL_SA_PASSWORD in .secrets/legacy-lab.env" >&2; exit 1; }
mkdir -p "$OUT"
tmp="$(mktemp -d)"; chmod 700 "$tmp"; trap 'rm -rf "$tmp"' EXIT
umask 077
mask() { sed -u "s/PWD=[^;]*/PWD=***/g"; }   # never echo the password, even in error messages
# Response-file values must not contain spaces (hence UID/PWD rather than "User Id"/"Password").

cat > "$tmp/extract.rsp" <<EOF
/Action:Extract
/SourceConnectionString:Server=$SERVER;Database=$DB;UID=sa;PWD=$PW;TrustServerCertificate=True;Encrypt=True
/TargetFile:$OUT/ticketdesk-clean.dacpac
/OverwriteFiles:True
/p:ExtractAllTableData=False
EOF
"$SQLPACKAGE" "@$tmp/extract.rsp" 2>&1 | mask
[[ -s "$OUT/ticketdesk-clean.dacpac" ]] || { echo "sqlpackage Extract failed" >&2; exit 1; }

# Script against a database name that does not exist on the server -> complete create script.
cat > "$tmp/script.rsp" <<EOF
/Action:Script
/SourceFile:$OUT/ticketdesk-clean.dacpac
/TargetConnectionString:Server=$SERVER;Database=TicketDesk_SchemaScriptTarget_DoesNotExist;UID=sa;PWD=$PW;TrustServerCertificate=True;Encrypt=True
/OutputPath:$tmp/schema.sql
/OverwriteFiles:True
/p:ExcludeObjectTypes=Users;Logins;RoleMembership;Permissions
/p:ScriptDatabaseOptions=False
EOF
"$SQLPACKAGE" "@$tmp/script.rsp" 2>&1 | mask
[[ -s "$tmp/schema.sql" ]] || { echo "sqlpackage Script produced no output" >&2; exit 1; }
# sqlpackage's create script drops an existing database of the same name first. Replace that block
# with a guard that stops instead, so the export is safe to run against any server.
"${TD_PYTHON:-python3}" -I - "$tmp/schema.sql" "$OUT/ticketdesk-schema.sql" <<'PY'
import re, sys
s = open(sys.argv[1], encoding="utf-8-sig").read()
drop = re.compile(r"IF \(DB_ID\(N'\$\(DatabaseName\)'\) IS NOT NULL\)\s*\nBEGIN\s*\n\s*ALTER DATABASE \[\$\(DatabaseName\)\]\s*\n\s*SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\s*\n\s*DROP DATABASE \[\$\(DatabaseName\)\];\s*\nEND")
s, n = drop.subn("IF (DB_ID(N'$(DatabaseName)') IS NOT NULL)\n    RAISERROR (N'Database $(DatabaseName) already exists; this script only creates a new database.', 16, 1);", s)
assert n == 1, "drop-database block not found"
# Force the caller to name the database and file locations (-v ...): comment out the defaults.
s, n = re.subn(r'^:setvar (DatabaseName|DefaultFilePrefix|DefaultDataPath|DefaultLogPath) .*$', lambda m: "-- " + m.group(0) + "   (pass with sqlcmd -v)", s, flags=re.M)
assert n == 4, n
# EF created the [notifications] schema while connected as the app pool login; own it by dbo instead.
s = re.sub(r"AUTHORIZATION \[IIS AppPool\\[^\]]+\]", "AUTHORIZATION [dbo]", s)
assert "IIS AppPool" not in s
assert "DROP DATABASE" not in s
open(sys.argv[2], "w", encoding="utf-8").write(s)
PY
chmod 644 "$OUT/ticketdesk-schema.sql" "$OUT/ticketdesk-clean.dacpac"
echo "tables in script: $(grep -c '^CREATE TABLE' "$OUT/ticketdesk-schema.sql")"
ls -la "$OUT/ticketdesk-schema.sql" "$OUT/ticketdesk-clean.dacpac"
