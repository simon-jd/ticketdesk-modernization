# Perf attribution: why legacy direct p95 moved from 48.2 ms (Round 1) to 89.0 ms (Round 2)

Question (brief r2b-tickets, task 0): Round 1 measured legacy direct p95 48.2 ms on snapshot `baseline-s` (then: 4.6.2 build,
EF6 schema, attachments on disk; today's `baseline-s-legacy`), Round 2 measured 89.0 ms with a changed k6 filter step on the
shared `baseline-s` (net48 + System.Web adapters build with the blob-store wrapper, `td-db` expand schema, attachments in
Azurite). Nothing was changed in the legacy app or its snapshots for this.

## Runs (all direct, k6 mix 10 ops/s, 30 s warm-up + 60 s, fresh reset of the snapshot before every run)

`runs.csv` has every run (host load, p50/p95/p99, per-op p50 and p95). `stepN/summary.md` per step.

| Step | What | Command |
|---|---|---|
| 1 | current mix, `baseline-s-legacy` vs `baseline-s`, interleaved A B A B A B | `TD_LOCK_OWNER=tickets ops/dev/perf-attribution.sh --out-dir step1 --runs 3` |
| 2 | the same plus `baseline-s-legacy` + expand schema (`td-db migrate`, pool restart: `ops/dev/perf-variant-schema.sh`), interleaved, 2 runs each, SQL Server statement statistics after every run (`after.txt`, `ops/dev/legacy-query-stats.sh`), per-request timeline (`k6-raw.csv.gz`) | `TD_PERF_RAW=1 ... perf-attribution.sh --out-dir step2 --runs 2 --snapshots baseline-s-legacy,baseline-s,baseline-s-legacy:schema --prepare baseline-s-legacy:schema=ops/dev/perf-variant-schema.sh --after 'ops/dev/legacy-query-stats.sh 15'` |
| 3 | the Round-1 mix (`ticketdesk-mix-round1.js` = `git show 35d92ea:tests/perf/ticketdesk-mix.js`) on `baseline-s-legacy`, 2 runs | `TD_PERF_MIX=.../ticketdesk-mix-round1.js ... --snapshots baseline-s-legacy --runs 2` |

## Results

Same (current) mix, interleaved, 5 runs per snapshot (steps 1 + 2):

| Snapshot | p50 median (runs) | p95 median (runs) |
|---|---|---|
| `baseline-s-legacy` (Round-1 state) | **24.5** ms (23.5, 24.5, 26.1, 24.3, 24.8) | **60.7** ms (61.7, 53.7, 95.0, 58.6, 60.7) |
| `baseline-s` (shared) | **28.2** ms (27.3, 30.2, 29.8, 28.2, 27.6) | **73.4** ms (67.2, 90.2, 96.2, 73.4, 69.9) |
| `baseline-s-legacy` + expand schema (step 2 only) | 24.5 ms (25.0, 23.9) | 63.4 ms (62.7, 64.1) |

Per-op p50 in step 2 (the round with the SQL statistics; mean of 2 runs), legacy / legacy + schema / shared:
list 46.0 / 48.6 / 58.1 ms, filter 43.0 / 44.1 / 51.7, create 26.9 / 27.1 / 35.2, comment 13.6 / 14.8 / 15.3, view 5.8 / 6.2 / 7.2.

SQL Server (step 2, `after.txt`): the legacy ticket-list statement keeps its plan (hash `0xD6B23CF8B2F40670`) on every
variant, but reads **1,059 → 1,339** logical pages per execution with the expand schema (legacy + schema and shared alike;
the `Tickets` rows are wider by `AssignedDate`), CPU ~7.0 → 8.2–9.2 ms per list. One of the list's COUNT statements gets a
new plan on the expanded schema (1,059 → 32 reads).

Round-1 mix on the legacy snapshot (step 3, host load average 7.5–7.9 against 2–6.6 in steps 1–2): p95 73.0 / 73.5 ms, p50
26.1 / 26.0 ms. The old mix is not faster than the current one, so the k6 change (filter posts the page's token; a VU loads a
list before its first filter) does not explain the jump.

## Answer

1. **Most of 48.2 → 89.0 ms is run-to-run spread, not the app**: the same `baseline-s-legacy` snapshot measured today spans
   53.7–95.0 ms p95 over 5 runs. In step 1 whole runs fell into a slow mode on **both** snapshots (list/filter p50 ~66–71 ms
   instead of ~42–49 ms; legacy run 3, shared runs 2–3), time-correlated and independent of the snapshot. Round 1's three
   runs (47.8–57.2) and Round 2's three (81.9–91.5) sit at the two ends of that spread. Compare only interleaved pairs, and
   use p50 and per-op p50, which are stable.
2. **The shared state is slower, by +3.7 ms p50 and +12.7 ms p95** (medians of 5 interleaved runs each).
3. **Split**: the expand schema costs little (+0.0 ms p50 overall, list +2.6 ms p50; the SQL side measured above). The bulk
   (list/filter +8–10 ms p50, create +8 ms) is in the app tier of the round-2 build: net48 retarget + System.Web adapters
   module (+ the blob-store wrapper). The blob store itself only touches view (+1 ms p50: the ticket page lists the ticket's
   blob folder over the qemu NAT) and create (commit of pending uploads); list and filter never call it. App Insights vs the
   adapters module was **not** split (one more variant would deploy the b4edcc3 package with the disk store on
   `baseline-s-legacy`, or turn the adapters module off); nothing was changed in the legacy app.
