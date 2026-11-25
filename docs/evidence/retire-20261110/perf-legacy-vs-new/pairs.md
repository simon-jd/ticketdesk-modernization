# Back-to-back pairs: baseline-s-legacy vs baseline-s-search+new-alone

5 pairs; in each pair the baseline-s-legacy run came first, then the baseline-s-search+new-alone run (each from a fresh VM reset).

| Pair | baseline-s-legacy p50 / p95 / p99 ms | baseline-s-search+new-alone p50 / p95 / p99 ms | p95 ratio (B / A) | requests A / B | errors A / B |
|---|---|---|---:|---|---|
| 1 | 26.2 / 65.5 / 87.9 | 10.7 / 42.3 / 53.0 | 0.65 | 1155 / 1138 | 0.00 % / 0.00 % |
| 2 | 26.3 / 87.1 / 109.9 | 10.2 / 39.4 / 48.8 | 0.45 | 1163 / 1140 | 0.00 % / 0.00 % |
| 3 | 25.9 / 89.7 / 113.2 | 5.6 / 17.5 / 24.2 | 0.19 | 1164 / 1137 | 0.00 % / 0.00 % |
| 4 | 24.7 / 63.6 / 92.1 | 8.8 / 36.8 / 47.8 | 0.58 | 1158 / 1140 | 0.00 % / 0.00 % |
| 5 | 28.4 / 96.8 / 118.0 | 9.8 / 41.6 / 50.4 | 0.43 | 1155 / 1138 | 0.00 % / 0.00 % |

| Median over the pairs | p50 ms | p95 ms | p99 ms | p95 by op (list / view / filter / comment / create) |
|---|---:|---:|---:|---|
| baseline-s-legacy | 26.2 | 87.1 | 109.9 | 103.3 / 57.0 / 107.5 / 44.7 / 100.3 |
| baseline-s-search+new-alone | 9.8 | 39.4 | 48.8 | 44.7 / 28.5 / 52.6 / 23.9 / 25.2 |

p95 ratio B / A per pair: median 0.45, range 0.19-0.65; baseline-s-search+new-alone faster at p95 in 5 of 5 pairs.
