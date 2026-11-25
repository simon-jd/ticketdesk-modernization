# Performance baseline (k6), legacy direct vs through the gateway

`ops/dev/perf-baseline.sh` with `tests/perf/ticketdesk-mix.js`, 2026-11-08 15:33–15:45Z, k6 v2.3.0 on lab-host.
Each run: VM reset to `baseline-s` (18–19 s), then 10 operations/s (constant arrival rate) for 30 s warm-up + 60 s
measured; only requests of the measured 60 s count. Mix per operation (per-VU seeded): list 35 %, view 35 % (page + 2 AJAX
panels), filter 15 %, comment 10 %, create 5 % → ~1.9 requests per operation; 37 seeded help-desk users, each VU signed in
once and kept its session; redirects not followed, anything but the expected status counts as a failed request.

| Target | Runs | Requests per run (median) | p50 ms | p95 ms | p99 ms | Error rate |
|---|---:|---:|---:|---:|---:|---:|
| legacy direct (`https://localhost:8443`) | 3 | 1,153 | 22.7 | 48.2 | 67.6 | 0.00 % |
| through the gateway (`https://localhost:5443` → legacy) | 3 | 1,154 | 24.4 | 52.7 | 70.2 | 0.00 % |

Medians of the 3 runs (per-run numbers and p95 per operation in `results.md` / `results.json`, raw k6 summaries in
`*-run*/summary.json`, k6 console in `*-run*/k6.txt`). All checks passed in every run (679–681). The executor dropped
2–64 iterations per run while the IIS app was starting after the reset (warm-up); the measured request counts
(1,145–1,158) match 600 operations × 1.9 requests.

Conditions and limits: the VM has 4 vCPU / 8 GB under KVM and is reached through qemu user-mode networking (slirp) on
127.0.0.1; k6, the gateway and the VM share one 32-core host with other projects' containers (host load and the top
docker containers before/after each run are in `*-run*/host-load.txt`: load average 2.7–6.6, busiest container
≤ 99 % of one core). Light load on purpose (≈ 19 requests/s): this is the reference for later "after" runs at the same
settings, not a capacity test. Run-to-run spread is about as large as the gateway's overhead (direct p95 47.8–57.2 ms,
gateway p95 47.9–54.0 ms), so compare medians of ≥ 3 runs measured back to back.
