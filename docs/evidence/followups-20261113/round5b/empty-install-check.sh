#!/usr/bin/env bash
# empty-install-check.sh DB PORT: the documented empty-install path (td-seed schema --create-database, td-db migrate --contract
# --legacy-retired, td-db create-admin) on a scratch database of td-sql, then the app's own new-user path once: the
# administrator signs in and adds a user on admin/new. Prints commands and results only (no password, connection string, cookie).
set -euo pipefail
. "$HOME/Desktop/ticketdesk-modernization/.tools/env.sh"
REPO=$HOME/Desktop/ticketdesk-modernization/wt-followups
DB="$1"; URL="https://localhost:$2"; EMAIL=first.admin@lab.ticketdesk.test
TDDB=$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db
TDSEED=$REPO/tools/Seed/TicketDesk.Seed.Cli/bin/Release/net10.0/td-seed
WORK=$(mktemp -d /tmp/td-r5b-empty-XXXXXX); chmod 700 "$WORK"; APP=""
cleanup() { if [[ -n "$APP" ]]; then kill "$APP" 2>/dev/null || true; wait "$APP" 2>/dev/null || true; fi; rm -rf "$WORK"; }
trap cleanup EXIT
q() { "$REPO/ops/dev/td-sql.sh" sql -b -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $1"; }
echo "# $(date -u +%FT%TZ)  git $(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet HEAD -- src tools || echo ' (+ uncommitted)')  td-sql $DB (scratch)"
q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END" >/dev/null
CONN="$("$REPO/ops/dev/td-sql.sh" conn "$DB")"
echo '$ td-seed schema --create-database'; TD_SQL_CONN="$CONN" "$TDSEED" schema --create-database | tail -1
echo '$ td-db migrate --contract --legacy-retired'; TD_DB_CONN="$CONN" "$TDDB" migrate --contract --legacy-retired | tail -1
echo "\$ (notifications.* tables: $(q "SELECT COUNT(*) FROM [$DB].sys.tables t JOIN [$DB].sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = N'notifications'"))"
umask 077; openssl rand -base64 18 | tr -d '\n/+=' > "$WORK/pw"; umask 022
echo "\$ td-db create-admin --email $EMAIL --display-name 'Lab First Admin' --password-file \$WORK/pw"
TD_DB_CONN="$CONN" "$TDDB" create-admin --email "$EMAIL" --display-name 'Lab First Admin' --password-file "$WORK/pw"
cp "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$WORK/registry.json"; mkdir -p "$WORK/att"
"$REPO/ops/dev/serve-modern.sh" --url "$URL" --registry "$WORK/registry.json" --attachments "$WORK/att" --db "$DB" > "$WORK/app.log" 2>&1 &
APP=$!
for _ in $(seq 1 60); do curl -ksf "$URL/health/ready" >/dev/null 2>&1 && break; sleep 1; done
J="$WORK/jar"; tok() { sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p' | head -1; }
t="$(curl -ks -c "$J" -b "$J" "$URL/user/sign-in" | tok)"
echo "\$ POST /user/sign-in (the administrator) -> $(curl -ks -o /dev/null -w '%{http_code}' -c "$J" -b "$J" "$URL/user/sign-in" --data-urlencode "UserNameOrEmail=$EMAIL" --data-urlencode "Password@$WORK/pw" --data-urlencode RememberMe=false --data-urlencode "__RequestVerificationToken=$t")"
t="$(curl -ks -c "$J" -b "$J" "$URL/admin/new" | tok)"
code="$(curl -ks -o "$WORK/new.html" -w '%{http_code} %{redirect_url}' -c "$J" -b "$J" "$URL/admin/new" --data-urlencode UserName=new.user@lab.ticketdesk.test \
  --data-urlencode Email=new.user@lab.ticketdesk.test --data-urlencode 'DisplayName=New User' --data-urlencode "Password@$WORK/pw" --data-urlencode "ConfirmPassword@$WORK/pw" --data-urlencode "__RequestVerificationToken=$t")"
echo "\$ POST /admin/new (a user through the app's own new-user path) -> $code"
echo "\$ (users: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUsers"); new user: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUsers WHERE Email = N'new.user@lab.ticketdesk.test'"); e-mail destinations: $(q "IF OBJECT_ID(N'[$DB].notifications.PushNotificationDestinations') IS NULL SELECT 'no table' ELSE SELECT CAST(COUNT(*) AS varchar) FROM [$DB].notifications.PushNotificationDestinations"))"
echo "# app log, error lines (first 3, trimmed):"; grep -i -m3 'fail\|exception\|invalid object' "$WORK/app.log" | cut -c1-220 || echo "  none"
for p in "$(cat "$WORK/pw")"; do if grep -rqF -e "$p" "$WORK/app.log" "$WORK/new.html"; then echo "the password is in the output: refusing" >&2; exit 1; fi; done
