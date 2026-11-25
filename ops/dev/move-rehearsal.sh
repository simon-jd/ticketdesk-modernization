#!/usr/bin/env bash
# Timed rehearsal of the Azure SQL move between two td-sql databases: the cutover path recommended in
# docs/sql/azure-sql-move.md (freeze writes -> SqlPackage export .bacpac -> import -> td-db reconcile -> switch).
#
#   ops/dev/move-rehearsal.sh S|M [OUT_DIR]
#
# Builds a fresh source at the given scale (tools/Seed, seed 42) with the td-db expand migrations, then times each
# step. Against Azure the import goes to the logical server with /p:DatabaseServiceObjective; nothing else changes.
# Connection details go to SqlPackage through a 0600 response file and to td-db through environment variables.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
. "$TD_W/.tools/env.sh"
SCALE="${1:?usage: move-rehearsal.sh S|M [OUT_DIR]}"
OUT="${2:-$REPO/docs/evidence/sql-move-$(date +%Y%m%d)}"
SRC="td_move_${SCALE}_src"; DST="td_move_${SCALE}_dst"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
mkdir -p "$OUT"
TDB="$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db"
[[ -x "$TDB" ]] || dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
PW="$(sed -n 's/^MSSQL_SA_PASSWORD=//p' "$TD_W/.secrets/td-sql.env")"
LOG="$OUT/rehearsal-$SCALE.txt"
now() { date +%s.%N; }
secs() { python3 -c "print(round($2 - $1, 2))"; }
sql() { "$HERE/td-sql.sh" sql -Q "$1" >/dev/null; }

{
echo "# move rehearsal, scale $SCALE, $(date -u +%FT%TZ), host load $(cut -d' ' -f1-3 /proc/loadavg), git $(git -C "$REPO" rev-parse --short HEAD)"
echo "# sqlpackage $(sqlpackage /version 2>/dev/null | head -1); source and target in td-sql ($("$HERE/td-sql.sh" sql -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(32))" | tr -d '\r'))"
sql "IF DB_ID(N'$SRC') IS NOT NULL BEGIN ALTER DATABASE [$SRC] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$SRC]; END; IF DB_ID(N'$DST') IS NOT NULL BEGIN ALTER DATABASE [$DST] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DST]; END"
echo "## prepare source $SRC (not part of the window)"
TD_SEED_DB="$SRC" "$HERE/seed.sh" schema --create-database
TD_SEED_DB="$SRC" "$HERE/seed.sh" run --scale "$SCALE" --seed 42 > "$WORK/seed.txt"; sed -n '1p' "$WORK/seed.txt"
TD_DB_CONN="$("$HERE/td-sql.sh" conn "$SRC")" "$TDB" migrate | tail -1

echo "## window"
t0=$(now)
# 1. freeze: no more legacy writes (on the real host: stop the app pool, then this)
sql "ALTER DATABASE [$SRC] SET READ_ONLY WITH ROLLBACK IMMEDIATE"
t1=$(now); echo "freeze (READ_ONLY): $(secs $t0 $t1) s"
# 2. export
cat > "$WORK/export.rsp" <<RSP
/Action:Export
/SourceServerName:127.0.0.1,14333
/SourceDatabaseName:$SRC
/SourceUser:sa
/SourcePassword:$PW
/SourceTrustServerCertificate:True
/TargetFile:$WORK/$SRC.bacpac
RSP
chmod 600 "$WORK/export.rsp"
sqlpackage "@$WORK/export.rsp" 2>&1 | grep -vi password | tail -3
t2=$(now); echo "export: $(secs $t1 $t2) s, bacpac $(stat -c %s "$WORK/$SRC.bacpac") bytes"
# 3. import into the new database (Azure: add /p:DatabaseEdition and /p:DatabaseServiceObjective)
cat > "$WORK/import.rsp" <<RSP
/Action:Import
/SourceFile:$WORK/$SRC.bacpac
/TargetServerName:127.0.0.1,14333
/TargetDatabaseName:$DST
/TargetUser:sa
/TargetPassword:$PW
/TargetTrustServerCertificate:True
RSP
chmod 600 "$WORK/import.rsp"
sqlpackage "@$WORK/import.rsp" 2>&1 | grep -vi password | tail -3
t3=$(now); echo "import: $(secs $t2 $t3) s"
# 4. reconcile every table (exit 1 on any mismatch stops the cutover)
set +e
TD_DB_SOURCE="$("$HERE/td-sql.sh" conn "$SRC")" TD_DB_TARGET="$("$HERE/td-sql.sh" conn "$DST")" "$TDB" reconcile --json "$OUT/reconcile-$SCALE.json"
rc=$?
set -e
t4=$(now); echo "reconcile: $(secs $t3 $t4) s, exit code $rc"
echo "window total (freeze -> reconciled): $(secs $t0 $t4) s"
python3 - "$OUT/timings-$SCALE.json" "$SCALE" "$(secs $t0 $t1)" "$(secs $t1 $t2)" "$(secs $t2 $t3)" "$(secs $t3 $t4)" "$(secs $t0 $t4)" "$(stat -c %s "$WORK/$SRC.bacpac")" "$rc" <<'PY'
import json, sys
o, s, f, e, i, r, t, b, rc = sys.argv[1:]
json.dump({"scale": s, "freezeSeconds": float(f), "exportSeconds": float(e), "importSeconds": float(i), "reconcileSeconds": float(r),
           "windowSeconds": float(t), "bacpacBytes": int(b), "reconcileExitCode": int(rc)}, open(o, "w"), indent=2)
PY
sql "ALTER DATABASE [$SRC] SET READ_WRITE WITH ROLLBACK IMMEDIATE"
exit $rc
} 2>&1 | tee "$LOG"
