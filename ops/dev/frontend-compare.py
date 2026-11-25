#!/usr/bin/env python3
"""Before/after table of the frontend-baseline.mjs summaries (Lighthouse scores, axe violations by impact).

    python3 -I ops/dev/frontend-compare.py --baseline R1.json --legacy NOW.json --server RAZOR.json --angular NG.json --out comparison.md
"""
import argparse
import json

IMPACTS = ("critical", "serious", "moderate", "minor")


def axe(p):
    by = p.get("axe_by_impact", {})
    rules = sum(v["rules"] for v in by.values())
    nodes = sum(v["nodes"] for v in by.values())
    detail = ", ".join(f"{i} {by[i]['rules']}/{by[i]['nodes']}" for i in IMPACTS if i in by) or "none"
    return f"{rules} / {nodes} ({detail})"


def main():
    ap = argparse.ArgumentParser()
    for k in ("baseline", "legacy", "server", "angular", "out"):
        ap.add_argument("--" + k, required=True)
    a = ap.parse_args()
    runs = [("Round 1 legacy (2026-11-08)", json.load(open(a.baseline))), ("Legacy now", json.load(open(a.legacy))),
            ("Server pages (Razor)", json.load(open(a.server))), ("Angular", json.load(open(a.angular)))]
    out = ["# Front end before/after", "",
           f"Lighthouse {runs[-1][1]['lighthouse']} (desktop preset, median of {runs[-1][1]['runs']} runs by performance), axe-core "
           f"{runs[-1][1]['axe']} (default rules), {runs[-1][1]['chrome']}. Axe: rules / nodes (by impact: rules/nodes).", ""]
    totals = {name: [0, 0] for name, _ in runs}
    for page in ("ticket-center", "ticket-detail", "new-ticket", "sign-in", "admin-users"):
        out += [f"## {page} (`{runs[-1][1]['pages'][page]['url']}`)", "", "| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |",
                "|---|---:|---:|---:|---|---:|---:|---|"]
        for name, d in runs:
            p = d["pages"][page]
            out.append(f"| {name} | {p['performance']:.0f} | {p['accessibility']:.0f} | {p['best_practices']:.0f} | {axe(p)} | {p['lcp_ms']:.0f} | "
                       f"{p['transfer_kb']} | {', '.join(p['lighthouse_failed_a11y_audits']) or '-'} |")
            if page in ("ticket-center", "ticket-detail", "new-ticket"):
                by = p.get("axe_by_impact", {})
                totals[name][0] += sum(v["rules"] for v in by.values())
                totals[name][1] += sum(v["nodes"] for v in by.values())
        out.append("")
    out += ["## The three replaced pages together", "", "| Run | Axe rule violations | Axe nodes |", "|---|---:|---:|"]
    out += [f"| {name} | {r} | {n} |" for name, (r, n) in totals.items()]
    open(a.out, "w", encoding="utf-8").write("\n".join(out) + "\n")
    print("\n".join(out[-6:]))


if __name__ == "__main__":
    main()
