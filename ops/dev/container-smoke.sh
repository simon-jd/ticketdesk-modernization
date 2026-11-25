#!/usr/bin/env bash
# container-smoke.sh - run the TicketDesk.Web image as built (user from the image: app, UID 1654) in front of the lab VM and
# check that it serves a migrated ticket page to a signed-in user: sign in through the container (the sign-in is still the
# legacy app's, proxied), then GET /ticket/ID, answered by the new app (remote authentication, VM database, lab Blob store).
#   TD_LOCK_OWNER=<you> ops/dev/container-smoke.sh IMAGE [TICKET_ID] [--out-dir DIR]
# Secrets go to the container through a temporary 0600 env file and are never printed.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
IMAGE="${1:?image}"; TICKET="${2:-1}"; OUT="${4:-}"
PORT="${TD_SMOKE_PORT:-18080}"; NAME="td-tickets-smoke-$$"
"$REPO/ops/onprem/vm/vmlock.sh" check "${TD_LOCK_OWNER:-}" || { echo "take the VM lock first" >&2; exit 1; }
S="$TD_W/.secrets"; ENV="$(mktemp)"; chmod 600 "$ENV"
trap 'docker rm -f "$NAME" >/dev/null 2>&1 || true; rm -f "$ENV"' EXIT
{
  echo "ASPNETCORE_HTTP_PORTS=$PORT"
  echo "Legacy__BaseUrl=https://localhost:8443/"
  echo "Legacy__ServerCertificateSha256=$(openssl x509 -in "$S/legacy-lab-ca/server.crt" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)"
  echo "Legacy__RemoteApp__Enabled=true"
  echo "Legacy__RemoteApp__ApiKey=$(sed -n 's/^TD_REMOTEAPP_API_KEY=//p' "$S/legacy-remoteapp.env")"
  echo "ConnectionStrings__TicketDesk=Server=127.0.0.1,11433;Database=TicketDesk;User Id=sa;Password=$(sed -n 's/^TD_SQL_SA_PASSWORD=//p' "$S/legacy-lab.env");TrustServerCertificate=True;Encrypt=True"
  echo "Attachments__Store=Blob"
  echo "Attachments__BlobConnectionString=$(sed -n 's/^TD_AZURE_STORAGE_CONNECTION_HOST=//p' "$S/legacy-filestore.env")"
} > "$ENV"
docker run -d --name "$NAME" --network host --env-file "$ENV" "$IMAGE" >/dev/null
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$PORT/health/live" >/dev/null 2>&1 && break; sleep 1; done
echo "image: $IMAGE ($(docker image inspect "$IMAGE" --format '{{.Size}} bytes, configured user {{.Config.User}}'))"
echo "process (pid uid args): $(docker top "$NAME" -eo pid,uid,args | tail -n +2 | head -1)"
echo "health/live: $(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/health/live")"
TD_SMOKE_USER="$(sed -n 's/^TD_ADMIN_EMAIL=//p' "$S/legacy-lab.env")" TD_SMOKE_PASSWORD="$(sed -n 's/^TD_ADMIN_PASSWORD=//p' "$S/legacy-lab.env")" \
python3 -I - "http://127.0.0.1:$PORT" "$TICKET" <<'PY'
import http.cookiejar, os, re, sys, urllib.parse, urllib.request
base, ticket = sys.argv[1], sys.argv[2]
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None
jar = http.cookiejar.CookieJar()
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), NoRedirect)
def get(path):
    try: r = op.open(base + path)
    except urllib.error.HTTPError as e: r = e
    return r.status, dict(r.headers), r.read().decode("utf-8", "replace")
s, h, body = get("/user/sign-in")
form = re.search(r"<form[^>]*action=\"[^\"]*sign-in[^\"]*\"[\s\S]*?</form>", body).group(0)
fields = dict(re.findall(r"<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"", form))
fields.update(UserNameOrEmail=os.environ["TD_SMOKE_USER"], Password=os.environ["TD_SMOKE_PASSWORD"], RememberMe="false")
try: r = op.open(base + "/user/sign-in", urllib.parse.urlencode(fields).encode())
except urllib.error.HTTPError as e: r = e
print(f"sign-in (legacy, proxied): {r.status}, backend {r.headers.get('X-TicketDesk-Backend') or 'legacy (not marked)'}")
s, h, body = get(f"/ticket/{ticket}")
title = re.search(r"<title>\s*([\s\S]*?)\s*</title>", body)
print(f"GET /ticket/{ticket}: {s}, {len(body)} bytes, X-TicketDesk-Backend: {h.get('X-TicketDesk-Backend')}, title: {title.group(1).strip() if title else None}")
print("panels: " + ", ".join(x for x in ("ticketDetailPanel", "activityButtonsPanel", "eventPanel", "__RequestVerificationToken") if x in body))
sys.exit(0 if s == 200 and (h.get("X-TicketDesk-Backend") or "").startswith("modern;") else 1)
PY
