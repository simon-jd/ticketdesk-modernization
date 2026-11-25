# AKS round targets (included by the root Makefile): the new app on AKS, cutover from the Azure VM, drills, one unattended
# session that destroys everything at its end. See docs/dev/azure-aks.md. Every target is one ops/azure/aks.sh command.
#
#   make azure-aks-dry-run           what the session would do, with the local checks (no Azure resource is created)
#   make azure-aks-images TAG=...    build ticketdesk-{web,worker,db}:TAG (v1 from ws/aks, v2 from modernize after retire)
#   make azure-aks-session           the unattended session in tmux td-aks-session (ledger rows first; trap -> destroy)
#   make azure-aks-destroy           stage none, or delete the resource group (any time)
#   make kind-aks-up / kind-aks-down the chart on a local kind cluster against td-sql (ops/k8s/kind.sh)

AZ_AKS := ops/azure/aks.sh
TAG ?= aks-dev

.PHONY: azure-aks-dry-run azure-aks-images azure-aks-session azure-aks-destroy kind-aks-up kind-aks-down

azure-aks-dry-run:
	$(AZ_AKS) session --dry-run

azure-aks-images:
	$(AZ_AKS) images $(TAG)

# the user's cap for the rerun (plan/DECISIONS.md "2026-11-11, aks session failed; rerun with cap $15.00"), also for the
# azure-ledger prerequisite; the session's ttl is 3.25 h and its deadline start + 165 min (coordinator 2026-11-11, aks.sh)
azure-aks-session: export TD_LEDGER_CAP := 15.00
azure-aks-session: azure-ledger
	tmux new-session -d -s td-aks-session "TTL_HOURS=3.25 $(CURDIR)/$(AZ_AKS) session > $(TD_W)/logs/aks/session-$$(date -u +%Y%m%dT%H%M%SZ).txt 2>&1"
	@echo "session started in tmux td-aks-session (log in $(TD_W)/logs/aks/)"

azure-aks-destroy:
	$(AZ_AKS) destroy

kind-aks-up:
	ops/k8s/kind.sh up && ops/k8s/kind.sh db && ops/k8s/kind.sh images $(TAG) && ops/k8s/kind.sh secrets && ops/k8s/kind.sh deploy $(TAG)

kind-aks-down:
	ops/k8s/kind.sh down
