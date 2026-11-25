#!/usr/bin/env python3
"""Entra ID sign-in probe against the lab: OIDC sign-in on the new app, then the legacy app accepts the cookie for the
account the first sign-in provisioned (or linked).

    python3 -I ops/dev/entra-signin-probe.py --login-hint entra.helpdesk@lab.test --name "Entra Helpdesk" --out DIR/entra-probe.json

Needs: the gateway with --shared-cookie and --entra pointing at tools/TestIdp (td-test-idp, which picks the account by
login_hint without a browser), the legacy app on the shared-cookie state (baseline-s-identity). With the real tenant the
browser steps need the user (see the identity handoff). Exit 1 if a check fails.
"""
import argparse
import html
import http.cookiejar
import json
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

COOKIE = ".AspNet.ApplicationCookie"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--gateway", default="https://localhost:5443/")
    ap.add_argument("--legacy", default="https://localhost:8443/")
    ap.add_argument("--login-hint", required=True)
    ap.add_argument("--name", required=True, help="display name the pages must show")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE  # lab certificate, localhost only
    jar = http.cookiejar.CookieJar()
    op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), urllib.request.HTTPSHandler(context=ctx), NoRedirect())
    steps, failures = [], []

    def go(url, data=None):
        req = urllib.request.Request(url, data=urllib.parse.urlencode(data).encode() if data is not None else None,
                                     headers={"User-Agent": "td-entra-probe/1.0", "Accept-Language": "en-US"})
        try:
            r = op.open(req, timeout=60)
            return r.status, dict(r.headers), r.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            return e.code, dict(e.headers), e.read().decode("utf-8", "replace")

    def check(label, ok, **info):
        steps.append({"step": label, "ok": bool(ok), **info})
        if not ok:
            failures.append(label)
        print(f"{'ok  ' if ok else 'FAIL'} {label} {json.dumps(info)}")

    has_cookie = lambda: any(c.name == COOKIE for c in jar)
    st, h, body = go(a.gateway + "user/sign-in")
    m = re.search(r'id="entraSignIn"><input name="__RequestVerificationToken" type="hidden" value="([^"]+)"', body)
    check("sign-in page offers Entra ID", st == 200 and m, status=st, backend=h.get("X-TicketDesk-Backend"))
    st, h, _ = go(a.gateway + "user/sign-in/entra", {"__RequestVerificationToken": m.group(1) if m else ""})
    authorize = h.get("Location", "")
    check("challenge redirects to the identity provider with PKCE", st == 302 and "code_challenge_method=S256" in authorize, status=st,
          authorize=authorize.split("?")[0])
    st, _, body = go(authorize + "&login_hint=" + urllib.parse.quote(a.login_hint))
    fp = re.search(r'<form method="post" action="([^"]+)"><input type="hidden" name="code" value="([^"]+)"><input type="hidden" name="state" value="([^"]+)">', body)
    check("identity provider answers with form_post", st == 200 and fp, status=st)
    st, h, _ = go(html.unescape(fp.group(1)), {"code": fp.group(2), "state": html.unescape(fp.group(3))})
    check("callback /signin-oidc accepted", st == 302 and "/user/sign-in/entra/callback" in h.get("Location", ""), status=st, location=h.get("Location"))
    st, h, _ = go(urllib.parse.urljoin(a.gateway, h.get("Location", "")))
    check(f"account linked/provisioned and {COOKIE} issued", st == 302 and has_cookie(), status=st, location=h.get("Location"))
    for label, url in [("new app page", a.gateway + "account/manage"), ("legacy page direct", a.legacy + "ticket/new"),
                       ("legacy page via gateway", a.gateway + "ticket/new")]:
        st, h, body = go(url)
        check(f"{label} shows the Entra user", st == 200 and a.name in body, status=st, url=url, backend=h.get("X-TicketDesk-Backend", "legacy (no marker)"))
    st, _, _ = go(a.legacy + "user/sign-out")
    check("sign-out on the legacy app", st == 302 and not has_cookie(), status=st)
    st, h, _ = go(a.gateway + "account/manage")
    check("new app anonymous after the legacy sign-out", st == 302 and "/user/sign-in" in h.get("Location", ""), status=st)
    json.dump({"login_hint": a.login_hint, "checks": len(steps), "failed": failures, "steps": steps}, open(a.out, "w"), indent=2)
    print(f"{len(steps) - len(failures)}/{len(steps)} checks passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
