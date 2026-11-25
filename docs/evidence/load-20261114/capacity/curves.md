# Capacity steps (docs/evidence/load-20261114/capacity/runs)

Pass = p95 <= 500 ms, failed <= 0.5 %, dropped iterations <= 0.5 % over the measured 120 s (after 30 s warm-up). CPU in cores (1.0 = one logical CPU), measured window averages.

## legacy

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 10 | 3 | 107.6 (100.2-108.1) | 0.00 (0.00) | 0.00 (0.00) | 3/3 | 19.5 | vCPU 1.26 (w3wp 0.76, sqlservr 0.14), emu 0.12 |
| 20 | 3 | 103.6 (100.1-125.0) | 0.00 (2.16) | 0.00 (22.17) | 2/3 | 39.1 | vCPU 2.26 (w3wp 1.49, sqlservr 0.26), emu 0.12 |
| 25 | 3 | 112.5 (101.9-121.1) | 0.00 (1.59) | 0.00 (16.17) | 2/3 | 49.0 | vCPU 2.69 (w3wp 1.81, sqlservr 0.32), emu 0.14 |
| 30 | 3 | 3508.4 (119.8-16398.8) | 1.63 (2.46) | 29.92 (38.64) | 1/3 | 41.5 | vCPU 2.25 (w3wp 1.52, sqlservr 0.29), emu 0.16 |
| 40 | 1 | 3230.0 (3230.0-3230.0) | 0.25 (0.25) | 29.31 (29.31) | 0/1 | 55.9 | vCPU 2.77 (w3wp 2.10, sqlservr 0.42), emu 0.18 |

Resources (medians over the repeats; CPU in cores of the 4 vCPUs; Windows counters inside the VM):

| Rate | vCPU threads | w3wp | sqlservr | VM CPU % | other processes on the 4 cores | qemu emulator threads | w3wp threads max | ASP.NET queued max | SQL sessions max | lock waits/s | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 1.26 | 0.76 | 0.14 | 27.6 | 0.17 | 0.12 | 81 | 0 | 37 | 0.1 | 0.03 |
| 20 | 2.26 | 1.49 | 0.26 | 49.3 | 0.06 | 0.12 | 71 | 0 | 48 | 0.1 | 0.05 |
| 25 | 2.69 | 1.81 | 0.32 | 59.1 | 0.07 | 0.14 | 75 | 0 | 43 | 0.2 | 0.06 |
| 30 | 2.25 | 1.52 | 0.29 | 51.3 | 0.03 | 0.16 | 156 | 159 | 89 | 1.5 | 0.05 |
| 40 | 2.77 | 2.10 | 0.42 | 68.6 | 0.49 | 0.18 | 189 | 146 | 100 | 25.8 | 0.07 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 10: WRITELOG=44;ASYNC_NETWORK_IO=15;PAGEIOLATCH_EX=13;PREEMPTIVE_OS_QUERYREGISTRY=4 (host load 11.72 -> 9.33)
- r1 rate 20: WRITELOG=58;ASYNC_NETWORK_IO=26;PAGEIOLATCH_EX=9;PREEMPTIVE_OS_QUERYREGISTRY=8 (host load 9.22 -> 5.02)
- r1 rate 25: WRITELOG=58;PREEMPTIVE_OS_QUERYREGISTRY=57;ASYNC_NETWORK_IO=54;SOS_SCHEDULER_YIELD=26 (host load 5.02 -> 9.36)
- r1 rate 30: WRITELOG=77;ASYNC_NETWORK_IO=51;PREEMPTIVE_OS_QUERYREGISTRY=33;PAGEIOLATCH_EX=12 (host load 9.36 -> 10.46)
- r1 rate 40: PREEMPTIVE_OS_QUERYREGISTRY=3431;LCK_M_S=3413;SOS_SCHEDULER_YIELD=1943;ASYNC_NETWORK_IO=597 (host load 9.78 -> 12.23)
- r2 rate 10: WRITELOG=46;ASYNC_NETWORK_IO=13;PAGEIOLATCH_EX=6;PREEMPTIVE_OS_QUERYREGISTRY=4 (host load 7.25 -> 5.1)
- r2 rate 20: WRITELOG=61;ASYNC_NETWORK_IO=27;PREEMPTIVE_OS_QUERYREGISTRY=8;PAGEIOLATCH_EX=7 (host load 5.1 -> 7.97)
- r2 rate 25: WRITELOG=72;ASYNC_NETWORK_IO=33;PREEMPTIVE_OS_QUERYREGISTRY=14;PAGEIOLATCH_EX=8 (host load 7.81 -> 5.44)
- r2 rate 30: PREEMPTIVE_OS_QUERYREGISTRY=374;SOS_SCHEDULER_YIELD=248;LCK_M_S=163;ASYNC_NETWORK_IO=86 (host load 5.24 -> 4.61)
- r3 rate 10: WRITELOG=43;ASYNC_NETWORK_IO=12;PAGEIOLATCH_EX=8;PREEMPTIVE_OS_QUERYREGISTRY=3 (host load 2.94 -> 3.0)
- r3 rate 20: WRITELOG=47;PREEMPTIVE_OS_QUERYREGISTRY=34;ASYNC_NETWORK_IO=25;SOS_SCHEDULER_YIELD=18 (host load 3.0 -> 2.99)
- r4 rate 25: WRITELOG=70;ASYNC_NETWORK_IO=37;PREEMPTIVE_OS_QUERYREGISTRY=18;PAGEIOLATCH_EX=8 (host load 8.77 -> 9.28)
- r4 rate 30: PREEMPTIVE_OS_QUERYREGISTRY=319;SOS_SCHEDULER_YIELD=175;ASYNC_NETWORK_IO=104;LCK_M_S=75 (host load 8.7 -> 4.73)

