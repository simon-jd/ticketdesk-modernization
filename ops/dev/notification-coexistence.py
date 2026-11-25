#!/usr/bin/env python3
"""Ticket writes for the notification coexistence check (ops/dev/notification-coexistence.sh), through the gateway.

    python3 -I ops/dev/notification-coexistence.py write --base https://localhost:5444/ --secrets $TD_W/.secrets/legacy-lab.env \
        --owner ID --assignee ID --phase new|legacy --ticket ID --out writes-PHASE.json
    python3 -I ops/dev/notification-coexistence.py analyze --mail DIR --writes writes-new.json writes-legacy.json \
        --expect-recipients a@x,b@y --broadcast c@z --out result.json

write: signs in as the lab admin (help desk; credentials from the secrets file, never printed), creates a ticket (owner and
assignee given) and adds a comment to --ticket (or to the new ticket); records which app answered each post
(X-TicketDesk-Backend) and the ticket ids. analyze: reads the sink's .eml files; a mail is the worker's when it has
X-TicketDesk-Sender: worker, otherwise the legacy app's; checks that every write was mailed exactly once to each expected
recipient, by the app that made the write, and that nothing else was sent. The legacy app's new-ticket broadcast is expected
once per subscriber of the new ticket (its own bug L9; the worker sends it once). Exit 1 on any failed check.
"""
import argparse
import collections
import email
import email.policy
import glob
import http.cookiejar
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


class Browser:
    def __init__(self, base):
        self.base = base
        self.jar = http.cookiejar.CookieJar()
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE  # lab certificate, localhost only
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), urllib.request.HTTPSHandler(context=ctx), NoRedirect())

    def go(self, path, data=None, ajax=False):
        headers = {"User-Agent": "td-notification-coexistence/1.0", "Accept-Language": "en-US"}
        if ajax:
            headers["X-Requested-With"] = "XMLHttpRequest"
        req = urllib.request.Request(self.base + path.lstrip("/"), data=urllib.parse.urlencode(data).encode() if data is not None else None, headers=headers)
        try:
            r = self.opener.open(req, timeout=120)
            return r.status, dict(r.headers), r.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            return e.code, dict(e.headers), e.read().decode("utf-8", "replace")


