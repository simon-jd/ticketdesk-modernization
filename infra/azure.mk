# Azure lab targets (included by the root Makefile). See infra/README.md and docs/dev/AZURE-BUDGET.md.
#
#   make azure-check                 terraform fmt -check + validate (no Azure calls besides init)
#   make azure-plan STAGE=lift       plan a stage (none | foundation | lift | replatform | aks | cutover)
#   make azure-up-foundation         apply a stage (ledger check first)    also: azure-up-lift/aks/cutover
#   make azure-down-foundation       step back to the previous stage        also: azure-down-lift/aks/cutover
#   make azure-down                  delete the whole resource group now (Entra app stays)
#   make azure-reap / azure-reap-dry delete / list expired-ttl resources in rg-ticketdesk-lab
#   make azure-status                what exists in the RG, with tags, and the ledger total
#   make azure-destroy-all           end of project: destroy everything incl. the Entra app
#   make azure-ttl TTL_HOURS=3.25    the ttl an apply would set now (no Azure call); TTL_HOURS may be fractional

SHELL := /bin/bash
TD_W ?= $(HOME)/Desktop/ticketdesk-modernization
AZ_RG := rg-ticketdesk-lab
AZ_TF_DIR := infra/terraform/envs/lab
AZ_TF_STATE := $(TD_W)/.tfstate/lab/terraform.tfstate
AZ_SECRETS_TFVARS := $(TD_W)/.secrets/azure-lab.tfvars
TTL_HOURS ?= 4
STAGE ?= foundation
# owner tag (agent-<name>) of whoever applies; empty = the variable's default.
AZ_OWNER ?=
# Lift stage inputs: the IIS certificate (lab CA, `make azure-lift-cert`) and two secrets read from
# $(TD_W)/.secrets inside the recipe shell (exported as TF_VAR_*, never echoed; scripts/tf_lift_env.sh).
AZ_LIFT_PFX := $(TD_W)/.secrets/legacy-lab-ca/azure-lift.pfx

# Stage to return to when a stage is taken down.
AZ_BASE_foundation := none
AZ_BASE_lift := foundation
AZ_BASE_aks := foundation
AZ_BASE_cutover := aks
AZ_BASE_replatform := foundation

# TTL_HOURS may be fractional (aks.sh's session: 3.25). `date -d` takes no "+3.25 hours", so the ttl is computed in whole
# minutes; anything but a non-negative number of hours fails the recipe (2026-11-12: "+3.25 hours" left ttl_utc empty
# and the session's stage none stopped on the variable's validation).
AZ_TTL_UTC = m=$$(awk -v h='$(TTL_HOURS)' 'BEGIN { if (h !~ /^[0-9]+([.][0-9]+)?$$/) exit 1; printf "%d", h * 60 + 0.5 }') && \
	date -u -d "+$$m minutes" +%Y-%m-%dT%H:%M:%SZ
# Every apply moves the group's ttl; scripts/azure_ttl_sync.sh then re-tags the children Azure created itself.
# umask 077: state and backups are written 0600 (directory 0700).
AZ_TF = source $(TD_W)/.tools/env.sh && umask 077 && cd $(AZ_TF_DIR) && \
	export ARM_SUBSCRIPTION_ID=$$(az account show --query id -o tsv) && \
	export TF_VAR_subscription_id=$$ARM_SUBSCRIPTION_ID TF_VAR_tenant_id=$$(az account show --query tenantId -o tsv) && \
	export TF_VAR_ttl_utc=$$($(AZ_TTL_UTC)) && \
	{ [ -n "$$TF_VAR_ttl_utc" ] || { echo "TTL_HOURS=$(TTL_HOURS): not a number of hours" >&2; false; }; } && \
	source $(CURDIR)/scripts/tf_lift_env.sh $(TD_W) $(AZ_LIFT_PFX) && \
	terraform
# AZ_EXTRA_ARGS: one-off overrides, e.g. AZ_EXTRA_ARGS="-var enable_sql=false"
AZ_EXTRA_ARGS ?=
AZ_VARFILES = $(if $(wildcard $(AZ_SECRETS_TFVARS)),-var-file=$(AZ_SECRETS_TFVARS)) -var-file=stages/$(1).tfvars \
	$(if $(AZ_OWNER),-var owner=$(AZ_OWNER)) $(AZ_EXTRA_ARGS)

