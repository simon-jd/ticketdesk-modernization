#!/usr/bin/env python3
"""Drive the legacy ticket center and ticket page so an Extended Events session can capture the SQL EF6 sends.

Signs in as the lab admin (help desk + admin roles, so all five lists exist), opens every list, a second page,
column sorts and a filter (the legacy app persists sort/filter per user: the script resets the lists at the end),
then one ticket page with its panels. Uses the HTTP client and sign-in of ops/onprem/smoke/legacy_smoke.py.
Read-only for tickets; changes only the admin's UserSettings row, which the final reset-user-lists restores.

    python3 -I ops/dev/legacy_list_driver.py [--base https://localhost:8443] [--json out.json]
"""
import argparse
import importlib.util
import json
import os
import re
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
TD_W = os.environ.get("TD_W", os.path.expanduser("~/Desktop/ticketdesk-modernization"))
spec = importlib.util.spec_from_file_location("legacy_smoke", os.path.join(HERE, "..", "onprem", "smoke", "legacy_smoke.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="https://localhost:8443")
    ap.add_argument("--env-file", default=os.path.join(TD_W, ".secrets", "legacy-lab.env"))
    ap.add_argument("--cacert", default=os.path.join(TD_W, ".secrets", "legacy-lab-ca", "ca.crt"))
    ap.add_argument("--json", help="write the request log here")
    a = ap.parse_args()
    cred = smoke.load_env(a.env_file)
    c = smoke.Client(a.base, a.cacert, False, 120)
    run = smoke.Run("legacy-list-driver")
    smoke.sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])  # values never printed

    steps = []

    def iso(t):
        return time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime(t)) + f".{int((t % 1) * 1000):03d}Z"

    def get(label, path):
        t0 = time.time()
        st, body, url, _ = c.req("GET", path)
        t1 = time.time()
        run.check(label, st == 200, f"HTTP {st}")
        steps.append({"label": label, "path": path, "status": st, "start": iso(t0), "end": iso(t1)})
        time.sleep(0.3)  # keeps the steps apart in the XE timeline
        return body

    try:
        get("ticket center default list (help desk: unassigned)", "/tickets")
        for name in ("unassigned", "assignedToMe", "mytickets", "opentickets", "historytickets"):
            get(f"list {name}", f"/tickets/{name}")
        get("opentickets page 2 (partial)", "/tickets/pageList/opentickets/2")
        get("opentickets sort by Title (partial)", "/tickets/sortList/opentickets?columnName=Title")
        get("opentickets sort by Title again: descending", "/tickets/sortList/opentickets?columnName=Title")
        get("opentickets add Priority to the sort", "/tickets/sortList/opentickets?columnName=Priority&isMultiSort=true")
        get("mytickets filter status=Active, assignedTo=anyone", "/tickets/filterList/mytickets?pageSize=20&ticketStatus=Active&owner=anyone&assignedTo=anyone")
        get("unassigned filter status=Any", "/tickets/filterList/unassigned?pageSize=20&ticketStatus=Any&owner=anyone&assignedTo=unassigned")

        body = get("opentickets (to pick a ticket)", "/tickets/opentickets")
        ids = [int(x) for x in re.findall(r'href="/ticket/(\d+)"', body)]
        run.check("a ticket link on the list", bool(ids), f"{len(ids)} links")
        tid = ids[0]
        get("ticket page", f"/ticket/{tid}")
        get("ticket details panel", f"/ticket/ticket-details?ticketId={tid}")
        get("ticket events panel", f"/ticket/ticket-events?ticketId={tid}")
        get("ticket files panel", f"/ticket/ticket-files?ticketId={tid}")

    finally:
        # sortList/filterList persisted the admin's list settings: put them back to the defaults
        st, _, _, _ = c.req("GET", "/tickets/reset-user-lists")
        run.check("reset the admin's list settings", st == 200, f"HTTP {st}")

    out = {"ok": all(x["ok"] for x in run.checks), "checks": run.checks, "steps": steps, "requests": c.log}
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump(out, f, indent=2)
            f.write("\n")
    print(json.dumps({"ok": out["ok"], "steps": len(steps), "requests": len(c.log)}))
    return 0 if out["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
