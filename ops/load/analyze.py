#!/usr/bin/env python3
"""analyze.py - capacity steps -> steps.csv + curves.md (Round 6 load).

  analyze.py DIR [--warmup 30] [--measure 120] [--p95 500] [--errors 0.5]
DIR holds <system>-r<repeat>/rate-<R>/ step directories (capacity.sh curve). A step passes when p95 <= --p95 ms, failed
requests <= --errors % and dropped iterations <= --errors % of the scheduled ones, all over the measured window.
CPU figures are in cores (100 % of one logical CPU = 1.0) and averaged over the measured window.
"""
import argparse, csv, glob, json, lzma, os, re, statistics as st, datetime as dt


def opentext(path):  # FILE or FILE.xz (the evidence keeps the larger samples compressed)
    return open(path) if os.path.exists(path) else lzma.open(path + ".xz", "rt")


def iso(s):
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00"))


def waits(path):
    d = {}
    try:
        for line in open(path):
            p = line.strip().split("|")
            if len(p) == 4 and p[1].isdigit():
                d[p[0]] = (int(p[1]), int(p[2]), int(p[3]))
    except FileNotFoundError:
        pass
    return d


def mean(xs):
    xs = [x for x in xs if x is not None]
    return round(sum(xs) / len(xs), 2) if xs else None


def mx(xs):
    xs = [x for x in xs if x is not None]
    return max(xs) if xs else None


