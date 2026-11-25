#!/usr/bin/env bash
# CI gate: terraform fmt -check over infra/terraform and validate of envs/lab without a backend (no state, no Azure call;
# providers from the agent's plugin cache TF_PLUGIN_CACHE_DIR, versions pinned by the committed .terraform.lock.hcl).
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
cd "$CI_REPO"
terraform version | head -1
ci_section "terraform fmt -check"
terraform fmt -check -recursive -diff infra/terraform
ci_section "terraform validate (envs/lab, -backend=false)"
terraform -chdir=infra/terraform/envs/lab init -backend=false -input=false -lockfile=readonly -no-color > "$CI_OUT/terraform-init.txt"
tail -3 "$CI_OUT/terraform-init.txt"
terraform -chdir=infra/terraform/envs/lab validate -no-color
