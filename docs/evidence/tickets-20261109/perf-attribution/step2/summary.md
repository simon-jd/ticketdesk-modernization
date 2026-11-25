# Direct p95 by snapshot (interleaved, 2 runs each)

## baseline-s-legacy

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 2 | 1165 | 24.6 | 59.7 | 76.0 | 0.00% | 73.9 / 34.4 / 72.0 / 27.4 / 69.0 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1166 | 24.3 | 58.6 | 74.2 | 0.00% | 100.00% | 17 |
| direct-run2 | 1164 | 24.8 | 60.7 | 77.8 | 0.00% | 100.00% | 115 |

## baseline-s

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 2 | 1151 | 27.9 | 71.6 | 81.9 | 0.00% | 80.6 / 43.7 / 75.5 / 29.1 / 71.7 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1165 | 28.2 | 73.4 | 83.3 | 0.00% | 100.00% | 24 |
| direct-run2 | 1137 | 27.6 | 69.9 | 80.6 | 0.00% | 100.00% | 1 |

## baseline-s-legacy-schema
prepare: `ops/dev/perf-variant-schema.sh`

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 2 | 1162 | 24.4 | 63.4 | 87.4 | 0.00% | 84.8 / 37.8 / 67.2 / 27.7 / 62.4 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1152 | 25.0 | 62.7 | 80.0 | 0.00% | 100.00% | 86 |
| direct-run2 | 1172 | 23.9 | 64.1 | 94.8 | 0.00% | 100.00% | 11 |

