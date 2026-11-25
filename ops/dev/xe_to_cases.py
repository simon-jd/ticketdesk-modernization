#!/usr/bin/env python3
"""Turn a legacy XE ring-buffer export into (1) a readable event list and (2) a td-sqlperf case file.

Events are assigned to the driver steps (legacy_list_driver.py --json) by time: VM clock + --clock-offset-ms
(VM minus host, measured right before the run) must fall inside the step's request window (+-150 ms).
Only SELECT statements become replay cases (the UserSettings writes of sortList/filterList stay in the event list).
User-id literals are replaced with --user-id (the scale-L user the replay should act as): on the VM every list query
is for the signed-in lab admin, whose id does not exist at scale L.

    python3 -I ops/dev/xe_to_cases.py --xml capture.xml[.xz] --steps driver.json --clock-offset-ms N --user-id ID \
        --events-out events.json --text-out statements.txt --cases-out cases-legacy.json
"""
import argparse
import datetime as dt
import json
import lzma
import re
import sys
import xml.etree.ElementTree as ET

GUID = re.compile(r"N'([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'")


def parse_time(s):
    s = s.rstrip("Z")
    if "." in s:
        head, frac = s.split(".")
        s = head + "." + (frac + "000000")[:6]
    return dt.datetime.fromisoformat(s).replace(tzinfo=dt.timezone.utc)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--xml", required=True)
    ap.add_argument("--steps", required=True)
    ap.add_argument("--clock-offset-ms", type=float, default=0.0)
    ap.add_argument("--user-id", required=True)
    ap.add_argument("--events-out", required=True)
    ap.add_argument("--text-out", required=True)
    ap.add_argument("--cases-out", required=True)
    a = ap.parse_args()

    opener = lzma.open if a.xml.endswith(".xz") else open
    with opener(a.xml, "rt", encoding="utf-8", errors="replace") as f:
        raw = f.read()
    raw = raw[raw.index("<RingBufferTarget"):raw.rindex("</RingBufferTarget>") + len("</RingBufferTarget>")]
    root = ET.fromstring(raw)
    events = []
    for e in root.findall("event"):
        d = {x.get("name"): (x.findtext("value") or "") for x in e.findall("data")}
        act = {x.get("name"): (x.findtext("value") or "") for x in e.findall("action")}
        text = d.get("statement") or d.get("batch_text") or ""
        events.append({
            "timestamp": e.get("timestamp"), "event": e.get("name"), "app": act.get("client_app_name"), "session": act.get("session_id"),
            "duration_us": int(d.get("duration") or 0), "cpu_us": int(d.get("cpu_time") or 0), "logical_reads": int(d.get("logical_reads") or 0),
            "row_count": int(d.get("row_count") or 0), "text": text,
        })
    events.sort(key=lambda x: x["timestamp"])
    app_events = [x for x in events if x["app"] and "sqlcmd" not in x["app"].lower()]

    steps = json.load(open(a.steps, encoding="utf-8"))["steps"]
    offset = dt.timedelta(milliseconds=a.clock_offset_ms)
    pad = dt.timedelta(milliseconds=150)
    for x in app_events:
        t = parse_time(x["timestamp"]) - offset
        x["step"] = next((s["label"] for s in steps if parse_time(s["start"]) - pad <= t <= parse_time(s["end"]) + pad), None)

    with open(a.events_out, "w", encoding="utf-8") as f:
        json.dump({"clock_offset_ms": a.clock_offset_ms, "events_total": len(events), "app_events": app_events}, f, indent=1)
        f.write("\n")

    lines, scenarios = [], []
    for s in steps:
        mine = [x for x in app_events if x["step"] == s["label"]]
        lines.append(f"### {s['label']}  (GET {s['path']}, {len(mine)} statements)")
        commands = []
        for x in mine:
            lines.append(f"-- {x['event']} {x['timestamp']} duration {x['duration_us']} us, cpu {x['cpu_us']} us, reads {x['logical_reads']}, rows {x['row_count']}")
            lines.append(x["text"].strip())
            body = x["text"]
            inner = body.split("N'", 1)[1] if body.lstrip().lower().startswith("exec sp_executesql") else body
            if inner.lstrip().upper().startswith("SELECT"):
                commands.append({"commandType": "Text", "text": GUID.sub("N'" + a.user_id + "'", body), "parameters": []})
        lines.append("")
        if commands:
            slug = re.sub(r"[^a-z0-9]+", "-", s["label"].lower()).strip("-")[:60]
            scenarios.append({"name": "legacy-" + slug, "description": f"legacy GET {s['path']}: {len(commands)} SELECT(s) EF6 sent (XE capture)", "commands": commands})
    unassigned = [x for x in app_events if x["step"] is None]
    lines.append(f"### not matched to a step: {len(unassigned)} statements (sign-in, menus, timers)")
    for x in unassigned:
        lines.append(f"-- {x['event']} {x['timestamp']} reads {x['logical_reads']}: " + re.sub(r"\s+", " ", x["text"])[:200])
    with open(a.text_out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    cases = {"set": "legacy", "database": "", "capturedAt": steps[0]["start"] if steps else "",
             "source": "Extended Events (rpc_completed, sql_batch_completed) on the legacy VM's SQL Server Express while legacy_list_driver.py used the ticket center; SELECTs only; user-id literals replaced by the scale-L user",
             "values": {"userId": a.user_id, "clockOffsetMs": str(a.clock_offset_ms)}, "scenarios": scenarios}
    with open(a.cases_out, "w", encoding="utf-8") as f:
        json.dump(cases, f, indent=2)
        f.write("\n")
    print(json.dumps({"events": len(events), "app_events": len(app_events), "matched": len(app_events) - len(unassigned), "scenarios": len(scenarios)}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
