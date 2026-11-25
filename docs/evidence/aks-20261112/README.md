# Evidence: AKS round, Phase B rerun (2026-11-12, session-20261112T062534Z)

| Path | What | Command |
|---|---|---|
| `session-20261112T062534Z/` | session log, steps.txt (19 ok, restore-drill failed), deadline.txt, marks, summary.txt | `make azure-aks-session` (`ops/azure/aks.sh session`) |
| `apply-*.txt` | the VM-first apply, the rest of stage cutover, stage aks (retire-vm); the alert e-mail redacted | `aks.sh apply` (inside the session) |
| `provision-*`, `smoke-vm-azure-sql.*`, `smoke-after-cert-rotation.*` | VM data, legacy smoke, the VM certificate rotation's provision | `aks.sh vm-data`, `rotate-vm-cert` |
| `deploy-v1-*/`, `deploy-v2-*/` | helm values (CIDR redacted), the v2 step: CD run 34, approval, probe | `aks.sh deploy v1`, `deploy-v2-cd` |
| `aks-smoke-v1.*`, `aks-smoke-v2.*`, `golden-v1/`, `golden-v2/` | app smoke and golden replay through AKS | `aks.sh smoke`, `golden` |
| `cutover-aks-*/`, `cert-rotation/`, `aks-cert-rotation/` | cutover probe, VM and AKS certificate rotations | `aks.sh cutover`, `rotate-vm-cert`, `rotate-aks-cert` |
| `retire-vm/`, `alert-drill/`, `slo/`, `restore-drill/` | VM deletion, alert drill, SLO KQL, restore drill (restore timed, app switch failed) | `aks.sh retire-vm`, `alert-drill`, `slo`, `restore-drill` |
| `intervention-lb-probe/` | the LB health probe fix made by hand during the session, its diagnosis and the Terraform patch | `az network nsg rule create`, `az monitor metrics list`, Run Command |
| `destroy-*.txt`, `teardown.txt` | the trap's destroy, then state to stage none, vault purge, end state | `aks.sh destroy`, `make azure-down / azure-down-foundation`, `az keyvault purge` |
