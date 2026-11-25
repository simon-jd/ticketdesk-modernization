#!/usr/bin/env python3
"""Availability probe for a cutover window (stdlib only; run with python3 -I).

  cutover_probe.py run --target-file F --out probe.jsonl [--stop-file S] [--interval 1] [--max-seconds N]
  cutover_probe.py summarize probe.jsonl [--marks marks.json]

`run` reads the base URL from --target-file before every tick, so switching clients is one write to that
file. Each tick (default 1 s) sends two reads to the current target:
  /         anonymous home page (must be HTTP 200 and contain "TicketDesk")
  /tickets  ticket center as the admin (must be HTTP 200 with ticket links, i.e. a database read); where the new app
            serves the ticket center as the Angular page (registry mode Angular: a page shell without data), the same
            tick reads /api/tickets?PageSize=5 instead, which must be HTTP 200 JSON with tickets (AKS round)
and with --write-ticket N, on its own thread and session (so slow or failing writes do not slow the reads), one
write per interval (path "write"): the add-comment form of ticket N (antiforgery token), then
POST /ticket-activity/add-comment; the legacy app answers a saved comment with HTTP 200 and an empty body, a failed
save with 500 (or the form again), so only "200 + empty body" counts as a successful write.
The admin signs in once per target (credentials from --env-file, default $TD_W/.secrets/legacy-lab.env;
sign-in requests are recorded as path "signin"). Every request is one JSON line: UTC time, target, path,
status, ms, ok, error. A request fails on a non-200 status, a missing marker, a redirect to sign-in, a
TLS/connection error or a timeout (--timeout, default 10 s). TLS is verified against the lab CA.

`summarize` prints totals per target and path, the failed requests, and the failure windows (runs of
consecutive failed ticks); with --marks (cutover timestamps) it also counts requests per phase.
"""
import argparse
import http.cookiejar
import threading
import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone

TD_W = os.environ.get("TD_W", os.path.expanduser("~/Desktop/ticketdesk-modernization"))


def utc():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def load_env(path):
    env = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip().strip("'\"")
    return env


class Session:
    def __init__(self, base, ctx, timeout):
        self.base = base.rstrip("/")
        self.timeout = timeout
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=ctx), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.signed_in = False

    def get(self, path, data=None, headers=None):
        body = urllib.parse.urlencode(data).encode() if data is not None else None
        req = urllib.request.Request(self.base + path, data=body, method="POST" if body else "GET", headers=headers or {})
        t0 = time.monotonic()
        try:
            with self.opener.open(req, timeout=self.timeout) as r:
                status, final, text = r.status, r.geturl(), r.read().decode("utf-8", "replace")
            err = None
        except urllib.error.HTTPError as e:
            status, final, text, err = e.code, e.geturl(), "", f"HTTP {e.code}"
        except Exception as e:  # noqa: BLE001 - every failure counts
            status, final, text, err = 0, "", "", f"{type(e).__name__}: {e}"[:200]
        return status, final, text, round((time.monotonic() - t0) * 1000), err


def sign_in(s, cred):
    st, final, text, ms, err = s.get("/user/sign-in")
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text)
    if st != 200 or not m:
        return st, ms, err or "no sign-in form"
    data = {"__RequestVerificationToken": m.group(1), "UserNameOrEmail": cred["TD_ADMIN_EMAIL"],
            "Password": cred["TD_ADMIN_PASSWORD"], "RememberMe": "false"}
    st2, final, text, ms2, err = s.get("/user/sign-in", data)
    ok = st2 == 200 and "sign-out" in text.lower()
    s.signed_in = ok
    return st2, ms + ms2, None if ok else (err or "sign-in failed")


def write_comment(s, ticket, tick):
    """One probe write: load the comment form (token), post a comment. Returns (status, ms, error or None)."""
    xhr = {"X-Requested-With": "XMLHttpRequest"}
    st, final, text, ms, err = s.get(f"/ticket-activity/load-activity?activity=AddComment&ticketId={ticket}", headers=xhr)
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text)
    if st != 200 or not m:
        return st, ms, err or "no comment form"
    data = {"__RequestVerificationToken": m.group(1), "ticketId": str(ticket), "comment": f"cutover probe write {tick} {utc()}"}
    st2, final, text2, ms2, err = s.get("/ticket-activity/add-comment", data, headers=xhr)
    ok = st2 == 200 and not text2.strip()
    return st2, ms + ms2, None if ok else (err or f"not saved (HTTP {st2}, {len(text2)} bytes)")


