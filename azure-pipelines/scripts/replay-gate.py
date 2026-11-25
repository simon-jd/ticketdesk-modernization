"""CI gate over ops/dev/replay-summary.py's summary.md: every UNEXPLAINED mismatch must be listed in the allowlist
(azure-pipelines/replay-allowed.txt: key, route id, difference kinds), else exit 1. Allowlisted rows that are no longer
unexplained are printed as stale (the list only shrinks: delete them). Usage: replay-gate.py SUMMARY.md ALLOWLIST"""
import re
import sys

summary, allow_file = sys.argv[1], sys.argv[2]
row = re.compile(r"^\| `([^`]+)` \| [^|]* \| ([^|]+) \| [^|]+ \| ([^|]+) \| \*\*UNEXPLAINED\*\* \|$")
found = set()
for line in open(summary, encoding="utf-8"):
    m = row.match(line.rstrip("\n"))
    if m:
        found.add((m.group(1), m.group(2).strip(), ",".join(k.strip() for k in m.group(3).split(","))))
allowed = set()
for line in open(allow_file, encoding="utf-8"):
    line = line.strip()
    if line and not line.startswith("#"):
        key, route, kinds = line.split()
        allowed.add((key, route, kinds))
new = sorted(found - allowed)
stale = sorted(allowed - found)
print(f"unexplained: {len(found)} (allowlisted {len(found & allowed)}, new {len(new)}); stale allowlist rows: {len(stale)}")
for k, r, kinds in new:
    print(f"##vso[task.logissue type=error]unexplained replay mismatch {k} {r} {kinds}")
for k, r, kinds in stale:
    print(f"##vso[task.logissue type=warning]allowlisted mismatch is gone, delete it from replay-allowed.txt: {k} {r} {kinds}")
sys.exit(1 if new else 0)
