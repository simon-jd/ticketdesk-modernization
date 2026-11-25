#!/usr/bin/env python3
"""Back-to-back pairs of a perf-attribution.sh run: run i of variant A next to run i of variant B (interleaved A B A B ...),
with p50/p95/p99, the per-operation p95 and the medians over the pairs.

    python3 -I ops/dev/perf-pairs.py DIR A_DIR B_DIR [--out DIR/pairs.md]

A_DIR / B_DIR are the variant folders inside DIR (e.g. baseline-s-legacy, baseline-s-search+new-alone); each holds
<target>-runN/summary.json (k6's summary of the measure phase, ops/dev/perf-baseline.sh).
"""
import glob
import json
import os
import statistics
import sys

OPS = ("list", "view", "filter", "comment", "create")


def runs(folder):
    out = {}
    for f in glob.glob(os.path.join(folder, "*-run*", "summary.json")):
        n = int(os.path.basename(os.path.dirname(f)).rsplit("-run", 1)[1])
        s = json.load(open(f))
        d = s["all"]["duration_ms"]
        out[n] = {"p50": d["med"], "p95": d["p(95)"], "p99": d["p(99)"], "requests": d["count"], "errors": s["all"]["failed"]["rate"],
                  "ops": {op: (s["by_op"][op]["duration_ms"] or {}).get("p(95)") for op in OPS}}
    return out


def main():
    args = sys.argv[1:]
    out_path = None
    if "--out" in args:
        i = args.index("--out")
        out_path = args[i + 1]
        del args[i:i + 2]
    base, a_name, b_name = args
    a, b = runs(os.path.join(base, a_name)), runs(os.path.join(base, b_name))
    pairs = sorted(set(a) & set(b))
    lines = [f"# Back-to-back pairs: {a_name} vs {b_name}", "",
             f"{len(pairs)} pairs; in each pair the {a_name} run came first, then the {b_name} run (each from a fresh VM reset).", "",
             f"| Pair | {a_name} p50 / p95 / p99 ms | {b_name} p50 / p95 / p99 ms | p95 ratio (B / A) | requests A / B | errors A / B |",
             "|---|---|---|---:|---|---|"]
    ratios = []
    for n in pairs:
        x, y = a[n], b[n]
        r = y["p95"] / x["p95"]
        ratios.append(r)
        lines.append(f"| {n} | {x['p50']:.1f} / {x['p95']:.1f} / {x['p99']:.1f} | {y['p50']:.1f} / {y['p95']:.1f} / {y['p99']:.1f} | {r:.2f} | "
                     f"{x['requests']} / {y['requests']} | {x['errors'] * 100:.2f} % / {y['errors'] * 100:.2f} % |")

    def med(d, k):
        return statistics.median(d[n][k] for n in pairs)

    lines += ["", "| Median over the pairs | p50 ms | p95 ms | p99 ms | p95 by op (list / view / filter / comment / create) |", "|---|---:|---:|---:|---|"]
    for name, d in ((a_name, a), (b_name, b)):
        ops = " / ".join(f"{statistics.median(d[n]['ops'][op] for n in pairs):.1f}" for op in OPS)
        lines.append(f"| {name} | {med(d, 'p50'):.1f} | {med(d, 'p95'):.1f} | {med(d, 'p99'):.1f} | {ops} |")
    lines += ["", f"p95 ratio B / A per pair: median {statistics.median(ratios):.2f}, range {min(ratios):.2f}-{max(ratios):.2f}; "
                  f"{b_name} faster at p95 in {sum(r < 1 for r in ratios)} of {len(ratios)} pairs."]
    text = "\n".join(lines) + "\n"
    if out_path:
        open(out_path, "w").write(text)
    print(text)


if __name__ == "__main__":
    main()
