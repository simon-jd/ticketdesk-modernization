# shellcheck shell=bash
# Azure DevOps helpers for the TicketDesk lab project (sourced by ops/ado/*.sh, never run directly).
#
# The PAT is read from $ADO_PAT_FILE inside each call and sent only as an HTTP header: through a process substitution
# for curl (never in argv) and through GIT_CONFIG_* environment variables for git (environ is readable only by the
# owner). It is never echoed, never put in a URL, a remote, a file in the repo or a pipeline variable.
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
ADO_ORG_URL="${ADO_ORG_URL:-https://dev.azure.com/<ado-org>}"
ADO_PROJECT="${ADO_PROJECT:-TicketDesk}"
ADO_REPO="${ADO_REPO:-TicketDesk}"
ADO_PAT_FILE="${ADO_PAT_FILE:-$TD_W/.secrets/ado.pat}"
ADO_GIT_URL="${ADO_GIT_URL:-$ADO_ORG_URL/$ADO_PROJECT/_git/$ADO_REPO}"

_ado_basic() { printf ':%s' "$(cat "$ADO_PAT_FILE")" | base64 -w0; }

# ado_api METHOD PATH [BODY-FILE] - PATH: "https://..." as is, "/_apis/..." org level, "_apis/..." project level.
# Prints the response body; returns 1 (body on stderr) for HTTP >= 400. BODY-FILE "-" reads stdin.
ado_api() {
  local method="$1" path="$2" body="${3:-}" url out code
  case "$path" in https://*) url="$path" ;; /*) url="$ADO_ORG_URL$path" ;; *) url="$ADO_ORG_URL/$ADO_PROJECT/$path" ;; esac
  out="$(mktemp)"
  local data=()
  [[ -n "$body" ]] && data=(-H 'Content-Type: application/json' --data-binary "@$body")
  code="$(curl -sS -X "$method" -H @<(printf 'Authorization: Basic %s\n' "$(_ado_basic)") -H 'Accept: application/json' \
            "${data[@]}" -o "$out" -w '%{http_code}' "$url")" || { rm -f "$out"; return 1; }
  if (( code >= 400 )); then echo "HTTP $code $method $url" >&2; cat "$out" >&2; echo >&2; rm -f "$out"; return 1; fi
  cat "$out"; rm -f "$out"
}

# ado_git ARGS... - git with the PAT as an extra header for dev.azure.com only.
ado_git() {
  GIT_TERMINAL_PROMPT=0 GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0="http.https://dev.azure.com/.extraHeader" \
    GIT_CONFIG_VALUE_0="Authorization: Basic $(_ado_basic)" git "$@"
}
