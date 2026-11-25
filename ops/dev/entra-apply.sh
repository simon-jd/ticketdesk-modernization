#!/usr/bin/env bash
# entra-apply.sh - targeted apply of the free Entra module only (app registration ticketdesk-lab-web: redirect URIs, app
# roles, the lab client secret), on the shared lab state, then the values the new app needs -> $TD_W/.secrets/entra-web.env
# (0600; the secret is never printed). Other agents own the rest of the state: this touches module.entra only.
#
#   ops/dev/entra-apply.sh plan | apply | secrets
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
REPO="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
. "$TD_W/.tools/env.sh"
umask 077
cd "$REPO/infra/terraform/envs/lab"
export ARM_SUBSCRIPTION_ID; ARM_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
export TF_VAR_subscription_id="$ARM_SUBSCRIPTION_ID" TF_VAR_tenant_id; TF_VAR_tenant_id="$(az account show --query tenantId -o tsv)"
export TF_VAR_ttl_utc; TF_VAR_ttl_utc="$(date -u -d '+4 hours' +%Y-%m-%dT%H:%M:%SZ)"
STATE="$TD_W/.tfstate/lab/terraform.tfstate"
VARS=(-var-file="$TD_W/.secrets/azure-lab.tfvars" -var-file=stages/none.tfvars -var owner=agent-identity)
terraform init -input=false -backend-config=path="$STATE" >/dev/null
case "${1:-plan}" in
  plan)  terraform plan -input=false -lock-timeout=120s -target=module.entra "${VARS[@]}" ;;
  apply) terraform apply -input=false -auto-approve -lock-timeout=300s -target=module.entra "${VARS[@]}" ;;
  secrets) ;;
  *) sed -n '2,7p' "$0"; exit 2 ;;
esac
if [[ "${1:-plan}" != plan ]]; then
  out="$TD_W/.secrets/entra-web.env"
  {
    echo "# Entra ID app ticketdesk-lab-web (written by ops/dev/entra-apply.sh $(date -u +%FT%TZ))"
    echo "TD_ENTRA_TENANT_ID=$TF_VAR_tenant_id"
    echo "TD_ENTRA_AUTHORITY=https://login.microsoftonline.com/$TF_VAR_tenant_id/v2.0"
    echo "TD_ENTRA_CLIENT_ID=$(terraform output -raw entra_client_id)"
    echo "TD_ENTRA_CLIENT_SECRET=$(terraform output -raw entra_client_secret)"
    echo "TD_ENTRA_REQUIRE_HTTPS=true"
  } > "$out"
  chmod 600 "$out"
  echo "wrote $out (client id $(sed -n 's/^TD_ENTRA_CLIENT_ID=//p' "$out"); secret not shown)"
fi
