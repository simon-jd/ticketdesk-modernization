#!/usr/bin/env python3
"""Summarize a replay through the gateway: which app answered each exchange, per wave, and every mismatch with its
expected-differences id (or UNEXPLAINED).

    python3 -I ops/dev/replay-summary.py --candidate gateway.jsonl --report compare/report.json \
        --routes docs/migration/routes.csv --explain docs/migration/replay-explanations.json --out summary.md

The new app marks its answers with `X-TicketDesk-Backend: modern; route=<id>` (`; ui=angular` when its Angular front end
answered: "answered by" angular, explained only by rules with `"answered_by": "angular"`); a migrated route that fell through
to the legacy app for one request says `legacy; route=<id>; reason=<switched-back|group|identity>`; plain legacy routes carry nothing.
--standalone (the new app alone, Round 4 on): there is no legacy app, so an exchange without a marker (a path the inventory does
not list, e.g. a 404) is the new app's too and is counted as "new (no marker)".
Exit 1 when a mismatch is unexplained.
"""
import argparse
import collections
import csv
import json
import re
import sys


def load_routes(path):
    rows = list(csv.DictReader(open(path, encoding="utf-8")))
    by_template = collections.defaultdict(list)
    for r in rows:
        by_template["/" + r["route"].strip("/")].append(r)
    return {r["id"]: r for r in rows}, by_template


def route_of(route_key, by_template):
    """td-replay route key ("GET /tickets/{listName?}/{page:int?}") -> routes.csv row."""
    verb, _, template = route_key.partition(" ")
    for r in by_template.get(template, []):
        if r["verb"] in ("ANY", verb) or verb in r["verb"].split("|"):
            return r
    return None


def diff_matches(rule, d):
    """One explanation rule against one diff entry of td-replay's report (kind, path, expected, actual)."""
    m = rule.get("diff", {})
    if "kind" in m and d["kind"] != m["kind"]:
        return False
    if "kind_prefix" in m and not d["kind"].startswith(m["kind_prefix"]):
        return False
    if "path" in m and d["path"] != m["path"]:
        return False
    if "path_regex" in m and not re.search(m["path_regex"], d["path"]):
        return False
    actual = d.get("actual") or ""
    if "actual_prefix" in m and not actual.startswith(m["actual_prefix"]):
        return False
    if "actual_in" in m and actual not in m["actual_in"]:
        return False
    if "actual_contains" in m and m["actual_contains"] not in actual:
        return False
    if "actual_regex" in m and not re.search(m["actual_regex"], actual):
        return False
    if "expected_in" in m and d.get("expected") not in m["expected_in"]:
        return False
    if "equivalent" in m and not equivalent(m["equivalent"], d.get("expected") or "", actual):
        return False
    return True


# R1 (sanitizer serialization): the legacy HtmlSanitizer 3.2 wrote URL attributes in System.Uri form and style values as
# written; the current one keeps URLs as written and serializes styles through AngleSharp.Css. Equivalent means: the same
# value once both are written in one form. Anything else (a dropped or added attribute, another value) stays unexplained.
_NAMED = {"black": "0, 0, 0", "white": "255, 255, 255", "red": "255, 0, 0", "lime": "0, 255, 0", "blue": "0, 0, 255",
          "yellow": "255, 255, 0", "aqua": "0, 255, 255", "fuchsia": "255, 0, 255", "silver": "192, 192, 192",
          "gray": "128, 128, 128", "maroon": "128, 0, 0", "olive": "128, 128, 0", "green": "0, 128, 0",
          "purple": "128, 0, 128", "teal": "0, 128, 128", "navy": "0, 0, 128", "orange": "255, 165, 0"}


def url_form(v):
    import urllib.parse as up
    try:
        u = up.urlsplit(v.strip())
    except ValueError:
        return v
    if u.scheme.lower() not in ("http", "https") or not u.netloc:
        return v
    path = up.quote(up.unquote(u.path or "/"), safe="/:@!$&'()*+,;=-._~")
    query = up.quote(up.unquote(u.query), safe="=&:@!$'()*+,;/?-._~")
    frag = up.quote(up.unquote(u.fragment), safe="=&:@!$'()*+,;/?-._~")
    return up.urlunsplit((u.scheme.lower(), u.netloc.lower(), path, query, frag))


def style_form(v):
    s = re.sub(r"\brgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", r"rgba(\1, \2, \3, 1)", v)
    s = re.sub(r"(?<=:)\s*([a-z]+)(?=\s*(;|$))", lambda m: " rgba(%s, 1)" % _NAMED[m.group(1)] if m.group(1) in _NAMED else m.group(0), s)
    s = re.sub(r"(?<![\d.])0px\b", "0", s)
    return re.sub(r"\s+", " ", s).strip().rstrip(";")


