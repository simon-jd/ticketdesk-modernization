# EF6-shaped writes (rolled back): original schema vs expanded schema (7 more indexes on Tickets, 1 on TicketTags, AssignedDate trigger)

Before: `writes` (2026-11-08T01:32:52.4359118+00:00); after: `writes` (2026-11-08T01:32:53.2661512+00:00). Median of the measured runs; p95 nearest rank.

| Scenario | Mode | Server elapsed ms before -> after (median, p95) | CPU ms before -> after (median) | Logical reads before -> after | Reads ratio |
|---|---|---|---|---|---|
| write-reassign | cold | 0 (1) -> 4 (5) | 0 -> 1 | 6 -> 47 | 0.1x |
| write-reassign | warm | 0 (0) -> 0 (0) | 0 -> 0 | 6 -> 47 | 0.1x |
| write-insert | cold | 2 (2) -> 5 (6) | 1 -> 1 | 12 -> 36 | 0.3x |
| write-insert | warm | 0 (0) -> 0 (0) | 0 -> 0 | 12 -> 36 | 0.3x |
