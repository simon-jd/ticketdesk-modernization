# Search as an internal user (S12 filter) at scale L: before and after td-db V0012

Same query (`search-internal-user`: "printer" as the owner with the most tickets, td-sqlperf, 20 warm + 5 cold runs) on
td-sql `TicketDeskLSearch` (copy of TicketDeskL with V0011; V0006 and V0012 applied by the migrate in `migrate-*.txt`).

| Step | SQL | Warm median server ms | Cold median ms | Logical reads |
|---|---|---:|---:|---:|
| before (`before/`) | EXISTS (subscriber) per hit | 138.5 | 303 | 1,033,583 |
| V0012 only (`after-index-same-sql/`) | same SQL | 136 | 300 | 1,033,583 (plan unchanged) |
| V0012 + query (`after/`) | the user's subscriptions read once (index seek) and hash-joined | **118.5** | **247** | **111,365** |

The remaining reads are the Tickets rows the hits join for Owner / CreatedBy / AssignedTo (no full index on those columns).
Summaries: `*/summary-search.md`; plans packed in `*/plans.tar.xz`.
