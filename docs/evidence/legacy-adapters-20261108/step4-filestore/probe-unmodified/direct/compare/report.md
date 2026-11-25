# Replay comparison report

Generated 2026-11-08 20:59:34Z

| | Baseline | Candidate |
|---|---|---|
| File | `docs/evidence/legacy-adapters-20261108/step4-filestore/probe-unmodified/golden.jsonl` | `docs/evidence/legacy-adapters-20261108/step4-filestore/probe-unmodified/direct/candidate.jsonl` |
| Target | https://localhost:8443/ | https://localhost:8443/ |
| Run | legacy-run1 | candidate-direct |
| Exchanges | 262 | 175 |
| Journey errors | 0 | 8 |

**Compared 175, matched 162, mismatched 100** (missing in candidate 87, extra in candidate 0).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /ticket-activity/load-activity` | 21 | 21 | missing×21 |
| `GET /ticket/{id:int}` | 14 | 13 | missing×13 |
| `POST /ticket/new` | 8 | 8 | error×8, status×8, location×8, body-html×8 |
| `GET /ticket/ticket-files` | 7 | 7 | missing×7 |
| `GET /ticket-activity/activity-buttons` | 6 | 6 | missing×6 |
| `GET /ticket/ticket-events` | 6 | 6 | missing×6 |
| `GET /ticket/ticket-details` | 5 | 5 | missing×5 |
| `POST /ticket-activity/take-over` | 4 | 4 | missing×4 |
| `POST /file/upload` | 3 | 3 | status×2, header×2, body-text×2, missing×1 |
| `GET /file/get-attachments-info` | 2 | 2 | header×1, missing×1 |
| `POST /ticket-activity/add-comment` | 2 | 2 | missing×2 |
| `POST /ticket-activity/assign` | 2 | 2 | missing×2 |
| `POST /ticket-activity/request-more-info` | 2 | 2 | missing×2 |
| `POST /ticket/change-ticket-subscription` | 2 | 2 | missing×2 |
| `GET /` | 29 | 1 | missing×1 |
| `GET /auto-complete/tag-list` | 1 | 1 | body-json×1 |
| `GET /file/{id}/{fileName}` | 1 | 1 | missing×1 |
| `GET /tickets/{listName?}/{page:int?}` | 16 | 1 | missing×1 |
| `GET /user/sign-out` | 2 | 1 | missing×1 |
| `POST /file/delete` | 1 | 1 | status×1, header×1, body-text×1 |
| `POST /ticket-activity/cancel-more-info` | 1 | 1 | missing×1 |
| `POST /ticket-activity/close` | 1 | 1 | missing×1 |
| `POST /ticket-activity/edit-ticket-info` | 1 | 1 | missing×1 |
| `POST /ticket-activity/force-close` | 1 | 1 | missing×1 |
| `POST /ticket-activity/give-up` | 1 | 1 | missing×1 |
| `POST /ticket-activity/modify-attachments` | 1 | 1 | missing×1 |
| `POST /ticket-activity/pass` | 1 | 1 | missing×1 |
| `POST /ticket-activity/reassign` | 1 | 1 | missing×1 |
| `POST /ticket-activity/reopen` | 1 | 1 | missing×1 |
| `POST /ticket-activity/resolve` | 1 | 1 | missing×1 |
| `POST /ticket-activity/supply-more-info` | 1 | 1 | missing×1 |
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
| `GET /bundles/{bundle}` | 1 | 0 |  |
| `GET /content/{bundle}` | 1 | 0 |  |
| `GET /emailtest/index/{id}` | 1 | 0 |  |
| `GET /first-run-setup/check-upgrade-progress` | 1 | 0 |  |
| `GET /first-run-setup/legacy-migration` | 1 | 0 |  |
| `GET /first-run-setup/upgrade-database` | 1 | 0 |  |
| `GET /first-run-setup/{action=index}` | 1 | 0 |  |
| `GET /index` | 1 | 0 |  |
| `GET /language` | 2 | 0 |  |
| `GET /navigation/change-project/{projectId:int}` | 2 | 0 |  |
| `GET /search` | 3 | 0 |  |
| `GET /search/{term}` | 1 | 0 |  |
| `GET /ticket/new` | 8 | 0 |  |
| `GET /ticket/{action=index}` | 1 | 0 |  |
| `GET /tickets/pageList/{listName=mytickets}/{page:int?}` | 1 | 0 |  |
| `GET /tickets/reset-user-lists` | 1 | 0 |  |
| `GET /tickets/sortList/{listName=opentickets}/{page:int?}` | 3 | 0 |  |
| `GET /user/register` | 2 | 0 |  |
| `GET /user/sign-in` | 19 | 0 |  |
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
| `POST /tickets/filterList/{listName=opentickets}/{page:int?}` | 1 | 0 |  |
| `POST /user/register` | 1 | 0 |  |
| `POST /user/sign-in` | 14 | 0 |  |

## Journey errors

- https://localhost:8443/ ticket-create-view#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ ticket-edit#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ ticket-comment#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ ticket-assign#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ ticket-close#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ ticket-workflow#003.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ attachments#007.0: step 'create ticket with attachment' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing
- https://localhost:8443/ internal-user#007.0: step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing

## Mismatches

### ticket-list#015.0 — GET /auto-complete/tag-list?term=net

Route `GET /auto-complete/tag-list`, step `tag autocomplete`, kinds: body-json

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| json-value | `$[0].id` | "network-041" | "network" |
| json-value | `$[0].text` | "network-041" | "network" |
| json-value | `$[2].id` | "network" | "network-041" |
| json-value | `$[2].text` | "network" | "network-041" |

### ticket-create-view#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-create-view#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-create-view#003.1 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#003.1` | GET /ticket/10001 | not present in candidate run |

