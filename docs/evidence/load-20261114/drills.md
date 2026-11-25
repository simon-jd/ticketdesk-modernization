# Failure drills on kind (Round 6)

Times in seconds after the fault. Failed = no expected answer (0 = connection refused/reset or the 30 s timeout). Last failure by class: 5xx / connection / 4xx (a 400 after a failed form page is the k6 VU posting a form whose page had failed, without its antiforgery token: client side). Recovered = end of the last bad second (failure or p95 > 500 ms), followed by >= 30 clean seconds. Burn = failed / (0.5 % of the run's requests); 30-day = failed / 0.5 % of 30 days at the run's request rate. Readiness = seconds without a Ready web pod / without a ready endpoint (1 s samples). Fault removed = the drill's recovery mark (pod-kill: a web pod Ready again, -0.2 with 2 replicas = the other one already was; db-egress: rules removed; sql-restart: SQL accepting connections; worker-kill: the third kill's hold released).

| Drill | Requests | Failed from the fault | First failure | Last 5xx / conn / 4xx | Fault removed | Recovered | Availability (run) | Run budget burn | Share of 30-day budget | p95 before / from the fault (ms) | Not ready |
|---|---:|---|---:|---|---:|---:|---:|---:|---:|---|---|
| db-egress | 46080 | 6349 of 29263 (0: 3, 400: 981, 500: 5365) | 15.6 | 80.4 / 60.1 / 132.6 | 60.4 | 133.0 | 86.222 % | 2755.6 % | 0.3827 % | 18.6 / 45.0 | 54 s / 54 s |
| pod-kill-2replicas | 51263 | 0 of 34433 () | None | None / None / None | -0.2 | 0.0 | 100.0 % | 0.0 % | 0.0 % | 26.0 / 27.7 | 0 s / 0 s |
| pod-kill | 51272 | 34 of 34450 (0: 32, 500: 2) | 1.1 | 6.4 / 3.1 / None | 3.4 | 6.0 | 99.934 % | 13.3 % | 0.0018 % | 23.5 / 26.0 | 3 s / 3 s |
| sql-restart | 50281 | 801 of 33474 (400: 111, 500: 690) | 30.4 | 35.4 / None / 68.5 | 36.3 | 69.1 | 98.407 % | 318.6 % | 0.0442 % | 23.5 / 24.4 | 0 s / 0 s |
| worker-kill | 51262 | 0 of 34398 () | None | None / None / None | 123.7 | 0.0 | 100.0 % | 0.0 % | 0.0 % | 25.7 / 24.5 | 0 s / 0 s |

Notifications (drill-worker-kill): expected 2633 (from dbo.TicketEventNotifications), delivered 2668 mails = 2665 distinct, lost 0, duplicated 3 (+3 extra mails), not in the expected set 32 (mails for events written before the drill's first event id, i.e. the reset's prime); held by the sink at the kills: 3; duplicates: ticket 10420 event 47005 at 05:35:25 and 05:36:25; ticket 10533 event 47710 at 05:36:26 and 05:37:26; ticket 10930 event 48450 at 05:37:27 and 05:38:27