# I1 (round 4): the sign-in cookie is the app's own Identity cookie, no longer the legacy OWIN one (shared-cookie mode). Equivalent
# means: the same Set-Cookie headers once the sign-in cookie of either name (with its attributes) is set aside; every other
# cookie of the exchange must be the same.
_SIGN_IN_COOKIES = (".aspnet.applicationcookie", ".aspnetcore.identity.application")


def cookie_form(v):
    entries = [e.strip() for e in v.split(" | ") if e.strip()]
    return sorted("<sign-in cookie>" if e.split(";")[0].strip().lower() in _SIGN_IN_COOKIES else e for e in entries)


# S3 (Round 5): a link that changed state (sign-out, language, project menus, the reset dialog) became a form that posts to
# the same URL with the same text, a button (td-replay's element description: "<a ... href="URL">TEXT" against
# "<form action="URL" ... method="post">TEXT"). Equivalent means: same URL, same text, the form posts. Anything else (another
# URL, another text, a GET form) stays unexplained.
_LINK = re.compile(r'^<a (?:[^>]* )?href="([^"]*)"[^>]*>(.*)$', re.S)
_POST_FORM = re.compile(r'^<form action="([^"]*)"[^>]* method="post"[^>]*>(.*)$', re.S)


def link_to_post(expected, actual):
    a, f = _LINK.match(expected), _POST_FORM.match(actual)
    return bool(a and f and a.group(1) == f.group(1) and a.group(2).strip() == f.group(2).strip())


# the antiforgery cookie of either app: the new app's (.TicketDesk.Antiforgery) and MVC 5's (__RequestVerificationToken), which the
# legacy app set on the first page with a form and the new app replaces with its own on the first page with the layout (S3)
_ANTIFORGERY_COOKIES = (".TicketDesk.Antiforgery;", "__RequestVerificationToken;")


def without_antiforgery_cookie(v):
    return sorted(e.strip() for e in v.split(" | ") if e.strip() and not e.strip().startswith(_ANTIFORGERY_COOKIES))


def equivalent(kind, expected, actual):
    if expected == actual:
        return False  # not a difference
    if kind == "link-to-post":
        return link_to_post(expected, actual)
    if kind == "antiforgery-cookie":  # S3: the same cookies once the antiforgery cookies (either app's) are set aside
        return without_antiforgery_cookie(expected) == without_antiforgery_cookie(actual)
    if kind == "identity-cookie":
        return cookie_form(expected) == cookie_form(actual)
    if kind == "url":
        return url_form(expected) == url_form(actual)
    if kind == "style":
        return style_form(expected) == style_form(actual)
    raise ValueError(f"unknown equivalence {kind}")


def rule_applies(r, key, route_id, who, body):
    """A rule names the exchange (keys) or its route (routes, or any_route: every route), for the app that answered; with
    when_body_contains only when the candidate's response body contains that text (no body, e.g. a missing exchange: no)."""
    if key in r.get("keys", []):
        return True
    if not (r.get("any_route") or route_id in r.get("routes", [])) or r.get("answered_by", "new") not in (who, "any"):
        return False
    return "when_body_contains" not in r or (body is not None and r["when_body_contains"] in body)


def explain_all(rules, key, route_id, who, diffs, body=None):
    """Expected-difference ids explaining every diff of the exchange, or None when one diff is not covered."""
    ids = set()
    for d in diffs:
        rule = next((r for r in rules if rule_applies(r, key, route_id, who, body) and diff_matches(r, d)), None)
        if rule is None:
            return None
        ids.add(rule["id"])
    return ids


