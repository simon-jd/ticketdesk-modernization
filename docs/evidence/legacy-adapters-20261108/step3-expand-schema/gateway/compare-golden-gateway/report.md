# Replay comparison report

Generated 2026-11-08 20:50:26Z

| | Baseline | Candidate |
|---|---|---|
| File | `docs/evidence/legacy-adapters-20261108/step3-expand-schema/gateway/baseline.jsonl` | `docs/evidence/legacy-adapters-20261108/step3-expand-schema/gateway/gateway.jsonl` |
| Target | https://localhost:8443/ | https://localhost:5443/ |
| Run | legacy-run1 | gateway |
| Exchanges | 262 | 262 |
| Journey errors | 0 | 0 |

**Compared 262, matched 258, mismatched 4** (missing in candidate 0, extra in candidate 0).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /admin/push-notification-settings/{action=index}` | 2 | 2 | body-html×2 |
| `GET /auto-complete/tag-list` | 1 | 1 | body-json×1 |
| `POST /admin/push-notification-settings/{action=index}` | 1 | 1 | body-html×1 |
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
| `GET /admin/search-manager` | 1 | 0 |  |
| `GET /admin/user/{id?}` | 3 | 0 |  |
| `GET /admin/users/page/{page:int?}` | 1 | 0 |  |
| `GET /admin/users/{page:int?}` | 7 | 0 |  |
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

## Mismatches

### ticket-list#015.0 — GET /auto-complete/tag-list?term=net

Route `GET /auto-complete/tag-list`, step `tag autocomplete`, kinds: body-json

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| json-value | `$[0].id` | "network-041" | "network" |
| json-value | `$[0].text` | "network-041" | "network" |
| json-value | `$[2].id` | "network" | "network-041" |
| json-value | `$[2].text` | "network" | "network-041" |

### admin-settings#010.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#004.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#005.0 — POST /admin/push-notification-settings

Route `POST /admin/push-notification-settings/{action=index}`, step `save push notification settings unchanged`, kinds: body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