def step(d, warm, meas):
    s = json.load(open(f"{d}/summary.json"))
    a = s["all"]; dur = a["duration_ms"] or {}; fl = a["failed"] or {}
    it = (s.get("iterations") or {}).get("count", 0); dr = (s.get("dropped_iterations") or {}).get("count", 0)
    row = {"rate": s["rate_per_s"], "requests": dur.get("count", 0), "req_per_s": round(dur.get("count", 0) / meas, 1),
           "p50_ms": round(dur.get("med", 0), 1), "p95_ms": round(dur.get("p(95)", 0), 1), "p99_ms": round(dur.get("p(99)", 0), 1),
           "failed_pct": round(100 * fl.get("rate", 0), 3), "dropped_pct": round(100 * dr / max(1, it + dr), 3),
           "dropped_all": dr, "checks_pct": round(100 * ((s.get("checks") or {}).get("rate") or 0), 2),
           "vus_peak": (s.get("vus") or {}).get("max")}
    # dropped iterations of the measured window only (the summary's count includes the warm-up): from the timeline
    if os.path.exists(f"{d}/timeline.csv"):
        tl = list(csv.DictReader(open(f"{d}/timeline.csv")))
        dm = sum(int(x["dropped_iterations"] or 0) for x in tl if x["phase"] == "measure")
        row["dropped_pct"] = round(100 * dm / max(1, s["rate_per_s"] * meas), 3); row["dropped_measure"] = dm
        ok = [x for x in tl if x["phase"] == "measure" and x["requests"] not in ("", "0")]
        row["worst_second_p95_ms"] = max((float(x["p95_ms"]) for x in ok if x["p95_ms"]), default=None)
    for op, x in s["by_op"].items():
        row[f"p95_{op}"] = round((x["duration_ms"] or {}).get("p(95)", 0), 1)
    # samples: keep the measured window (the sampler starts with k6)
    samp = ([json.loads(l) for l in opentext(f"{d}/samples.jsonl")]
            if os.path.exists(f"{d}/samples.jsonl") or os.path.exists(f"{d}/samples.jsonl.xz") else [])
    if samp:
        t0 = iso(samp[0]["t"]) - dt.timedelta(seconds=2)
        samp = [x for x in samp if warm <= (iso(x["t"]) - t0).total_seconds() <= warm + meas]
    g = lambda f: [f(x) for x in samp]
    cores = lambda v: None if v is None else v / 100
    row["sut_cpu_cores"] = mean(g(lambda x: cores(x["cpu"].get("sut", 0) * 4)))
    row["k6_cpu_cores"] = mean(g(lambda x: cores(x["proc"].get("k6"))))
    row["k6_cpus_busy_max_pct"] = mx(g(lambda x: x["cpu"].get("k6_max")))
    if "app" in (samp[0]["proc"] if samp else {}):
        row["app_cpu_cores"] = mean(g(lambda x: cores(x["proc"].get("app"))))
        row["app_threads_max"] = mx(g(lambda x: x["proc"].get("app_threads")))
        row["sql_cpu_cores"] = mean(g(lambda x: cores(x["cg"].get("sql"))))
        own = [None if x["proc"].get("app") is None or x["cg"].get("sql") is None else (x["proc"]["app"] + x["cg"]["sql"]) / 100 for x in samp]
    else:
        row["vcpu_cpu_cores"] = mean(g(lambda x: cores(x["proc"].get("vcpu"))))
        row["emu_cpu_cores"] = mean(g(lambda x: cores(x["proc"].get("emu"))))
        own = [cores(x["proc"].get("vcpu")) for x in samp]
    tot = g(lambda x: x["cpu"].get("sut", 0) * 4 / 100)
    row["foreign_on_sut_cores"] = mean([t - o for t, o in zip(tot, own) if o is not None]) if samp else None
    sq = [x["sql"]["sql"] for x in samp if "sql" in x and "error" not in x["sql"]["sql"]]
    row["sql_sessions_max"] = mx([q["sessions"] for q in sq])
    row["sql_requests_avg"] = mean([q["requests"] for q in sq]); row["sql_requests_max"] = mx([q["requests"] for q in sq])
    row["sql_blocked_max"] = mx([q["blocked"] for q in sq]); row["sql_runnable_avg"] = mean([q["runnable"] for q in sq])
    tw = {}
    for q in sq:
        if q.get("top_wait"):
            tw[q["top_wait"]] = tw.get(q["top_wait"], 0) + 1
    row["sql_top_wait_seen"] = ";".join(f"{k}x{v}" for k, v in sorted(tw.items(), key=lambda kv: -kv[1])[:3])
    # VM counters (legacy)
    vc = f"{d}/vm-counters.csv"
    if os.path.exists(vc):
        rows = [r for r in csv.DictReader(l for l in open(vc) if "," in l and not l.startswith("#"))]
        if rows and samp:
            t_start = iso(samp[0]["t"]) - dt.timedelta(seconds=2 + 0)
            rows = [r for r in rows if r.get("t", "").endswith("Z") and warm <= (iso(r["t"]) - (t0)).total_seconds() <= warm + meas]
        f = lambda k: [float(r[k]) for r in rows if r.get(k) not in (None, "")]
        row["vm_cpu_total_pct"] = mean(f("cpu_total_pct")); row["w3wp_cores"] = mean([v / 100 for v in f("w3wp_pct")])
        row["sqlservr_cores"] = mean([v / 100 for v in f("sqlservr_pct")]); row["w3wp_threads_max"] = mx(f("w3wp_threads"))
        row["proc_queue_avg"] = mean(f("proc_queue")); row["aspnet_queued_max"] = mx(f("aspnet_queued"))
        row["aspnet_executing_avg"] = mean(f("aspnet_executing")); row["http_queue_max"] = mx(f("http_queue"))
        row["lock_waits_s_avg"] = mean(f("lock_waits_s")); row["sql_batches_s_avg"] = mean(f("sql_batches_s"))
        row["page_life_min"] = min(f("page_life_s")) if f("page_life_s") else None
    wb, wa = waits(f"{d}/waits-before.txt"), waits(f"{d}/waits-after.txt")
    span = warm + meas
    benign = ("DISPATCHER_QUEUE_SEMAPHORE", "AZURE_IMDS_VERSIONS", "SERVER_IDLE_CHECK", "FT_IFTS_STOPLIST_CACHE_MANAGER", "SOS_WORK_DISPATCHER")
    delta = sorted(((k, wa[k][1] - wb.get(k, (0, 0, 0))[1], wa[k][0] - wb.get(k, (0, 0, 0))[0]) for k in wa if k not in benign), key=lambda x: -x[1])
    row["top_waits_ms_per_s"] = ";".join(f"{k}={v / span:.0f}" for k, v, n in delta[:4] if v > 0)
    hl = open(f"{d}/host-load.txt").read() if os.path.exists(f"{d}/host-load.txt") else ""
    la = re.findall(r"load average: ([\d.]+), ([\d.]+), ([\d.]+)", hl)
    row["host_load1_before"] = float(la[0][0]) if la else None
    row["host_load1_after"] = float(la[1][0]) if len(la) > 1 else None
    return row


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dir"); ap.add_argument("--warmup", type=float, default=30); ap.add_argument("--measure", type=float, default=120)
    ap.add_argument("--p95", type=float, default=500); ap.add_argument("--errors", type=float, default=0.5)
    ap.add_argument("--out", default=None)
    a = ap.parse_args(); out = a.out or a.dir
    rows = []
    for d in sorted(glob.glob(f"{a.dir}/*-r*/rate-*")):
        if not os.path.exists(f"{d}/summary.json"):
            continue
        sysrep = os.path.basename(os.path.dirname(d)); m = re.match(r"(.+)-r(\d+[a-z]?)$", sysrep)
        r = {"system": m.group(1), "repeat": m.group(2)}
        r.update(step(d, a.warmup, a.measure))
        r["pass"] = r["p95_ms"] <= a.p95 and r["failed_pct"] <= a.errors and r["dropped_pct"] <= a.errors
        rows.append(r)
    rows.sort(key=lambda r: (r["system"], r["repeat"], r["rate"]))
    keys = []
    for r in rows:
        keys += [k for k in r if k not in keys]
    with open(f"{out}/steps.csv", "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=keys); w.writeheader(); [w.writerow(r) for r in rows]
    lines = [f"# Capacity steps ({a.dir})", "",
             f"Pass = p95 <= {a.p95:g} ms, failed <= {a.errors:g} %, dropped iterations <= {a.errors:g} % over the measured {a.measure:g} s "
             f"(after {a.warmup:g} s warm-up). CPU in cores (1.0 = one logical CPU), measured window averages.", ""]
    for sysname in sorted({r["system"] for r in rows}):
        rs = [r for r in rows if r["system"] == sysname]
        lines += [f"## {sysname}", "", "| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |",
                  "|---:|---:|---|---|---|---:|---:|---|"]
        for rate in sorted({r["rate"] for r in rs}):
            x = [r for r in rs if r["rate"] == rate]; p = [r["p95_ms"] for r in x]
            cpu = ("app %.2f + sql %.2f" % (st.median([r.get("app_cpu_cores") or 0 for r in x]), st.median([r.get("sql_cpu_cores") or 0 for r in x]))
                   if "app_cpu_cores" in x[0] else "vCPU %.2f (w3wp %.2f, sqlservr %.2f), emu %.2f" % (
                       st.median([r.get("vcpu_cpu_cores") or 0 for r in x]), st.median([r.get("w3wp_cores") or 0 for r in x]),
                       st.median([r.get("sqlservr_cores") or 0 for r in x]), st.median([r.get("emu_cpu_cores") or 0 for r in x])))
            lines.append(f"| {rate} | {len(x)} | {st.median(p):.1f} ({min(p):.1f}-{max(p):.1f}) | {st.median([r['failed_pct'] for r in x]):.2f} ({max(r['failed_pct'] for r in x):.2f}) | "
                         f"{st.median([r['dropped_pct'] for r in x]):.2f} ({max(r['dropped_pct'] for r in x):.2f}) | {sum(r['pass'] for r in x)}/{len(x)} | "
                         f"{st.median([r['req_per_s'] for r in x]):.1f} | {cpu} |")
        lines.append("")
        med = lambda x, k: (st.median([r[k] for r in x if r.get(k) not in (None, "")]) if any(r.get(k) not in (None, "") for r in x) else None)
        f = lambda v, n=2: "" if v is None else (f"{v:.{n}f}" if isinstance(v, float) else str(v))
        if "app_cpu_cores" in rs[0]:
            lines += ["Resources (medians over the repeats; CPU in cores of the 4):", "",
                      "| Rate | app | SQL | app + SQL | other processes on the 4 cores | app threads max | SQL sessions max | SQL blocked max | SQL runnable avg | k6 cores |",
                      "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
            for rate in sorted({r["rate"] for r in rs}):
                x = [r for r in rs if r["rate"] == rate]
                a, q = med(x, "app_cpu_cores"), med(x, "sql_cpu_cores")
                lines.append(f"| {rate} | {f(a)} | {f(q)} | {f(a + q if a is not None and q is not None else None)} | {f(med(x, 'foreign_on_sut_cores'))} | {f(med(x, 'app_threads_max'), 0)} | "
                             f"{f(med(x, 'sql_sessions_max'), 0)} | {f(med(x, 'sql_blocked_max'), 0)} | {f(med(x, 'sql_runnable_avg'), 1)} | {f(med(x, 'k6_cpu_cores'))} |")
        else:
            lines += ["Resources (medians over the repeats; CPU in cores of the 4 vCPUs; Windows counters inside the VM):", "",
                      "| Rate | vCPU threads | w3wp | sqlservr | VM CPU % | other processes on the 4 cores | qemu emulator threads | w3wp threads max | ASP.NET queued max | SQL sessions max | lock waits/s | k6 cores |",
                      "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
            for rate in sorted({r["rate"] for r in rs}):
                x = [r for r in rs if r["rate"] == rate]
                lines.append(f"| {rate} | {f(med(x, 'vcpu_cpu_cores'))} | {f(med(x, 'w3wp_cores'))} | {f(med(x, 'sqlservr_cores'))} | {f(med(x, 'vm_cpu_total_pct'), 1)} | "
                             f"{f(med(x, 'foreign_on_sut_cores'))} | {f(med(x, 'emu_cpu_cores'))} | {f(med(x, 'w3wp_threads_max'), 0)} | {f(med(x, 'aspnet_queued_max'), 0)} | "
                             f"{f(med(x, 'sql_sessions_max'), 0)} | {f(med(x, 'lock_waits_s_avg'), 1)} | {f(med(x, 'k6_cpu_cores'))} |")
        lines += ["", "Top SQL waits per step (ms of wait per second of the step, warm-up included):", ""]
        for r in rs:
            lines.append(f"- r{r['repeat']} rate {r['rate']}: {r['top_waits_ms_per_s'] or '-'} (host load {r['host_load1_before']} -> {r['host_load1_after']})")
        lines.append("")
        caps = {}
        for rep in sorted({r["repeat"] for r in rs}):
            ok = [r["rate"] for r in rs if r["repeat"] == rep and r["pass"]]
            caps[rep] = max(ok) if ok else None
        lines += [f"Highest passing rate per repeat: " + ", ".join(f"r{k} {v}" for k, v in caps.items())]
        # sustained: the highest rate that passed in every repeat, with every lower rate passing too
        rates = sorted({r["rate"] for r in rs}); reps = sorted({r["repeat"] for r in rs}); sustained = None
        for rate in rates:
            x = {r["repeat"]: r for r in rs if r["rate"] == rate}
            if x and all(r["pass"] for r in x.values()):
                sustained = rate
            else:
                break
        first_fail = next((rate for rate in rates if any(not r["pass"] for r in rs if r["rate"] == rate)), None)
        lines += [f"Sustained (passed in every repeat that ran it, and every lower rate too): {sustained}; first rate with a failing repeat: {first_fail}", ""]
    open(f"{out}/curves.md", "w").write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