.PHONY: azure-init azure-fmt azure-check azure-plan azure-ledger azure-reap azure-reap-dry azure-status \
	azure-down azure-destroy-all azure-prices azure-ttl

azure-ttl:
	@t=$$($(AZ_TTL_UTC)) && [ -n "$$t" ] && echo "TTL_HOURS=$(TTL_HOURS) -> ttl_utc $$t (now $$(date -u +%Y-%m-%dT%H:%M:%SZ))" || \
		{ echo "TTL_HOURS=$(TTL_HOURS): not a number of hours" >&2; exit 1; }

azure-init:
	@mkdir -p -m 700 $(dir $(AZ_TF_STATE))
	$(AZ_TF) init -input=false -backend-config=path=$(AZ_TF_STATE)

azure-fmt:
	source $(TD_W)/.tools/env.sh && terraform fmt -recursive infra/terraform

azure-check: azure-init
	source $(TD_W)/.tools/env.sh && terraform fmt -check -recursive infra/terraform
	$(AZ_TF) validate

azure-ledger:
	@python3 scripts/azure_ledger.py check

azure-plan: azure-init
	$(AZ_TF) plan -input=false $(call AZ_VARFILES,$(STAGE))

azure-up-%: azure-init azure-ledger
	@echo "apply stage $* start $$(date -u +%FT%TZ)"; s=$$(date +%s); \
	$(AZ_TF) apply -input=false -auto-approve $(call AZ_VARFILES,$*); rc=$$?; \
	chmod 600 $(dir $(AZ_TF_STATE))* 2>/dev/null; \
	$(CURDIR)/scripts/azure_ttl_sync.sh || true; \
	echo "apply stage $* end $$(date -u +%FT%TZ) elapsed_s=$$(( $$(date +%s) - s )) rc=$$rc"; exit $$rc

azure-down-%: azure-init
	@echo "stage $* down (to $(AZ_BASE_$*)) start $$(date -u +%FT%TZ)"; s=$$(date +%s); \
	$(AZ_TF) apply -input=false -auto-approve $(call AZ_VARFILES,$(AZ_BASE_$*)); rc=$$?; \
	chmod 600 $(dir $(AZ_TF_STATE))* 2>/dev/null; \
	$(CURDIR)/scripts/azure_ttl_sync.sh || true; \
	echo "stage $* down end $$(date -u +%FT%TZ) elapsed_s=$$(( $$(date +%s) - s )) rc=$$rc"; exit $$rc

# Emergency / end-of-session stop: delete the resource group directly, then let Terraform notice.
azure-down:
	@if [[ "$$(az group exists -n $(AZ_RG))" == "true" ]]; then \
		echo "deleting $(AZ_RG) $$(date -u +%FT%TZ)"; az group delete -n $(AZ_RG) --yes; \
	else echo "$(AZ_RG) does not exist"; fi
	@if [[ -f $(AZ_TF_STATE) ]]; then $(MAKE) --no-print-directory azure-init >/dev/null && \
		$(AZ_TF) apply -refresh-only -input=false -auto-approve $(call AZ_VARFILES,none) >/dev/null && \
		echo "terraform state refreshed (deleted resources dropped)"; fi

azure-destroy-all: azure-init
	$(AZ_TF) destroy -input=false $(call AZ_VARFILES,none)

azure-reap:
	scripts/azure_reaper.sh

azure-reap-dry:
	scripts/azure_reaper.sh --dry-run

azure-status:
	@az resource list -g $(AZ_RG) --query "[].{name:name, type:type, ttl:tags.ttl, usd_h:tags.est_usd_per_hour}" -o table 2>/dev/null \
		|| echo "$(AZ_RG) does not exist"
	@python3 scripts/azure_ledger.py total | sed 's/^/ledger running estimate USD: /'

azure-prices:
	python3 scripts/azure_prices.py docs/evidence/azure-prices-$$(date -u +%Y%m%d)
