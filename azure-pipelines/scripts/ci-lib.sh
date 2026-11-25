# shellcheck shell=bash
# Sourced by the CI scripts (azure-pipelines/scripts/*.sh) on the self-hosted agent lab-host.
#
# Lab secrets stay on the agent host: $TD_W/.secrets (0700, never in the repo, never uploaded to Azure DevOps). A script
# reads only the values it needs, one by one with sed (the files are never sourced), exports them into its own process,
# and registers each with the agent's log masker (##vso[task.setsecret]) so an accidental echo shows *** in the log.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
CI_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CI_OUT="${CI_OUT:-${BUILD_ARTIFACTSTAGINGDIRECTORY:-$CI_REPO/.ci-out}}"
mkdir -p "$CI_OUT"
exec 3>&1   # the step's real stdout: logging commands go there even from inside $(...)

ci_secret() { # ci_secret FILE KEY - print the value of KEY= in $TD_W/.secrets/FILE (masked in the pipeline log)
  local v
  v="$(sed -n "s/^$2=//p" "$TD_W/.secrets/$1" | head -n 1)"
  [[ -n "$v" ]] || { echo "missing $2 in \$TD_W/.secrets/$1" >&2; return 1; }
  [[ -n "${TF_BUILD:-}" ]] && printf '##vso[task.setsecret]%s\n' "$v" >&3
  printf '%s' "$v"
}

# td-sql (127.0.0.1:14333): sa for the integration tests and the database copies
ci_sql_env() {
  TD_SQL_SA_PASSWORD="$(ci_secret td-sql.env MSSQL_SA_PASSWORD)"
  TD_SQL_TEST_CONN="Server=127.0.0.1,14333;Database=master;User Id=sa;Password=$TD_SQL_SA_PASSWORD;TrustServerCertificate=True;Encrypt=True"
  export TD_SQL_SA_PASSWORD TD_SQL_TEST_CONN
}
ci_sql_conn() { printf '%s' "${TD_SQL_TEST_CONN/Database=master/Database=$1}"; }

# everything `dotnet test` of the solution reads (tests skip without them)
ci_test_env() {
  ci_sql_env
  TD_SEED_USER_PASSWORD="$(ci_secret td-seed.env TD_SEED_USER_PASSWORD)"
  TD_AZURE_STORAGE_CONNECTION_HOST="$(ci_secret legacy-filestore.env TD_AZURE_STORAGE_CONNECTION_HOST)"
  # the Service Bus emulator's fixed development key (ops/dev/servicebus-emulator.sh conn)
  TD_SERVICEBUS_TEST_CONN="$(bash "$CI_REPO/ops/dev/servicebus-emulator.sh" conn)"
  TD_MODERN_SEEDED_DB="${TD_MODERN_SEEDED_DB:-TicketDesk}"
  export TD_SEED_USER_PASSWORD TD_AZURE_STORAGE_CONNECTION_HOST TD_SERVICEBUS_TEST_CONN TD_MODERN_SEEDED_DB
}

# the lab services the CI uses must already run on the host (shared with the other agents: never started/stopped here)
ci_require_services() {
  local c
  for c in "$@"; do
    [[ "$(docker inspect -f '{{.State.Running}}' "$c" 2>/dev/null)" == true ]] || { echo "##vso[task.logissue type=error]host service $c is not running" >&3; return 1; }
  done
}

ci_section() { printf '##[section]%s\n' "$*" >&3; }
