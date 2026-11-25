#!/usr/bin/env python3
"""check-attachments.py - read migrated attachments back through the legacy app and compare them with the move manifest.

  python3 -I ops/onprem/filestore/check-attachments.py --manifest filestore-move.csv [--base https://localhost:8443]

Signs in as the lab admin (legacy_smoke.py's sign-in, $TD_W/.secrets/legacy-lab.env), then for every committed file of
the manifest written by ops/windows/Move-TdFileStore.ps1 (Name = <ticketId>/<file>; pending uploads are skipped) downloads
GET /file/<ticketId>/<file> and compares size and MD5 with the manifest. Exit 1 on any difference.
"""
import argparse, base64, csv, hashlib, os, sys, urllib.parse, urllib.request

sys.dont_write_bytecode = True  # importing legacy_smoke must not leave __pycache__ in the repo
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "smoke"))
import legacy_smoke as ls  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("--manifest", required=True)
ap.add_argument("--base", default="https://localhost:8443")
ap.add_argument("--env-file", default=os.path.join(ls.TD_W, ".secrets", "legacy-lab.env"))
ap.add_argument("--cacert", default=os.path.join(ls.TD_W, ".secrets", "legacy-lab-ca", "ca.crt"))
a = ap.parse_args()
cred = ls.load_env(a.env_file)
c = ls.Client(a.base, a.cacert, False, 120)
run = ls.Run("check-attachments")
ls.sign_in(c, run, cred["TD_ADMIN_EMAIL"], cred["TD_ADMIN_PASSWORD"])
with open(a.manifest, encoding="utf-8-sig") as f:
    rows = [r for r in csv.DictReader(f) if not r["Name"].startswith("pending/")]
bad = 0
for r in rows:
    tid, name = r["Name"].split("/", 1)
    url = f"{c.base}/file/{tid}/{urllib.parse.quote(name)}"
    try:
        resp = c.opener.open(urllib.request.Request(url), timeout=120)
        status, raw = resp.status, resp.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, b""
    md5 = base64.b64encode(hashlib.md5(raw).digest()).decode()
    ok = status == 200 and len(raw) == int(r["Bytes"]) and md5 == r["Md5"]
    bad += 0 if ok else 1
    print(f"{'ok ' if ok else 'BAD'} GET /file/{tid}/{name} -> {status}, {len(raw)} bytes, md5 {md5} (manifest {r['Bytes']} bytes, {r['Md5']})")
print(f"{len(rows) - bad} of {len(rows)} committed attachments read back through the app with the manifest's size and MD5")
sys.exit(1 if bad or not run.checks or not all(x["ok"] for x in run.checks) else 0)