def self_test():
    # the 19 corpus cases of R1 (docs/evidence/tickets-20261110/sanitizer/differences.tsv) in miniature
    assert equivalent("url", "http://example.com/", "http://example.com")
    assert equivalent("url", "http://example.com/%C3%BC", "http://example.com/ü")
    assert equivalent("url", "http://example.com/?q=%3Cscript%3E", "http://example.com/?q=<script>")
    assert not equivalent("url", "http://example.com/", "http://evil.example/")
    assert not equivalent("url", "http://example.com/a", "javascript:alert(1)")
    assert equivalent("style", "color: red", "color: rgba(255, 0, 0, 1)")
    assert equivalent("style", "margin: 0px 0px 0px 40px", "margin: 0 0 0 40px")
    assert equivalent("style", "color: rgb(255, 0, 0); font-family: \"Arial\"", "color: rgba(255, 0, 0, 1); font-family: \"Arial\"")
    assert not equivalent("style", "color: red", "color: blue")
    assert not equivalent("style", "color: red", "color: red; position: fixed")
    assert equivalent("identity-cookie", ".AspNet.ApplicationCookie; httponly; path=/; secure",
                      ".AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure")
    assert equivalent("identity-cookie", ".AspNet.ApplicationCookie; path=/ | _culture; path=/",
                      ".AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure | _culture; path=/")
    assert not equivalent("identity-cookie", ".AspNet.ApplicationCookie; path=/ | _culture; path=/", ".AspNetCore.Identity.Application; path=/")
    assert not equivalent("identity-cookie", ".AspNet.ApplicationCookie; path=/", ".AspNetCore.Identity.Application; path=/ | other; path=/")
    assert not equivalent("identity-cookie", "", ".AspNetCore.Identity.Application; path=/")
    assert equivalent("link-to-post", '<a href="/user/sign-out">Log Off', '<form action="/user/sign-out" class="td-menu-post" method="post">Log Off')
    assert equivalent("link-to-post", '<a class="btn btn-primary" href="/tickets/reset-user-lists">Save',
                      '<form action="/tickets/reset-user-lists" method="post" style="display: inline;">Save')
    assert not equivalent("link-to-post", '<a href="/user/sign-out">Log Off', '<form action="/user/sign-in" class="td-menu-post" method="post">Log Off')
    assert not equivalent("link-to-post", '<a href="/language?name=pt-BR">Português', '<form action="/language?name=pt-BR" method="post">English')
    assert not equivalent("link-to-post", '<a href="/user/sign-out">Log Off', '<form action="/user/sign-out" method="get">Log Off')
    assert not equivalent("link-to-post", '<a href="/user/sign-out">Log Off', '<button>Log Off')
    assert equivalent("antiforgery-cookie", "", ".TicketDesk.Antiforgery; httponly; path=/; samesite=strict")
    assert equivalent("antiforgery-cookie", "_culture; path=/", "_culture; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict")
    assert not equivalent("antiforgery-cookie", "", ".TicketDesk.Antiforgery; httponly; path=/ | other; path=/")
    assert equivalent("antiforgery-cookie", "__RequestVerificationToken; httponly; path=/", "")
    assert not equivalent("antiforgery-cookie", "__RequestVerificationToken; httponly; path=/ | _culture; path=/", "")
    r = {"id": "S3", "any_route": True, "when_body_contains": "td-menu-post", "diff": {"kind": "header"}}
    assert rule_applies(r, "k", "home.about", "new", '<form class="td-menu-post">') and not rule_applies(r, "k", "home.about", "new", "<p>")
    assert not rule_applies(r, "k", "home.about", "new", None) and not rule_applies(r, "k", "home.about", "legacy", "td-menu-post")
    print("self-test ok")


