# Capacity steps (docs/evidence/load-20261114/capacity/diag)

Pass = p95 <= 500 ms, failed <= 0.5 %, dropped iterations <= 0.5 % over the measured 120 s (after 30 s warm-up). CPU in cores (1.0 = one logical CPU), measured window averages.

## legacy-minthreads

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 20 | 1 | 106.9 (106.9-106.9) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 39.2 | vCPU 2.36 (w3wp 1.58, sqlservr 0.30), emu 0.13 |
| 25 | 1 | 103.6 (103.6-103.6) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 49.1 | vCPU 2.71 (w3wp 1.85, sqlservr 0.33), emu 0.14 |
| 30 | 1 | 109.0 (109.0-109.0) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 58.7 | vCPU 3.11 (w3wp 2.17, sqlservr 0.40), emu 0.15 |
| 40 | 1 | 141.6 (141.6-141.6) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 77.9 | vCPU 3.68 (w3wp 2.72, sqlservr 0.59), emu 0.19 |

Resources (medians over the repeats; CPU in cores of the 4 vCPUs; Windows counters inside the VM):

| Rate | vCPU threads | w3wp | sqlservr | VM CPU % | other processes on the 4 cores | qemu emulator threads | w3wp threads max | ASP.NET queued max | SQL sessions max | lock waits/s | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 20 | 2.36 | 1.58 | 0.30 | 52.2 | 0.10 | 0.13 | 234 | 0 | 14 | 0.1 | 0.06 |
| 25 | 2.71 | 1.85 | 0.33 | 60.2 | 0.02 | 0.14 | 257 | 0 | 14 | 0.2 | 0.06 |
| 30 | 3.11 | 2.17 | 0.40 | 70.5 | 0.02 | 0.15 | 237 | 0 | 7 | 0.2 | 0.07 |
| 40 | 3.68 | 2.72 | 0.59 | 89.3 | 0.01 | 0.19 | 254 | 0 | 52 | 1.1 | 0.10 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 20: WRITELOG=63;ASYNC_NETWORK_IO=28;PAGEIOLATCH_EX=12;PREEMPTIVE_OS_QUERYREGISTRY=9 (host load 4.55 -> 11.52)
- r1 rate 25: WRITELOG=74;ASYNC_NETWORK_IO=33;PREEMPTIVE_OS_QUERYREGISTRY=15;PAGEIOLATCH_EX=8 (host load 11.32 -> 5.55)
- r1 rate 30: WRITELOG=77;ASYNC_NETWORK_IO=46;PREEMPTIVE_OS_QUERYREGISTRY=28;PAGEIOLATCH_EX=10 (host load 5.75 -> 5.59)
- r1 rate 40: PREEMPTIVE_OS_QUERYREGISTRY=461;SOS_SCHEDULER_YIELD=217;LCK_M_S=186;ASYNC_NETWORK_IO=125 (host load 5.59 -> 7.04)

Highest passing rate per repeat: r1 40
Sustained (passed in every repeat that ran it, and every lower rate too): 40; first rate with a failing repeat: None

## new-rcsi

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 150 | 1 | 27.9 (27.9-27.9) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 290.8 | app 1.01 + sql 1.18 |
| 200 | 1 | 161.7 (161.7-161.7) | 0.03 (0.03) | 0.00 (0.00) | 1/1 | 387.3 | app 1.20 + sql 2.02 |
| 250 | 1 | 3869.4 (3869.4-3869.4) | 0.31 (0.31) | 17.37 (17.37) | 0/1 | 406.4 | app 1.31 + sql 2.48 |
| 300 | 1 | 5162.1 (5162.1-5162.1) | 0.36 (0.36) | 37.93 (37.93) | 0/1 | 369.8 | app 1.23 + sql 2.53 |

Resources (medians over the repeats; CPU in cores of the 4):

| Rate | app | SQL | app + SQL | other processes on the 4 cores | app threads max | SQL sessions max | SQL blocked max | SQL runnable avg | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 150 | 1.01 | 1.18 | 2.19 | 0.12 | 28 | 36 | 0 | 0.8 | 0.35 |
| 200 | 1.20 | 2.02 | 3.22 | 0.18 | 33 | 100 | 0 | 6.6 | 0.51 |
| 250 | 1.31 | 2.48 | 3.79 | 0.02 | 33 | 100 | 1 | 58.6 | 0.53 |
| 300 | 1.23 | 2.53 | 3.76 | 0.02 | 36 | 100 | 0 | 68.6 | 0.45 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 150: SOS_SCHEDULER_YIELD=46;WRITELOG=19;RESERVED_MEMORY_ALLOCATION_EXT=8;PREEMPTIVE_OS_AUTHENTICATIONOPS=5 (host load 5.26 -> 6.33)
- r1 rate 200: SOS_SCHEDULER_YIELD=1143;WRITELOG=73;PREEMPTIVE_OS_GENERICOPS=23;RESERVED_MEMORY_ALLOCATION_EXT=17 (host load 6.07 -> 17.53)
- r1 rate 250: SOS_SCHEDULER_YIELD=10046;WRITELOG=142;PREEMPTIVE_OS_GENERICOPS=121;PREEMPTIVE_OS_FILEOPS=87 (host load 16.36 -> 19.35)
- r1 rate 300: SOS_SCHEDULER_YIELD=12978;WRITELOG=139;PREEMPTIVE_OS_GENERICOPS=128;PREEMPTIVE_OS_FILEOPS=91 (host load 18.77 -> 18.81)

Highest passing rate per repeat: r1 200
Sustained (passed in every repeat that ran it, and every lower rate too): 200; first rate with a failing repeat: 250

