# Replay summary

Compared 262, matched 258, mismatched 4 (missing 0, extra 0); unexplained mismatches: 0.

Answered by the new app: 64 exchanges on 17 routes; by the legacy app: 198.

| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 2 | 2 | 0 | 0 | 0 | 0 |
| 1 | 34 | 34 | 0 | 0 | 0 | 0 |
| 2 | 98 | 28 | 70 | 3 | 3 | 0 |
| 3 | 11 | 0 | 11 | 0 | 0 | 0 |
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
| home.access-denied | 1 | 3 |
| home.index | 1 | 1 |
| home.root | 1 | 29 |
| ticket.files | 2 | 7 |
| ticket.list-redirect | 2 | 1 |

## Migrated routes answered by legacy for some requests

Reason `identity`: the request carried the legacy sign-in cookie and remote authentication is off; `switched-back`/`group`: switched back in the registry (the route or another of its group).

| Route (reason) | Exchanges |
|---|---:|
| tickets.index (group) | 18 |
| tickets.page (group) | 1 |

## Mismatches

| Key | Request | Route | Answered by | Kinds | Expected difference |
|---|---|---|---|---|---|
| `attachments#006.0` | GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9 | file.attachments-info | new | status, body-text | L1 |
| `admin-settings#010.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#004.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#005.0` | POST /admin/push-notification-settings | admin.push-settings.post | legacy | body-html | G6 |
