#!/usr/bin/env python3
"""drills_summary.py EVDIR: one table of the drill-*/result.json files -> EVDIR/drills.md"""
import glob, json, os, sys

ev = sys.argv[1]
rows = []
for f in sorted(glob.glob(f"{ev}/drill-*/result.json")):
    r = json.load(open(f)); n = os.path.basename(os.path.dirname(f))[6:]
    rd = r["readiness"]; eb = r["error_budget"]; ff = r.get("from_fault", {})
    rows.append(f"| {n} | {r['requests']} | {ff.get('failed')} of {ff.get('requests')} ({', '.join(f'{k}: {v}' for k, v in sorted(r.get('failed_by_status', {}).items()))}) | "
                f"{r.get('first_failure_after_fault_s')} | {r.get('last_5xx_after_fault_s')} / {r.get('last_conn_after_fault_s')} / {r.get('last_4xx_after_fault_s')} | "
                f"{r.get('fault_removed_after_fault_s')} | {r.get('recovered_after_fault_s')} | {r['availability_pct']} % | {eb['run_burn_pct']} % | {eb['share_of_30d_budget_pct']} % | "
                f"{r['before_fault']['p95_ms']} / {ff.get('p95_ms')} | {rd['seconds_no_ready_web_pod']} s / {rd['seconds_no_ready_endpoint']} s |")
hdr = ["# Failure drills on kind (Round 6)", "",
       "Times in seconds after the fault. Failed = no expected answer (0 = connection refused/reset or the 30 s timeout). "
       "Last failure by class: 5xx / connection / 4xx (a 400 after a failed form page is the k6 VU posting a form whose page had "
       "failed, without its antiforgery token: client side). Recovered = end of the last bad second (failure or p95 > 500 ms), "
       "followed by >= 30 clean seconds. Burn = failed / (0.5 % of the run's requests); 30-day = failed / 0.5 % of 30 days at the "
       "run's request rate. Readiness = seconds without a Ready web pod / without a ready endpoint (1 s samples). Fault removed = "
       "the drill's recovery mark (pod-kill: a web pod Ready again, -0.2 with 2 replicas = the other one already was; db-egress: "
       "rules removed; sql-restart: SQL accepting connections; worker-kill: the third kill's hold released).", "",
       "| Drill | Requests | Failed from the fault | First failure | Last 5xx / conn / 4xx | Fault removed | Recovered | Availability (run) | Run budget burn | Share of 30-day budget | p95 before / from the fault (ms) | Not ready |",
       "|---|---:|---|---:|---|---:|---:|---:|---:|---:|---|---|"]
out = "\n".join(hdr + rows) + "\n"
for f in sorted(glob.glob(f"{ev}/drill-*/result.json")):
    r = json.load(open(f))
    if "notifications" in r:
        nt = r["notifications"]
        out += (f"\nNotifications ({os.path.basename(os.path.dirname(f))}): expected {nt['expected']} (from dbo.TicketEventNotifications), "
                f"delivered {nt['delivered_mails']} mails = {nt['distinct_delivered']} distinct, lost {nt['lost']}, duplicated {nt['duplicated']} "
                f"(+{nt['duplicate_extra_mails']} extra mails), not in the expected set {nt['unexpected']} (mails for events written before the "
                f"drill's first event id, i.e. the reset's prime); held by the sink at the kills: {len(nt['held_by_sink'])}; duplicates: "
                + "; ".join(f"ticket {x['ticket']} event {x['event']} at {' and '.join(t[11:19] for t in x['received'])}" for x in nt['duplicates']) + "\n")
open(f"{ev}/drills.md", "w").write(out); print(out)
