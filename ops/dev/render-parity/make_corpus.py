#!/usr/bin/env python3
"""Build the render-parity corpus: every ticket's Details and every event's Comment of the baseline data set (as the legacy
app stores them) plus the tricky inputs (each as Markdown and as HTML) and the golden journeys' texts.

    python3 -I make_corpus.py --tickets tickets.raw --events events.raw --golden legacy-golden.jsonl --out corpus.tsv

Output lines: id <TAB> isHtml (0/1) <TAB> base64(UTF-8 text) or '-' for null.
"""
import argparse, base64, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tricky_inputs import CASES

def load(path):
    s = open(path, encoding="utf-8").read()
    return json.loads(s[s.index("["):s.rindex("]") + 1])

def enc(t):
    return "-" if t is None else base64.b64encode(t.encode("utf-8")).decode("ascii")

ap = argparse.ArgumentParser()
ap.add_argument("--tickets"); ap.add_argument("--events"); ap.add_argument("--golden"); ap.add_argument("--out", required=True)
a = ap.parse_args()
rows = []
for i, c in enumerate(CASES):
    rows.append((f"x{i:03d}-md", 0, c)); rows.append((f"x{i:03d}-html", 1, c))
if a.golden:  # texts the journeys post (details and comments), rendered both ways
    seen = set()
    for line in open(a.golden, encoding="utf-8"):
        e = json.loads(line)
        for f in (e["request"].get("form") or []):
            if f["key"].lower() in ("details", "comment") and f["value"] not in seen:
                seen.add(f["value"]); n = len(seen)
                rows.append((f"g{n:03d}-md", 0, f["value"])); rows.append((f"g{n:03d}-html", 1, f["value"]))
for path in (a.tickets, a.events):
    if path:
        for r in load(path):
            rows.append((r["id"], 1 if r["h"] else 0, r.get("x")))
with open(a.out, "w", encoding="ascii", newline="\n") as w:
    for i, h, t in rows:
        w.write(f"{i}\t{h}\t{enc(t)}\n")
print(f"{len(rows)} cases -> {a.out}")
