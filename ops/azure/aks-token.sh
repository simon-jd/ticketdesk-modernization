#!/usr/bin/env bash
# aks-token.sh - kubectl exec credential for the lab AKS cluster (Entra ID + Azure RBAC, local accounts off), used by
# ops/azure/aks.sh kube instead of kubelogin (not on the lab's download list): an access token of the AKS Entra server
# application (6dae42f8-4368-4678-94ff-3960e28e3630) from the signed-in az CLI, as an ExecCredential on stdout for kubectl
# only (what kubelogin's azurecli mode does). Nothing is written to disk or logged.
set -euo pipefail
az account get-access-token --resource 6dae42f8-4368-4678-94ff-3960e28e3630 -o json | python3 -I -c '
import json, sys, datetime as d
t = json.load(sys.stdin)
exp = d.datetime.fromtimestamp(int(t["expires_on"]), d.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
print(json.dumps({"apiVersion": "client.authentication.k8s.io/v1", "kind": "ExecCredential",
                  "status": {"token": t["accessToken"], "expirationTimestamp": exp}}))'
