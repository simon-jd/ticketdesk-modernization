# Sourced by infra/azure.mk before every terraform call: exports the lift and AKS stages' inputs from
# $TD_W/.secrets as TF_VAR_* in the recipe shell only (never echoed, never on a command line).
#   source scripts/tf_lift_env.sh TD_W PFX_PATH
# Missing files leave the variables empty; the lift stage's preconditions then stop the plan.
_td_w="$1"
_pfx="$2"
_envval() { [[ -f "$1" ]] && sed -n "s/^$2=//p" "$1" | head -1; return 0; }
TF_VAR_app_admin_password="$(_envval "$_td_w/.secrets/legacy-lab.env" TD_ADMIN_PASSWORD)"
TF_VAR_lift_cert_pfx_password="$(_envval "$_td_w/.secrets/legacy-lab-cert.env" TD_CERT_PFX_PASSWORD)"
TF_VAR_lift_cert_pfx_path=""
[[ -f "$_pfx" ]] && TF_VAR_lift_cert_pfx_path="$_pfx"
# AKS stages: the app endpoint's certificate (ops/azure/aks.sh cert), same PFX password
TF_VAR_aks_cert_pfx_path=""
[[ -f "$_td_w/.secrets/legacy-lab-ca/aks-web.pfx" ]] && TF_VAR_aks_cert_pfx_path="$_td_w/.secrets/legacy-lab-ca/aks-web.pfx"
export TF_VAR_app_admin_password TF_VAR_lift_cert_pfx_password TF_VAR_lift_cert_pfx_path TF_VAR_aks_cert_pfx_path
unset -f _envval
unset _td_w _pfx
