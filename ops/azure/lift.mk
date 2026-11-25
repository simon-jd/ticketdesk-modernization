# Lift & shift targets (included by the root Makefile): the legacy app on the Azure VM. See docs/dev/azure-lift.md.
# Every target is one ops/azure/lift.sh command, so a pipeline can run the same steps.
#
#   make azure-lift-cert             IIS certificate for the VM's DNS name from the lab CA (once)
#   make azure-lift-up               apply the lift stage (foundation + VM + storage + secrets + certificate)
#   make azure-lift-stage            upload the ops/windows bundle + publish package (SEED=1: + scale-S .bak)
#   make azure-lift-provision        provision the VM through Run Command (STEPS=a+b for some steps, ARGS=Name=Value)
#   make azure-lift-smoke            legacy smoke test against https://<vm fqdn>/
#   make azure-lift-backup-drill     backup -> blob, lose DB + local backups, restore from blob, smoke
#   make azure-lift-cutover          local VM -> Azure VM with write freeze + probe (TD_LOCK_OWNER holds the VM lock)
#   make azure-lift-rebuild          from nothing: apply + stage + provision + smoke, timed (BACKUP=dir to restore)
#   make azure-lift-down             back to stage none (nothing billable; the Entra app stays)

AZ_LIFT := ops/azure/lift.sh
AZ_LIFT_OWNER := agent-azure-lift
STEPS ?=
ARGS ?=
SEED ?=
BACKUP ?=

.PHONY: azure-lift-cert azure-lift-up azure-lift-stage azure-lift-provision azure-lift-smoke \
	azure-lift-backup-drill azure-lift-cutover azure-lift-rebuild azure-lift-down

azure-lift-cert:
	$(AZ_LIFT) cert

azure-lift-up: azure-lift-cert
	$(MAKE) --no-print-directory azure-up-lift AZ_OWNER=$(AZ_LIFT_OWNER)

azure-lift-stage:
	$(AZ_LIFT) stage $(if $(SEED),--seed) $(if $(BACKUP),--backup $(BACKUP))

azure-lift-provision:
	$(AZ_LIFT) provision $(STEPS) $(ARGS)

azure-lift-smoke:
	$(AZ_LIFT) smoke

azure-lift-backup-drill:
	$(AZ_LIFT) backup-drill

azure-lift-cutover:
	$(AZ_LIFT) cutover

azure-lift-rebuild: azure-lift-cert azure-ledger
	$(AZ_LIFT) rebuild $(BACKUP)

azure-lift-down:
	$(MAKE) --no-print-directory azure-down-foundation AZ_OWNER=$(AZ_LIFT_OWNER)
