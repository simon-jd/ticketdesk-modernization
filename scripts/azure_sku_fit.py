#!/usr/bin/env python3
"""Which VM sizes can this subscription actually deploy in a region?

  scripts/azure_sku_fit.py [REGION] [MAX_VCPU]      (defaults: swedencentral 4)

Joins `az vm list-skus --all` (restrictions per size) with `az vm list-usage` (vCPU quota per
family) and prints, for every size with at most MAX_VCPU vCPUs, whether it is deployable:
not restricted for the subscription AND its family quota is > 0. Run it before creating compute.
"""
import json
import subprocess
import sys

region = sys.argv[1] if len(sys.argv) > 1 else "swedencentral"
max_vcpu = float(sys.argv[2]) if len(sys.argv) > 2 else 4


def az(*args):
    return json.loads(subprocess.run(["az", *args, "-o", "json"], check=True, capture_output=True, text=True).stdout)


skus = az("vm", "list-skus", "-l", region, "--resource-type", "virtualMachines", "--all")
usage = {u["name"]["value"].lower(): u for u in az("vm", "list-usage", "-l", region)}
total = usage.get("cores", {})
print(f"region {region}: total regional vCPUs {total.get('currentValue')}/{total.get('limit')}")

rows = []
for s in skus:
    caps = {c["name"]: c["value"] for c in s.get("capabilities", [])}
    vcpu = float(caps.get("vCPUs", 999))
    if vcpu > max_vcpu:
        continue
    fam = s.get("family", "")
    q = usage.get(fam.lower())
    limit = int(q["limit"]) if q else 0
    restr = ",".join(r.get("reasonCode", "") for r in s.get("restrictions", []))
    ok = not restr and limit > 0
    rows.append((not ok, fam, s["name"], caps.get("vCPUs"), caps.get("MemoryGB"),
                 caps.get("CpuArchitectureType"), limit, restr or "-"))

print(f"{'deployable':10} {'family':28} {'size':24} {'vCPU':>4} {'GiB':>5} {'arch':6} {'quota':>5}  restriction")
for bad, fam, name, v, mem, arch, limit, restr in sorted(rows):
    if bad and limit == 0 and restr == "-":
        restr = "family quota 0"
    print(f"{'YES' if not bad else 'no':10} {fam:28} {name:24} {v:>4} {mem:>5} {arch or '?':6} {limit:>5}  {restr}")
