# Replay summary

Compared 262, matched 199, mismatched 64 (missing 0, extra 1); unexplained mismatches: 0.

Answered by the new app: 186 exchanges on 49 routes; by the legacy app: 77.

| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 2 | 2 | 0 | 0 | 0 | 0 |
| 1 | 35 | 35 | 0 | 1 | 1 | 0 |
| 2 | 98 | 76 | 22 | 29 | 29 | 0 |
| 3 | 11 | 11 | 0 | 4 | 4 | 0 |
| 4 | 62 | 62 | 0 | 29 | 29 | 0 |
| 5 | 50 | 0 | 50 | 1 | 1 | 0 |
| retire | 5 | 0 | 5 | 0 | 0 | 0 |

## Routes answered by the new app

| Route id | Wave | Exchanges |
|---|---|---:|
| account.manage | 2 | 1 |
| activity.add-comment | 4 | 2 |
| activity.assign | 4 | 2 |
| activity.buttons | 2 | 6 |
| activity.cancel-more-info | 4 | 1 |
| activity.close | 4 | 1 |
| activity.edit-ticket-info | 4 | 1 |
| activity.force-close | 4 | 1 |
| activity.give-up | 4 | 1 |
| activity.load | 4 | 21 |
| activity.modify-attachments | 4 | 1 |
| activity.pass | 4 | 1 |
| activity.reassign | 4 | 1 |
| activity.reopen | 4 | 1 |
| activity.request-more-info | 4 | 2 |
| activity.resolve | 4 | 1 |
| activity.supply-more-info | 4 | 1 |
| activity.take-over | 4 | 4 |
| admin.demo | 2 | 1 |
| admin.projects | 2 | 5 |
| admin.search-manager | 2 | 1 |
| admin.users | 2 | 7 |
| admin.users-page | 2 | 1 |
| autocomplete.tag-list | 2 | 1 |
| bundles.scripts | 0 | 1 |
| bundles.styles | 0 | 1 |
| file.attachments-info | 2 | 2 |
| file.delete | 4 | 1 |
| file.get | 2 | 1 |
| file.upload | 4 | 3 |
| home.about | 1 | 1 |
| home.access-denied | 1 | 4 |
| home.index | 1 | 1 |
| home.language | 3 | 2 |
| home.root | 1 | 29 |
| navigation.change-project | 3 | 2 |
| ticket.details | 2 | 5 |
| ticket.events | 2 | 6 |
| ticket.files | 2 | 7 |
| ticket.list-redirect | 2 | 1 |
| ticket.new.get | 4 | 8 |
| ticket.new.post | 4 | 8 |
| ticket.subscription | 3 | 2 |
| ticket.view | 2 | 14 |
| tickets.filter | 3 | 1 |
| tickets.index | 2 | 16 |
| tickets.page | 2 | 1 |
| tickets.reset-lists | 3 | 1 |
| tickets.sort | 3 | 3 |

## Mismatches

