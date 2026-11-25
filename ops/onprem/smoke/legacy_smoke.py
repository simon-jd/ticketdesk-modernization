#!/usr/bin/env python3
"""HTTP driver for the legacy TicketDesk site (stdlib only; run with python3 -I).

  legacy_smoke.py setup  --base URL   first-run setup: create the database and the admin user
  legacy_smoke.py demo   --base URL   sign in as admin and create TicketDesk's demo data
  legacy_smoke.py smoke  --base URL   home page, sign in, create a ticket, view it, add a comment
  legacy_smoke.py wait   --base URL   read-only readiness check: home page and sign-in as admin
  legacy_smoke.py reindex --base URL  sign in as admin and rebuild the Lucene index (admin/search-manager)

Credentials come from an env file (default $TD_W/.secrets/legacy-lab.env: TD_ADMIN_EMAIL,
TD_ADMIN_PASSWORD, TD_ADMIN_NAME). TLS is verified against the lab CA (--cacert) unless --insecure.
Every request is logged (method, path, status, ms) to stdout and the run ends with a JSON summary;
exit code 0 only when every check passed.
"""
import argparse
import html.parser
import http.cookiejar
import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

TD_W = os.environ.get("TD_W", os.path.expanduser("~/Desktop/ticketdesk-modernization"))


class Form:
    def __init__(self, attrs):
        self.action = attrs.get("action", "")
        self.method = attrs.get("method", "get").lower()
        self.id = attrs.get("id", "")
        self.fields = {}      # name -> value
        self.selects = {}     # name -> [option values]


class FormParser(html.parser.HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.forms, self.loose = [], Form({})
        self._form, self._select, self._textarea, self._opt = None, None, None, None
        self.title, self._in_title = "", False

    def _cur(self):
        return self._form or self.loose

    def handle_starttag(self, tag, a):
        a = {k: (v if v is not None else "") for k, v in a}
        if tag == "form":
            self._form = Form(a)
            self.forms.append(self._form)
        elif tag == "input" and a.get("name"):
            t = a.get("type", "text").lower()
            if t in ("checkbox", "radio") and "checked" not in a:
                self._cur().fields.setdefault(a["name"], None)
                return
            if t in ("submit", "button", "image"):
                return
            # first value wins (MVC checkboxes emit a hidden "false" after the checkbox)
            if self._cur().fields.get(a["name"]) is None:
                self._cur().fields[a["name"]] = a.get("value", "")
        elif tag == "select" and a.get("name"):
            self._select = a["name"]
            self._cur().selects[self._select] = []
            self._cur().fields.setdefault(self._select, None)
        elif tag == "option" and self._select:
            v = a.get("value")
            self._opt = v
            if v is not None:
                self._cur().selects[self._select].append(v)
                if "selected" in a:
                    self._cur().fields[self._select] = v
        elif tag == "textarea" and a.get("name"):
            self._textarea = a["name"]
            self._cur().fields[self._textarea] = ""
        elif tag == "title":
            self._in_title = True

    def handle_endtag(self, tag):
        if tag == "form":
            self._form = None
        elif tag == "select":
            self._select = None
        elif tag == "textarea":
            self._textarea = None
        elif tag == "title":
            self._in_title = False

    def handle_data(self, data):
        if self._textarea:
            self._cur().fields[self._textarea] += data
        if self._in_title:
            self.title += data


def parse(body):
    p = FormParser()
    p.feed(body)
    return p


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


class Client:
    def __init__(self, base, cacert, insecure, timeout):
        self.base = base.rstrip("/")
        ctx = ssl.create_default_context(cafile=None if insecure else cacert)
        if insecure:
            ctx.check_hostname, ctx.verify_mode = False, ssl.CERT_NONE
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=ctx), urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())
        self.timeout = timeout
        self.log = []

    def req(self, method, path, data=None, headers=None, follow=True):
        url = path if path.startswith("http") else self.base + path
        for _ in range(10):
            body = urllib.parse.urlencode(data).encode() if data is not None else None
            r = urllib.request.Request(url, data=body, method=method, headers=headers or {})
            if body is not None:
                r.add_header("Content-Type", "application/x-www-form-urlencoded")
            t0 = time.monotonic()
            try:
                resp = self.opener.open(r, timeout=self.timeout)
                status, hdrs, raw = resp.status, resp.headers, resp.read()
            except urllib.error.HTTPError as e:
                status, hdrs, raw = e.code, e.headers, e.read()
            ms = round((time.monotonic() - t0) * 1000)
            loc = hdrs.get("Location")
            entry = {"method": method, "url": url[len(self.base):] if url.startswith(self.base) else url,
                     "status": status, "ms": ms}
            if loc:
                entry["location"] = loc
            self.log.append(entry)
            print(f"  {method:4} {entry['url']} -> {status} {ms}ms" + (f" -> {loc}" if loc else ""), flush=True)
            if follow and status in (301, 302, 303, 307) and loc:
                url = urllib.parse.urljoin(url, loc)
                if status != 307:
                    method, data = "GET", None
                continue
            return status, raw.decode("utf-8", "replace"), url, loc
        raise RuntimeError("too many redirects")


