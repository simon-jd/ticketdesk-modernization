# Azure Service Bus Basic: cost note (no apply)

Azure Retail Prices API, 2026-11-10 (`retail-prices-servicebus-basic-swedencentral.json.txt`): Service Bus **Basic** in
swedencentral is consumption only, **$0.05 per million messaging operations** (meter "Basic Messaging Operations", no
hourly base charge). Basic has queues (no topics), the dead-letter queue, scheduled messages and a 256 KB message limit,
which is all the worker uses.

Estimate for the AKS round: one ticket write = 1 send + 1 receive/complete (+1 schedule + 1 receive per retry), so a full
golden replay (about 40 ticket writes) is under 200 operations and a day of lab use stays far below one million: **< $0.01**
in total. The namespace itself costs nothing while idle, so it can live as long as the AKS round; it still gets the ledger
row and ttl tag like every other resource. Not created in this round (no Terraform change).
