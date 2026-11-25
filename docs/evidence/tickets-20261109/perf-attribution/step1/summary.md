# Direct p95 by snapshot (interleaved, 3 runs each)

## baseline-s-legacy

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 3 | 1165 | 24.5 | 61.7 | 86.8 | 0.00% | 77.8 / 41.1 / 84.0 / 33.2 / 69.5 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1168 | 23.5 | 61.7 | 86.8 | 0.00% | 100.00% | 12 |
| direct-run2 | 1152 | 24.5 | 53.7 | 80.0 | 0.00% | 100.00% | 65 |
| direct-run3 | 1165 | 26.1 | 95.0 | 119.1 | 0.00% | 100.00% | 31 |

## baseline-s

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 3 | 1158 | 29.8 | 90.2 | 107.4 | 0.00% | 100.7 / 69.7 / 108.1 / 51.0 / 94.9 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1158 | 27.3 | 67.2 | 97.4 | 0.00% | 100.00% | 114 |
| direct-run2 | 1160 | 30.2 | 90.2 | 107.4 | 0.00% | 100.00% | 26 |
| direct-run3 | 1153 | 29.8 | 96.2 | 117.1 | 0.00% | 100.00% | 83 |