### ticket-create-view#004.0 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `view ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#004.0` | GET /ticket/10001 | not present in candidate run |

### ticket-create-view#005.0 — GET /ticket/ticket-details?ticketId=10001

Route `GET /ticket/ticket-details`, step `ticket details panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#005.0` | GET /ticket/ticket-details?ticketId=10001 | not present in candidate run |

### ticket-create-view#006.0 — GET /ticket/ticket-events?ticketId=10001

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#006.0` | GET /ticket/ticket-events?ticketId=10001 | not present in candidate run |

### ticket-create-view#007.0 — GET /ticket/ticket-files?ticketId=10001

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#007.0` | GET /ticket/ticket-files?ticketId=10001 | not present in candidate run |

### ticket-create-view#008.0 — GET /ticket-activity/activity-buttons?ticketId=10001

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#008.0` | GET /ticket-activity/activity-buttons?ticketId=10001 | not present in candidate run |

### ticket-create-view#009.0 — POST /ticket/change-ticket-subscription?ticketId=10001

Route `POST /ticket/change-ticket-subscription`, step `unsubscribe`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#009.0` | POST /ticket/change-ticket-subscription?ticketId=10001 | not present in candidate run |

### ticket-create-view#010.0 — POST /ticket/change-ticket-subscription?ticketId=10001

Route `POST /ticket/change-ticket-subscription`, step `subscribe again`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#010.0` | POST /ticket/change-ticket-subscription?ticketId=10001 | not present in candidate run |

### ticket-create-view#011.0 — GET /ticket/999999999

Route `GET /ticket/{id:int}`, step `unknown ticket redirects to list`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#011.0` | GET /ticket/999999999 | not present in candidate run |

### ticket-create-view#011.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `unknown ticket redirects to list`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-create-view#011.1` | GET /tickets | not present in candidate run |

### ticket-edit#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-edit#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-edit#003.1 — GET /ticket/10002

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#003.1` | GET /ticket/10002 | not present in candidate run |

### ticket-edit#004.0 — GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo

Route `GET /ticket-activity/load-activity`, step `load EditTicketInfo form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#004.0` | GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo | not present in candidate run |

### ticket-edit#005.0 — POST /ticket-activity/edit-ticket-info?ticketId=10002

Route `POST /ticket-activity/edit-ticket-info`, step `submit EditTicketInfo`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#005.0` | POST /ticket-activity/edit-ticket-info?ticketId=10002 | not present in candidate run |

### ticket-edit#006.0 — GET /ticket/ticket-details?ticketId=10002

Route `GET /ticket/ticket-details`, step `ticket details panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#006.0` | GET /ticket/ticket-details?ticketId=10002 | not present in candidate run |