Highest passing rate per repeat: r1 30, r2 25, r3 10, r4 25
Sustained (passed in every repeat that ran it, and every lower rate too): 10; first rate with a failing repeat: 20

## new

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 100 | 3 | 30.5 (21.1-37.5) | 0.00 (0.00) | 0.00 (0.00) | 3/3 | 193.8 | app 0.71 + sql 0.84 |
| 150 | 3 | 33.0 (25.8-56.5) | 0.00 (0.01) | 0.00 (0.00) | 3/3 | 290.9 | app 0.96 + sql 1.22 |
| 200 | 3 | 4765.2 (4640.4-4906.2) | 0.87 (1.17) | 21.13 (21.51) | 0/3 | 306.6 | app 0.92 + sql 1.50 |
| 225 | 1 | 12622.8 (12622.8-12622.8) | 7.97 (7.97) | 71.39 (71.39) | 0/1 | 133.7 | app 0.42 + sql 0.60 |

Resources (medians over the repeats; CPU in cores of the 4):

| Rate | app | SQL | app + SQL | other processes on the 4 cores | app threads max | SQL sessions max | SQL blocked max | SQL runnable avg | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0.71 | 0.84 | 1.55 | 1.13 | 27 | 13 | 0 | 0.4 | 0.24 |
| 150 | 0.96 | 1.22 | 2.18 | 0.14 | 26 | 33 | 1 | 1.4 | 0.33 |
| 200 | 0.92 | 1.50 | 2.42 | 0.22 | 32 | 100 | 100 | 3.7 | 0.36 |
| 225 | 0.42 | 0.60 | 1.02 | 1.36 | 32 | 100 | 100 | 3.3 | 0.14 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 100: LCK_M_S=28;SOS_SCHEDULER_YIELD=27;WRITELOG=20;RESERVED_MEMORY_ALLOCATION_EXT=6 (host load 10.55 -> 12.02)
- r1 rate 150: SOS_SCHEDULER_YIELD=72;LCK_M_S=65;WRITELOG=21;RESERVED_MEMORY_ALLOCATION_EXT=9 (host load 12.02 -> 6.45)
- r1 rate 200: LCK_M_S=20850;LCK_M_IX=4057;SOS_SCHEDULER_YIELD=503;WRITELOG=36 (host load 5.93 -> 7.34)
- r1 rate 225: LCK_M_S=77671;LCK_M_IX=8233;SOS_SCHEDULER_YIELD=1806;WRITELOG=48 (host load 7.47 -> 11.17)
- r2 rate 100: WRITELOG=27;LCK_M_S=27;SOS_SCHEDULER_YIELD=16;RESERVED_MEMORY_ALLOCATION_EXT=6 (host load 8.81 -> 12.03)
- r2 rate 150: SOS_SCHEDULER_YIELD=180;LCK_M_S=132;WRITELOG=49;RESERVED_MEMORY_ALLOCATION_EXT=11 (host load 11.86 -> 13.16)
- r2 rate 200: LCK_M_S=23736;LCK_M_IX=3793;SOS_SCHEDULER_YIELD=1033;WRITELOG=58 (host load 12.98 -> 11.09)
- r3 rate 100: WRITELOG=11;SOS_SCHEDULER_YIELD=9;LCK_M_S=5;RESERVED_MEMORY_ALLOCATION_EXT=5 (host load 3.53 -> 3.83)
- r3 rate 150: SOS_SCHEDULER_YIELD=44;LCK_M_S=27;WRITELOG=19;RESERVED_MEMORY_ALLOCATION_EXT=9 (host load 3.52 -> 5.41)
- r3 rate 200: LCK_M_S=22366;LCK_M_IX=4057;SOS_SCHEDULER_YIELD=1003;WRITELOG=43 (host load 5.41 -> 9.2)

Highest passing rate per repeat: r1 150, r2 150, r3 150
Sustained (passed in every repeat that ran it, and every lower rate too): 150; first rate with a failing repeat: 200

