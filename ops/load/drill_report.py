#!/usr/bin/env python3
"""drill_report.py - numbers of one failure drill (drills.sh drill): DIR/result.json and a text summary.

Failed requests, time to recover, error-budget burn against the SLO (availability 99.5 %, p95 <= 500 ms), what the
readiness probe did (pods.tsv: Ready and endpoints every second; events.txt), and for the worker drill the notification
count (expected from the database vs delivered to the SMTP sink: lost, duplicated).
"""
import csv, datetime as dt, glob, json, lzma, os, re, sys
from collections import Counter

SLO_AVAIL, SLO_P95 = 0.995, 500.0


def iso(s):
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).timestamp()


def main():
    d = sys.argv[1]
    t0 = int(open(f"{d}/timeline-t0.txt").read()) / 1000
    marks = []
    for line in open(f"{d}/marks.txt"):
        p = line.split()
        if len(p) >= 2 and re.match(r"\d{4}-", p[1]):
            marks.append((p[0], iso(p[1]) - t0, line.strip()))
    reqs = []
    with lzma.open(f"{d}/requests.csv.xz", "rt") as f:
        next(f)
        for line in f:
            ms, op, st, dur, ok = line.rstrip("\n").split(",")
            reqs.append((int(ms) / 1000, op, st, float(dur), ok == "1"))
    faults = [m for m in marks if m[0] == "fault"]
    f_at = faults[0][1] if faults else None
    total = len(reqs); failed = sum(1 for r in reqs if not r[4])
    slow = sum(1 for r in reqs if r[3] > SLO_P95)
    dur_s = max(r[0] for r in reqs) - min(r[0] for r in reqs) if reqs else 0
    tl = list(csv.DictReader(open(f"{d}/timeline.csv")))
    sec = {int(x["second"]): x for x in tl}
    def bad(s):
        x = sec.get(s)
        return x is None or x["requests"] in ("", "0") or int(x["failed"]) > 0 or (x["p95_ms"] and float(x["p95_ms"]) > SLO_P95)
    res = {"dir": os.path.basename(d), "requests": total, "failed": failed, "availability_pct": round(100 * (1 - failed / max(1, total)), 3),
           "slow_over_500ms": slow, "run_s": round(dur_s, 1), "marks": [m[2] for m in marks]}
    if f_at is not None:
        before = [r for r in reqs if r[0] < f_at]
        res["before_fault"] = {"requests": len(before), "failed": sum(1 for r in before if not r[4]),
                               "p95_ms": pct([r[3] for r in before], 95)}
        after = [r for r in reqs if r[0] >= f_at]
        res["from_fault"] = {"requests": len(after), "failed": sum(1 for r in after if not r[4]), "p95_ms": pct([r[3] for r in after], 95)}
        # failed requests by when they were sent; first and last failure after the fault
        fails = [r for r in after if not r[4]]
        res["first_failure_after_fault_s"] = round(min(r[0] for r in fails) - f_at, 1) if fails else None
        res["last_failure_after_fault_s"] = round(max(r[0] + r[3] / 1000 for r in fails) - f_at, 1) if fails else None
        res["failed_by_status"] = dict(Counter(r[2] or "none" for r in fails))
        # by class: 5xx / connection errors (0) are the server's; a 400 after a failed form page is the client posting a form
        # without its antiforgery token (the k6 VU keeps the token of its last page), not a server error
        for cls, pred in (("5xx", lambda st: st.startswith("5")), ("conn", lambda st: st in ("0", "")), ("4xx", lambda st: st.startswith("4"))):
            fs = [r for r in fails if pred(r[2])]
            res[f"last_{cls}_after_fault_s"] = round(max(r[0] + r[3] / 1000 for r in fs) - f_at, 1) if fs else None
        recover_marks = [m[1] for m in marks if m[0] in ("recover", "ready", "sql-accepting", "hold-off")]
        if recover_marks:
            res["fault_removed_after_fault_s"] = round(max(recover_marks) - f_at, 1)
        # recovered: the end of the last bad second (a failure, or p95 > 500 ms, or no answer) after the fault, provided 30 s
        # in a row are clean after it (else: not recovered within the run)
        last = max(sec)
        bads = [s for s in range(int(f_at), last + 1) if bad(s)]
        if not bads:
            res["recovered_after_fault_s"] = 0.0
        else:
            lb = max(bads)
            res["recovered_after_fault_s"] = round(lb + 1 - f_at, 1) if last - lb >= 30 else None
        res["bad_seconds_after_fault"] = sum(1 for s in range(int(f_at), last + 1) if bad(s))
    # error budget: the run's own budget, and the share of a 30-day budget at this request rate
    budget_run = (1 - SLO_AVAIL) * total
    rps = total / max(1, dur_s)
    res["error_budget"] = {"run_budget_requests": round(budget_run, 1), "run_burn_pct": round(100 * failed / max(1e-9, budget_run), 1),
                           "share_of_30d_budget_pct": round(100 * failed / ((1 - SLO_AVAIL) * rps * 30 * 86400), 4),
                           "req_per_s": round(rps, 1), "p95_run_ms": pct([r[3] for r in reqs], 95),
                           "latency_slo_met_over_run": pct([r[3] for r in reqs], 95) <= SLO_P95}
    # readiness: pods.tsv lines "time \t pod=Ready/restarts ... \t endpoint:ready ..."
    pods = f"{d}/pods.tsv"
    rows = ([l.rstrip("\n").split("\t") for l in (open(pods) if os.path.exists(pods) else lzma.open(pods + ".xz", "rt"))]
            if os.path.exists(pods) or os.path.exists(pods + ".xz") else [])
    web_not_ready = 0; no_ready_ep = 0; restarts = {}
    for r in rows:
        pods = dict(x.split("=", 1) for x in r[1].split() if "=" in x) if len(r) > 1 else {}
        webs = {k: v for k, v in pods.items() if k.startswith("td-web")}
        if webs and not any(v.startswith("True") for v in webs.values()):
            web_not_ready += 1
        eps = r[2].split() if len(r) > 2 else []
        if not any(e.endswith(":true") for e in eps):
            no_ready_ep += 1
        for k, v in pods.items():
            restarts[k] = v.split("/")[-1]
    res["readiness"] = {"samples_1s": len(rows), "seconds_no_ready_web_pod": web_not_ready, "seconds_no_ready_endpoint": no_ready_ep,
                        "pods_seen": restarts,
                        "probe": json.load(open(f"{d}/readiness-probe.json")) if os.path.exists(f"{d}/readiness-probe.json") else None}
    if os.path.exists(f"{d}/events.txt"):
        ev = open(f"{d}/events.txt").read()
        res["readiness"]["unhealthy_events"] = [l for l in ev.splitlines() if "Unhealthy" in l][:12]
    em = f"{d}/expected-mails.txt"
    if os.path.exists(em) or os.path.exists(em + ".xz"):
        exp = Counter()
        for line in (open(em) if os.path.exists(em) else lzma.open(em + ".xz", "rt")):
            p = line.strip().split("|")
            if len(p) == 3 and p[0].isdigit():
                exp[(p[0], p[1], p[2].lower())] += 1
        got = Counter(); multi = 0; times = {}; rows = []
        if not os.path.isdir(f"{d}/mail") and os.path.exists(f"{d}/delivered.csv.xz"):  # a re-run after the mails were packed
            for line in list(lzma.open(f"{d}/delivered.csv.xz", "rt"))[1:]:
                fn, t, evs, rcpt, rec = line.rstrip("\n").split(",", 4)
                for e in evs.split("+"):
                    if e:
                        got[(t, e, rcpt)] += 1; times.setdefault((t, e, rcpt), []).append(rec)
                multi += 1 if "+" in evs else 0
        for f in sorted(glob.glob(f"{d}/mail/*.eml")):
            hd = open(f, encoding="utf-8", errors="replace").read()
            g = lambda n: (re.search(rf"^{n}: *(.*)$", hd, re.M | re.I) or [None, ""])[1].strip()
            rcpt = g("X-Sink-Rcpt-To").strip("<>").lower(); t = g("X-TicketDesk-Ticket"); evs = [e for e in g("X-TicketDesk-Events").split(",") if e]
            rows.append(f"{os.path.basename(f)},{t},{'+'.join(evs)},{rcpt},{g('X-Sink-Received')}")
            if len(evs) > 1:
                multi += 1
            for e in evs:
                got[(t, e, rcpt)] += 1
                times.setdefault((t, e, rcpt), []).append(g("X-Sink-Received"))
        if rows:
            with lzma.open(f"{d}/delivered.csv.xz", "wt", preset=9) as f:
                f.write("file,ticket,events,rcpt,received\n" + "\n".join(rows) + "\n")
        lost = [k for k in exp if got[k] == 0]
        dups = {k: v for k, v in got.items() if v > 1}
        unexpected = [k for k in got if k not in exp]
        held = [l.strip() for l in open(f"{d}/mail.log") if l.startswith("held")] if os.path.exists(f"{d}/mail.log") else []
        res["notifications"] = {"expected": sum(exp.values()), "delivered_mails": sum(got.values()), "distinct_delivered": len(got),
                                "lost": len(lost), "duplicated": len(dups), "duplicate_extra_mails": sum(v - 1 for v in dups.values()),
                                "unexpected": len(unexpected), "mails_with_several_events": multi, "held_by_sink": held,
                                "duplicates": [{"ticket": k[0], "event": k[1], "received": times[k]} for k in list(dups)[:10]],
                                "lost_examples": [list(k[:2]) for k in lost[:10]]}
    json.dump(res, open(f"{d}/result.json", "w"), indent=1)
    print(json.dumps(res, indent=1))


def pct(xs, p):
    if not xs:
        return None
    xs = sorted(xs); k = (len(xs) - 1) * p / 100; f = int(k); c = min(f + 1, len(xs) - 1)
    return round(xs[f] + (xs[c] - xs[f]) * (k - f), 1)


if __name__ == "__main__":
    main()
