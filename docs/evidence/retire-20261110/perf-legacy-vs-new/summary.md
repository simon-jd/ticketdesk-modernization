# p95 by snapshot and target (interleaved, 5 runs each)

## baseline-s-legacy (target direct)

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| direct | 5 | 1158 | 26.2 | 87.1 | 109.9 | 0.00% | 103.3 / 57.0 / 107.5 / 44.7 / 100.3 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| direct-run1 | 1155 | 26.2 | 65.5 | 87.9 | 0.00% | 100.00% | 44 |
| direct-run2 | 1163 | 26.3 | 87.1 | 109.9 | 0.00% | 100.00% | 100 |
| direct-run3 | 1164 | 25.9 | 89.7 | 113.2 | 0.00% | 100.00% | 116 |
| direct-run4 | 1158 | 24.7 | 63.6 | 92.1 | 0.00% | 100.00% | 28 |
| direct-run5 | 1155 | 28.4 | 96.8 | 118.0 | 0.00% | 100.00% | 68 |

## baseline-s-search+new-alone (target modern)
prepare: `ops/dev/td-db-app-login.sh apply && ops/dev/vm-db-contract.sh`

| Target | Runs | Requests (median) | p50 ms | p95 ms | p99 ms | Error rate | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---:|---:|---:|---|
| modern | 5 | 1138 | 9.8 | 39.4 | 48.8 | 0.00% | 44.7 / 28.5 / 52.6 / 23.9 / 25.2 |

Per run:

| Run | Requests | p50 | p95 | p99 | Errors | Checks | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| modern-run1 | 1138 | 10.7 | 42.3 | 53.0 | 0.00% | 100.00% | 0 |
| modern-run2 | 1140 | 10.2 | 39.4 | 48.8 | 0.00% | 100.00% | 0 |
| modern-run3 | 1137 | 5.6 | 17.5 | 24.2 | 0.00% | 100.00% | 0 |
| modern-run4 | 1140 | 8.8 | 36.8 | 47.8 | 0.00% | 100.00% | 0 |
| modern-run5 | 1138 | 9.8 | 41.6 | 50.4 | 0.00% | 100.00% | 0 |

