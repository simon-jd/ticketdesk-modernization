#!/usr/bin/env python3
"""Shared sign-in probe: a cookie issued by one app is accepted by the other, and signing out of one ends both.

    python3 -I ops/dev/shared-signin-probe.py --gateway https://localhost:5443/ --legacy https://localhost:8443/ \
        --secrets $TD_W/.secrets/legacy-lab.env --out DIR/probe.json

Needs the gateway (new app) running with Authentication:SharedCookie on and the wave-5 routes migrated, and the legacy
app deployed with ticketdesk:SharedCookieKeyRing=database. Credentials come from the secrets file (TD_ADMIN_EMAIL,
TD_ADMIN_PASSWORD) and are never printed; the admin's name is shown as {admin}. Exit 1 if a check fails.

A: sign in on the new app -> the legacy app (directly on its own port, and through the gateway for a route it still
   serves) shows the signed-in user; sign out on the new app -> the legacy app treats the browser as anonymous.
B: sign in on the legacy app (directly) -> new-app pages show the user; sign out on the legacy app -> the new app treats
   the browser as anonymous.
"""
import argparse
import http.cookiejar
import json
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

LEGACY_PAGE = "ticket/new"        # served by the legacy app (wave 4, not migrated here); needs a signed-in internal user
NEW_PAGE = "account/manage"       # served by the new app (wave 2); needs a signed-in user
COOKIE = ".AspNet.ApplicationCookie"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


def secrets(path):
    out = {}
    for line in open(path, encoding="utf-8"):
        if "=" in line and not line.lstrip().startswith("#"):
            k, v = line.rstrip("\n").split("=", 1)
            out[k.strip()] = v
    return out


class Browser:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE  # lab certificate; the probe only talks to localhost
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar),
                                                  urllib.request.HTTPSHandler(context=ctx), NoRedirect())

    def go(self, url, data=None):
        req = urllib.request.Request(url, data=urllib.parse.urlencode(data).encode() if data else None,
                                     headers={"User-Agent": "td-shared-signin-probe/1.0", "Accept-Language": "en-US"})
        try:
            r = self.opener.open(req, timeout=60)
            return r.status, dict(r.headers), r.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            return e.code, dict(e.headers), e.read().decode("utf-8", "replace")

    def has_cookie(self):
        return any(c.name == COOKIE or c.name.startswith(COOKIE + "C") for c in self.jar)


def token(html, action_part):
    m = re.search(r'<form action="[^"]*' + re.escape(action_part) + r'[^"]*"[^>]*>.*?name="__RequestVerificationToken" type="hidden" value="([^"]+)"', html, re.S)
    return m.group(1) if m else None


def backend(headers):
    return headers.get("X-TicketDesk-Backend", "legacy (no marker)")


def page_user(html, name):
    """The user menu: the display name when signed in, "Account" when anonymous."""
    if name and name in html:
        return "{admin}"
    m = re.search(r'<i class="fa fa-user"></i><span class="">\s*([^<]+?)\s*</span>', html)
    return m.group(1) if m else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--gateway", default="https://localhost:5443/")
    ap.add_argument("--legacy", default="https://localhost:8443/")
    ap.add_argument("--secrets", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    s = secrets(a.secrets)
    user, password, name = s["TD_ADMIN_EMAIL"], s["TD_ADMIN_PASSWORD"], s.get("TD_ADMIN_NAME")
    steps, failures = [], []

    def check(label, ok, **info):
        steps.append({"step": label, "ok": bool(ok), **info})
        if not ok:
            failures.append(label)
        print(f"{'ok  ' if ok else 'FAIL'} {label} {json.dumps(info)}")

    def sign_in(b, base, who):
        st, h, html = b.go(base + "user/sign-in")
        t = token(html, "/user/sign-in")
        check(f"{who}: sign-in page", st == 200 and t, status=st, backend=backend(h))
        st, h, _ = b.go(base + "user/sign-in", {"__RequestVerificationToken": t or "", "UserNameOrEmail": user, "Password": password, "RememberMe": "false"})
        check(f"{who}: sign-in post issues {COOKIE}", st == 302 and b.has_cookie(), status=st, backend=backend(h),
              set_cookie=[c.split("=")[0] for c in h.get("Set-Cookie", "").split(", ") if "=" in c])

    def signed_in(b, url, who, expect):
        st, h, html = b.go(url)
        u = page_user(html, name) if st == 200 else None
        ok = (st == 200 and u == "{admin}") if expect else (st == 302 and "/user/sign-in" in h.get("Location", ""))
        check(f"{who}: GET {url} {'signed in' if expect else 'anonymous (challenge)'}", ok, status=st, backend=backend(h), user=u,
              location=h.get("Location"))

    # A: new app issues, legacy accepts
    b = Browser()
    sign_in(b, a.gateway, "A new app")
    signed_in(b, a.legacy + LEGACY_PAGE, "A legacy direct", True)
    signed_in(b, a.gateway + LEGACY_PAGE, "A legacy via gateway", True)
    signed_in(b, a.gateway + NEW_PAGE, "A new app", True)
    st, h, _ = b.go(a.gateway + "user/sign-out")
    check("A new app: sign-out deletes the cookie", st == 302 and not b.has_cookie(), status=st, backend=backend(h))
    signed_in(b, a.legacy + LEGACY_PAGE, "A legacy direct after sign-out", False)
    signed_in(b, a.gateway + NEW_PAGE, "A new app after sign-out", False)

    # B: legacy issues, new app accepts
    b = Browser()
    sign_in(b, a.legacy, "B legacy direct")
    signed_in(b, a.gateway + NEW_PAGE, "B new app", True)
    signed_in(b, a.gateway + "admin/users", "B new app (admin role)", True)
    signed_in(b, a.legacy + LEGACY_PAGE, "B legacy direct", True)
    st, h, _ = b.go(a.legacy + "user/sign-out")
    check("B legacy direct: sign-out deletes the cookie", st == 302 and not b.has_cookie(), status=st)
    signed_in(b, a.gateway + NEW_PAGE, "B new app after sign-out", False)
    signed_in(b, a.legacy + LEGACY_PAGE, "B legacy direct after sign-out", False)

    json.dump({"gateway": a.gateway, "legacy": a.legacy, "legacy_page": LEGACY_PAGE, "new_page": NEW_PAGE,
               "checks": len(steps), "failed": failures, "steps": steps}, open(a.out, "w"), indent=2)
    print(f"{len(steps) - len(failures)}/{len(steps)} checks passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
