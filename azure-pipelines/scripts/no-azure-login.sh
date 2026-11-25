#!/usr/bin/env bash
# CI gate: the agent gives jobs no Azure identity of the host. AZURE_CONFIG_DIR must be the agent's empty directory, no
# ARM_*/AZURE_CLIENT_* variables, and `az account show` must fail. Azure is reachable only through a service connection.
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
echo "AZURE_CONFIG_DIR=${AZURE_CONFIG_DIR:-<unset>}"
[[ -n "${AZURE_CONFIG_DIR:-}" && "$AZURE_CONFIG_DIR" != "$HOME/.azure" ]] || { echo "##vso[task.logissue type=error]AZURE_CONFIG_DIR is not the agent's own directory" >&3; exit 1; }
leaked="$(env | grep -E '^(ARM_|AZURE_CLIENT_|AZURE_TENANT_ID|AZURE_SUBSCRIPTION_ID|AZURE_FEDERATED_TOKEN)' | cut -d= -f1 | tr '\n' ' ' || true)"
[[ -z "$leaked" ]] || { echo "##vso[task.logissue type=error]Azure credential variables in the job environment: $leaked" >&3; exit 1; }
if az account show -o none 2>"$CI_OUT/az-account-show.txt"; then
  echo "##vso[task.logissue type=error]az is signed in inside the job" >&3; exit 1
fi
echo "az account show: $(head -1 "$CI_OUT/az-account-show.txt")"
echo "OK: no Azure login inherited by the job"
