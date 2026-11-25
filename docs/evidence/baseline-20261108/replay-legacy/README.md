# Legacy recording stability (run A vs run B)

`ops/dev/record-legacy.sh`, 2026-11-08 16:02Z, on the `baseline-s` snapshot re-saved after S9 (`customErrors`
RemoteOnly). Working tree: `74805ef` plus the comparer and deploy fixes committed right after as `7c6ae12` and `e800817`.
Reset → record all 15 journeys (run 1) → reset → record again (run 2) → `td-replay compare`.

- run 1: 262 exchanges, 0 journey errors (`record-run1.txt`); run 2: 262 exchanges, 0 journey errors (`record-run2.txt`)
- compare: **262 compared, 262 matched, 0 mismatched**, no ignore selectors (`compare-run2.txt`, `compare-run1-run2/report.md`)
- `legacy-run1.jsonl.xz` is the golden recording (`tests/characterization/golden/legacy-golden.jsonl.xz`, same content).
  Decompress with `xz -dc`.
- The first recording (07:28Z, `customErrors` Off) also gave 262/262; it differs from this one only in the two 500 pages
  (`../s9/`).