def run(a):
    cred = load_env(a.env_file)
    ctx = ssl.create_default_context(cafile=a.cacert)
    out = open(a.out, "a", encoding="utf-8", buffering=1)
    cur, s, t_start, tick = None, None, time.monotonic(), 0

    lock = threading.Lock()

    def rec(**kw):
        with lock:
            out.write(json.dumps({"t": utc(), "tick": tick, "target": cur, **kw}) + "\n")

    def stopped():
        return (a.stop_file and os.path.exists(a.stop_file)) or (a.max_seconds and time.monotonic() - t_start > a.max_seconds)

    def writer():
        wcur, ws, wtick = None, None, 0
        while not stopped():
            t0 = time.monotonic()
            try:
                target = open(a.target_file, encoding="utf-8").read().strip()
            except OSError:
                target = wcur
            if target != wcur:
                wcur, ws = target, Session(target, ctx, a.timeout)
            if not ws.signed_in:
                st, ms, err = sign_in(ws, cred)
                with lock:
                    out.write(json.dumps({"t": utc(), "tick": wtick, "target": wcur, "path": "signin-writer", "status": st, "ms": ms,
                                          "ok": err is None, "error": err}) + "\n")
            if ws.signed_in:
                st, ms, err = write_comment(ws, a.write_ticket, wtick)
                if err and st in (302, 0):
                    ws.signed_in = False
            else:
                st, ms, err = 0, 0, "not signed in"
            with lock:
                out.write(json.dumps({"t": utc(), "tick": wtick, "target": wcur, "path": "write", "status": st, "ms": ms,
                                      "ok": err is None, "error": err}) + "\n")
            wtick += 1
            time.sleep(max(0.0, a.interval - (time.monotonic() - t0)))

    wt = threading.Thread(target=writer, daemon=True) if a.write_ticket else None
    if wt:
        wt.start()

    while not stopped():
        t0 = time.monotonic()
        try:
            target = open(a.target_file, encoding="utf-8").read().strip()
        except OSError:
            target = cur
        if target != cur:
            cur, s = target, Session(target, ctx, a.timeout)
            rec(path="switch", status=0, ms=0, ok=True, error=None)
        st, final, text, ms, err = s.get("/")
        ok = st == 200 and "TicketDesk" in text
        rec(path="/", status=st, ms=ms, ok=ok, error=None if ok else (err or "marker missing"))
        if not s.signed_in:
            st, ms, err = sign_in(s, cred)
            rec(path="signin", status=st, ms=ms, ok=err is None, error=err)
        if s.signed_in:
            st, final, text, ms, err = s.get("/tickets")
            ok = st == 200 and "/ticket/" in text and "/user/sign-in" not in final
            if st == 200 and not ok and "/user/sign-in" not in final and "/app/" in text:
                # the Angular page shell (new app): the ticket list comes from the API it calls
                st, final, text, ms2, err = s.get("/api/tickets?PageSize=5", headers={"Accept": "application/json"})
                ms += ms2
                ok = st == 200 and '"ticketId"' in text
            if not ok and "/user/sign-in" in final:
                s.signed_in = False
            rec(path="/tickets", status=st, ms=ms, ok=ok, error=None if ok else (err or "no ticket list"))
        else:
            rec(path="/tickets", status=0, ms=0, ok=False, error="not signed in")
        tick += 1
        time.sleep(max(0.0, a.interval - (time.monotonic() - t0)))
    if wt:
        wt.join(timeout=a.timeout * 3)
    out.close()
    summarize(a.out, None)


def parse_t(s):
    fmt = "%Y-%m-%dT%H:%M:%S.%fZ" if "." in s else "%Y-%m-%dT%H:%M:%SZ"
    return datetime.strptime(s, fmt).replace(tzinfo=timezone.utc)


def write_downtime(writes):
    """From the last successful write before the first failed one to the first successful write after the last
    failed one: the span in which a client could not write (None if no write failed)."""
    bad = [i for i, w in enumerate(writes) if not w["ok"]]
    if not bad:
        return None
    before = next((writes[i]["t"] for i in range(bad[0] - 1, -1, -1) if writes[i]["ok"]), None)
    after = next((writes[i]["t"] for i in range(bad[-1] + 1, len(writes)) if writes[i]["ok"]), None)
    span = round((parse_t(after) - parse_t(before)).total_seconds(), 1) if before and after else None
    return {"last_ok_before": before, "first_failed": writes[bad[0]]["t"], "last_failed": writes[bad[-1]]["t"],
            "first_ok_after": after, "seconds": span}


