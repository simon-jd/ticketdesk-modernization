#!/usr/bin/env python3
"""Sum the Azure spend ledger (docs/dev/azure-spend.md) and enforce the project cap.

  scripts/azure_ledger.py total          print the running estimate
  scripts/azure_ledger.py check          exit 1 if the running estimate exceeds the cap ($13.50; TD_LEDGER_CAP overrides it for
                                         a session the user approved a higher cap for: $13.70 for the Round 4 aks session,
                                         plan/DECISIONS.md "aks Phase A review"; never above the $15 budget)

The ledger rows sit between <!-- ledger:begin --> and <!-- ledger:end -->; the column named
"Estimate USD" is summed. The consumption API returns no cost on this subscription, so this
estimate is the control (AGENT-RULES.md, Azure section).
"""
import os
import re
import sys
from pathlib import Path

LEDGER = Path(__file__).resolve().parent.parent / "docs" / "dev" / "azure-spend.md"
BUDGET = 15.00
CAP = float(os.environ.get("TD_LEDGER_CAP") or 13.50)
if not 0 < CAP <= BUDGET:
    sys.exit(f"TD_LEDGER_CAP {CAP} is outside (0, {BUDGET}]")


def rows(text):
    m = re.search(r"<!-- ledger:begin -->(.*?)<!-- ledger:end -->", text, re.S)
    if not m:
        sys.exit(f"{LEDGER}: ledger markers not found")
    lines = [l.strip() for l in m.group(1).strip().splitlines() if l.strip().startswith("|")]
    header = [c.strip() for c in lines[0].strip("|").split("|")]
    col = header.index("Estimate USD")
    for l in lines[2:]:  # skip header and |---| separator
        cells = [c.strip() for c in l.strip("|").split("|")]
        yield cells, float(cells[col].replace("$", "") or 0)


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "total"
    total = sum(v for _, v in rows(LEDGER.read_text()))
    if cmd == "total":
        print(f"{total:.2f}")
    elif cmd == "check":
        ok = total <= CAP
        print(f"ledger running estimate ${total:.2f} / cap ${CAP:.2f}: {'OK' if ok else 'OVER CAP - stop and report'}")
        sys.exit(0 if ok else 1)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
