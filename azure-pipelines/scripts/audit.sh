#!/usr/bin/env bash
# CI gate: known-vulnerable packages. NuGet: `dotnet list package --vulnerable --include-transitive` over the solution (the
# restore itself already fails on NU1901-NU1904 through TreatWarningsAsErrors); npm: `npm audit` of src/web (any severity).
. "$(dirname "${BASH_SOURCE[0]}")/ci-lib.sh"
cd "$CI_REPO"
OUT="$CI_OUT/audit"; mkdir -p "$OUT"
SLN=src/modern/TicketDesk.Modern.sln
rc=0
ci_section "NuGet restore + vulnerable packages"
if ! dotnet restore "$SLN" -nologo > "$OUT/nuget-restore.txt" 2>&1; then
  grep -E 'NU190[1-4]|error' "$OUT/nuget-restore.txt" | sort -u | head -20
  echo "##vso[task.logissue type=error]NuGet restore failed (NuGet audit: see nuget-restore.txt)" >&3; rc=1
else
  dotnet list "$SLN" package --vulnerable --include-transitive --format json > "$OUT/nuget-vulnerable.json"
  python3 -I - "$OUT/nuget-vulnerable.json" <<'PY' || rc=1
import json, sys
d = json.load(open(sys.argv[1]))
hits = []
for p in d.get("projects", []):
    for fw in p.get("frameworks", []):
        for kind in ("topLevelPackages", "transitivePackages"):
            for pkg in fw.get(kind, []):
                for v in pkg.get("vulnerabilities", []):
                    hits.append((p["path"].split("/")[-1], pkg["id"], pkg.get("resolvedVersion"), v.get("severity"), v.get("advisoryurl")))
print(f"NuGet: {len(d.get('projects', []))} projects, {len(hits)} vulnerable package references")
for h in sorted(set(hits)):
    print("##vso[task.logissue type=error]vulnerable package %s %s %s (%s) %s" % h)
sys.exit(1 if hits else 0)
PY
fi
ci_section "npm audit (src/web)"
(cd src/web && npm audit --json > "$OUT/npm-audit.json") || true
python3 -I - "$OUT/npm-audit.json" <<'PY' || rc=1
import json, sys
d = json.load(open(sys.argv[1]))
v = d.get("metadata", {}).get("vulnerabilities", {})
total = v.get("total", sum(x for k, x in v.items() if k != "total"))
print(f"npm: {v}")
for name, adv in sorted(d.get("vulnerabilities", {}).items()):
    print(f"##vso[task.logissue type=error]npm package {name}: {adv.get('severity')}")
sys.exit(1 if total else 0)
PY
exit "$rc"