def token(html):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]*)"', html) or re.search(r'value="([^"]*)"[^>]*name="__RequestVerificationToken"', html)
    return m.group(1) if m else None


def first_option(html, name):
    m = re.search(r'<select[^>]*name="%s"[^>]*>(.*?)</select>' % re.escape(name), html, re.S)
    if not m:
        return None
    opts = [v for v in re.findall(r'<option[^>]*value="([^"]*)"', m.group(1)) if v]
    return opts[0] if opts else None


def secrets(path):
    out = {}
    for line in open(path, encoding="utf-8"):
        if "=" in line and not line.lstrip().startswith("#"):
            k, v = line.rstrip("\n").split("=", 1)
            out[k.strip()] = v
    return out


def write(a):
    s = secrets(a.secrets)
    b = Browser(a.base)
    st, h, html = b.go("user/sign-in")
    st, h, _ = b.go("user/sign-in", {"__RequestVerificationToken": token(html) or "", "UserNameOrEmail": s["TD_ADMIN_EMAIL"], "Password": s["TD_ADMIN_PASSWORD"], "RememberMe": "false"})
    if st != 302:
        sys.exit(f"sign-in failed: {st}")
    writes = []
    st, h, html = b.go("ticket/new")
    if st != 200:
        sys.exit(f"ticket/new: {st}")
    form = {
        "__RequestVerificationToken": token(html) or "", "ProjectId": first_option(html, "ProjectId") or "1",
        "Title": f"Coexistence check ({a.phase} app): dock does not charge", "Details": f"Written through the gateway while the {a.phase} app answers ticket writes.",
        "IsHtml": "false", "TicketType": first_option(html, "TicketType") or "Problem", "Category": first_option(html, "Category") or "Hardware",
        "Priority": first_option(html, "Priority") or "Low", "Owner": a.owner, "AssignedTo": a.assignee, "TagList": "coexistence",
        "affectsCustomer": "false", "DueDateAsString": "", "TargetDateAsString": "", "ResolutionDateAsString": "",
    }
    m = re.search(r'name="tempId"[^>]*value="([^"]*)"', html)
    if m:
        form["tempId"] = m.group(1)
    st, h, _ = b.go("ticket/new", form)
    loc = h.get("Location", "")
    created = re.search(r"/ticket/(\d+)", loc)
    if st != 302 or not created:
        sys.exit(f"create failed: {st} {loc}")
    new_id = int(created.group(1))
    writes.append({"kind": "create", "ticket": new_id, "backend": h.get("X-TicketDesk-Backend", "legacy (not listed)"), "status": st})
    target = a.ticket or new_id
    st, h, html = b.go(f"ticket-activity/load-activity?ticketId={target}&activity=AddComment", ajax=True)
    act = re.search(r'<form[^>]*action="([^"]*add-comment[^"]*)"', html)
    if st != 200 or not act:
        sys.exit(f"load-activity: {st}")
    st, h, body = b.go(act.group(1).replace("&amp;", "&"), {"__RequestVerificationToken": token(html) or "", "comment": f"Comment written while the {a.phase} app answers ticket writes."}, ajax=True)
    if st != 200 or body.strip():
        sys.exit(f"add-comment failed: {st} {body[:200]}")
    writes.append({"kind": "comment", "ticket": target, "backend": h.get("X-TicketDesk-Backend", "legacy (not listed)"), "status": st})
    json.dump({"phase": a.phase, "writes": writes}, open(a.out, "w"), indent=2)
    print(json.dumps(writes))


def analyze(a):
    mails = []
    for f in sorted(glob.glob(os.path.join(a.mail, "*.eml"))):
        m = email.message_from_binary_file(open(f, "rb"), policy=email.policy.default)
        subject = str(m["Subject"] or "")
        t = re.search(r"Ticket #(\d+)", subject)
        mails.append({"file": os.path.basename(f), "sender": "worker" if m["X-TicketDesk-Sender"] == "worker" else "legacy",
                      "ticket": int(t.group(1)) if t else None, "new": subject.startswith("[New]"),
                      "to": str(m["To"].addresses[0].addr_spec).lower() if m["To"] else "", "events": m["X-TicketDesk-Events"]})
    writes = []
    for w in a.writes:
        d = json.load(open(w))
        for x in d["writes"]:
            x["app"] = "worker" if x["backend"].startswith("modern;") else "legacy"
            writes.append(x)
    recipients = [r.lower() for r in a.expect_recipients.split(",")]
    expected = collections.Counter()
    for w in writes:
        for r in recipients:
            expected[(w["app"], w["ticket"], r, False)] += 1
        if w["kind"] == "create":
            # L9: the legacy app sends the new-ticket broadcast once per notification row of the create event (one per
            # subscriber: here the owner and the assignee), the worker once per ticket
            expected[(w["app"], w["ticket"], a.broadcast.lower(), True)] += len(recipients) if w["app"] == "legacy" else 1
    actual = collections.Counter((m["sender"], m["ticket"], m["to"], m["new"]) for m in mails)
    checks = []
    def check(label, ok, **info):
        checks.append({"check": label, "ok": bool(ok), **info})
    check("every write mailed exactly once per expected recipient, by the app that made the write", actual == expected,
          missing=[list(k) for k in (expected - actual)], unexpected=[list(k) for k in (actual - expected)])
    # one write = one event: worker mails name their events; legacy mails do not, but each legacy-written ticket here has
    # one legacy write, so a second legacy mail to the same recipient for the same ticket would be a double send
    worker_keys = collections.Counter((m["ticket"], m["events"], m["to"]) for m in mails if m["sender"] == "worker")
    legacy_writes = collections.Counter(w["ticket"] for w in writes if w["app"] == "legacy")
    legacy_keys = collections.Counter((m["ticket"], m["to"], m["new"]) for m in mails if m["sender"] == "legacy")
    legacy_broadcast = len(recipients)  # L9, see above
    doubles = [list(k) for k, v in worker_keys.items() if v > 1] + \
              [list(k) for k, v in legacy_keys.items() if v > legacy_writes[k[0]] * (legacy_broadcast if k[2] else 1)]
    check("no recipient got the same write twice (no double send)", not doubles, doubles=doubles)
    worker_tickets = {w["ticket"] for w in writes if w["app"] == "worker"}
    legacy_tickets = {w["ticket"] for w in writes if w["app"] == "legacy"}
    check("both apps wrote and both sent", worker_tickets and legacy_tickets and any(m["sender"] == "worker" for m in mails) and any(m["sender"] == "legacy" for m in mails))
    result = {"writes": writes, "mails": mails, "counts": {"total": len(mails), "worker": sum(m["sender"] == "worker" for m in mails),
              "legacy": sum(m["sender"] == "legacy" for m in mails), "expected": sum(expected.values())}, "checks": checks}
    json.dump(result, open(a.out, "w"), indent=2)
    for c in checks:
        print(("PASS " if c["ok"] else "FAIL ") + c["check"])
    print(json.dumps(result["counts"]))
    return 0 if all(c["ok"] for c in checks) else 1


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    w = sub.add_parser("write")
    w.add_argument("--base", required=True); w.add_argument("--secrets", required=True); w.add_argument("--owner", required=True)
    w.add_argument("--assignee", required=True); w.add_argument("--phase", required=True); w.add_argument("--ticket", type=int); w.add_argument("--out", required=True)
    z = sub.add_parser("analyze")
    z.add_argument("--mail", required=True); z.add_argument("--writes", nargs="+", required=True)
    z.add_argument("--expect-recipients", required=True); z.add_argument("--broadcast", required=True); z.add_argument("--out", required=True)
    a = ap.parse_args()
    if a.cmd == "write":
        write(a)
        return 0
    return analyze(a)


if __name__ == "__main__":
    sys.exit(main())
