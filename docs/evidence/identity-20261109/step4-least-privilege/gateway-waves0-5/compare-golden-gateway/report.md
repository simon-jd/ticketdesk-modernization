# Replay comparison report

Generated 2026-11-10 07:27:05Z

| | Baseline | Candidate |
|---|---|---|
| File | `~/Desktop/ticketdesk-modernization/wt-identity/docs/evidence/identity-20261109/step4-least-privilege/gateway-waves0-5/baseline.jsonl` | `~/Desktop/ticketdesk-modernization/wt-identity/docs/evidence/identity-20261109/step4-least-privilege/gateway-waves0-5/gateway.jsonl` |
| Target | https://localhost:8443/ | https://localhost:5443/ |
| Run | legacy-run1 | gateway |
| Exchanges | 262 | 264 |
| Journey errors | 0 | 0 |

**Compared 262, matched 188, mismatched 76** (missing in candidate 0, extra in candidate 2).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /user/sign-in` | 19 | 19 | header×19, set-cookie×15 |
| `GET /tickets/{listName?}/{page:int?}` | 16 | 14 | header×13, body-html×13, status×1, location×2, body-text×1 |
| `GET /ticket/new` | 8 | 8 | set-cookie×8 |
| `GET /admin/project/{projectId:int}` | 4 | 3 | header×3, body-html×3 |
| `GET /admin/user/{id?}` | 3 | 3 | header×3, body-html×3 |
| `GET /tickets/sortList/{listName=opentickets}/{page:int?}` | 3 | 3 | header×3, body-html×3 |
| `GET /` | 29 | 2 | body-html×2 |
| `GET /admin/application-settings/{action=index}` | 2 | 2 | header×2 |
| `GET /admin/new` | 2 | 2 | header×2 |
| `GET /admin/project/new` | 2 | 2 | header×2 |
| `GET /admin/push-notification-settings/{action=index}` | 2 | 2 | header×2, body-html×2 |
| `GET /access-denied` | 3 | 1 | header×1, body-html×1 |
| `GET /account/change-password` | 1 | 1 | header×1 |
| `GET /account/edit-profile` | 1 | 1 | header×1 |
| `GET /admin/push-notification-settings/configure-provider` | 1 | 1 | header×1, body-html×1 |
| `GET /file/get-attachments-info` | 2 | 1 | status×1, body-text×1 |
| `GET /tickets/pageList/{listName=mytickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
| `POST /account/change-password` | 1 | 1 | header×1 |
| `POST /account/edit-profile` | 1 | 1 | status×1, location×1, header×1, set-cookie×1, body-html×1 |
| `POST /admin/application-settings/{action=index}` | 1 | 1 | header×1 |
| `POST /admin/push-notification-settings/configure-provider` | 1 | 1 | header×1, body-html×1 |
| `POST /admin/push-notification-settings/{action=index}` | 1 | 1 | header×1, body-html×1 |
| `POST /tickets/filterList/{listName=opentickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
| `POST /user/register` | 1 | 1 | header×1, set-cookie×1 |
| `POST /user/sign-in` | 14 | 1 | header×1 |
| `GET /about` | 1 | 0 |  |
| `GET /account/manage` | 1 | 0 |  |
| `GET /admin/data-management/demo` | 1 | 0 |  |
| `GET /admin/projects` | 5 | 0 |  |
| `GET /admin/push-notification-settings/list-providers` | 1 | 0 |  |
| `GET /admin/search-manager` | 1 | 0 |  |
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
| `GET /ticket-activity/load-activity` | 21 | 0 |  |
| `GET /ticket/ticket-details` | 5 | 0 |  |
| `GET /ticket/ticket-events` | 6 | 0 |  |
| `GET /ticket/ticket-files` | 7 | 0 |  |
| `GET /ticket/{action=index}` | 1 | 0 |  |
| `GET /ticket/{id:int}` | 14 | 0 |  |
| `GET /tickets/reset-user-lists` | 1 | 0 |  |
| `GET /user/register` | 2 | 0 |  |
| `GET /user/sign-out` | 2 | 0 |  |
| `POST /admin/new` | 1 | 0 |  |
| `POST /admin/project/delete/{projectId:int}` | 1 | 0 |  |
| `POST /admin/project/new` | 1 | 0 |  |
| `POST /admin/project/{projectId:int}` | 1 | 0 |  |
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

## Mismatches

### anonymous#003.1 — GET /user/sign-in?ReturnUrl=%2Ftickets

Route `GET /user/sign-in`, step `ticket center requires sign-in`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### anonymous#004.1 — GET /user/sign-in?ReturnUrl=%2Fticket%2F1

Route `GET /user/sign-in`, step `ticket requires sign-in`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |

### anonymous#005.1 — GET /user/sign-in?ReturnUrl=%2Fadmin%2Fusers

Route `GET /user/sign-in`, step `admin requires sign-in`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |

### anonymous#006.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |

### sign-in#000.0 — GET /user/sign-in?ReturnUrl=%2Faccount%2Fmanage

Route `GET /user/sign-in`, step `sign-in page with return url`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### sign-in#001.0 — POST /user/sign-in?ReturnUrl=%2Faccount%2Fmanage

Route `POST /user/sign-in`, step `sign-in with wrong password`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |

### sign-in#004.0 — GET /account/change-password

Route `GET /account/change-password`, step `change password page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### sign-in#005.0 — POST /account/change-password

Route `POST /account/change-password`, step `change password with wrong current password`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### sign-in#006.0 — GET /account/edit-profile

Route `GET /account/edit-profile`, step `edit profile page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### sign-in#007.0 — POST /account/edit-profile

Route `POST /account/edit-profile`, step `save profile unchanged`, kinds: status, location, header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 302 |
| location | `Location` |  | /account/manage?Message=ProfileSaveSuccess |
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | .AspNet.ApplicationCookie; httponly; path=/; secure |
| element | `html/head[1]/meta[1]` | <meta charset="utf-8"> | <title>Object moved |
| removed | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | — |
| removed | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | — |
| removed | `html/head[1]/meta[4]` | <meta content="none" name="msapplication-config"> | — |
| removed | `html/head[1]/title[1]` | <title>Account Profile | — |
| removed | `html/head[1]/style[1]` | <style type="text/css"> | — |
| removed | `html/head[1]/link[1]` | <link href="/content/styles?v={v}" rel="stylesheet"> | — |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <h2>Object moved to here . |
| removed | `html/body[1]/div[1]` | <div class="body-content">Manage your account Account Profile The Username field is required. Email Display Name Save Back to account options Tick… | — |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### sign-in#009.1 — GET /user/sign-in?ReturnUrl=%2Ftickets

Route `GET /user/sign-in`, step `ticket center after sign-out`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |

### search#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-list#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-list#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center default list`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
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

### ticket-create-view#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-create-view#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### ticket-create-view#011.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `unknown ticket redirects to list`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[3]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### ticket-edit#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-edit#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### ticket-comment#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-comment#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### ticket-assign#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-assign#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### ticket-close#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-close#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### ticket-workflow#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### ticket-workflow#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### attachments#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### attachments#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### attachments#006.0 — GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9

Route `GET /file/get-attachments-info`, step `pending attachments info`, kinds: status, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 500 | 200 |
| body-text | `line 1` |  | [{"name":"replay-note.txt","size":29,"isAttached":false,"type":"text/plain","url":"/file/{guid}/replay-note.txt"}] |

### admin-settings#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### admin-settings#002.0 — GET /admin/application-settings

Route `GET /admin/application-settings/{action=index}`, step `application settings`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-settings#005.0 — GET /admin/user/91efcdb5-506c-4b1b-af6f-08b63dee1475

Route `GET /admin/user/{id?}`, step `edit user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-settings#006.0 — GET /admin/new

Route `GET /admin/new`, step `new user page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-settings#008.0 — GET /admin/project/new

Route `GET /admin/project/new`, step `new project page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-settings#009.0 — GET /admin/project/1

Route `GET /admin/project/{projectId:int}`, step `edit default project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-settings#010.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### admin-changes#002.0 — GET /admin/application-settings

Route `GET /admin/application-settings/{action=index}`, step `application settings`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-changes#003.0 — POST /admin/application-settings

Route `POST /admin/application-settings/{action=index}`, step `save application settings unchanged`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-changes#004.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#005.0 — POST /admin/push-notification-settings

Route `POST /admin/push-notification-settings/{action=index}`, step `save push notification settings unchanged`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5443 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5443'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#006.0 — GET /admin/push-notification-settings/configure-provider?providerAssemblyQualifiedName=TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider%2C%20TicketDesk.PushNotifications%2C%20Version%3D1.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull

Route `GET /admin/push-notification-settings/configure-provider`, step `configure SMTP provider`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/input[1]/@placeholder` | — | (not set: use the secret store) |
| text | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/span[2]/text()[1]` | Leave empty if authentication is not required | Leave empty if authentication is not required Stored in the secret store (PushNotifications:Secrets:Smtp:SmtpPassword), never in the database. |
| added | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#007.0 — POST /admin/push-notification-settings/configure-provider

Route `POST /admin/push-notification-settings/configure-provider`, step `save SMTP provider unchanged`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/input[1]/@placeholder` | — | (not set: use the secret store) |
| text | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/span[2]/text()[1]` | Leave empty if authentication is not required | Leave empty if authentication is not required Stored in the secret store (PushNotifications:Secrets:Smtp:SmtpPassword), never in the database. |
| added | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#008.0 — GET /admin/project/new

Route `GET /admin/project/new`, step `new project page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-changes#011.0 — GET /admin/project/6

Route `GET /admin/project/{projectId:int}`, step `edit replay project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#013.0 — GET /admin/project/6

Route `GET /admin/project/{projectId:int}`, step `edit replay project again`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#015.0 — GET /admin/new

Route `GET /admin/new`, step `new user page`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-changes#018.0 — GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119

Route `GET /admin/user/{id?}`, step `edit new user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#020.0 — GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119

Route `GET /admin/user/{id?}`, step `edit new user again`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### internal-user#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### internal-user#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
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

Route `GET /ticket/new`, step `new ticket page`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` |  | __RequestVerificationToken; httponly; path=/ |

### register-disabled#001.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page for an antiforgery token`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |

### register-disabled#002.0 — POST /user/register

Route `POST /user/register`, step `register anyway`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | no-cache | private |
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure |  |

### register-disabled#002.1 — GET /

Route `GET /`, step `register anyway`, kinds: body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| removed | `html/body[1]/nav[1]/div[3]` | <div class="container-fluid navbar-default" style="border-top: 1px solid #e7e7e7; padding: 7px;">Project: All Projects All Projects Default Facilities Finance Systems HR Services IT Help Desk Search: | — |

### register-disabled#003.0 — GET /

Route `GET /`, step `home as the registered user`, kinds: body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| removed | `html/body[1]/nav[1]/div[3]` | <div class="container-fluid navbar-default" style="border-top: 1px solid #e7e7e7; padding: 7px;">Project: All Projects All Projects Default Facilities Finance Systems HR Services IT Help Desk Search: | — |

### register-disabled#004.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center as a pending user`, kinds: header, location, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Content-Type` | text/html;charset=utf-8 | — |
| location | `Location` | /access-denied | /user/sign-in?ReturnUrl=%2Ftickets |
| body-text | `line 1` | <html><head><title>Object moved</title></head><body> |  |

### register-disabled#004.1 — GET /access-denied

Route `GET /access-denied`, step `ticket center as a pending user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| header | `X-Frame-Options` | DENY | SAMEORIGIN, DENY |
| text | `html/head[1]/title[1]/text()[1]` | TicketDesk | Sign In |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| removed | `html/body[1]/nav[1]/div[3]` | <div class="container-fluid navbar-default" style="border-top: 1px solid #e7e7e7; padding: 7px;">Project: All Projects All Projects Default Facilities Finance Systems HR Services IT Help Desk Search: | — |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/legend[1]/text()[1]` | Access Denied | Sign In |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/@class` | col-lg-7 col-lg-offset-2 col-md-8 col-md-offset-1 col-sm-12 | col-lg-7 col-lg-offset-3 col-md-8 col-md-offset-2 col-sm-12 |
| element | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[1]` | <div class="panel panel-danger">Your account is waiting on approval by an administrator. Please wait a while and try again, or contact the system admini… | <form action="/user/sign-in?ReturnUrl=%2Ftickets" class="form-horizontal" method="post" role="form">Enter your Sign In Credentials Username / Email Password Remember me? Sign In |

### sign-in#007.1 — GET /account/manage?Message=ProfileSaveSuccess

Route `GET /account/manage`, step `save profile unchanged`, kinds: extra

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| extra | `sign-in#007.1` | — | GET /account/manage?Message=ProfileSaveSuccess |

### internal-user#004.1 — GET /access-denied

Route `GET /access-denied`, step `unassigned (help desk only)`, kinds: extra

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| extra | `internal-user#004.1` | — | GET /access-denied |

