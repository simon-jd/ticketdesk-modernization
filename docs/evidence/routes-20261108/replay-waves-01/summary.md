# Replay summary

Compared 262, matched 259, mismatched 3 (missing 0, extra 0); unexplained mismatches: 0.

Answered by the new app: 36 exchanges on 6 routes; by the legacy app: 226.

| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 2 | 2 | 0 | 0 | 0 | 0 |
| 1 | 34 | 34 | 0 | 0 | 0 | 0 |
| 2 | 98 | 0 | 98 | 2 | 2 | 0 |
| 3 | 11 | 0 | 11 | 0 | 0 | 0 |
| 4 | 62 | 0 | 62 | 0 | 0 | 0 |
| 5 | 50 | 0 | 50 | 1 | 1 | 0 |
| retire | 5 | 0 | 5 | 0 | 0 | 0 |

## Routes answered by the new app

| Route id | Wave | Exchanges |
|---|---|---:|
| bundles.scripts | 0 | 1 |
| bundles.styles | 0 | 1 |
| home.about | 1 | 1 |
| home.access-denied | 1 | 3 |
| home.index | 1 | 1 |
| home.root | 1 | 29 |

## Mismatches

| Key | Request | Route | Answered by | Kinds | Expected difference |
|---|---|---|---|---|---|
| `admin-settings#010.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#004.0` | GET /admin/push-notification-settings | admin.push-settings.get | legacy | body-html | G6 |
| `admin-changes#005.0` | POST /admin/push-notification-settings | admin.push-settings.post | legacy | body-html | G6 |
