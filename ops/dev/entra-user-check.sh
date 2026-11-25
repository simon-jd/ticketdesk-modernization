#!/usr/bin/env bash
# entra-user-check.sh - the one-time real Entra ID sign-in check (University tenant), run with the user.
#
#   TD_LOCK_OWNER=<you> ops/dev/entra-user-check.sh      [needs the VM lock for the reset; keep it while the user signs in]
#
# Resets the VM to baseline-s-identity (legacy app with the shared cookie), creates the app's least-privilege login,
# then runs the gateway in the foreground on https://localhost:5443 with the shared cookie, waves 0-5 and Entra ID from
# $TD_W/.secrets/entra-web.env (ops/dev/entra-apply.sh). Ctrl+C stops it. Afterwards: `ops/dev/entra-user-check.sh
# --record DIR` writes what the sign-in created (no tokens, no secrets) and resets the VM to baseline-s.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
if [[ "${1:-}" == --record ]]; then
  out="${2:?DIR}"; mkdir -p "$out"
  SQLCMDPASSWORD="$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$TD_W/.secrets/legacy-lab.env")" "$TD_W/.tools/bin/sqlcmd" -S 127.0.0.1,11433 -U sa -C -d TicketDesk -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT 'login=' + l.LoginProvider + ' user_created_by_entra=' + CASE WHEN u.PasswordHash IS NULL THEN 'yes' ELSE 'no (linked)' END + ' roles=' + ISNULL(STRING_AGG(r.Name, ','), '-') FROM dbo.IdentityUserLogins l JOIN dbo.IdentityUsers u ON u.Id = l.UserId LEFT JOIN dbo.IdentityUserRoles ur ON ur.UserId = u.Id LEFT JOIN dbo.IdentityRoles r ON r.Id = ur.RoleId WHERE l.LoginProvider = N'EntraId' GROUP BY l.LoginProvider, u.PasswordHash" > "$out/entra-user.txt"
  cat "$out/entra-user.txt"
  "$REPO/ops/onprem/baseline/baseline-s.sh" reset | tail -1
  exit 0
fi
[[ -f "$TD_W/.secrets/entra-web.env" ]] || { echo "no $TD_W/.secrets/entra-web.env (ops/dev/entra-apply.sh secrets)" >&2; exit 1; }
TD_BASELINE_SNAPSHOT=baseline-s-identity "$REPO/ops/onprem/baseline/baseline-s.sh" reset | tail -1
"$HERE/td-db-app-login.sh" apply | tail -1
cat <<'TXT'
Gateway starting on https://localhost:5443 (Ctrl+C to stop). On the user's machine:
  ssh -N -L 5443:localhost:5443 -L 8443:localhost:8443 lab-host
  open https://localhost:5443/user/sign-in  ->  "Entra ID"
TXT
TD_MODERN_DB=vm TD_MODERN_LOGIN=app exec "$HERE/replay-gateway.sh" --serve --waves 0,1,2,3,4,5 --shared-cookie --entra "$TD_W/.secrets/entra-web.env"
