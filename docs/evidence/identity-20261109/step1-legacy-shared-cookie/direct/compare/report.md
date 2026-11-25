# Replay comparison report

Generated 2026-11-09 06:46:16Z

| | Baseline | Candidate |
|---|---|---|
| File | `~/Desktop/ticketdesk-modernization/wt-identity/docs/evidence/identity-20261109/step1-legacy-shared-cookie/golden.jsonl` | `~/Desktop/ticketdesk-modernization/wt-identity/docs/evidence/identity-20261109/step1-legacy-shared-cookie/direct/candidate.jsonl` |
| Target | https://localhost:8443/ | https://localhost:8443/ |
| Run | legacy-run1 | candidate-direct |
| Exchanges | 262 | 262 |
| Journey errors | 0 | 0 |

**Compared 262, matched 262, mismatched 0** (missing in candidate 0, extra in candidate 0).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /` | 29 | 0 |  |
| `GET /about` | 1 | 0 |  |
| `GET /access-denied` | 3 | 0 |  |
| `GET /account/change-password` | 1 | 0 |  |
| `GET /account/edit-profile` | 1 | 0 |  |
| `GET /account/manage` | 1 | 0 |  |
| `GET /admin/application-settings/{action=index}` | 2 | 0 |  |
| `GET /admin/data-management/demo` | 1 | 0 |  |
| `GET /admin/new` | 2 | 0 |  |
| `GET /admin/project/new` | 2 | 0 |  |
| `GET /admin/project/{projectId:int}` | 4 | 0 |  |
| `GET /admin/projects` | 5 | 0 |  |
| `GET /admin/push-notification-settings/configure-provider` | 1 | 0 |  |
| `GET /admin/push-notification-settings/list-providers` | 1 | 0 |  |
| `GET /admin/push-notification-settings/{action=index}` | 2 | 0 |  |
| `GET /admin/search-manager` | 1 | 0 |  |
| `GET /admin/user/{id?}` | 3 | 0 |  |
| `GET /admin/users/page/{page:int?}` | 1 | 0 |  |
| `GET /admin/users/{page:int?}` | 7 | 0 |  |
| `GET /auto-complete/tag-list` | 1 | 0 |  |
| `GET /bundles/{bundle}` | 1 | 0 |  |
| `GET /content/{bundle}` | 1 | 0 |  |
| `GET /emailtest/index/{id}` | 1 | 0 |  |
| `GET /file/get-attachments-info` | 2 | 0 |  |
| `GET /file/{id}/{fileName}` | 1 | 0 |  |
| `GET /first-run-setup/check-upgrade-progress` | 1 | 0 |  |
| `GET /first-run-setup/legacy-migration` | 1 | 0 |  |
| `GET /first-run-setup/upgrade-database` | 1 | 0 |  |
| `GET /first-run-setup/{action=index}` | 1 | 0 |  |
| `GET /index` | 1 | 0 |  |
| `GET /language` | 2 | 0 |  |
| `GET /navigation/change-project/{projectId:int}` | 2 | 0 |  |
| `GET /search` | 3 | 0 |  |
| `GET /search/{term}` | 1 | 0 |  |
| `GET /ticket-activity/activity-buttons` | 6 | 0 |  |
| `GET /ticket-activity/load-activity` | 21 | 0 |  |
| `GET /ticket/new` | 8 | 0 |  |
| `GET /ticket/ticket-details` | 5 | 0 |  |
| `GET /ticket/ticket-events` | 6 | 0 |  |
| `GET /ticket/ticket-files` | 7 | 0 |  |
| `GET /ticket/{action=index}` | 1 | 0 |  |
| `GET /ticket/{id:int}` | 14 | 0 |  |
| `GET /tickets/pageList/{listName=mytickets}/{page:int?}` | 1 | 0 |  |
| `GET /tickets/reset-user-lists` | 1 | 0 |  |
| `GET /tickets/sortList/{listName=opentickets}/{page:int?}` | 3 | 0 |  |
| `GET /tickets/{listName?}/{page:int?}` | 16 | 0 |  |
| `GET /user/register` | 2 | 0 |  |
| `GET /user/sign-in` | 19 | 0 |  |
| `GET /user/sign-out` | 2 | 0 |  |
| `POST /account/change-password` | 1 | 0 |  |
| `POST /account/edit-profile` | 1 | 0 |  |
| `POST /admin/application-settings/{action=index}` | 1 | 0 |  |
| `POST /admin/new` | 1 | 0 |  |
| `POST /admin/project/delete/{projectId:int}` | 1 | 0 |  |
| `POST /admin/project/new` | 1 | 0 |  |
| `POST /admin/project/{projectId:int}` | 1 | 0 |  |
| `POST /admin/push-notification-settings/configure-provider` | 1 | 0 |  |
| `POST /admin/push-notification-settings/{action=index}` | 1 | 0 |  |
| `POST /admin/user/delete/{id?}` | 1 | 0 |  |
| `POST /admin/user/{id?}` | 1 | 0 |  |
| `POST /file/delete` | 1 | 0 |  |
| `POST /file/upload` | 3 | 0 |  |
| `POST /ticket-activity/add-comment` | 2 | 0 |  |
| `POST /ticket-activity/assign` | 2 | 0 |  |
| `POST /ticket-activity/cancel-more-info` | 1 | 0 |  |
| `POST /ticket-activity/close` | 1 | 0 |  |
| `POST /ticket-activity/edit-ticket-info` | 1 | 0 |  |
| `POST /ticket-activity/force-close` | 1 | 0 |  |
| `POST /ticket-activity/give-up` | 1 | 0 |  |
| `POST /ticket-activity/modify-attachments` | 1 | 0 |  |
| `POST /ticket-activity/pass` | 1 | 0 |  |
| `POST /ticket-activity/reassign` | 1 | 0 |  |
| `POST /ticket-activity/reopen` | 1 | 0 |  |
| `POST /ticket-activity/request-more-info` | 2 | 0 |  |
| `POST /ticket-activity/resolve` | 1 | 0 |  |
| `POST /ticket-activity/supply-more-info` | 1 | 0 |  |
| `POST /ticket-activity/take-over` | 4 | 0 |  |
| `POST /ticket/change-ticket-subscription` | 2 | 0 |  |
| `POST /ticket/new` | 8 | 0 |  |
| `POST /tickets/filterList/{listName=opentickets}/{page:int?}` | 1 | 0 |  |
| `POST /user/register` | 1 | 0 |  |
| `POST /user/sign-in` | 14 | 0 |  |