### ticket-edit#007.0 — GET /ticket/ticket-events?ticketId=10002

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#007.0` | GET /ticket/ticket-events?ticketId=10002 | not present in candidate run |

### ticket-edit#008.0 — GET /ticket/ticket-files?ticketId=10002

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#008.0` | GET /ticket/ticket-files?ticketId=10002 | not present in candidate run |

### ticket-edit#009.0 — GET /ticket-activity/activity-buttons?ticketId=10002

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-edit#009.0` | GET /ticket-activity/activity-buttons?ticketId=10002 | not present in candidate run |

### ticket-comment#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-comment#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-comment#003.1 — GET /ticket/10003

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-comment#003.1` | GET /ticket/10003 | not present in candidate run |

### ticket-comment#004.0 — GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-comment#004.0` | GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment | not present in candidate run |

### ticket-comment#005.0 — POST /ticket-activity/add-comment?ticketId=10003

Route `POST /ticket-activity/add-comment`, step `submit AddComment`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-comment#005.0` | POST /ticket-activity/add-comment?ticketId=10003 | not present in candidate run |

### ticket-comment#006.0 — GET /ticket/ticket-events?ticketId=10003

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-comment#006.0` | GET /ticket/ticket-events?ticketId=10003 | not present in candidate run |

### ticket-assign#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-assign#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-assign#003.1 — GET /ticket/10004

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#003.1` | GET /ticket/10004 | not present in candidate run |

### ticket-assign#004.0 — GET /ticket-activity/load-activity?ticketId=10004&activity=Assign

Route `GET /ticket-activity/load-activity`, step `load Assign form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#004.0` | GET /ticket-activity/load-activity?ticketId=10004&activity=Assign | not present in candidate run |

### ticket-assign#005.0 — POST /ticket-activity/assign?ticketId=10004

Route `POST /ticket-activity/assign`, step `submit Assign`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#005.0` | POST /ticket-activity/assign?ticketId=10004 | not present in candidate run |

### ticket-assign#006.0 — GET /ticket/ticket-details?ticketId=10004

Route `GET /ticket/ticket-details`, step `ticket details panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#006.0` | GET /ticket/ticket-details?ticketId=10004 | not present in candidate run |

### ticket-assign#007.0 — GET /ticket/ticket-events?ticketId=10004

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#007.0` | GET /ticket/ticket-events?ticketId=10004 | not present in candidate run |

### ticket-assign#008.0 — GET /ticket/ticket-files?ticketId=10004

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#008.0` | GET /ticket/ticket-files?ticketId=10004 | not present in candidate run |

### ticket-assign#009.0 — GET /ticket-activity/activity-buttons?ticketId=10004

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-assign#009.0` | GET /ticket-activity/activity-buttons?ticketId=10004 | not present in candidate run |

### ticket-close#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-close#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-close#003.1 — GET /ticket/10005

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#003.1` | GET /ticket/10005 | not present in candidate run |

### ticket-close#004.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#004.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=TakeOver | not present in candidate run |

### ticket-close#005.0 — POST /ticket-activity/take-over?ticketId=10005

Route `POST /ticket-activity/take-over`, step `submit TakeOver`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#005.0` | POST /ticket-activity/take-over?ticketId=10005 | not present in candidate run |

### ticket-close#006.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=Resolve

Route `GET /ticket-activity/load-activity`, step `load Resolve form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#006.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Resolve | not present in candidate run |

### ticket-close#007.0 — POST /ticket-activity/resolve?ticketId=10005

Route `POST /ticket-activity/resolve`, step `submit Resolve`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#007.0` | POST /ticket-activity/resolve?ticketId=10005 | not present in candidate run |

### ticket-close#008.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=Close

Route `GET /ticket-activity/load-activity`, step `load Close form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#008.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Close | not present in candidate run |

### ticket-close#009.0 — POST /ticket-activity/close?ticketId=10005

Route `POST /ticket-activity/close`, step `submit Close`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#009.0` | POST /ticket-activity/close?ticketId=10005 | not present in candidate run |

### ticket-close#010.0 — GET /ticket/10005

