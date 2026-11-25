#!/usr/bin/env python3
"""aks_estimate.py PRICES_TSV [--aks-hours H] [--vm-hours H] [--ttl-hours T] - cost estimate of the AKS round's session (ledger style).

Unit prices come from the Retail Prices API rows that scripts/azure_prices.py saved (PRICES_TSV); every line below names the
row it uses. Same conventions as docs/dev/azure-spend.md: hourly items x planned hours, monthly-billed items as a whole month
per creation (private DNS zone, metric alert rule), daily-billed items as a whole day (ACR Basic, the restore drill's Basic
database), Log Analytics as one full day at the 0.1 GB cap beyond the free 5 GB. Prints the rows, the session total, the
project total against the cap, the same total under the alternatives the coordinator can choose from, and the worst case
if the session's trap never runs (the reaper deletes the group at its ttl = session start + T).
"""
import argparse
import csv

LEDGER_NOW = 12.98   # after the failed session of 2026-11-11 (K1 actuals 1.25)
CAP = 15.00   # the user's cap for the rerun (plan/DECISIONS.md "2026-11-11, aks session failed; rerun with cap $15.00")


def price(rows, query, **match):
    for r in rows:
        if r["query"] == query and all(r[k] == v for k, v in match.items()):
            return float(r["usd"]), r["unit"], f'{r["product"]} / {r["meter"]}'
    raise SystemExit(f"no price row for {query} {match}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prices")
    ap.add_argument("--aks-hours", type=float, default=3.0)
    ap.add_argument("--vm-hours", type=float, default=1.6)   # VM first: ~0.1 h longer than in the first plan
    ap.add_argument("--ttl-hours", type=float, default=3.25)   # aks.sh's TTL_HOURS (coordinator 2026-11-11; was 4)
    a = ap.parse_args()
    rows = list(csv.DictReader(open(a.prices), delimiter="\t"))
    H, V = a.aks_hours, a.vm_hours
    node, _, n_src = price(rows, "vm", armSku="Standard_B2as_v2", product="Virtual Machines Basv2 Series", meter="B2as v2")
    win, _, w_src = price(rows, "vm", armSku="Standard_B2as_v2", product="Virtual Machines Basv2 Series Windows", meter="B2as v2")
    p4, _, p4_src = price(rows, "disk-premium-ssd", sku="P4 LRS", meter="P4 LRS Disk")
    s4, _, s4_src = price(rows, "disk-standard-hdd", sku="S4 LRS", meter="S4 LRS Disk")
    free_tier, _, ft_src = price(rows, "aks", meter="FreeTierInfrastructureCost Uptime SLA")
    lb, _, lb_src = price(rows, "load-balancer", meter="Standard Included LB Rules and Outbound Rules")
    pip, _, pip_src = price(rows, "public-ip", meter="Standard IPv4 Static Public IP")
    acr, _, acr_src = price(rows, "container-registry", meter="Basic Registry Unit")
    sqlb, _, sqlb_src = price(rows, "sql-basic", meter="B DTU")
    alert, _, al_src = price(rows, "monitor-alerts", meter="Alerts Metric Monitored", region="Global", tierMin="10.0")
    pe = 0.01   # Private Link Standard endpoint per hour (azure-spend.md, Global meter)
    items = [
        ("AKS node Standard_B2as_v2 (Linux)", f"{node}/h x {H} h", node * H, n_src),
        ("AKS node OS disk P4 Premium SSD 30 GB (AKS gives premium-capable sizes Premium SSD)", f"{p4}/month / 730 x {H} h", p4 / 730 * H, p4_src),
        ("AKS FreeTierInfrastructureCost meter (counted until shown not to apply)", f"{free_tier}/h x {H} h", free_tier * H, ft_src),
        ("Standard load balancer (AKS: outbound + the app's service rule)", f"{lb}/h x {H} h", lb * H, lb_src),
        ("2 Standard public IPs (AKS outbound, app service)", f"2 x {pip}/h x {H} h", 2 * pip * H, pip_src),
        ("Windows VM Standard_B2as_v2 (until retired)", f"{win}/h x {V} h", win * V, w_src),
        ("VM OS disk S4 + VM public IP", f"{s4}/month / 730 x {V} h + {pip}/h x {V} h", s4 / 730 * V + pip * V, s4_src),
        ("SQL private endpoint", f"{pe}/h x {H} h", pe * H, "azure-spend.md Private Link Standard"),
        ("privatelink.database.windows.net private DNS zone (whole month per creation)", "0.50/month", 0.50, "azure-spend.md"),
        ("Log Analytics + App Insights (one day at the 0.1 GB cap beyond the free 5 GB)", "0.1 GB x 2.99", 0.30, "azure-spend.md"),
        ("Container Registry Basic (whole day)", f"{acr}/day", acr, acr_src),
        ("Key Vault operations (one 10K block)", "0.03/10K", 0.03, "azure-spend.md"),
        ("2 storage accounts (ops, attachments)", "under 1 GB, under 10K ops", 0.02, "azure-spend.md"),
        ("Service Bus Basic operations", "0.05 per million, < 100K ops", 0.01, "service-bus Basic Messaging Operations"),
        ("metric alert rule requests/failed (whole month; the first 10 monitored metrics are $0, counted anyway)", f"{alert}/month", alert, al_src),
        ("restore drill: Basic single database (whole day)", f"{sqlb}/day", sqlb, sqlb_src),
        ("Azure SQL free-offer database, identities, VNet, NSGs, action group", "no meter", 0.0, ""),
    ]
    total = sum(i[2] for i in items)
    print(f"# AKS round session estimate (AKS {H} h, VM {V} h), unit prices from {a.prices}")
    for name, how, usd, src in items:
        print(f"{usd:7.3f}  {name}  [{how}]  ({src})")
    print(f"{total:7.3f}  session total")
    print(f"project: {LEDGER_NOW:.2f} + {total:.2f} = {LEDGER_NOW + total:.2f} vs cap {CAP:.2f}: "
          f"{'fits, margin %.2f' % (CAP - LEDGER_NOW - total) if LEDGER_NOW + total <= CAP else 'OVER by %.2f' % (LEDGER_NOW + total - CAP)}")
    print("alternatives (coordinator's choice; nothing changed yet):")
    no_pe = total - pe * H - 0.50
    print(f"  A  SQL by VNet service endpoint + firewall VNet rules instead of private endpoint + DNS zone: {no_pe:.2f} -> {LEDGER_NOW + no_pe:.2f}")
    prorated = total - 0.50 + 0.50 / 730 * H - 0.30 - alert + alert / 730 * H - sqlb + sqlb / 24 * 1.0
    print(f"  B  ledger at exposure (DNS zone, alert prorated by hour; LA inside the free 5 GB; Basic DB 1 h): {prorated:.2f} -> {LEDGER_NOW + prorated:.2f}")
    small = total - (node - 0.0389) * H
    print(f"  C  node Standard_B2als_v2 (2 vCPU / 4 GiB, $0.0389/h) instead of B2as_v2: {small:.2f} -> {LEDGER_NOW + small:.2f}")
    print(f"  D  A + C: {no_pe - (node - 0.0389) * H:.2f} -> {LEDGER_NOW + no_pe - (node - 0.0389) * H:.2f}")
    print(f"  E  raise the automatic cap (user: total budget $15): {total:.2f} -> {LEDGER_NOW + total:.2f} of 15.00")
    aks_rate = node + p4 / 730 + free_tier + lb + 2 * pip + pe   # what runs on with the cluster, per hour
    vm_rate = win + s4 / 730 + pip                                 # the VM, its disk and IP, per hour
    worst_at = lambda t: total + aks_rate * (t - H) + vm_rate * (t - V)
    T = a.ttl_hours
    worst = worst_at(T)
    print(f"worst case (trap dead: the reaper deletes the group at ttl = start + {T} h, VM never retired): {worst:.2f} -> {LEDGER_NOW + worst:.2f}")
    # the reaper runs every 15 min, and deleting the group took ~11 min on 2026-11-11 (destroy-20261111T180623Z.txt: 666 s)
    lag = (15 + 11) / 60
    print(f"  + the reaper's lag (next run within 15 min, ~11 min to delete): {worst + (aks_rate + vm_rate) * lag:.2f} -> "
          f"{LEDGER_NOW + worst + (aks_rate + vm_rate) * lag:.2f}")
    print(f"  (with the earlier 4 h ttl: {worst_at(4.0):.2f} -> {LEDGER_NOW + worst_at(4.0):.2f}; per hour past the plan: AKS {aks_rate:.4f}, VM {vm_rate:.4f})")


if __name__ == "__main__":
    main()
