# TicketDesk modernization. Each area keeps its targets in its own include file.
.DEFAULT_GOAL := help

include infra/azure.mk
include ops/azure/lift.mk
include ops/azure/replatform.mk
include ops/azure/aks.mk

.PHONY: help
help:
	@grep -hE '^#   make ' $(MAKEFILE_LIST) | sed 's/^#   //'