def summarize(path, marks_path):
    rows = [json.loads(l) for l in open(path, encoding="utf-8") if l.strip()]
    reads = [r for r in rows if r["path"] in ("/", "/tickets")]
    by = {}
    for r in reads:
        k = (r["target"], r["path"])
        b = by.setdefault(k, {"requests": 0, "failed": 0, "ms": []})
        b["requests"] += 1
        b["failed"] += 0 if r["ok"] else 1
        if r["ok"]:
            b["ms"].append(r["ms"])
    per = []
    for (target, p), b in sorted(by.items()):
        ms = sorted(b["ms"])
        p95 = ms[min(len(ms) - 1, int(round(0.95 * (len(ms) - 1))))] if ms else None
        per.append({"target": target, "path": p, "requests": b["requests"], "failed": b["failed"],
                    "median_ms": ms[len(ms) // 2] if ms else None, "p95_ms": p95})
    # failure windows: consecutive ticks with at least one failed read
    bad_ticks = sorted({(r["tick"], r["target"]) for r in reads if not r["ok"]})
    windows, prev = [], None
    for tick, target in bad_ticks:
        t = [r["t"] for r in reads if r["tick"] == tick and r["target"] == target]
        if prev and tick == prev[0] + 1 and target == prev[1]:
            windows[-1]["end"] = t[-1]
            windows[-1]["ticks"] += 1
        else:
            windows.append({"target": target, "start": t[0], "end": t[-1], "ticks": 1})
        prev = (tick, target)
    writes = [r for r in rows if r["path"] == "write"]
    write_windows, prev = [], None
    for r in writes:
        if r["ok"]:
            prev = None
            continue
        if prev is not None and r["tick"] == prev + 1:
            write_windows[-1]["end"] = r["t"]
            write_windows[-1]["ticks"] += 1
        else:
            write_windows.append({"target": r["target"], "start": r["t"], "end": r["t"], "ticks": 1})
        prev = r["tick"]
    wms = sorted(r["ms"] for r in writes if r["ok"])
    summary = {
        "file": os.path.basename(path), "start": rows[0]["t"] if rows else None, "end": rows[-1]["t"] if rows else None,
        "ticks": len({(r["tick"], r["target"]) for r in reads}), "read_requests": len(reads),
        "failed_reads": sum(1 for r in reads if not r["ok"]),
        "signins": sum(1 for r in rows if r["path"] == "signin"),
        "failed_signins": sum(1 for r in rows if r["path"] == "signin" and not r["ok"]),
        "switches": [{"t": r["t"], "target": r["target"]} for r in rows if r["path"] == "switch"],
        "per_target_path": per, "failure_windows": windows,
        "write_requests": len(writes), "failed_writes": sum(1 for r in writes if not r["ok"]),
        "write_median_ms": wms[len(wms) // 2] if wms else None, "write_failure_windows": write_windows,
        "write_downtime": write_downtime(writes),
        "failed": [{k: r[k] for k in ("t", "target", "path", "status", "ms", "error")} for r in rows if not r["ok"]][:200],
    }
    if marks_path:
        marks = json.load(open(marks_path, encoding="utf-8"))
        pts = sorted((parse_t(v), k) for k, v in marks.items()
                     if isinstance(v, str) and re.match(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", v))
        phases = []
        for i in range(len(pts) - 1):
            a, b = pts[i], pts[i + 1]
            inside = [r for r in reads if a[0] <= parse_t(r["t"]) < b[0]]
            winside = [r for r in writes if a[0] <= parse_t(r["t"]) < b[0]]
            phases.append({"from": a[1], "to": b[1], "seconds": round((b[0] - a[0]).total_seconds(), 1),
                           "read_requests": len(inside), "failed_reads": sum(1 for r in inside if not r["ok"]),
                           "write_requests": len(winside), "failed_writes": sum(1 for r in winside if not r["ok"])})
        summary["phases"] = phases
    print(json.dumps(summary, indent=2))
    return summary


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--target-file", required=True)
    r.add_argument("--out", required=True)
    r.add_argument("--stop-file")
    r.add_argument("--interval", type=float, default=1.0)
    r.add_argument("--max-seconds", type=float, default=0)
    r.add_argument("--timeout", type=float, default=10.0)
    r.add_argument("--write-ticket", type=int, default=0, help="also write: one comment per tick on this ticket")
    r.add_argument("--env-file", default=os.path.join(TD_W, ".secrets", "legacy-lab.env"))
    r.add_argument("--cacert", default=os.path.join(TD_W, ".secrets", "legacy-lab-ca", "ca.crt"))
    s = sub.add_parser("summarize")
    s.add_argument("file")
    s.add_argument("--marks")
    a = ap.parse_args()
    if a.cmd == "run":
        run(a)
    else:
        summarize(a.file, a.marks)
    return 0


if __name__ == "__main__":
    sys.exit(main())
