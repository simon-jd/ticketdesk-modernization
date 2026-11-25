#!/usr/bin/env python3
"""Fetch unit prices for every SKU the TicketDesk lab plan uses, from the public
Azure Retail Prices API (https://prices.azure.com/api/retail/prices, no auth).

Usage: scripts/azure_prices.py OUT_DIR [REGION]      (REGION default: swedencentral, the lab region)
Writes OUT_DIR/raw/<query>.json (every item the API returned, with the request URL)
and OUT_DIR/prices.tsv (the rows the ledger quotes). Only the standard library is used.
"""
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://prices.azure.com/api/retail/prices"
REGION = sys.argv[2] if len(sys.argv) > 2 else "swedencentral"

# name -> OData filter. Region-less services (DNS, Entra) are queried without armRegionName.
BASE = f"currencyCode eq 'USD' and priceType eq 'Consumption'"
# Standard_B2as_v2 is the lab size (Windows VM and AKS nodes); the others are fallbacks.
VM_SKUS = ["Standard_B2as_v2", "Standard_B2als_v2", "Standard_B2s_v2", "Standard_B4as_v2",
           "Standard_B2s", "Standard_B2ms", "Standard_B2ps_v2", "Standard_D2as_v5"]
QUERIES = {
    "vm": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Virtual Machines' and ("
          + " or ".join(f"armSkuName eq '{s}'" for s in VM_SKUS) + ")",
    "disk-standard-hdd": f"{BASE} and armRegionName eq '{REGION}' and productName eq 'Standard HDD Managed Disks' and (skuName eq 'S4 LRS' or skuName eq 'S6 LRS')",
    # Load Balancer and Private Link are priced under armRegionName 'Global'
    "load-balancer": f"{BASE} and armRegionName eq 'Global' and serviceName eq 'Load Balancer' and skuName eq 'Standard'",
    "public-ip": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Virtual Network' and productName eq 'IP Addresses'",
    "private-link": f"{BASE} and armRegionName eq 'Global' and productName eq 'Virtual Network Private Link' and skuName eq 'Standard'",
    "dns": f"{BASE} and serviceName eq 'Azure DNS'",
    "log-analytics": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Log Analytics'",
    "app-insights": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Application Insights'",
    "key-vault": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Key Vault'",
    "sql-serverless": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'SQL Database' and productName eq 'SQL Database General Purpose - Serverless - Compute Gen5'",
    "sql-storage": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'SQL Database' and productName eq 'SQL Database Single/Elastic Pool General Purpose - Storage'",
    "aks": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Azure Kubernetes Service'",
    "service-bus": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Service Bus'",
    "monitor-notifications": f"{BASE} and serviceName eq 'Azure Monitor' and productName eq 'Azure Monitor' and contains(meterName, 'Email')",
    "bandwidth": f"{BASE} and serviceName eq 'Bandwidth' and armRegionName eq '{REGION}'",
    # lift stage: artifacts/backups storage account (StorageV2, Standard LRS, Hot)
    "storage-blob-hot-lrs": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Storage' and productName eq 'General Block Blob v2' and skuName eq 'Hot LRS'",
    # AKS round: managed OS disks of AKS nodes are Premium SSD on sizes with premium storage (no SKU choice in AKS),
    # the registry (Basic, this round only), the restore-drill database (smallest single database), alert rules
    "disk-premium-ssd": f"{BASE} and armRegionName eq '{REGION}' and productName eq 'Premium SSD Managed Disks' and (skuName eq 'P4 LRS' or skuName eq 'P6 LRS')",
    "container-registry": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'Container Registry'",
    "sql-basic": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'SQL Database' and productName eq 'SQL Database Single Basic'",
    "sql-standard": f"{BASE} and armRegionName eq '{REGION}' and serviceName eq 'SQL Database' and productName eq 'SQL Database Single Standard' and skuName eq 'S0'",
    "monitor-alerts": f"{BASE} and serviceName eq 'Azure Monitor' and contains(meterName, 'Alert')",
}


def fetch(filt):
    url = API + "?" + urllib.parse.urlencode({"$filter": filt, "api-version": "2023-01-01-preview"})
    items, pages, first = [], 0, url
    while url:
        for attempt in range(5):
            try:
                with urllib.request.urlopen(url, timeout=60) as r:
                    doc = json.load(r)
                break
            except Exception:  # transient 429/5xx
                if attempt == 4:
                    raise
                time.sleep(2 ** attempt)
        items += doc.get("Items", [])
        url = doc.get("NextPageLink")
        pages += 1
    return first, pages, items


def main():
    out = Path(sys.argv[1])
    (out / "raw").mkdir(parents=True, exist_ok=True)
    rows = []
    for name, filt in QUERIES.items():
        url, pages, items = fetch(filt)
        (out / "raw" / f"{name}.json").write_text(json.dumps(
            {"fetched_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "url": url,
             "pages": pages, "count": len(items), "items": items}, indent=1))
        for it in items:
            if it.get("type") != "Consumption":
                continue
            rows.append([name, it.get("serviceName", ""), it.get("productName", ""), it.get("skuName", ""),
                         it.get("armSkuName", ""), it.get("meterName", ""), it.get("armRegionName", ""),
                         str(it.get("tierMinimumUnits", "")), str(it.get("retailPrice", "")),
                         it.get("unitOfMeasure", ""), it.get("effectiveStartDate", "")])
        print(f"{name}: {len(items)} items ({pages} pages)", file=sys.stderr)
    hdr = ["query", "service", "product", "sku", "armSku", "meter", "region", "tierMin", "usd", "unit", "effective"]
    (out / "prices.tsv").write_text("\t".join(hdr) + "\n" + "\n".join("\t".join(r) for r in rows) + "\n")


if __name__ == "__main__":
    main()
