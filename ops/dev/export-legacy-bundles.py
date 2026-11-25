#!/usr/bin/env python3
"""Snapshot the legacy app's optimized bundles (System.Web.Optimization) for the new app (wave 0).

    python3 -I ops/dev/export-legacy-bundles.py [--legacy https://localhost:8443/] [--out src/modern/TicketDesk.Web/LegacyBundles]

The legacy app builds each bundle at run time (concatenation + WebGrease minification) and links it as
/content/<name>?v=<hash> or /bundles/<name>?v=<hash>, where <hash> is HttpServerUtility.UrlTokenEncode(SHA-256 of the
bundle text as UTF-16LE). No .NET bundler reproduces WebGrease's output byte for byte, so the new app serves these
snapshots instead. The legacy code is frozen during the migration, so the snapshot cannot drift; this script checks that
each file's computed hash equals the v= the legacy pages link, and the new app's tests check the files against the
manifest. Read-only against the legacy app (plain GETs), no VM lock needed.
"""
import argparse
import base64
import hashlib
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.request

# Every bundle registered in TicketDesk.Web.Client/App_Start/Startup.Bundles.cs (Release build).
STYLES = ["styles", "datepicker", "wizard", "select2", "editor", "summernote"]
SCRIPTS = ["datepicker", "editticket", "application-settings", "ticketcenter", "admin-users", "project-settings", "search",
           "admin-edit-user", "editor", "select2", "summernote", "markdown", "common", "modernizr",
           "select2_locale_en-US", "common_locale_en-US", "summernote_locale_en-US",
           "select2_locale_pt-BR", "common_locale_pt-BR", "summernote_locale_pt-BR"]
# Pages whose links prove the hashes (anonymous ones; the sign-in page links the same layout bundles).
PAGES = ["", "about", "user/sign-in"]


def url_token_encode(data: bytes) -> str:
    """System.Web.HttpServerUtility.UrlTokenEncode: base64url without padding plus the padding count as a digit."""
    b64 = base64.b64encode(data).decode("ascii")
    pad = len(b64) - len(b64.rstrip("="))
    return b64.rstrip("=").replace("+", "-").replace("/", "_") + str(pad)


def bundle_hash(text: str) -> str:
    return url_token_encode(hashlib.sha256(text.encode("utf-16-le")).digest())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--legacy", default="https://localhost:8443/")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..", "..", "src", "modern", "TicketDesk.Web", "LegacyBundles"))
    ap.add_argument("--cacert", default=os.path.expanduser("~/Desktop/ticketdesk-modernization/.secrets/legacy-lab-ca/ca.crt"))
    a = ap.parse_args()
    base = a.legacy.rstrip("/") + "/"
    ctx = ssl.create_default_context(cafile=a.cacert)
    os.makedirs(a.out, exist_ok=True)

    def get(path):
        req = urllib.request.Request(base + path, headers={"User-Agent": "td-export-legacy-bundles"})
        try:
            with urllib.request.urlopen(req, context=ctx) as r:
                return r.status, dict(r.headers.items()), r.read()
        except urllib.error.HTTPError as e:
            return e.code, dict(e.headers.items()), e.read()

    linked = {}
    for page in PAGES:
        status, _, body = get(page)
        for m in re.finditer(r'(?:href|src)="/((?:content|bundles)/[^"?]+)\?v=([^"]+)"', body.decode("utf-8")):
            linked[m.group(1)] = m.group(2)

    entries = []
    for kind, names in (("content", STYLES), ("bundles", SCRIPTS)):
        for name in names:
            path = f"{kind}/{name}"
            status, headers, body = get(path)
            if status != 200:
                print(f"error: GET /{path} -> {status}", file=sys.stderr)
                return 1
            text = body.decode("utf-8")
            if body.startswith(b"\xef\xbb\xbf"):
                print(f"error: /{path} starts with a BOM; the hash rule below would not hold", file=sys.stderr)
                return 1
            v = bundle_hash(text)
            if path in linked and linked[path] != v:
                print(f"error: /{path}: computed v={v} but the legacy page links v={linked[path]}", file=sys.stderr)
                return 1
            file = f"{kind}.{name}{'.css' if kind == 'content' else '.js'}"
            with open(os.path.join(a.out, file), "wb") as f:
                f.write(body)
            entries.append({
                "path": "/" + path, "file": file, "contentType": headers.get("Content-Type", ""),
                "cacheControl": headers.get("Cache-Control", ""), "v": v, "bytes": len(body),
                "sha256": hashlib.sha256(body).hexdigest(), "linkVerified": path in linked,
            })
            print(f"/{path}: {len(body)} B v={v}{' (matches the legacy link)' if path in linked else ''}")

    manifest = {
        "source": base, "note": "Snapshot of the legacy System.Web.Optimization bundles; regenerate with ops/dev/export-legacy-bundles.py",
        "bundles": entries,
    }
    with open(os.path.join(a.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"{len(entries)} bundles, {sum(e['bytes'] for e in entries)} bytes, {sum(e['linkVerified'] for e in entries)} hashes verified against legacy links")
    return 0


if __name__ == "__main__":
    sys.exit(main())
