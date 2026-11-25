# Legacy EF6 SQL replayed at scale L: original schema vs expanded schema (coexistence)

Before: `legacy` (2026-11-08T01:28:02.7448157+00:00); after: `legacy` (2026-11-08T01:28:41.6385178+00:00). Median of the measured runs; p95 nearest rank.

| Scenario | Mode | Server elapsed ms before -> after (median, p95) | CPU ms before -> after (median) | Logical reads before -> after | Reads ratio |
|---|---|---|---|---|---|
| legacy-ticket-center-default-list-help-desk-unassigned | cold | 204 (211) -> 233 (247) | 709 -> 773 | 215,014 -> 216,558 | 1x |
| legacy-ticket-center-default-list-help-desk-unassigned | warm | 89 (143) -> 89 (134) | 652 -> 659 | 215,014 -> 216,558 | 1x |
| legacy-list-unassigned | cold | 192 (202) -> 227 (236) | 700 -> 751 | 214,461 -> 216,005 | 1x |
| legacy-list-unassigned | warm | 79.5 (144) -> 83 (143) | 590 -> 620 | 214,461 -> 216,005 | 1x |
| legacy-list-assignedtome | cold | 139 (158) -> 173 (177) | 302 -> 372 | 107,261 -> 108,033 | 1x |
| legacy-list-assignedtome | warm | 28.5 (38) -> 32 (34) | 220 -> 248 | 107,261 -> 108,033 | 1x |
| legacy-list-mytickets | cold | 177 (185) -> 213 (218) | 584 -> 648 | 214,461 -> 216,005 | 1x |
| legacy-list-mytickets | warm | 66.5 (118) -> 68 (117) | 495 -> 520 | 214,461 -> 216,005 | 1x |
| legacy-list-opentickets | cold | 246 (279) -> 215 (217) | 1,089 -> 894 | 214,461 -> 111,169 | 1.9x |
| legacy-list-opentickets | warm | 128 (172) -> 150 (153) | 956 -> 850 | 214,461 -> 111,169 | 1.9x |
| legacy-list-historytickets | cold | 241 (318) -> 48 (49) | 1,039 -> 43 | 214,461 -> 4,207 | 51x |
| legacy-list-historytickets | warm | 132 (176) -> 33 (42) | 1,001 -> 33 | 214,461 -> 3,263 | 65.7x |
| legacy-opentickets-page-2-partial | cold | 264 (290) -> 246 (286) | 1,253 -> 1,030 | 214,445 -> 111,153 | 1.9x |
| legacy-opentickets-page-2-partial | warm | 134 (139) -> 151 (216) | 1,014 -> 872 | 214,445 -> 111,153 | 1.9x |
| legacy-opentickets-sort-by-title-partial | cold | 239 (245) -> 216 (217) | 1,022 -> 969 | 214,455 -> 111,163 | 1.9x |
| legacy-opentickets-sort-by-title-partial | warm | 136 (142) -> 149 (161) | 1,032 -> 845 | 214,455 -> 111,163 | 1.9x |
| legacy-opentickets-sort-by-title-again-descending | cold | 243 (261) -> 217 (266) | 1,022 -> 1,021 | 214,455 -> 111,163 | 1.9x |
| legacy-opentickets-sort-by-title-again-descending | warm | 122 (200) -> 142 (147) | 927 -> 786 | 214,455 -> 111,163 | 1.9x |
| legacy-opentickets-add-priority-to-the-sort | cold | 250 (286) -> 218 (223) | 1,054 -> 837 | 214,455 -> 111,163 | 1.9x |
| legacy-opentickets-add-priority-to-the-sort | warm | 123 (138) -> 173 (195) | 924 -> 902 | 214,455 -> 111,163 | 1.9x |
| legacy-mytickets-filter-status-active-assignedto-anyone | cold | 178 (180) -> 210 (211) | 573 -> 606 | 214,455 -> 215,999 | 1x |
| legacy-mytickets-filter-status-active-assignedto-anyone | warm | 67 (75) -> 63 (64) | 508 -> 480 | 214,455 -> 215,999 | 1x |
| legacy-unassigned-filter-status-any | cold | 195 (252) -> 227 (231) | 758 -> 722 | 214,455 -> 215,999 | 1x |
| legacy-unassigned-filter-status-any | warm | 79.5 (92) -> 85 (161) | 592 -> 630 | 214,455 -> 215,999 | 1x |
| legacy-opentickets-to-pick-a-ticket | cold | 269 (284) -> 218 (227) | 1,172 -> 957 | 214,461 -> 111,169 | 1.9x |
| legacy-opentickets-to-pick-a-ticket | warm | 130 (185) -> 150 (207) | 982 -> 860 | 214,461 -> 111,169 | 1.9x |
| legacy-ticket-page | cold | 1 (1) -> 1 (1) | 3 -> 1 | 57 -> 57 | 1x |
| legacy-ticket-page | warm | 0 (0) -> 0 (0) | 0 -> 0 | 57 -> 57 | 1x |
| legacy-ticket-details-panel | cold | 0 (1) -> 0 (1) | 1 -> 1 | 13 -> 13 | 1x |
| legacy-ticket-details-panel | warm | 0 (0) -> 0 (0) | 0 -> 0 | 13 -> 13 | 1x |
| legacy-ticket-events-panel | cold | 1 (2) -> 0 (1) | 1 -> 1 | 10 -> 10 | 1x |
| legacy-ticket-events-panel | warm | 0 (0) -> 0 (0) | 0 -> 0 | 10 -> 10 | 1x |
| legacy-ticket-files-panel | cold | 0 (1) -> 0 (0) | 0 -> 0 | 2 -> 2 | 1x |
| legacy-ticket-files-panel | warm | 0 (0) -> 0 (0) | 0 -> 0 | 2 -> 2 | 1x |
