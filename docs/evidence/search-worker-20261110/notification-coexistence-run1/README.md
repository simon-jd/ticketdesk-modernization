# Notification coexistence, run 1 (2026-11-10 23:09Z, snapshot baseline-s-search)

`TD_LOCK_OWNER=search-worker ops/dev/notification-coexistence.sh --out-dir ...` (`run.txt`): push notifications on in the shared
database (SMTP → host sink, consolidation off, broadcast to one custom address, destinations for the owner and the assignee);
gateway on :5444 publishing to the Service Bus emulator; worker container (UID 1654) on the host; phase 1 with the ticket
writes on the new app (create 10001 + comment), phase 2 with them switched back to legacy (create 10002 + comment on 10001).

Result (`result.json`): 11 mails; **worker 5 of 5 expected** (10001 create → owner, assignee, [New] broadcast; comment →
owner, assignee), **legacy 6 for 5 expected** (10002 create → owner, assignee, and **two** [New] broadcast mails; comment
on 10001 → owner, assignee). Every write was mailed only by the app that made it, and no write by both: ticket 10001, written
by both apps, got the worker's mails for its two new-app events and the legacy app's mails for its legacy comment
(`X-TicketDesk-Sender: worker` vs none). The extra mail is the legacy app's own: it held two push items "new ticket" for
10002 (`push-items.txt`) for one broadcast destination. Its new-ticket broadcast creates one item per *notification row* of
the create event, i.e. once per subscriber of the new ticket (`Startup.PushNotifications.cs`,
`CreateNewTicketBroadcastNotification` → `notes.ToNewTicketPushNotificationInfoCollection`), here owner and assignee → two
identical [New] mails; the worker sends the broadcast once per ticket (expected difference L9). The checker of run 1 expected
one legacy broadcast mail, so its first two checks failed on exactly that mail; the checker now expects the legacy count
(run 2).
