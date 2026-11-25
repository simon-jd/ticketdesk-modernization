# Replay summary

Compared 262, matched 240, mismatched 23 (missing 0, extra 1); unexplained mismatches: 0.

Answered by the new app: 91 exchanges on 24 routes; by the legacy app: 172.

| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 2 | 2 | 0 | 0 | 0 | 0 |
| 1 | 35 | 35 | 0 | 1 | 1 | 0 |
| 2 | 98 | 45 | 53 | 17 | 17 | 0 |
| 3 | 11 | 9 | 2 | 4 | 4 | 0 |
| 4 | 62 | 0 | 62 | 0 | 0 | 0 |
| 5 | 50 | 0 | 50 | 1 | 1 | 0 |
| retire | 5 | 0 | 5 | 0 | 0 | 0 |

## Routes answered by the new app

| Route id | Wave | Exchanges |
|---|---|---:|
| account.manage | 2 | 1 |
| admin.demo | 2 | 1 |
| admin.projects | 2 | 5 |
| admin.search-manager | 2 | 1 |
| admin.users | 2 | 7 |
| admin.users-page | 2 | 1 |
| autocomplete.tag-list | 2 | 1 |
| bundles.scripts | 0 | 1 |
| bundles.styles | 0 | 1 |
| file.attachments-info | 2 | 2 |
| file.get | 2 | 1 |
| home.about | 1 | 1 |
| home.access-denied | 1 | 4 |
| home.index | 1 | 1 |
| home.language | 3 | 2 |
| home.root | 1 | 29 |
| navigation.change-project | 3 | 2 |
| ticket.files | 2 | 7 |
| ticket.list-redirect | 2 | 1 |
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
| `ticket-create-view#011.1` | GET /tickets | tickets.index | new | header, set-cookie, body-html | S3 |
| `attachments#006.0` | GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9 | file.attachments-info | new | status, body-text | L1 |
| `admin-settings#010.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#004.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#005.0` | POST /admin/push-notification-settings | admin.push-settings.post | legacy | body-html | G6 |
| `internal-user#002.0` | GET /tickets | tickets.index | new | header, set-cookie, body-html | S3 |
| `internal-user#003.0` | GET /tickets/mytickets | tickets.index | new | header, body-html | S3 |
| `internal-user#004.0` | GET /tickets/unassigned | tickets.index | new | status, location, body-html | L2 |
| `internal-user#004.1` | GET /access-denied | home.access-denied | new | extra | L2 |
