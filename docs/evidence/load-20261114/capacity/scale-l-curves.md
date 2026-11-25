# Capacity steps (capacity/scale-l (server pages) and capacity/scale-l-api (ticket center through /api/tickets))

Pass = p95 <= 500 ms, failed <= 0.5 %, dropped iterations <= 0.5 % over the measured 120 s (after 30 s warm-up). CPU in cores (1.0 = one logical CPU), measured window averages.

## new-L-api

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 25 | 1 | 106.3 (106.3-106.3) | 0.00 (0.00) | 0.00 (0.00) | 1/1 | 49.4 | app 0.34 + sql 0.92 |
| 50 | 1 | 187.6 (187.6-187.6) | 0.02 (0.02) | 0.00 (0.00) | 1/1 | 98.1 | app 0.72 + sql 1.96 |
| 100 | 1 | 5498.3 (5498.3-5498.3) | 0.26 (0.26) | 28.05 (28.05) | 0/1 | 142.1 | app 0.88 + sql 2.83 |

Resources (medians over the repeats; CPU in cores of the 4):

| Rate | app | SQL | app + SQL | other processes on the 4 cores | app threads max | SQL sessions max | SQL blocked max | SQL runnable avg | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 | 0.34 | 0.92 | 1.26 | 0.43 | 36 | 10 | 0 | 0.3 | 0.14 |
| 50 | 0.72 | 1.96 | 2.68 | 0.41 | 32 | 22 | 0 | 1.5 | 0.31 |
| 100 | 0.88 | 2.83 | 3.71 | 0.01 | 38 | 100 | 1 | 94.6 | 0.37 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 25: SOS_SCHEDULER_YIELD=59;PAGEIOLATCH_SH=19;WRITELOG=18;PREEMPTIVE_OS_GENERICOPS=6 (host load 6.18 -> 8.93)
- r1 rate 50: SOS_SCHEDULER_YIELD=1065;WRITELOG=66;PAGEIOLATCH_SH=24;PREEMPTIVE_OS_GENERICOPS=18 (host load 8.94 -> 15.26)
- r1 rate 100: SOS_SCHEDULER_YIELD=44424;PREEMPTIVE_OS_GENERICOPS=222;PREEMPTIVE_OS_FILEOPS=158;WRITELOG=105 (host load 14.76 -> 10.68)

Highest passing rate per repeat: r1 50
Sustained (passed in every repeat that ran it, and every lower rate too): 50; first rate with a failing repeat: 100

## new-L-serverpages

| Rate ops/s | Repeats | p95 ms median (min-max) | Failed % median (max) | Dropped % median (max) | Passed | req/s median | CPU cores (median) |
|---:|---:|---|---|---|---:|---:|---|
| 5 | 1 | 23220.9 (23220.9-23220.9) | 0.36 (0.36) | 6.67 (6.67) | 0/1 | 9.2 | app 0.25 + sql 2.80 |
| 10 | 1 | 30000.1 (30000.1-30000.1) | 6.04 (6.04) | 31.58 (31.58) | 0/1 | 13.7 | app 0.27 + sql 3.03 |
| 50 | 1 | 30000.8 (30000.8-30000.8) | 23.25 (23.25) | 85.20 (85.20) | 0/1 | 15.7 | app 0.23 + sql 2.72 |

Resources (medians over the repeats; CPU in cores of the 4):

| Rate | app | SQL | app + SQL | other processes on the 4 cores | app threads max | SQL sessions max | SQL blocked max | SQL runnable avg | k6 cores |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 0.25 | 2.80 | 3.05 | 0.15 | 36 | 35 | 0 | 4.6 | 0.18 |
| 10 | 0.27 | 3.03 | 3.30 | 0.01 | 36 | 50 | 3 | 6.5 | 0.20 |
| 50 | 0.23 | 2.72 | 2.95 | 0.16 | 35 | 100 | 0 | 5.3 | 0.12 |

Top SQL waits per step (ms of wait per second of the step, warm-up included):

- r1 rate 5: RESOURCE_SEMAPHORE=22626;SOS_SCHEDULER_YIELD=3932;IO_COMPLETION=81;WRITELOG=35 (host load 9.07 -> 13.32)
- r1 rate 10: RESOURCE_SEMAPHORE=40410;SOS_SCHEDULER_YIELD=5488;IO_COMPLETION=64;PREEMPTIVE_OS_GENERICOPS=53 (host load 13.32 -> 8.77)
- r1 rate 50: RESOURCE_SEMAPHORE=96916;SOS_SCHEDULER_YIELD=4140;WRITELOG=39;PREEMPTIVE_OS_GENERICOPS=34 (host load 7.81 -> 9.36)

Highest passing rate per repeat: r1 None
Sustained (passed in every repeat that ran it, and every lower rate too): None; first rate with a failing repeat: 5

