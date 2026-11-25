#!/usr/bin/env python3
"""timeline.py - per-second timeline of a k6 run from its CSV output (K6_CSV_TIME_FORMAT=unix_milli, gzip).

  timeline.py K6.csv.gz OUTDIR WARMUP_S
Writes OUTDIR/timeline.csv (second since the start, phase, mix requests, failed, p50/p95/max ms, dropped iterations) and
OUTDIR/requests.csv.xz (one line per mix request: ms since the start, op, status, duration ms, expected 0/1; the raw data the
summaries come from, compact).
"""
import csv, gzip, lzma, sys
from collections import defaultdict


def pct(xs, p):
    if not xs:
        return ""
    xs = sorted(xs); k = (len(xs) - 1) * p / 100; f = int(k); c = min(f + 1, len(xs) - 1)
    return round(xs[f] + (xs[c] - xs[f]) * (k - f), 1)


def main():
    src, out, warm = sys.argv[1], sys.argv[2], float(sys.argv[3])
    rows = []; dropped = defaultdict(int); t0 = None
    with gzip.open(src, "rt", newline="") as f:
        r = csv.DictReader(f)
        for x in r:
            m = x["metric_name"]
            if m not in ("http_req_duration", "dropped_iterations", "iterations"):
                continue
            ts = int(x["timestamp"])
            t0 = ts if t0 is None else min(t0, ts)
            if m == "dropped_iterations":
                dropped[ts] += int(float(x["metric_value"])); continue
            if m == "iterations":
                continue
            tags = dict(kv.split("=", 1) for kv in (x.get("extra_tags") or "").split("&") if "=" in kv)
            if tags.get("mix") != "yes":
                continue
            rows.append((ts, tags.get("op", ""), x.get("status", ""), float(x["metric_value"]), 1 if x.get("expected_response") == "true" else 0))
    if t0 is None:
        print("no samples"); return
    sec = defaultdict(list)
    for ts, op, st, d, ok in rows:
        sec[(ts - t0) // 1000].append((d, ok))
    dsec = defaultdict(int)
    for ts, n in dropped.items():
        dsec[(ts - t0) // 1000] += n
    last = max(list(sec) + list(dsec) + [0])
    with open(f"{out}/timeline.csv", "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["second", "phase", "requests", "failed", "p50_ms", "p95_ms", "max_ms", "dropped_iterations"])
        for s in range(0, last + 1):
            v = sec.get(s, [])
            ds = [d for d, _ in v]
            w.writerow([s, "warmup" if s < warm else "measure", len(v), sum(1 for _, ok in v if not ok), pct(ds, 50), pct(ds, 95),
                        round(max(ds), 1) if ds else "", dsec.get(s, 0)])
    open(f"{out}/timeline-t0.txt", "w").write(f"{t0}\n")  # epoch ms of second 0 (aligns marks with the timeline)
    with lzma.open(f"{out}/requests.csv.xz", "wt", preset=9) as f:
        f.write("ms,op,status,duration_ms,expected\n")
        for ts, op, st, d, ok in sorted(rows):
            f.write(f"{ts - t0},{op},{st},{d:.1f},{ok}\n")
    print(f"{len(rows)} mix requests, {last + 1} s")


if __name__ == "__main__":
    main()
