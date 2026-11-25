#!/usr/bin/env bash
# Real run of td-db create-admin on a scratch database of td-sql (TicketDesk_r5firstadmin, an empty install), then one sign-in
# through the running new app. Prints commands and results only: no password, no connection string, no cookie value.
set -euo pipefail
. "$HOME/Desktop/ticketdesk-modernization/.tools/env.sh"
REPO=$HOME/Desktop/ticketdesk-modernization/wt-followups
S=$HOME/Desktop/ticketdesk-modernization/scratch-followups
DB=TicketDesk_r5firstadmin; URL=https://localhost:5489; EMAIL=first.admin@lab.ticketdesk.test
TDDB=$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db
TDSEED=$REPO/tools/Seed/TicketDesk.Seed.Cli/bin/Release/net10.0/td-seed
WORK=$(mktemp -d /tmp/td-r5-firstadmin-XXXXXX); chmod 700 "$WORK"
APP=""
cleanup() { if [[ -n "$APP" ]]; then kill "$APP" 2>/dev/null || true; wait "$APP" 2>/dev/null || true; fi; rm -rf "$WORK"; }
trap cleanup EXIT
q() { "$REPO/ops/dev/td-sql.sh" sql -b -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $1"; }
echo "# $(date -u +%FT%TZ)  git $(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet HEAD -- src tools || echo ' (+ uncommitted)')  td-sql $DB (scratch)"
q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END" >/dev/null
CONN="$("$REPO/ops/dev/td-sql.sh" conn "$DB")"
echo '$ td-seed schema --create-database            (TD_SQL_CONN = the scratch database)'
TD_SQL_CONN="$CONN" "$TDSEED" schema --create-database | tail -2
echo '$ td-db migrate --contract --legacy-retired   (TD_DB_CONN)'
TD_DB_CONN="$CONN" "$TDDB" migrate --contract --legacy-retired | tail -1
echo "\$ (users: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUsers"), administrators: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUserRoles ur JOIN [$DB].dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE r.Name = N'TdAdministrators'"))"
# the password: random, in a 0600 file of a 0700 temporary folder, no trailing newline; never echoed
umask 077; openssl rand -base64 18 | tr -d '\n/+=' > "$WORK/pw"; umask 022
echo "\$ td-db create-admin --email $EMAIL --display-name 'Lab First Admin' --password-file \$WORK/pw"
set +e; TD_DB_CONN="$CONN" "$TDDB" create-admin --email "$EMAIL" --display-name 'Lab First Admin' --password-file "$WORK/pw"; echo "exit $?"
echo '$ td-db create-admin --email second.admin@lab.ticketdesk.test --display-name Second --password-stdin < $WORK/pw   (a second run)'
TD_DB_CONN="$CONN" "$TDDB" create-admin --email second.admin@lab.ticketdesk.test --display-name Second --password-stdin < "$WORK/pw"; echo "exit $?"
echo '$ td-db create-admin ... --password <value>   (refused before anything runs)'
TD_DB_CONN="$CONN" "$TDDB" create-admin --email x@lab.ticketdesk.test --display-name X --password not-shown 2>&1 | sed 's/not-shown/<VALUE>/'; echo "exit ${PIPESTATUS[0]}"
set -e
echo "\$ (users: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUsers"), administrators: $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUserRoles ur JOIN [$DB].dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE r.Name = N'TdAdministrators'"))"
HASH0="$(q "SELECT CONVERT(varchar(64), HASHBYTES('SHA2_256', PasswordHash), 2) FROM [$DB].dbo.IdentityUsers WHERE Email = N'$EMAIL'")"
echo "\$ (stored hash: format byte $(q "SELECT CONVERT(varchar(4), SUBSTRING(CAST(N'' AS xml).value('xs:base64Binary(sql:column(\"h\"))', 'varbinary(max)'), 1, 1), 1) FROM (SELECT PasswordHash h FROM [$DB].dbo.IdentityUsers WHERE Email = N'$EMAIL') t") = Identity V3)"

# the app on that database, then one sign-in through its sign-in page
cp "$REPO/src/modern/TicketDesk.Web/migration-registry.json" "$WORK/registry.json"; mkdir -p "$WORK/att"
"$REPO/ops/dev/serve-modern.sh" --url "$URL" --registry "$WORK/registry.json" --attachments "$WORK/att" --db "$DB" > "$WORK/app.log" 2>&1 &
APP=$!
for _ in $(seq 1 60); do curl -ksf "$URL/health/ready" >/dev/null 2>&1 && break; sleep 1; done
echo "\$ curl $URL/health/ready -> $(curl -ks -o /dev/null -w '%{http_code}' "$URL/health/ready")"
J="$WORK/jar"
page="$(curl -ks -c "$J" -b "$J" "$URL/user/sign-in")"
token="$(printf '%s' "$page" | sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p' | head -1)"
echo "\$ GET /user/sign-in -> form with a token: $([[ -n "$token" ]] && echo yes || echo no)"
code="$(curl -ks -o /dev/null -w '%{http_code} %{redirect_url}' -c "$J" -b "$J" "$URL/user/sign-in" \
  --data-urlencode "UserNameOrEmail=$EMAIL" --data-urlencode "Password@$WORK/pw" --data-urlencode "RememberMe=false" --data-urlencode "__RequestVerificationToken=$token")"
echo "\$ POST /user/sign-in (the administrator's e-mail and password) -> $code; sign-in cookie set: $(grep -q '.AspNetCore.Identity.Application' "$J" && echo yes || echo no)"
admin="$(curl -ks -c "$J" -b "$J" -w '\n%{http_code}' "$URL/admin/users")"
echo "\$ GET /admin/users -> $(printf '%s' "$admin" | tail -1); lists 'Lab First Admin': $(printf '%s' "$admin" | grep -q 'Lab First Admin' && echo yes || echo no)"
HASH1="$(q "SELECT CONVERT(varchar(64), HASHBYTES('SHA2_256', PasswordHash), 2) FROM [$DB].dbo.IdentityUsers WHERE Email = N'$EMAIL'")"
echo "\$ (password hash after the sign-in: $([[ "$HASH0" == "$HASH1" ]] && echo 'unchanged, no rehash needed' || echo CHANGED))"
for p in "$(cat "$WORK/pw")"; do if grep -rqF -e "$p" "$WORK/app.log"; then echo "the password is in the app log: refusing" >&2; exit 1; fi; done
echo "# app log: $(grep -c . "$WORK/app.log") lines, $(grep -ci 'error\|fail' "$WORK/app.log" || true) with error/fail"
