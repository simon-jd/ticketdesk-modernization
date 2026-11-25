# Notification coexistence, run 2 (2026-11-10 23:15Z, snapshot baseline-s-search) — the reference run

Same scenario as run 1 (`../notification-coexistence-run1/README.md`), with the checker that models the legacy broadcast
count (L9) and probes of the broadcast destinations and the legacy worker process at each phase (`probes.txt`).

- Writes (`writes-new.json`, `writes-legacy.json`): phase 1 answered by the new app (create 10001 `modern; route=ticket.new.post`,
  comment on 10001 `modern; route=activity.add-comment`); phase 2 switched back (create 10002 and comment on 10001, both
  `legacy; ...; reason=switched-back`).
- Mails (`result.json`, `mail/`): **11 = worker 5 + legacy 6, as expected**. Checks: every write mailed exactly once per
  expected recipient by the app that made it **PASS**; no recipient got the same write twice **PASS**; both apps wrote and both
  sent **PASS**. Ticket 10001, written by both apps, got the worker's mails for its two new-app events and the legacy mails
  for the legacy comment only.
- `probes.txt`: one broadcast destination row and one legacy w3wp process from before phase 1 to the end, so the second
  legacy [New] mail of 10002 is L9 (one broadcast per subscriber of the new ticket), not a second destination.
- Legacy side (`push-items.txt`): push items only for the legacy writes (10002 new ticket x 2, 10002 x 2, 10001 x 2 — the
  legacy comment), all Sent; none for the new app's writes (it writes no push items).
- The worker ran as the built image (`run.txt`: UID 1654) with the least-privilege login `td_modern_app`.