Route `GET /ticket/{id:int}`, step `view closed ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#010.0` | GET /ticket/10005 | not present in candidate run |

### ticket-close#011.0 — GET /ticket/ticket-details?ticketId=10005

Route `GET /ticket/ticket-details`, step `ticket details panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#011.0` | GET /ticket/ticket-details?ticketId=10005 | not present in candidate run |

### ticket-close#012.0 — GET /ticket/ticket-events?ticketId=10005

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#012.0` | GET /ticket/ticket-events?ticketId=10005 | not present in candidate run |

### ticket-close#013.0 — GET /ticket/ticket-files?ticketId=10005

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#013.0` | GET /ticket/ticket-files?ticketId=10005 | not present in candidate run |

### ticket-close#014.0 — GET /ticket-activity/activity-buttons?ticketId=10005

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-close#014.0` | GET /ticket-activity/activity-buttons?ticketId=10005 | not present in candidate run |

### ticket-workflow#003.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `ticket-workflow#003.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### ticket-workflow#003.1 — GET /ticket/10006

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#003.1` | GET /ticket/10006 | not present in candidate run |

### ticket-workflow#004.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#004.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | not present in candidate run |

### ticket-workflow#005.0 — POST /ticket-activity/take-over?ticketId=10006

Route `POST /ticket-activity/take-over`, step `submit TakeOver`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#005.0` | POST /ticket-activity/take-over?ticketId=10006 | not present in candidate run |

### ticket-workflow#006.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo

Route `GET /ticket-activity/load-activity`, step `load RequestMoreInfo form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#006.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo | not present in candidate run |

### ticket-workflow#007.0 — POST /ticket-activity/request-more-info?ticketId=10006

Route `POST /ticket-activity/request-more-info`, step `submit RequestMoreInfo`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#007.0` | POST /ticket-activity/request-more-info?ticketId=10006 | not present in candidate run |

### ticket-workflow#008.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=CancelMoreInfo

Route `GET /ticket-activity/load-activity`, step `load CancelMoreInfo form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#008.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=CancelMoreInfo | not present in candidate run |

### ticket-workflow#009.0 — POST /ticket-activity/cancel-more-info?ticketId=10006

Route `POST /ticket-activity/cancel-more-info`, step `submit CancelMoreInfo`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#009.0` | POST /ticket-activity/cancel-more-info?ticketId=10006 | not present in candidate run |

### ticket-workflow#010.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo

Route `GET /ticket-activity/load-activity`, step `load RequestMoreInfo form (2)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#010.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo | not present in candidate run |

### ticket-workflow#011.0 — POST /ticket-activity/request-more-info?ticketId=10006

Route `POST /ticket-activity/request-more-info`, step `submit RequestMoreInfo (2)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#011.0` | POST /ticket-activity/request-more-info?ticketId=10006 | not present in candidate run |

### ticket-workflow#012.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=SupplyMoreInfo

Route `GET /ticket-activity/load-activity`, step `load SupplyMoreInfo form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#012.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=SupplyMoreInfo | not present in candidate run |

### ticket-workflow#013.0 — POST /ticket-activity/supply-more-info?ticketId=10006

Route `POST /ticket-activity/supply-more-info`, step `submit SupplyMoreInfo`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#013.0` | POST /ticket-activity/supply-more-info?ticketId=10006 | not present in candidate run |

### ticket-workflow#014.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=Pass

Route `GET /ticket-activity/load-activity`, step `load Pass form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#014.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=Pass | not present in candidate run |

### ticket-workflow#015.0 — POST /ticket-activity/pass?ticketId=10006

Route `POST /ticket-activity/pass`, step `submit Pass`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#015.0` | POST /ticket-activity/pass?ticketId=10006 | not present in candidate run |

### ticket-workflow#016.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ReAssign

Route `GET /ticket-activity/load-activity`, step `load ReAssign form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#016.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ReAssign | not present in candidate run |

### ticket-workflow#017.0 — POST /ticket-activity/reassign?ticketId=10006

Route `POST /ticket-activity/reassign`, step `submit ReAssign`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#017.0` | POST /ticket-activity/reassign?ticketId=10006 | not present in candidate run |

### ticket-workflow#018.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form (2)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#018.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | not present in candidate run |

