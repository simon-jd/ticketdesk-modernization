#!/usr/bin/env python3
"""aks_ledger_rows.py - the AKS session's rows in the spend ledger (docs/dev/azure-spend.md), before anything is created.

  aks_ledger_rows.py add PREFIX START_UTC PLANNED_END_UTC [--dry-run]
        append rows PREFIX.1.. (e.g. K1) before <!-- ledger:end -->; refuses if PREFIX rows exist; --dry-run prints them and
        the resulting total without writing
  aks_ledger_rows.py close PREFIX DELETE_UTC NOTE
        fill "Actual delete (UTC)" of the PREFIX rows that are still open and append NOTE to their notes

Estimates are the cent-rounded lines of ops/azure/aks_estimate.py (AKS 3 h, VM 1.5 h; same unit prices, same conventions).
"""
import re
import sys
from pathlib import Path

LEDGER = Path(__file__).resolve().parents[2] / "docs" / "dev" / "azure-spend.md"
SC = "swedencentral"
ROWS = [  # resource, SKU / meter, region, unit price, estimate, notes
    ("`aks-tdlab-swedencentral` node pool (VMSS, 1 node)", "Standard_B2as_v2 Linux PAYG", SC, "$0.0778/h", "0.23",
     "AKS round session, 3 h planned; AllocationFailed fallbacks B2als_v2 ($0.0389/h) / B2s_v2 ($0.0864/h)"),
    ("node OS disk (AKS-managed, 30 GB)", "P4 LRS Premium SSD", SC, "$5.8072/month", "0.02",
     "user exception 2026-11-10 (AGENT-RULES banned list): AKS has no HDD OS disk for these sizes"),
    ("AKS cluster, Free tier", "meter FreeTierInfrastructureCost Uptime SLA", SC, "$0.05/h", "0.15", "counted until shown not to apply"),
    ("AKS Standard load balancer (outbound + the app's service rule)", "Standard, first 5 rules", "Global", "$0.025/h", "0.08", ""),
    ("2 public IPs (AKS outbound, the app's service `tdlab-aks-36d2d4`)", "Standard IPv4 static", SC, "$0.005/h each", "0.03", ""),
    ("`vm-tdlab-legacy` (+ NIC, boot diagnostics; system-assigned + `id-tdlab-sqldeploy`)", "Standard_B2as_v2, Windows PAYG", SC,
     "$0.087/h", "0.14", "1.6 h planned: allocated first (before anything with a fixed charge, ~0.1 h earlier), retired before v2; AllocationFailed fallbacks B2als_v2 ($0.0481/h), B2s_v2 ($0.0956/h)"),
    ("`osdisk-tdlab-legacy` + `pip-tdlab-legacy`", "S4 LRS + Standard IPv4 static", SC, "$1.536/month + $0.005/h", "0.01", "1.6 h"),
    ("`pe-sql-tdlab` (+ NIC)", "Standard Private Endpoint", "Global", "$0.01/h + $0.01/GB", "0.03", ""),
    ("`privatelink.database.windows.net` (+ VNet link)", "Private DNS zone", "global", "$0.50 per zone-month", "0.50", "counted as a full month"),
    ("`log-tdlab-swedencentral` + `appi-tdlab-swedencentral`", "PerGB2018, daily cap 0.1 GB", SC, "$0 first 5 GB/month, then $2.99/GB", "0.30",
     "worst case: one full day at the cap beyond the free 5 GB"),
    ("`acrtdlab36d2d4`", "Container Registry Basic", SC, "$0.1666/day + $0.10/GB-month", "0.17", "this round only (brief); counted as a full day"),
    ("`kv-tdlab-36d2d4` (+ secrets, certificates `td-iis-legacy`, `td-aks-web` + rotated versions, key `td-dataprotection`)", "Key Vault Standard",
     SC, "$0.03 per 10K operations", "0.03", "one 10K block; imported certificates, no CA-issued renewals; software key"),
    ("`sttdlab36d2d4` + `sttdlabfiles36d2d4`", "StorageV2 Standard LRS Hot", SC, "$0.0184/GB-month; writes $0.05/10K", "0.02", ""),
    ("`sb-tdlab-36d2d4` (queue `ticket-notifications`)", "Service Bus Basic", SC, "$0.05 per million operations", "0.01", "local auth off"),
    ("`alert-tdlab-failed-requests` (+ action group `ag-tdlab-drill`, no receivers)", "metric alert, 1 metric monitored", "Global",
     "$0.10/month (first 10 metrics $0)", "0.10", "counted as a full month"),
    ("`sqldb-ticketdesk-pitr` (restore drill copy)", "SQL Database Single Basic", SC, "$0.161/day", "0.16",
     "deleted right after the drill; counted as a full day"),
    ("`sql-tdlab-36d2d4` + `sqldb-ticketdesk`, identities `id-tdlab-app` / `id-tdlab-sqldeploy` + federated credentials, RG, VNet, subnets, NSGs, action groups, role assignments",
     "GP_S_Gen5_2 free offer (AutoPause) + no meter", SC, "$0", "0.00", ""),
]


def table(text):
    m = re.search(r"(<!-- ledger:begin -->)(.*?)(<!-- ledger:end -->)", text, re.S)
    if not m:
        sys.exit("ledger markers not found")
    return m


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    text = LEDGER.read_text()
    m = table(text)
    body = m.group(2)
    if cmd == "add":
        prefix, start, end = sys.argv[2:5]
        if re.search(rf"^\| {re.escape(prefix)}\.\d+ \|", body, re.M):
            sys.exit(f"rows {prefix}.* exist already")
        new = "".join(f"| {prefix}.{i} | {r} | {sku} | {reg} | {price} | {start} | {end} | {est} | — | {note} |\n"
                      for i, (r, sku, reg, price, est, note) in enumerate(ROWS, 1))
        total_rows = sum(float(r[4]) for r in ROWS)
        if "--dry-run" in sys.argv:
            print(new, end="")
            print(f"session rows: {total_rows:.2f}")
            return
        LEDGER.write_text(text[:m.start(3)].rstrip("\n") + "\n" + new + text[m.start(3):])
        print(f"added {len(ROWS)} rows {prefix}.1-{prefix}.{len(ROWS)} ({total_rows:.2f} USD)")
    elif cmd == "close":
        prefix, when, note = sys.argv[2:5]
        out, n = [], 0
        for line in body.splitlines(keepends=True):
            if line.startswith(f"| {prefix}."):
                cells = line.rstrip("\n").split("|")   # ['', ' id ', ..., ' notes ', '']
                if cells[9].strip() == "—":
                    cells[9] = f" {when} "
                    cells[10] = f" {(cells[10].strip() + '; ' if cells[10].strip() else '')}{note} "
                    n += 1
                line = "|".join(cells) + "\n"
            out.append(line)
        LEDGER.write_text(text[:m.start(2)] + "".join(out) + text[m.end(2):])
        print(f"closed {n} rows {prefix}.*")
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
