#!/usr/bin/env python3
"""signin_all.py - sign each user of a users file in once (the new app rehashes a V2 hash to V3 at the first password
sign-in). The password is read from an env file by name and never printed.

  signin_all.py --base URL --users users.json --password-env FILE:KEY [--cacert CA]
"""
import argparse, http.cookiejar, json, os, re, ssl, sys, time, urllib.parse, urllib.request

TD_W = os.path.expanduser("~/Desktop/ticketdesk-modernization")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--users", required=True)
    ap.add_argument("--password-env", required=True)
    ap.add_argument("--cacert", default=f"{TD_W}/.secrets/legacy-lab-ca/ca.crt")
    a = ap.parse_args()
    f, key = a.password_env.split(":")
    pw = next(l.rstrip("\n").split("=", 1)[1] for l in open(f) if l.startswith(key + "="))
    ctx = ssl.create_default_context(cafile=a.cacert)
    ok = 0; t0 = time.time()
    users = json.load(open(a.users))
    for u in users:
        jar = http.cookiejar.CookieJar()
        op = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ctx), urllib.request.HTTPCookieProcessor(jar), NoRedirect)
        page = op.open(f"{a.base}/user/sign-in", timeout=30).read().decode()
        tok = re.search(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"', page).group(1)
        data = urllib.parse.urlencode({"__RequestVerificationToken": tok, "UserNameOrEmail": u, "Password": pw, "RememberMe": "false"}).encode()
        try:
            op.open(f"{a.base}/user/sign-in", data=data, timeout=30)
            status = 200
        except urllib.error.HTTPError as e:
            status = e.code; loc = e.headers.get("Location")
        if status == 302 and loc == "/":
            ok += 1
        else:
            print(f"sign-in failed for user #{users.index(u)}: HTTP {status}", file=sys.stderr)
    print(f"signed in {ok} of {len(users)} users in {time.time() - t0:.1f} s")
    sys.exit(0 if ok == len(users) else 1)


if __name__ == "__main__":
    main()
