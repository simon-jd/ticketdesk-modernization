v1 without the VM (the v2-step outage of 2026-11-12), reproduced on kind and fixed in the chart; NOT run on AKS.
kind cluster td-aks, image aks-v1, Legacy:BaseUrl https://legacy-gone.lab/ with a host alias to 192.0.2.10, whose packets
the kind node drops (iptables FORWARD DROP): every connect to the "VM" times out, as on AKS after retire-vm.
  before-timeout5/  the chart at 5fffb1d (readiness timeoutSeconds 5)
  after-timeout8/   the chart with readiness timeoutSeconds 8
Command: KIND_EV=docs/evidence/aks-20261112/fixes/kind TD_KIND_TAG=aks-v1 ops/k8s/kind.sh up|db|images aks-v1|secrets, then
CHART_DIR=<chart at 5fffb1d> ops/k8s/kind.sh legacy-gone aks-v1 300 before-timeout5 and ops/k8s/kind.sh legacy-gone aks-v1
300 after-timeout8; kind.sh down afterwards. On kind the pod was never Ready with 5 s (the legacy was unreachable from its
start); on AKS it had been Ready before the VM was deleted and flapped, since some answers came in just under 5 s.
Render check after the change: ops/azure/aks-cd-check.sh template -> aks.sh and cd-aks.sh identical, 373 lines,
sha256 5f399df570d51e1e (azure-aks.md updated). Azure Repos modernize needs the chart synced before the next CD preflight.