def main():
    if sys.argv[1:] == ["--self-test"]:
        self_test()
        return 0
    ap = argparse.ArgumentParser()
    ap.add_argument("--candidate", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--routes", required=True)
    ap.add_argument("--explain")
    ap.add_argument("--registry", help="the registry file of the run: held routes it does not list are not counted as held")
    ap.add_argument("--standalone", action="store_true", help="the new app alone: unmarked exchanges are the new app's, not legacy's")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    by_id, by_template = load_routes(a.routes)
    report = json.load(open(a.report, encoding="utf-8"))
    explain = json.load(open(a.explain, encoding="utf-8")) if a.explain else []
    mismatch = {m["key"]: m for m in report["mismatches"]}
    listed = set(json.load(open(a.registry, encoding="utf-8"))["Migration"]["Routes"]) if a.registry else None

    stats = collections.defaultdict(lambda: collections.Counter())
    held = collections.Counter()
    served_new = collections.Counter()
    served_angular = collections.Counter()
    lines_mis = []
    unexplained = 0
    seen = {}  # candidate key -> (routes.csv row, answered by)
    bodies = {}  # candidate key -> response body (rules with when_body_contains)
    for line in open(a.candidate, encoding="utf-8"):
        if not line.strip():
            continue
        e = json.loads(line)
        key = f"{e['journey']}#{e['stepIndex']:03d}.{e['hop']}"
        headers = {k.lower(): v for k, v in ((e.get("response") or {}).get("headers") or {}).items()}
        backend = ", ".join(headers.get("x-ticketdesk-backend", []))
        m = re.match(r"(modern|legacy); route=([^;]+)(?:; reason=(\w+))?", backend)
        row = route_of(e["routeKey"], by_template)
        if m and m.group(1) == "modern":
            row = by_id.get(m.group(2), row)
            who = "angular" if "ui=angular" in backend else "new"
            served_new[m.group(2)] += 1
            if who == "angular":
                served_angular[m.group(2)] += 1
        elif a.standalone and not m:
            who = "new"
            served_new["(no marker)"] += 1
        else:
            who = "legacy"
            if m and (listed is None or m.group(2) in listed):
                held[f"{m.group(2)} ({m.group(3)})"] += 1
        seen[key] = (row, who)
        bodies[key] = (e.get("response") or {}).get("body")
        wave = row["wave"] if row else "?"
        s = stats[wave]
        s["exchanges"] += 1
        s["new" if who == "angular" else who] += 1
        if key in mismatch:
            s["mismatches"] += 1
            mm = mismatch[key]
            ids = explain_all(explain, key, row["id"] if row else None, who, mm["diffs"], bodies[key])
            if ids:
                s["explained"] += 1
            else:
                s["unexplained"] += 1
                unexplained += 1
            kinds = ", ".join(mm["kinds"])
            lines_mis.append(f"| `{key}` | {mm['method']} {mm['path']} | {row['id'] if row else '?'} | {who} | {kinds} | "
                             + (", ".join(sorted(ids)) if ids else "**UNEXPLAINED**") + " |")

    # golden exchanges the candidate does not have (e.g. the redirect a legacy 302 led to, where the new app answers without
    # one): explained like the exchange that started the step (its hop 0), or by a rule naming the key
    missing = 0
    for key, mm in mismatch.items():
        if key in seen:
            continue
        missing += 1
        row, who = seen.get(key.rsplit(".", 1)[0] + ".0", (route_of(mm["routeKey"], by_template), "new" if a.standalone else "legacy"))
        s = stats[row["wave"] if row else "?"]
        s["mismatches"] += 1
        ids = explain_all(explain, key, row["id"] if row else None, who, mm["diffs"])
        if ids:
            s["explained"] += 1
        else:
            s["unexplained"] += 1
            unexplained += 1
        lines_mis.append(f"| `{key}` | {mm['method']} {mm['path']} | {row['id'] if row else '?'} (hop 0) | {who} | {', '.join(mm['kinds'])} | "
                         + (", ".join(sorted(ids)) if ids else "**UNEXPLAINED**") + " |")

    def wave_order(w):
        return (0, int(w)) if w.isdigit() else (1, w)

    out = ["# Replay summary" + (" (the new app alone)" if a.standalone else ""), "",
           f"Compared {report['compared']}, matched {report['matched']}, mismatched {report['mismatched']} "
           f"(missing {report['missingInCandidate']}, extra {report['extraInCandidate']}); unexplained mismatches: {unexplained} "
           f"(missing exchanges included: {missing}).", "",
           f"Answered by the new app: {sum(served_new.values())} exchanges on {len([r for r in served_new if r != '(no marker)'])} routes "
           + (f"(+{served_new['(no marker)']} without a route marker) " if served_new.get("(no marker)") else "") +
           f"({sum(served_angular.values())} by its Angular front end on {len(served_angular)} routes); by the legacy app: "
           f"{sum(s['legacy'] for s in stats.values())}.", "",
           "| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |", "|---|---:|---:|---:|---:|---:|---:|"]
    for w in sorted(stats, key=wave_order):
        s = stats[w]
        out.append(f"| {w} | {s['exchanges']} | {s['new']} | {s['legacy']} | {s['mismatches']} | {s['explained']} | {s['unexplained']} |")
    out += ["", "## Routes answered by the new app", "", "| Route id | Wave | Exchanges | of these by the Angular front end |", "|---|---|---:|---:|"]
    for rid, n in sorted(served_new.items()):
        out.append(f"| {rid} | {by_id[rid]['wave'] if rid in by_id else '?'} | {n} | {served_angular.get(rid, 0)} |")
    if held:
        out += ["", "## Migrated routes answered by legacy for some requests", "",
                "Reason `identity`: the request carried the legacy sign-in cookie and remote authentication is off; "
                "`switched-back`/`group`: switched back in the registry (the route or another of its group).", "", "| Route (reason) | Exchanges |", "|---|---:|"]
        for k, n in sorted(held.items()):
            out.append(f"| {k} | {n} |")
    if lines_mis:
        out += ["", "## Mismatches", "", "| Key | Request | Route | Answered by | Kinds | Expected difference |", "|---|---|---|---|---|---|"] + lines_mis
    open(a.out, "w", encoding="utf-8").write("\n".join(out) + "\n")
    print("\n".join(out[:3]))
    return 1 if unexplained else 0


if __name__ == "__main__":
    sys.exit(main())
