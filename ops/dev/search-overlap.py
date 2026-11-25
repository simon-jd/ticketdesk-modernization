#!/usr/bin/env python3
"""Legacy (Lucene) vs new (SQL full-text) search results of a replay: the ticket ids of each search exchange.

    python3 -I ops/dev/search-overlap.py --baseline golden.jsonl[.xz] --candidate gateway.jsonl --out overlap.md

For every exchange on search / search/{term}: result count of each side, how many of the legacy top 20 are also in the
new top 20, and how many sit at the same rank. Expected difference F1 (docs/migration/expected-differences.md).
"""
import argparse
import json
import lzma
import re

PANEL = re.compile(r'<div id="ticketDetailPanel"[\s\S]*?href="/ticket/(\d+)"')


def load(path):
    opener = lzma.open if path.endswith(".xz") else open
    out = {}
    with opener(path, "rt", encoding="utf-8") as f:
        for line in f:
            if not line.strip():
                continue
            e = json.loads(line)
            p = e["request"]["path"]
            if p == "/search" or p.startswith("/search?") or p.startswith("/search/"):
                out[f"{e['journey']}#{e['stepIndex']:03d}.{e['hop']}"] = (p, [int(x) for x in PANEL.findall((e.get("response") or {}).get("body") or "")])
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--baseline", required=True)
    ap.add_argument("--candidate", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    base, cand = load(a.baseline), load(a.candidate)
    lines = ["# Search results: legacy (Lucene, golden) vs new app (SQL Server full-text)", "",
             "| Exchange | Request | Legacy results | New results | In both | Same rank | Legacy ids | New ids |", "|---|---|---:|---:|---:|---:|---|---|"]
    for key in sorted(base):
        path, old = base[key]
        new = cand.get(key, (path, []))[1]
        both = len(set(old) & set(new))
        same = sum(1 for i, x in enumerate(old) if i < len(new) and new[i] == x)
        lines.append(f"| `{key}` | `GET {path}` | {len(old)} | {len(new)} | {both} | {same} | {' '.join(map(str, old))} | {' '.join(map(str, new))} |")
    open(a.out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