### ticket-workflow#019.0 — POST /ticket-activity/take-over?ticketId=10006

Route `POST /ticket-activity/take-over`, step `submit TakeOver (2)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#019.0` | POST /ticket-activity/take-over?ticketId=10006 | not present in candidate run |

### ticket-workflow#020.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=GiveUp

Route `GET /ticket-activity/load-activity`, step `load GiveUp form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#020.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=GiveUp | not present in candidate run |

### ticket-workflow#021.0 — POST /ticket-activity/give-up?ticketId=10006

Route `POST /ticket-activity/give-up`, step `submit GiveUp`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#021.0` | POST /ticket-activity/give-up?ticketId=10006 | not present in candidate run |

### ticket-workflow#022.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=Assign

Route `GET /ticket-activity/load-activity`, step `load Assign form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#022.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=Assign | not present in candidate run |

### ticket-workflow#023.0 — POST /ticket-activity/assign?ticketId=10006

Route `POST /ticket-activity/assign`, step `submit Assign`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#023.0` | POST /ticket-activity/assign?ticketId=10006 | not present in candidate run |

### ticket-workflow#024.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form (3)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#024.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver | not present in candidate run |

### ticket-workflow#025.0 — POST /ticket-activity/take-over?ticketId=10006

Route `POST /ticket-activity/take-over`, step `submit TakeOver (3)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#025.0` | POST /ticket-activity/take-over?ticketId=10006 | not present in candidate run |

### ticket-workflow#026.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ForceClose

Route `GET /ticket-activity/load-activity`, step `load ForceClose form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#026.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ForceClose | not present in candidate run |

### ticket-workflow#027.0 — POST /ticket-activity/force-close?ticketId=10006

Route `POST /ticket-activity/force-close`, step `submit ForceClose`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#027.0` | POST /ticket-activity/force-close?ticketId=10006 | not present in candidate run |

### ticket-workflow#028.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ReOpen

Route `GET /ticket-activity/load-activity`, step `load ReOpen form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#028.0` | GET /ticket-activity/load-activity?ticketId=10006&activity=ReOpen | not present in candidate run |

### ticket-workflow#029.0 — POST /ticket-activity/reopen?ticketId=10006

Route `POST /ticket-activity/reopen`, step `submit ReOpen`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#029.0` | POST /ticket-activity/reopen?ticketId=10006 | not present in candidate run |

### ticket-workflow#030.0 — GET /ticket/10006

Route `GET /ticket/{id:int}`, step `view reopened ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#030.0` | GET /ticket/10006 | not present in candidate run |

### ticket-workflow#031.0 — GET /ticket/ticket-details?ticketId=10006

Route `GET /ticket/ticket-details`, step `ticket details panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#031.0` | GET /ticket/ticket-details?ticketId=10006 | not present in candidate run |

### ticket-workflow#032.0 — GET /ticket/ticket-events?ticketId=10006

Route `GET /ticket/ticket-events`, step `ticket events panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#032.0` | GET /ticket/ticket-events?ticketId=10006 | not present in candidate run |

### ticket-workflow#033.0 — GET /ticket/ticket-files?ticketId=10006

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#033.0` | GET /ticket/ticket-files?ticketId=10006 | not present in candidate run |

### ticket-workflow#034.0 — GET /ticket-activity/activity-buttons?ticketId=10006

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `ticket-workflow#034.0` | GET /ticket-activity/activity-buttons?ticketId=10006 | not present in candidate run |

### attachments#003.0 — POST /file/upload?tempId=dfc51453-464d-4aec-b8c4-a5cb0bef194c

Route `POST /file/upload`, step `upload pending file`, kinds: status, header, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 500 |
| header | `Content-Type` | application/json;charset=utf-8 | text/html;charset=utf-8 |
| body-text | `line 1` | {"message":""} |  |

### attachments#004.0 — POST /file/upload?tempId=dfc51453-464d-4aec-b8c4-a5cb0bef194c

Route `POST /file/upload`, step `upload a second pending file`, kinds: status, header, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 500 |
| header | `Content-Type` | application/json;charset=utf-8 | text/html;charset=utf-8 |
| body-text | `line 1` | {"message":""} |  |

### attachments#005.0 — POST /file/delete