| Key | Request | Route | Answered by | Kinds | Expected difference |
|---|---|---|---|---|---|
| `ticket-list#002.0` | GET /tickets | tickets.index | new | header, set-cookie, body-html | S3 |
| `ticket-list#003.1` | GET /tickets | tickets.index | new | header, body-html | S3 |
| `ticket-list#004.0` | GET /tickets/mytickets | tickets.index | new | header, body-html | S3 |
| `ticket-list#005.0` | GET /tickets/unassigned | tickets.index | new | header, body-html | S3 |
| `ticket-list#006.0` | GET /tickets/assignedToMe | tickets.index | new | header, body-html | S3 |
| `ticket-list#007.0` | GET /tickets/historytickets | tickets.index | new | header, body-html | S3 |
| `ticket-list#008.0` | GET /tickets/opentickets | tickets.index | new | header, body-html | S3 |
| `ticket-list#009.0` | GET /tickets/pageList/opentickets/2 | tickets.page | new | header, body-html | S3 |
| `ticket-list#010.0` | POST /tickets/filterList | tickets.filter | new | header, body-html | S3 |
| `ticket-list#011.0` | GET /tickets/sortList/opentickets?columnName=Title | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#012.0` | GET /tickets/sortList/opentickets?columnName=Title | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#013.0` | GET /tickets/sortList/opentickets?columnName=Priority&isMultiSort=true | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#014.1` | GET /tickets | tickets.index | new | header, body-html | S3 |
| `ticket-list#017.0` | GET /tickets/opentickets | tickets.index | new | header, body-html | S3 |
| `ticket-create-view#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-create-view#003.1` | GET /ticket/10001 | ticket.view | new | header, body-html | S3 |
| `ticket-create-view#004.0` | GET /ticket/10001 | ticket.view | new | header, body-html | S3 |
| `ticket-create-view#011.1` | GET /tickets | tickets.index | new | header, body-html | S3 |
| `ticket-edit#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-edit#003.1` | GET /ticket/10002 | ticket.view | new | header, body-html | S3 |
| `ticket-edit#004.0` | GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo | activity.load | new | header | S3 |
| `ticket-comment#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-comment#003.1` | GET /ticket/10003 | ticket.view | new | header, body-html | S3 |
| `ticket-comment#004.0` | GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment | activity.load | new | header | S3 |
| `ticket-assign#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-assign#003.1` | GET /ticket/10004 | ticket.view | new | header, body-html | S3 |
| `ticket-assign#004.0` | GET /ticket-activity/load-activity?ticketId=10004&activity=Assign | activity.load | new | header | S3 |
| `ticket-close#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-close#003.1` | GET /ticket/10005 | ticket.view | new | header, body-html | S3 |
| `ticket-close#004.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=TakeOver | activity.load | new | header | S3 |
| `ticket-close#006.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Resolve | activity.load | new | header | S3 |
| `ticket-close#008.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Close | activity.load | new | header | S3 |
| `ticket-close#010.0` | GET /ticket/10005 | ticket.view | new | header, body-html | S3 |
| `ticket-workflow#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `ticket-workflow#003.1` | GET /ticket/10006 | ticket.view | new | header, body-html | S3 |
| `ticket-workflow#004.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | activity.load | new | header | S3 |
| `ticket-workflow#006.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo | activity.load | new | header | S3 |
| `ticket-workflow#008.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=CancelMoreInfo | activity.load | new | header | S3 |
| `ticket-workflow#010.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo | activity.load | new | header | S3 |
| `ticket-workflow#012.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=SupplyMoreInfo | activity.load | new | header | S3 |
| `ticket-workflow#014.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=Pass | activity.load | new | header | S3 |
| `ticket-workflow#016.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ReAssign | activity.load | new | header | S3 |
| `ticket-workflow#018.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | activity.load | new | header | S3 |
| `ticket-workflow#020.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=GiveUp | activity.load | new | header | S3 |
| `ticket-workflow#022.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=Assign | activity.load | new | header | S3 |
| `ticket-workflow#024.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | activity.load | new | header | S3 |
| `ticket-workflow#026.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ForceClose | activity.load | new | header | S3 |
| `ticket-workflow#028.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ReOpen | activity.load | new | header | S3 |
| `ticket-workflow#030.0` | GET /ticket/10006 | ticket.view | new | header, body-html | S3 |
| `attachments#002.0` | GET /ticket/new | ticket.new.get | new | header, set-cookie, body-html | S3 |
| `attachments#006.0` | GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9 | file.attachments-info | new | status, body-text | L1 |
| `attachments#007.1` | GET /ticket/10007 | ticket.view | new | header, body-html | S3 |
| `attachments#011.0` | GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments | activity.load | new | header | S3 |
| `admin-settings#010.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#004.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#005.0` | POST /admin/push-notification-settings | admin.push-settings.post | legacy | body-html | G6 |
| `internal-user#002.0` | GET /tickets | tickets.index | new | header, set-cookie, body-html | S3 |
| `internal-user#003.0` | GET /tickets/mytickets | tickets.index | new | header, body-html | S3 |
| `internal-user#004.0` | GET /tickets/unassigned | tickets.index | new | status, location, body-html | L2 |
| `internal-user#004.1` | GET /access-denied | home.access-denied | new | extra | L2 |
| `internal-user#006.0` | GET /ticket/new | ticket.new.get | new | header, body-html | S3 |
| `internal-user#007.1` | GET /ticket/10008 | ticket.view | new | header, body-html | S3 |
| `internal-user#008.0` | GET /ticket/10008 | ticket.view | new | header, body-html | S3 |
| `internal-user#010.0` | GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment | activity.load | new | header | S3 |
