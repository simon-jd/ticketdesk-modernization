#!/usr/bin/env bash
# td-db container image (built locally, not pushed): --help, then the empty-install path's migrate and one create-admin
# against a scratch database of td-sql, from inside the image. The connection string goes in through a 0600 env file and
# the password through standard input; neither is printed.
set -euo pipefail
. "$HOME/Desktop/ticketdesk-modernization/.tools/env.sh"
REPO=$HOME/Desktop/ticketdesk-modernization/wt-followups
IMG=ticketdesk-db:r5b-followups; DB=TicketDesk_r5b_image; EMAIL=image.admin@lab.ticketdesk.test
TDSEED=$REPO/tools/Seed/TicketDesk.Seed.Cli/bin/Release/net10.0/td-seed
WORK=$(mktemp -d /tmp/td-r5b-image-XXXXXX); chmod 700 "$WORK"; trap 'rm -rf "$WORK"' EXIT
q() { "$REPO/ops/dev/td-sql.sh" sql -b -h -1 -W -Q "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $1"; }
echo "# $(date -u +%FT%TZ)  git $(git -C "$REPO" rev-parse --short HEAD)$(git -C "$REPO" diff --quiet HEAD -- src tools || echo ' (+ uncommitted)')"
echo "# image $IMG $(docker image inspect -f '{{.Id}} {{.Size}} bytes, user {{.Config.User}}, entrypoint {{json .Config.Entrypoint}}' $IMG)"
echo "# base: $(grep -m1 '^FROM .* AS runtime' $REPO/src/modern/TicketDesk.Database/Dockerfile)"
echo '$ docker run --rm $IMG create-admin --help'
docker run --rm $IMG create-admin --help; echo "exit $?"
q "IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END" >/dev/null
CONN="$("$REPO/ops/dev/td-sql.sh" conn "$DB")"
echo '$ td-seed schema --create-database   (host)'; TD_SQL_CONN="$CONN" "$TDSEED" schema --create-database | tail -1
umask 077; printf 'TD_DB_CONN=%s\n' "$CONN" > "$WORK/env"; openssl rand -base64 18 | tr -d '\n/+=' > "$WORK/pw"; umask 022
echo '$ docker run --rm --network host --env-file $WORK/env $IMG migrate --contract --legacy-retired'
docker run --rm --network host --env-file "$WORK/env" $IMG migrate --contract --legacy-retired | tail -1
echo "\$ docker run --rm -i --network host --env-file \$WORK/env $IMG create-admin --email $EMAIL --display-name 'Image Admin' --password-stdin < \$WORK/pw"
set +e; docker run --rm -i --network host --env-file "$WORK/env" $IMG create-admin --email "$EMAIL" --display-name 'Image Admin' --password-stdin < "$WORK/pw"; echo "exit $?"
echo '$ (the same again: refused)'
docker run --rm -i --network host --env-file "$WORK/env" $IMG create-admin --email other@lab.ticketdesk.test --display-name Other --password-stdin < "$WORK/pw"; echo "exit $?"; set -e
echo "\$ (users $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUsers"), administrators $(q "SELECT COUNT(*) FROM [$DB].dbo.IdentityUserRoles ur JOIN [$DB].dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE r.Name = N'TdAdministrators'"), e-mail destinations $(q "SELECT COUNT(*) FROM [$DB].notifications.PushNotificationDestinations"), hash format byte $(q "SELECT CONVERT(varchar(4), SUBSTRING(CAST(N'' AS xml).value('xs:base64Binary(sql:column(\"h\"))', 'varbinary(max)'), 1, 1), 1) FROM (SELECT PasswordHash h FROM [$DB].dbo.IdentityUsers WHERE Email = N'$EMAIL') t"))"
q "ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB];" >/dev/null && echo "# $DB dropped"