def load_env(path):
    env = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip().strip("'\"")
    return env


class Run:
    def __init__(self, name):
        self.name, self.checks, self.t0 = name, [], time.monotonic()

    def check(self, label, ok, detail=""):
        self.checks.append({"check": label, "ok": bool(ok), "detail": detail})
        print(f"[{'PASS' if ok else 'FAIL'}] {label}" + (f" ({detail})" if detail else ""), flush=True)
        if not ok:
            raise AssertionError(label)


def validation_errors(body):
    """MVC validation messages in a returned form (for failure details)."""
    msgs = re.findall(r'class="field-validation-error[^"]*"[^>]*>(?:<span[^>]*>)?([^<]+)', body)
    msgs += re.findall(r'validation-summary-errors[^>]*>\s*<ul>(.*?)</ul>', body, re.S)
    return "; ".join(re.sub(r"<[^>]+>", " ", m).strip() for m in msgs)[:500]


def antiforgery(form):
    return form.fields.get("__RequestVerificationToken")


def sign_in(c, run, email, password):
    st, body, url, _ = c.req("GET", "/user/sign-in")
    run.check("sign-in page", st == 200 and "__RequestVerificationToken" in body, f"HTTP {st}")
    p = parse(body)
    form = next(f for f in p.forms if "sign-in" in f.action.lower())
    data = {k: v for k, v in form.fields.items() if v is not None}
    for k in list(data):
        if k.lower() in ("usernameoremail", "username", "email"):
            data[k] = email
        if k.lower() == "password":
            data[k] = password
        if k.lower() == "rememberme":
            data[k] = "false"
    st, body, url, _ = c.req("POST", form.action or "/user/sign-in", data)
    signed = st == 200 and "sign-out" in body.lower()
    run.check("sign in as admin", signed, f"HTTP {st}, landed on {url[len(c.base):]}")


def cmd_setup(c, run, cred):
    st, body, url, _ = c.req("GET", "/")
    p = parse(body)
    form = next((f for f in p.forms if "create-database" in f.action), None)
    if form is None:
        run.check("first-run setup page offers create-database", False,
                  f"HTTP {st} at {url[len(c.base):]}, title={p.title.strip()!r}")
    run.check("first-run setup page", True, f"redirected to {url[len(c.base):]}")
    data = {"__RequestVerificationToken": antiforgery(form), "Email": cred["TD_ADMIN_EMAIL"],
            "Password": cred["TD_ADMIN_PASSWORD"], "DisplayName": cred.get("TD_ADMIN_NAME", "TicketDesk Admin")}
    st, body, url, _ = c.req("POST", form.action, data)
    run.check("create database + admin", st == 200 and "create-database" not in body, f"HTTP {st}")
    st, body, url, _ = c.req("GET", "/")
    run.check("home page after setup", st == 200 and "first-run-setup" not in url, f"HTTP {st} {url[len(c.base):]}")


def cmd_demo(c, run, cred):
    sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])
    st, body, url, _ = c.req("GET", "/admin/data-management/create-demo-data")
    run.check("create demo data", st == 200, f"HTTP {st}")
    time.sleep(5)  # the action unloads the app domain afterwards
    for _ in range(24):
        st, body, url, _ = c.req("GET", "/tickets")
        if st == 200:
            break
        time.sleep(5)
    run.check("ticket center after demo data", st == 200 and "/ticket/" in body, f"HTTP {st}")


