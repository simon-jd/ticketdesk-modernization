# Replay comparison report

Generated 2026-11-10 00:59:56Z

| | Baseline | Candidate |
|---|---|---|
| File | `docs/evidence/tickets-20261109/replay-step2-wave4/baseline.jsonl` | `docs/evidence/tickets-20261109/replay-step2-wave4/gateway.jsonl` |
| Target | https://localhost:8443/ | https://localhost:5443/ |
| Run | legacy-run1 | gateway |
| Exchanges | 262 | 263 |
| Journey errors | 0 | 0 |

**Compared 262, matched 199, mismatched 64** (missing in candidate 0, extra in candidate 1).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /ticket-activity/load-activity` | 21 | 21 | header×21 |
| `GET /tickets/{listName?}/{page:int?}` | 16 | 13 | header×12, set-cookie×2, body-html×13, status×1, location×1 |
| `GET /ticket/{id:int}` | 14 | 12 | header×12, body-html×12 |
| `GET /ticket/new` | 8 | 8 | header×8, set-cookie×7, body-html×8 |
| `GET /tickets/sortList/{listName=opentickets}/{page:int?}` | 3 | 3 | header×3, body-html×3 |
| `GET /admin/push-notification-settings/{action=index}` | 2 | 2 | body-html×2 |
| `GET /file/get-attachments-info` | 2 | 1 | status×1, body-text×1 |
| `GET /tickets/pageList/{listName=mytickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
| `POST /admin/push-notification-settings/{action=index}` | 1 | 1 | body-html×1 |
| `POST /tickets/filterList/{listName=opentickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
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
| `GET /auto-complete/tag-list` | 1 | 0 |  |
| `GET /bundles/{bundle}` | 1 | 0 |  |
| `GET /content/{bundle}` | 1 | 0 |  |
| `GET /emailtest/index/{id}` | 1 | 0 |  |
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
| `GET /ticket/ticket-details` | 5 | 0 |  |
| `GET /ticket/ticket-events` | 6 | 0 |  |
| `GET /ticket/ticket-files` | 7 | 0 |  |
| `GET /ticket/{action=index}` | 1 | 0 |  |
| `GET /tickets/reset-user-lists` | 1 | 0 |  |
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
| `POST /user/register` | 1 | 0 |  |
| `POST /user/sign-in` | 14 | 0 |  |

## Mismatches

### ticket-list#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center default list`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#003.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket index redirects to the list`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#004.0 — GET /tickets/mytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `my tickets`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#005.0 — GET /tickets/unassigned

Route `GET /tickets/{listName?}/{page:int?}`, step `unassigned`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#006.0 — GET /tickets/assignedToMe

Route `GET /tickets/{listName?}/{page:int?}`, step `assigned to me`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#007.0 — GET /tickets/historytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `history`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#008.0 — GET /tickets/opentickets

Route `GET /tickets/{listName?}/{page:int?}`, step `open tickets`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#009.0 — GET /tickets/pageList/opentickets/2

Route `GET /tickets/pageList/{listName=mytickets}/{page:int?}`, step `open tickets page 2`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#010.0 — POST /tickets/filterList

Route `POST /tickets/filterList/{listName=opentickets}/{page:int?}`, step `filter open tickets`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#011.0 — GET /tickets/sortList/opentickets?columnName=Title

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `sort by title`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[43]/@class` | clickable | clickable overdue |

### ticket-list#012.0 — GET /tickets/sortList/opentickets?columnName=Title

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `sort by title again (flip)`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |

### ticket-list#013.0 — GET /tickets/sortList/opentickets?columnName=Priority&isMultiSort=true

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `add priority to sort`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |

### ticket-list#014.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `reset list settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-list#017.0 — GET /tickets/opentickets

Route `GET /tickets/{listName?}/{page:int?}`, step `open tickets in project 2`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-create-view#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-create-view#003.1 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-create-view#004.0 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `view ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-create-view#011.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `unknown ticket redirects to list`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-edit#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-edit#003.1 — GET /ticket/10002

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-edit#004.0 — GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo

Route `GET /ticket-activity/load-activity`, step `load EditTicketInfo form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-comment#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-comment#003.1 — GET /ticket/10003

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-comment#004.0 — GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-assign#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-assign#003.1 — GET /ticket/10004

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-assign#004.0 — GET /ticket-activity/load-activity?ticketId=10004&activity=Assign

Route `GET /ticket-activity/load-activity`, step `load Assign form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-close#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-close#003.1 — GET /ticket/10005

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-close#004.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-close#006.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=Resolve

Route `GET /ticket-activity/load-activity`, step `load Resolve form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-close#008.0 — GET /ticket-activity/load-activity?ticketId=10005&activity=Close

Route `GET /ticket-activity/load-activity`, step `load Close form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-close#010.0 — GET /ticket/10005

Route `GET /ticket/{id:int}`, step `view closed ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-workflow#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-workflow#003.1 — GET /ticket/10006

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### ticket-workflow#004.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#006.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo

Route `GET /ticket-activity/load-activity`, step `load RequestMoreInfo form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#008.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=CancelMoreInfo

Route `GET /ticket-activity/load-activity`, step `load CancelMoreInfo form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#010.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=RequestMoreInfo

Route `GET /ticket-activity/load-activity`, step `load RequestMoreInfo form (2)`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#012.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=SupplyMoreInfo

Route `GET /ticket-activity/load-activity`, step `load SupplyMoreInfo form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#014.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=Pass

Route `GET /ticket-activity/load-activity`, step `load Pass form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#016.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ReAssign

Route `GET /ticket-activity/load-activity`, step `load ReAssign form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#018.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form (2)`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#020.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=GiveUp

Route `GET /ticket-activity/load-activity`, step `load GiveUp form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#022.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=Assign

Route `GET /ticket-activity/load-activity`, step `load Assign form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#024.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=TakeOver

Route `GET /ticket-activity/load-activity`, step `load TakeOver form (3)`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#026.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ForceClose

Route `GET /ticket-activity/load-activity`, step `load ForceClose form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#028.0 — GET /ticket-activity/load-activity?ticketId=10006&activity=ReOpen

Route `GET /ticket-activity/load-activity`, step `load ReOpen form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-workflow#030.0 — GET /ticket/10006

Route `GET /ticket/{id:int}`, step `view reopened ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### attachments#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### attachments#006.0 — GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9

Route `GET /file/get-attachments-info`, step `pending attachments info`, kinds: status, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 500 | 200 |
| body-text | `line 1` |  | [{"name":"replay-note.txt","size":29,"isAttached":false,"type":"text/plain","url":"/file/{guid}/replay-note.txt"}] |

### attachments#007.1 — GET /ticket/10007

Route `GET /ticket/{id:int}`, step `create ticket with attachment`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### attachments#011.0 — GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments

Route `GET /ticket-activity/load-activity`, step `load ModifyAttachments form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

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

### internal-user#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### internal-user#003.0 — GET /tickets/mytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `my tickets`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### internal-user#004.0 — GET /tickets/unassigned

Route `GET /tickets/{listName?}/{page:int?}`, step `unassigned (help desk only)`, kinds: status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 500 | 302 |
| location | `Location` |  | /access-denied |
| element | `html/head[1]/meta[1]` | <meta content="width=device-width" name="viewport"> | <title>Object moved |
| removed | `html/head[1]/title[1]` | <title>Error | — |
| element | `html/body[1]/hgroup[1]` | <hgroup>Error. An error occurred while processing your request. | <h2>Object moved to here . |

### internal-user#006.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/script[12]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### internal-user#007.1 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### internal-user#008.0 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `view own ticket`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| added | `html/body[1]/script[13]` | — | <script type="text/javascript">(function () { // S3 RequestVerificationToken var token = $('input[name="__RequestVerificationToken"]').first().val(); $… |

### internal-user#010.0 — GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### internal-user#004.1 — GET /access-denied

Route `GET /access-denied`, step `unassigned (help desk only)`, kinds: extra

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| extra | `internal-user#004.1` | — | GET /access-denied |

