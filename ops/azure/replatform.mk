# Azure SQL re-platform targets (included by the root Makefile): the legacy app on the Azure VM moves from SQL Express
# to Azure SQL (free offer, private endpoint, Entra-only, managed identity). See docs/sql/azure-sql-move.md.
# Every target is one ops/azure/replatform.sh command.
#
#   make azure-replatform-up         apply the replatform stage (foundation + VM + Azure SQL + attachments account)
#   make azure-replatform-stage      sql tools bundle + scripts + publish package + shared baseline .bak to blob
#   make azure-replatform-provision  Run Command steps (STEPS=a+b, ARGS="Name=Value ..."; default: SQL Express baseline)
#   make azure-replatform-cutover    SQL Express -> Azure SQL with a write freeze, probe, reconciliation (REHEARSAL=1)
#   make azure-replatform-rebuild    from nothing: apply + stage BACPAC + provision without SQL Express + smoke (BACPAC=file)
#   make azure-replatform-down       back to stage none (nothing billable; the Entra app stays)

AZ_RP := ops/azure/replatform.sh
AZ_RP_OWNER := agent-azure-sql
RP_BASE_STEPS := fetch+prereqs+sql+sqlupdate+site+sqltools+restore+deploy+verify
REHEARSAL ?=
BACPAC ?=

.PHONY: azure-replatform-up azure-replatform-stage azure-replatform-provision azure-replatform-cutover \
	azure-replatform-rebuild azure-replatform-down

azure-replatform-up: azure-lift-cert
	$(MAKE) --no-print-directory azure-up-replatform AZ_OWNER=$(AZ_RP_OWNER)

azure-replatform-stage:
	$(AZ_RP) tools && $(AZ_RP) stage --seed

azure-replatform-provision:
	$(AZ_RP) provision $(if $(STEPS),$(STEPS),$(RP_BASE_STEPS) RestorePrefix=seed RestoreName=baseline-s-shared Database=express) $(ARGS)

azure-replatform-cutover:
	$(AZ_RP) cutover $(if $(REHEARSAL),--rehearsal)

azure-replatform-rebuild: azure-lift-cert azure-ledger
	$(AZ_RP) rebuild $(BACPAC)

azure-replatform-down:
	$(MAKE) --no-print-directory azure-down-foundation AZ_OWNER=$(AZ_RP_OWNER)
