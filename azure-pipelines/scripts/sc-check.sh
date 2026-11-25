#!/usr/bin/env bash
# Token exchange for the service connection TD_ARM_SERVICE_CONNECTION_ID (sc-check.yml): Azure DevOps OIDC token ->
# Entra ID client-credentials grant with that token as client assertion -> ARM access token -> read-only ARM calls.
# Tokens are registered with the log masker and never printed; only their non-secret claims are shown. Creates nothing.
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
: "${SYSTEM_ACCESSTOKEN:?map System.AccessToken}" "${TD_ARM_SERVICE_CONNECTION_ID:?variable group td-lab-azure}"
OUT="$CI_OUT/sc-check"; mkdir -p "$OUT"
claims() { python3 -I -c 'import base64,json,sys
p=sys.stdin.read().split(".")[1]; d=json.loads(base64.urlsafe_b64decode(p+"="*(-len(p)%4)))
keep=sys.argv[1].split(",")
print(json.dumps({k: d[k] for k in keep if k in d}))' "$1"; }

ci_section "1. OIDC token of the service connection (Azure DevOps)"
oidc_url="${SYSTEM_COLLECTIONURI}${SYSTEM_TEAMPROJECTID}/_apis/distributedtask/hubs/build/plans/${SYSTEM_PLANID}/jobs/${SYSTEM_JOBID}/oidctoken?serviceConnectionId=${TD_ARM_SERVICE_CONNECTION_ID}&api-version=7.1-preview.1"
resp="$(curl -sS -X POST -H @<(printf 'Authorization: Bearer %s\n' "$SYSTEM_ACCESSTOKEN") -H 'Content-Type: application/json' -d '{}' \
  -w '\n%{http_code}' "$oidc_url")"
code="${resp##*$'\n'}"; resp="${resp%$'\n'*}"
idt="$(jq -r '.oidcToken // empty' <<<"$resp" 2>/dev/null || true)"
[[ -n "$idt" ]] || { echo "no OIDC token: HTTP $code $(head -c 400 <<<"$resp")"; exit 1; }
printf '##vso[task.setsecret]%s\n' "$idt"
echo "OIDC token claims: $(claims iss,sub,aud <<<"$idt")" | tee "$OUT/oidc-claims.txt"

ci_section "2. Entra ID token exchange (client assertion = OIDC token)"
tok_resp="$(printf 'client_id=%s&grant_type=client_credentials&scope=%s&client_assertion_type=%s&client_assertion=%s' \
  "$TD_ARM_CLIENT_ID" "https%3A%2F%2Fmanagement.azure.com%2F.default" "urn%3Aietf%3Aparams%3Aoauth%3Aclient-assertion-type%3Ajwt-bearer" "$idt" \
  | curl -sS -X POST -H 'Content-Type: application/x-www-form-urlencoded' --data-binary @- \
      "https://login.microsoftonline.com/${TD_ARM_TENANT_ID}/oauth2/v2.0/token")"
at="$(jq -r '.access_token // empty' <<<"$tok_resp")"
[[ -n "$at" ]] || { echo "token exchange failed: $(jq -c '{error, error_codes, error_description: (.error_description // "" | .[0:300])}' <<<"$tok_resp")" | tee "$OUT/exchange.txt"; exit 1; }
printf '##vso[task.setsecret]%s\n' "$at"
echo "ARM access token: $(jq -c '{token_type, expires_in}' <<<"$tok_resp") claims $(claims aud,iss,appid,oid,idtyp,tid <<<"$at")" | tee "$OUT/exchange.txt"
TD_AT="$at" python3 -I - "$TD_ARM_CLIENT_ID" "$TD_ARM_SP_OBJECT_ID" <<'PY' | tee -a "$OUT/exchange.txt"
import base64, json, os, sys
p = os.environ["TD_AT"].split(".")[1]; d = json.loads(base64.urlsafe_b64decode(p + "=" * (-len(p) % 4)))
ok = d.get("appid") == sys.argv[1] and d.get("oid") == sys.argv[2] and d.get("aud", "").startswith("https://management")
print("token is for app ticketdesk-lab-ado / its service principal, audience ARM:", ok)
sys.exit(0 if ok else 1)
PY

ci_section "3. read-only ARM calls with that token"
arm() { curl -sS -H @<(printf 'Authorization: Bearer %s\n' "$at") -o "$OUT/arm.json" -w '%{http_code}' "https://management.azure.com$1"; }
code="$(arm "/subscriptions?api-version=2022-12-01")"
echo "GET /subscriptions -> HTTP $code, subscriptions visible: $(jq '.value | length' "$OUT/arm.json")" | tee -a "$OUT/arm.txt"
code="$(arm "/subscriptions/${TD_ARM_SUBSCRIPTION_ID}/resourcegroups/${TD_ARM_RESOURCE_GROUP}?api-version=2022-09-01")"
echo "GET resource group ${TD_ARM_RESOURCE_GROUP} -> HTTP $code $(jq -c '{error: .error.code, name, location, tags: (.tags // {} | {owner, ttl})}' "$OUT/arm.json")" | tee -a "$OUT/arm.txt"
rm -f "$OUT/arm.json"
echo "authenticated: yes (token exchange succeeded); nothing created"
