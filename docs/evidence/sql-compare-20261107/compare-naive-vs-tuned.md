# Naive port (TicketDeskLBase, no new indexes) vs tuned (TicketDeskL, procedures + indexes), scale L

Before: `naive` (2026-11-08T01:23:56.3847771+00:00); after: `tuned` (2026-11-08T01:27:39.8204544+00:00). Median of the measured runs; p95 nearest rank.

| Scenario | Mode | Server elapsed ms before -> after (median, p95) | CPU ms before -> after (median) | Logical reads before -> after | Reads ratio |
|---|---|---|---|---|---|
| list-default | cold | 263 (282) -> 53 (54) | 1,389 -> 41 | 109,130 -> 1,960 | 55.7x |
| list-default | warm | 182 (212) -> 35 (36) | 1,096 -> 35.5 | 109,130 -> 1,960 | 55.7x |
| list-open-p1 | cold | 284 (314) -> 53 (55) | 1,189 -> 41 | 214,548 -> 3,326 | 64.5x |
| list-open-p1 | warm | 146 (174) -> 51 (52) | 1,040 -> 50 | 214,548 -> 3,326 | 64.5x |
| list-open-p500 | cold | 274 (276) -> 62 (63) | 1,192 -> 46 | 214,510 -> 3,414 | 62.8x |
| list-open-p500 | warm | 148 (164) -> 36 (37) | 1,036 -> 36 | 214,510 -> 3,414 | 62.8x |
| list-unassigned | cold | 189 (192) -> 17 (19) | 640 -> 10 | 214,479 -> 2,062 | 104x |
| list-unassigned | warm | 74 (79) -> 4 (4) | 550 -> 4 | 214,479 -> 1,117 | 192x |
| list-assignedToMe | cold | 172 (177) -> 6 (8) | 516 -> 2 | 214,481 -> 203 | 1056.6x |
| list-assignedToMe | warm | 56 (59) -> 0 (0) | 427 -> 0 | 214,481 -> 203 | 1056.6x |
| list-mytickets-heavy | cold | 182 (186) -> 10 (11) | 588 -> 4 | 214,480 -> 705 | 304.2x |
| list-mytickets-heavy | warm | 67 (77) -> 2 (2) | 498 -> 2 | 214,480 -> 705 | 304.2x |
| list-mytickets-light | cold | 167 (179) -> 2 (3) | 534 -> 1 | 214,408 -> 43 | 4986.2x |
| list-mytickets-light | warm | 59 (66) -> 0 (0) | 454 -> 0 | 214,408 -> 43 | 4986.2x |
| list-history | cold | 246 (280) -> 77 (78) | 1,019 -> 63 | 214,557 -> 3,325 | 64.5x |
| list-history | warm | 124 (158) -> 33 (35) | 905 -> 33.5 | 214,557 -> 3,325 | 64.5x |
| list-project-big | cold | 164 (165) -> 17 (18) | 510 -> 11 | 107,721 -> 564 | 191x |
| list-project-big | warm | 58 (74) -> 7 (12) | 374 -> 8 | 107,721 -> 564 | 191x |
| list-project-small | cold | 146 (150) -> 9 (10) | 269 -> 1 | 107,384 -> 213 | 504.2x |
| list-project-small | warm | 27 (42) -> 0 (0) | 196 -> 0.5 | 107,384 -> 213 | 504.2x |
| list-status-sort-title | cold | 240 (273) -> 198 (209) | 989 -> 671 | 214,535 -> 110,740 | 1.9x |
| list-status-sort-title | warm | 118 (131) -> 80 (87) | 864 -> 541 | 214,535 -> 110,740 | 1.9x |
| list-tag | cold | 240 (247) -> 86 (89) | 905 -> 73 | 122,547 -> 3,062 | 40x |
| list-tag | warm | 136 (157) -> 105 (106) | 840 -> 105 | 122,547 -> 3,062 | 40x |
| detail | cold | 0 (1) -> 1 (1) | 1 -> 1 | 59 -> 59 | 1x |
| detail | warm | 0 (0) -> 0 (0) | 0 -> 0 | 59 -> 59 | 1x |
| report-open-aging | cold | 141 (145) -> 90 (99) | 460 -> 82 | 107,201 -> 3,705 | 28.9x |
| report-open-aging | warm | 41 (46) -> 46 (74) | 312 -> 46 | 107,201 -> 3,705 | 28.9x |
| report-workload | cold | 6,573 (6,656) -> 99 (104) | 14,023 -> 300 | 22,296,021 -> 19,194 | 1161.6x |
| report-workload | warm | 6,322 (6,468) -> 63.5 (67) | 14,112 -> 279 | 22,295,977 -> 19,096 | 1167.6x |
| report-time-to-close | cold | 138 (143) -> 73 (116) | 277 -> 283 | 107,201 -> 4,046 | 26.5x |
| report-time-to-close | warm | 26.5 (30) -> 78.5 (104) | 211 -> 329 | 107,201 -> 4,046 | 26.5x |