Route `POST /file/delete`, step `delete the second pending file`, kinds: status, header, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 500 |
| header | `Content-Type` | application/json;charset=utf-8 | text/html;charset=utf-8 |
| body-text | `line 1` | {"message":""} |  |

### attachments#006.0 — GET /file/get-attachments-info?tempId=dfc51453-464d-4aec-b8c4-a5cb0bef194c

Route `GET /file/get-attachments-info`, step `pending attachments info`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Content-Type` | application/json;charset=utf-8 | text/html;charset=utf-8 |

### attachments#007.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket with attachment`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `attachments#007.0` | — | step 'create ticket with attachment' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### attachments#007.1 — GET /ticket/10007

Route `GET /ticket/{id:int}`, step `create ticket with attachment`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#007.1` | GET /ticket/10007 | not present in candidate run |

### attachments#008.0 — GET /ticket/ticket-files?ticketId=10007

Route `GET /ticket/ticket-files`, step `ticket files panel`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#008.0` | GET /ticket/ticket-files?ticketId=10007 | not present in candidate run |

### attachments#009.0 — GET /file/get-attachments-info?id=10007

Route `GET /file/get-attachments-info`, step `attached files info`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#009.0` | GET /file/get-attachments-info?id=10007 | not present in candidate run |

### attachments#010.0 — GET /file/10007/replay-note.txt

Route `GET /file/{id}/{fileName}`, step `download attachment`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#010.0` | GET /file/10007/replay-note.txt | not present in candidate run |

### attachments#011.0 — GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments

Route `GET /ticket-activity/load-activity`, step `load ModifyAttachments form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#011.0` | GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments | not present in candidate run |

### attachments#012.0 — POST /file/upload?tempId=a67ee7d6-2aa8-4331-a785-36472486d6f2

Route `POST /file/upload`, step `upload file for ModifyAttachments`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#012.0` | POST /file/upload?tempId=a67ee7d6-2aa8-4331-a785-36472486d6f2 | not present in candidate run |

### attachments#013.0 — POST /ticket-activity/modify-attachments?ticketId=10007

Route `POST /ticket-activity/modify-attachments`, step `submit ModifyAttachments`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#013.0` | POST /ticket-activity/modify-attachments?ticketId=10007 | not present in candidate run |

### attachments#014.0 — GET /ticket/ticket-files?ticketId=10007

Route `GET /ticket/ticket-files`, step `ticket files panel after modify`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `attachments#014.0` | GET /ticket/ticket-files?ticketId=10007 | not present in candidate run |

### internal-user#007.0 — POST /ticket/new

Route `POST /ticket/new`, step `create ticket`, kinds: error, status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| error | `internal-user#007.0` | — | step 'create ticket' failed: extractor 'ticketId' (location: /ticket/(\d+)) found nothing |
| status | `status` | 302 | 500 |
| location | `Location` | /ticket/{var:ticketId} |  |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta content="width=device-width" name="viewport"> |
| added | `html/head[1]/title[2]` | — | <title>Error |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <hgroup>Error. An error occurred while processing your request. |

### internal-user#007.1 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#007.1` | GET /ticket/10008 | not present in candidate run |

### internal-user#008.0 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `view own ticket`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#008.0` | GET /ticket/10008 | not present in candidate run |

### internal-user#009.0 — GET /ticket-activity/activity-buttons?ticketId=10008

Route `GET /ticket-activity/activity-buttons`, step `activity buttons`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#009.0` | GET /ticket-activity/activity-buttons?ticketId=10008 | not present in candidate run |

### internal-user#010.0 — GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#010.0` | GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment | not present in candidate run |

### internal-user#011.0 — POST /ticket-activity/add-comment?ticketId=10008

Route `POST /ticket-activity/add-comment`, step `submit AddComment`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#011.0` | POST /ticket-activity/add-comment?ticketId=10008 | not present in candidate run |

### internal-user#012.0 — GET /user/sign-out

Route `GET /user/sign-out`, step `sign out`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#012.0` | GET /user/sign-out | not present in candidate run |

### internal-user#012.1 — GET /

Route `GET /`, step `sign out`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `internal-user#012.1` | GET / | not present in candidate run |

