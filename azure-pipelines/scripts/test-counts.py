"""Count test results for the CI summary: `trx DIR` (TRX files) or `junit FILE...` (JUnit XML): one line per file + total.
`failed DIR`: one line "TEST-PROJECT-DIR|Namespace.Class.Method" per failed test in the TRX files (for the retry)."""
import glob
import os
import sys
import xml.etree.ElementTree as ET


def trx(d):
    tot = {"total": 0, "passed": 0, "failed": 0, "skipped": 0}
    for f in sorted(glob.glob(os.path.join(d, "**", "*.trx"), recursive=True)):
        c = ET.parse(f).getroot().find("{*}ResultSummary/{*}Counters")
        n = {"total": int(c.get("total", 0)), "passed": int(c.get("passed", 0)),
             "failed": int(c.get("failed", 0)) + int(c.get("error", 0)) + int(c.get("timeout", 0)) + int(c.get("aborted", 0)),
             "skipped": int(c.get("notExecuted", 0)) + int(c.get("inconclusive", 0))}
        name = ET.parse(f).getroot().find("{*}TestDefinitions/{*}UnitTest/{*}TestMethod")
        storage = os.path.basename(name.get("codeBase", f)) if name is not None else os.path.basename(f)
        print(f"{storage}: total {n['total']}, passed {n['passed']}, failed {n['failed']}, skipped {n['skipped']}")
        for k in tot:
            tot[k] += n[k]
    return tot


def junit(files):
    tot = {"total": 0, "passed": 0, "failed": 0, "skipped": 0}
    for f in files:
        r = ET.parse(f).getroot()
        suites = [r] if r.tag == "testsuite" else r.findall("testsuite")
        t = sum(int(s.get("tests", 0)) for s in suites)
        fl = sum(int(s.get("failures", 0)) + int(s.get("errors", 0)) for s in suites)
        sk = sum(int(s.get("skipped", 0)) for s in suites)
        print(f"{os.path.basename(f)}: total {t}, passed {t - fl - sk}, failed {fl}, skipped {sk}")
        for k, v in (("total", t), ("passed", t - fl - sk), ("failed", fl), ("skipped", sk)):
            tot[k] += v
    return tot


def failed(d):
    for f in sorted(glob.glob(os.path.join(d, "**", "*.trx"), recursive=True)):
        root = ET.parse(f).getroot()
        defs = {u.get("id"): u.find("{*}TestMethod") for u in root.iterfind(".//{*}UnitTest")}
        for r in root.iterfind(".//{*}UnitTestResult"):
            if r.get("outcome") in ("Failed", "Error", "Timeout", "Aborted"):
                m = defs.get(r.get("testId"))
                if m is None:
                    continue
                dll = m.get("codeBase", "")
                proj = dll.split("/bin/")[0] if "/bin/" in dll else os.path.dirname(dll)
                print(f"{proj}|{m.get('className')}.{m.get('name')}")


if __name__ == "__main__":
    kind, args = sys.argv[1], sys.argv[2:]
    if kind == "failed":
        failed(args[0])
        sys.exit(0)
    tot = trx(args[0]) if kind == "trx" else junit(args)
    print(f"TOTAL: total {tot['total']}, passed {tot['passed']}, failed {tot['failed']}, skipped {tot['skipped']}")
