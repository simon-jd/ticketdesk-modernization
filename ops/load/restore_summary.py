#!/usr/bin/env python3
"""restore_summary.py DIR: the restore-drill runs (DIR/run-*/result.json) -> DIR/summary.md"""
import glob, json, os, statistics as st, sys

d = sys.argv[1]
rows, rto, rpo, lost = [], [], [], []
for f in sorted(glob.glob(f"{d}/run-*/result.json")):
    r = json.load(open(f)); n = os.path.basename(os.path.dirname(f))
    t = r.get("rto_s (loss -> smoke passed)"); p = r.get("rpo_s (last committed - last restored write)")
    if t is not None and r.get("smoke_ok"): rto.append(t)
    if p is not None: rpo.append(p)
    if r.get("events_lost") is not None: lost.append(r["events_lost"])
    rows.append(f"| {n} | {r['loss']} | {r['loss_after_last_log_backup_s']} | {r['restore_s']} | {r['switch_s (helm upgrade --wait)']} | "
                f"{r.get('smoke_attempts', 1)} ({'passed' if r.get('smoke_ok') else 'FAILED'}) | {t if t is not None else 'not measured'} | {p} | "
                f"{r['last_committed_event'][0]} / {r['last_restored_event'][0]} | {r['events_lost']} |")
L = ["# Restore drill on kind against td-sql (Round 6)", "",
     "Full backup at +20 s, log backups every 30 s, loss (all sessions killed, last committed write read, DROP DATABASE) 17 s "
     "after the 4th log backup, restore to the point of the loss (STOPAT), app switched through a values file, server-page "
     "smoke every 2 s until it passes. Times in seconds.", "",
     "| Run | Loss (UTC) | Loss after the last log backup | Restore | Switch (helm --wait) | Smoke attempts | RTO (loss -> smoke passed) | RPO (last committed - last restored write) | Last event committed / restored | Writes lost (events) |",
     "|---|---|---:|---:|---:|---|---:|---:|---|---:|"] + rows
if rto:
    L += ["", f"RTO over {len(rto)} measured runs: median **{st.median(rto):.1f} s** (min {min(rto):.1f}, max {max(rto):.1f}); "
              f"RPO over {len(rpo)} runs: median **{st.median(rpo):.1f} s** (min {min(rpo):.1f}, max {max(rpo):.1f}); writes lost {min(lost)}-{max(lost)} events."]
for f in sorted(glob.glob(f"{d}/run-*/result.json")):
    r = json.load(open(f))
    if r.get("note"): L += ["", f"{os.path.basename(os.path.dirname(f))}: {r['note']}"]
open(f"{d}/summary.md", "w").write("\n".join(L) + "\n"); print("\n".join(L))
