#!/usr/bin/env python3
"""Smoke test of the new app (stdlib only; run with python3 -I). AKS round.

  app_smoke.py --base URL [--env-file F] [--cacert CA | --insecure] [--json OUT] [--marker-ticket N] [--read-only]

The legacy smoke (ops/onprem/smoke/legacy_smoke.py) posts the MVC new-ticket form, which the new app serves as the Angular
page (registry mode Angular), so this one goes through the REST API the Angular front end uses: home page, sign in (the
server page, same as legacy_smoke), GET /api/tickets/options, the new-ticket page (Angular shell: sets the XSRF-TOKEN
cookie), POST /api/tickets, the ticket page, a comment (POST /api/tickets/{id}/activities), the comment in GET
/api/tickets/{id}. Writes send the RequestVerificationToken header (the XSRF-TOKEN cookie's value).

--marker-ticket N (restore drill): also report the newest probe comment on ticket N ("cutover probe write <tick> <UTC>",
written by ops/azure/cutover_probe.py --write-ticket N), i.e. the last write the database holds.
Exit code 0 only when every check passed; the JSON summary has the same shape as legacy_smoke's.
"""
import argparse
import importlib.util
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True  # importing legacy_smoke must not leave __pycache__ in ops/onprem/smoke
_spec = importlib.util.spec_from_file_location("legacy_smoke", os.path.join(HERE, "..", "onprem", "smoke", "legacy_smoke.py"))
ls = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ls)
MARKER = re.compile(r"cutover probe write (\d+) (\d{4}-\d\d-\d\dT[0-9:.]+Z)")


def jreq(c, method, path, obj=None):
    headers = {"Accept": "application/json"}
    body = None
    if obj is not None:
        body = json.dumps(obj).encode()
        headers["Content-Type"] = "application/json"
        token = next((ck.value for ck in c.jar if ck.name == "XSRF-TOKEN"), None)
        if token:
            headers["RequestVerificationToken"] = token
    r = urllib.request.Request(c.base + path, data=body, method=method, headers=headers)
    t0 = time.monotonic()
    try:
        resp = c.opener.open(r, timeout=c.timeout)
        status, raw = resp.status, resp.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read()
    ms = round((time.monotonic() - t0) * 1000)
    c.log.append({"method": method, "url": path, "status": status, "ms": ms})
    print(f"  {method:4} {path} -> {status} {ms}ms", flush=True)
    text = raw.decode("utf-8", "replace")
    try:
        return status, json.loads(text) if text else None, text
    except json.JSONDecodeError:
        return status, None, text


def smoke(c, run, cred, marker_ticket, read_only=False):
    st, body, url, _ = c.req("GET", "/")
    run.check("home page over HTTPS", st == 200 and "<title>" in body.lower(), f"HTTP {st}")
    ls.sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])
    st, opts, _ = jreq(c, "GET", "/api/tickets/options")
    run.check("ticket options (API)", st == 200 and opts and opts.get("ticketTypes") and opts.get("categories"), f"HTTP {st}")
    st, body, url, _ = c.req("GET", "/ticket/new")
    xsrf = any(ck.name == "XSRF-TOKEN" for ck in c.jar)
    run.check("new-ticket page", st == 200 and xsrf, f"HTTP {st}, XSRF-TOKEN cookie {'set' if xsrf else 'missing'}")
    if read_only:  # no write: the database stays as restored (a golden replay follows)
        st, body, url, _ = c.req("GET", "/ticket/30")
        run.check("ticket page", st == 200, f"HTTP {st} /ticket/30")
        st, t, _ = jreq(c, "GET", "/api/tickets/30")
        run.check("ticket (API)", st == 200 and t and t.get("ticketId") == 30, f"HTTP {st}")
        return {"read_only": True}
    stamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    new = {"title": f"App smoke ticket {stamp}", "details": f"Created by ops/k8s/app_smoke.py at {stamp}.", "isHtml": False,
           "ticketType": opts["ticketTypes"][0], "category": opts["categories"][0]}
    if len(opts.get("projects") or []) > 1:
        new["projectId"] = opts["projects"][0]["projectId"]
    st, created, text = jreq(c, "POST", "/api/tickets", new)
    tid = (created or {}).get("ticketId")
    run.check("create ticket (API)", st == 201 and tid, f"HTTP {st}{'' if tid else ': ' + text[:200]}")
    st, body, url, _ = c.req("GET", f"/ticket/{tid}")
    run.check("ticket page", st == 200, f"HTTP {st} /ticket/{tid}")
    comment = f"app smoke comment {stamp}"
    st, _, text = jreq(c, "POST", f"/api/tickets/{tid}/activities", {"activity": "AddComment", "comment": comment})
    run.check("add comment (API)", st in (200, 204), f"HTTP {st}{'' if st in (200, 204) else ': ' + text[:200]}")
    st, _, text = jreq(c, "GET", f"/api/tickets/{tid}")
    run.check("comment visible (API)", st == 200 and comment in text, f"HTTP {st}")
    extra = {"ticket_id": tid}
    if marker_ticket:
        st, _, text = jreq(c, "GET", f"/api/tickets/{marker_ticket}")
        marks = [(m.group(2), int(m.group(1))) for m in MARKER.finditer(text)]
        last = max(marks) if marks else None
        run.check("probe marker comments readable", st == 200 and last is not None, f"HTTP {st}, {len(marks)} markers")
        extra["marker_ticket"] = marker_ticket
        extra["markers"] = len(marks)
        extra["last_marker_utc"], extra["last_marker_tick"] = last if last else (None, None)
    return extra


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--env-file", default=os.path.join(ls.TD_W, ".secrets", "legacy-lab.env"))
    ap.add_argument("--cacert", default=os.path.join(ls.TD_W, ".secrets", "legacy-lab-ca", "ca.crt"))
    ap.add_argument("--insecure", action="store_true")
    ap.add_argument("--timeout", type=float, default=60)
    ap.add_argument("--marker-ticket", type=int)
    ap.add_argument("--read-only", action="store_true", help="no ticket or comment written (before a golden replay)")
    ap.add_argument("--json")
    a = ap.parse_args()
    cred = ls.load_env(a.env_file)
    c = ls.Client(a.base, a.cacert, a.insecure, a.timeout)
    run = ls.Run("app-smoke")
    extra, ok = {}, True
    print(f"app_smoke against {a.base} at {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
    try:
        extra = smoke(c, run, cred, a.marker_ticket, a.read_only) or {}
    except Exception as e:  # noqa: BLE001 - report any failure in the summary
        ok = False
        if not isinstance(e, AssertionError):
            run.checks.append({"check": "exception", "ok": False, "detail": repr(e)})
            print(f"[FAIL] exception: {e!r}")
    summary = {"command": "app-smoke", "base": a.base, "ok": ok and all(x["ok"] for x in run.checks),
               "checks_passed": sum(x["ok"] for x in run.checks), "checks_total": len(run.checks),
               "requests": len(c.log), "seconds": round(time.monotonic() - run.t0, 1),
               "checks": run.checks, "requests_log": c.log, **extra}
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            f.write(json.dumps(summary, indent=2) + "\n")
    print(json.dumps({k: summary[k] for k in ("command", "ok", "checks_passed", "checks_total", "requests", "seconds")}
                     | {k: v for k, v in extra.items() if k != "ticket_id"}))
    return 0 if summary["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