def cmd_smoke(c, run, cred):
    st, body, url, _ = c.req("GET", "/")
    p = parse(body)
    run.check("home page over HTTPS", st == 200 and "TicketDesk" in body, f"HTTP {st}, title={p.title.strip()!r}")
    sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])

    st, body, url, _ = c.req("GET", "/ticket/new")
    run.check("new ticket form", st == 200 and "__RequestVerificationToken" in body, f"HTTP {st}")
    p = parse(body)
    form = next(f for f in p.forms if f.action.lower().rstrip("/").endswith("/ticket/new"))
    # like a browser: unchecked checkboxes are not sent; empty selects get their first real option
    data = {k: v for k, v in form.fields.items() if v is not None or k in form.selects}
    for name, opts in form.selects.items():
        if not data.get(name):
            non_empty = [o for o in opts if o]
            data[name] = non_empty[0] if non_empty else ""
    stamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    title = f"Smoke test ticket {stamp}"
    data["Title"] = title
    data["Details"] = f"Created by ops/onprem/smoke/legacy_smoke.py at {stamp}."
    if "TagList" in data and not data["TagList"]:
        data["TagList"] = "smoke"
    st, body, url, loc = c.req("POST", form.action, data)
    m = re.search(r"/ticket/(\d+)$", urllib.parse.urlparse(url).path)
    run.check("create ticket", st == 200 and m is not None,
              f"HTTP {st}, landed on {url[len(c.base):]}" + ("" if m else f", errors: {validation_errors(body)}, sent: {sorted(data)}"))
    tid = int(m.group(1))

    st, body, url, _ = c.req("GET", f"/ticket/{tid}")
    run.check("view ticket", st == 200 and title in body, f"ticket #{tid}")

    st, body, url, _ = c.req("GET", f"/ticket-activity/load-activity?activity=AddComment&ticketId={tid}",
                             headers={"X-Requested-With": "XMLHttpRequest"})
    p = parse(body)
    form = next((f for f in p.forms if "add-comment" in f.action), None) or p.loose
    token = antiforgery(form)
    run.check("comment form", st == 200 and token, f"HTTP {st}")
    comment = f"Smoke comment {stamp}"
    data = {k: (v or "") for k, v in form.fields.items()}
    data.update({"__RequestVerificationToken": token, "ticketId": str(tid), "comment": comment})
    st, body, url, _ = c.req("POST", "/ticket-activity/add-comment", data,
                             headers={"X-Requested-With": "XMLHttpRequest"})
    run.check("add comment", st == 200, f"HTTP {st}, {len(body)} bytes")

    st, body, url, _ = c.req("GET", f"/ticket/ticket-events?ticketId={tid}",
                             headers={"X-Requested-With": "XMLHttpRequest"})
    run.check("comment visible in ticket events", st == 200 and comment in body, f"HTTP {st}")
    return {"ticket_id": tid, "title": title, "comment": comment}


def cmd_wait(c, run, cred):
    st, body, url, _ = c.req("GET", "/")
    run.check("home page", st == 200 and "TicketDesk" in body, f"HTTP {st}")
    sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])


def cmd_reindex(c, run, cred):
    sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])
    t0 = time.monotonic()
    st, body, url, _ = c.req("GET", "/admin/search-manager/populate-index")
    run.check("populate search index", st == 200 and "search-manager" in url, f"HTTP {st}")
    secs = round(time.monotonic() - t0, 1)
    st, body, url, _ = c.req("GET", "/search?term=printer")
    hits = len(set(re.findall(r'href="/ticket/(\d+)"', body)))
    run.check("search finds tickets", st == 200 and hits > 0, f"HTTP {st}, {hits} ticket links for 'printer'")
    return {"populate_seconds": secs, "printer_hits": hits}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=["setup", "demo", "smoke", "wait", "reindex"])
    ap.add_argument("--base", default="https://localhost:8443")
    ap.add_argument("--env-file", default=os.path.join(TD_W, ".secrets", "legacy-lab.env"))
    ap.add_argument("--cacert", default=os.path.join(TD_W, ".secrets", "legacy-lab-ca", "ca.crt"))
    ap.add_argument("--insecure", action="store_true")
    ap.add_argument("--timeout", type=float, default=600)
    ap.add_argument("--json", help="write the summary JSON here")
    a = ap.parse_args()
    cred = load_env(a.env_file)
    c = Client(a.base, a.cacert, a.insecure, a.timeout)
    run = Run(a.command)
    extra, ok = {}, True
    print(f"legacy_smoke {a.command} against {a.base} at {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
    try:
        extra = {"setup": cmd_setup, "demo": cmd_demo, "smoke": cmd_smoke, "wait": cmd_wait,
                 "reindex": cmd_reindex}[a.command](c, run, cred) or {}
    except Exception as e:  # noqa: BLE001 - report any failure in the summary
        ok = False
        if not isinstance(e, AssertionError):
            run.checks.append({"check": "exception", "ok": False, "detail": repr(e)})
            print(f"[FAIL] exception: {e!r}")
    summary = {"command": a.command, "base": a.base, "ok": ok and all(x["ok"] for x in run.checks),
               "checks_passed": sum(x["ok"] for x in run.checks), "checks_total": len(run.checks),
               "requests": len(c.log), "seconds": round(time.monotonic() - run.t0, 1),
               "checks": run.checks, "requests_log": c.log, **extra}
    out = json.dumps(summary, indent=2)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            f.write(out + "\n")
    print(json.dumps({k: summary[k] for k in ("command", "ok", "checks_passed", "checks_total", "requests", "seconds")}))
    return 0 if summary["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
