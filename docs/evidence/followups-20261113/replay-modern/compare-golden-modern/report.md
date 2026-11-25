# Replay comparison report

Generated 2026-11-13 22:43:46Z

| | Baseline | Candidate |
|---|---|---|
| File | `/tmp/td-replay-modern-dirdlr/baseline.jsonl` | `~/Desktop/ticketdesk-modernization/wt-followups/docs/evidence/followups-20261113/replay-modern/modern.jsonl` |
| Target | https://localhost:8443/ | https://localhost:5483/ |
| Run | legacy-run1 | modern |
| Exchanges | 262 | 260 |
| Journey errors | 0 | 0 |

**Compared 258, matched 87, mismatched 177** (missing in candidate 4, extra in candidate 2).

## By route

| Route | Requests | Mismatches | Kinds |
|---|---:|---:|---|
| `GET /` | 29 | 29 | header×25, set-cookie×2, body-html×25, missing×4 |
| `GET /ticket-activity/load-activity` | 21 | 21 | header×21 |
| `GET /user/sign-in` | 19 | 19 | header×19, set-cookie×15, body-html×19 |
| `GET /tickets/{listName?}/{page:int?}` | 16 | 14 | header×13, set-cookie×12, body-html×13, status×1, location×2, body-text×1 |
| `POST /user/sign-in` | 14 | 14 | header×1, body-html×1, set-cookie×13 |
| `GET /ticket/{id:int}` | 14 | 12 | header×12, set-cookie×12, body-html×12 |
| `GET /ticket/new` | 8 | 8 | header×8, set-cookie×8, body-html×8 |
| `GET /admin/projects` | 5 | 5 | header×5, body-html×5 |
| `GET /admin/users/{page:int?}` | 7 | 5 | header×5, body-html×5 |
| `GET /access-denied` | 3 | 3 | header×3, body-html×3 |
| `GET /admin/project/{projectId:int}` | 4 | 3 | header×3, body-html×3 |
| `GET /admin/user/{id?}` | 3 | 3 | header×3, body-html×3 |
| `GET /search` | 3 | 3 | header×3, body-html×3 |
| `GET /tickets/sortList/{listName=opentickets}/{page:int?}` | 3 | 3 | header×3, body-html×3 |
| `GET /admin/application-settings/{action=index}` | 2 | 2 | header×2, body-html×2 |
| `GET /admin/new` | 2 | 2 | header×2, body-html×2 |
| `GET /admin/project/new` | 2 | 2 | header×2, body-html×2 |
| `GET /admin/push-notification-settings/{action=index}` | 2 | 2 | header×2, body-html×2 |
| `GET /user/sign-out` | 2 | 2 | set-cookie×2 |
| `GET /about` | 1 | 1 | header×1, body-html×1 |
| `GET /account/change-password` | 1 | 1 | header×1, body-html×1 |
| `GET /account/edit-profile` | 1 | 1 | header×1, body-html×1 |
| `GET /account/manage` | 1 | 1 | header×1, body-html×1 |
| `GET /admin/data-management/demo` | 1 | 1 | header×1, body-html×1 |
| `GET /admin/push-notification-settings/configure-provider` | 1 | 1 | header×1, body-html×1 |
| `GET /admin/search-manager` | 1 | 1 | header×1, body-html×1 |
| `GET /emailtest/index/{id}` | 1 | 1 | status×1, header×1, body-text×1 |
| `GET /file/get-attachments-info` | 2 | 1 | status×1, body-text×1 |
| `GET /first-run-setup/check-upgrade-progress` | 1 | 1 | status×1, location×1, header×1, body-html×1 |
| `GET /first-run-setup/legacy-migration` | 1 | 1 | status×1, location×1, header×1, body-html×1 |
| `GET /first-run-setup/upgrade-database` | 1 | 1 | status×1, location×1, header×1, body-html×1 |
| `GET /first-run-setup/{action=index}` | 1 | 1 | status×1, location×1, header×1, body-html×1 |
| `GET /index` | 1 | 1 | header×1, body-html×1 |
| `GET /search/{term}` | 1 | 1 | header×1, body-html×1 |
| `GET /tickets/pageList/{listName=mytickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
| `POST /account/change-password` | 1 | 1 | header×1, body-html×1 |
| `POST /account/edit-profile` | 1 | 1 | status×1, location×1, header×1, set-cookie×1, body-html×1 |
| `POST /admin/application-settings/{action=index}` | 1 | 1 | header×1, body-html×1 |
| `POST /admin/push-notification-settings/configure-provider` | 1 | 1 | header×1, body-html×1 |
| `POST /admin/push-notification-settings/{action=index}` | 1 | 1 | header×1, body-html×1 |
| `POST /tickets/filterList/{listName=opentickets}/{page:int?}` | 1 | 1 | header×1, body-html×1 |
| `POST /user/register` | 1 | 1 | header×1, set-cookie×1 |
| `GET /admin/push-notification-settings/list-providers` | 1 | 0 |  |
| `GET /admin/users/page/{page:int?}` | 1 | 0 |  |
| `GET /auto-complete/tag-list` | 1 | 0 |  |
| `GET /bundles/{bundle}` | 1 | 0 |  |
| `GET /content/{bundle}` | 1 | 0 |  |
| `GET /file/{id}/{fileName}` | 1 | 0 |  |
| `GET /language` | 2 | 0 |  |
| `GET /navigation/change-project/{projectId:int}` | 2 | 0 |  |
| `GET /ticket-activity/activity-buttons` | 6 | 0 |  |
| `GET /ticket/ticket-details` | 5 | 0 |  |
| `GET /ticket/ticket-events` | 6 | 0 |  |
| `GET /ticket/ticket-files` | 7 | 0 |  |
| `GET /ticket/{action=index}` | 1 | 0 |  |
| `GET /tickets/reset-user-lists` | 1 | 0 |  |
| `GET /user/register` | 2 | 0 |  |
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

### anonymous#000.0 — GET /

Route `GET /`, step `home`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#001.0 — GET /index

Route `GET /index`, step `home by name`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#002.0 — GET /about

Route `GET /about`, step `about`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#003.1 — GET /user/sign-in?ReturnUrl=%2Ftickets

Route `GET /user/sign-in`, step `ticket center requires sign-in`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ |  |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#004.1 — GET /user/sign-in?ReturnUrl=%2Fticket%2F1

Route `GET /user/sign-in`, step `ticket requires sign-in`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#005.1 — GET /user/sign-in?ReturnUrl=%2Fadmin%2Fusers

Route `GET /user/sign-in`, step `admin requires sign-in`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#006.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#007.1 — GET /

Route `GET /`, step `register page (disabled by default)`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#008.0 — GET /access-denied

Route `GET /access-denied`, step `access denied page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#009.0 — GET /first-run-setup

Route `GET /first-run-setup/{action=index}`, step `first-run setup (disabled)`, kinds: status, location, header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 302 | 410 |
| location | `Location` | / |  |
| header | `Cache-Control` | private | no-store, no-cache |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta charset="utf-8"> |
| added | `html/head[1]/meta[1]` | — | <meta content="IE=edge" http-equiv="X-UA-Compatible"> |
| added | `html/head[1]/meta[2]` | — | <meta content="width=device-width, initial-scale=1.0" name="viewport"> |
| added | `html/head[1]/meta[3]` | — | <meta content="none" name="msapplication-config"> |
| added | `html/head[1]/title[2]` | — | <title>First-run setup |
| added | `html/head[1]/style[1]` | — | <style type="text/css">/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdo… |
| added | `html/head[1]/link[1]` | — | <link href="/content/styles?v={v}" rel="stylesheet"> |
| added | `html/head[1]/script[1]` | — | <script src="/bundles/modernizr?v={v}"> |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Account Sign In Settings Manage Users Manage Projects Applicatio… |
| added | `html/body[1]/div[1]` | — | <div class="body-content">First-run setup First-run setup was part of the old TicketDesk application and is retired with it. This installation is … |
| added | `html/body[1]/script[1]` | — | <script type="text/javascript">window.currentCulture = 'en-US'; |
| added | `html/body[1]/script[2]` | — | <script src="/bundles/common?v={v}"> |
| added | `html/body[1]/script[3]` | — | <script src="/bundles/common_locale_en-US?v={v}"> |
| added | `html/body[1]/script[4]` | — | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… |
| added | `html/body[1]/script[5]` | — | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); |

### anonymous#009.1 — GET /

Route `GET /`, step `first-run setup (disabled)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `anonymous#009.1` | GET / | not present in candidate run |

### anonymous#010.0 — GET /first-run-setup/upgrade-database

Route `GET /first-run-setup/upgrade-database`, step `upgrade database (disabled)`, kinds: status, location, header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 302 | 410 |
| location | `Location` | / |  |
| header | `Cache-Control` | private | no-store, no-cache |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta charset="utf-8"> |
| added | `html/head[1]/meta[1]` | — | <meta content="IE=edge" http-equiv="X-UA-Compatible"> |
| added | `html/head[1]/meta[2]` | — | <meta content="width=device-width, initial-scale=1.0" name="viewport"> |
| added | `html/head[1]/meta[3]` | — | <meta content="none" name="msapplication-config"> |
| added | `html/head[1]/title[2]` | — | <title>First-run setup |
| added | `html/head[1]/style[1]` | — | <style type="text/css">/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdo… |
| added | `html/head[1]/link[1]` | — | <link href="/content/styles?v={v}" rel="stylesheet"> |
| added | `html/head[1]/script[1]` | — | <script src="/bundles/modernizr?v={v}"> |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Account Sign In Settings Manage Users Manage Projects Applicatio… |
| added | `html/body[1]/div[1]` | — | <div class="body-content">First-run setup First-run setup was part of the old TicketDesk application and is retired with it. This installation is … |
| added | `html/body[1]/script[1]` | — | <script type="text/javascript">window.currentCulture = 'en-US'; |
| added | `html/body[1]/script[2]` | — | <script src="/bundles/common?v={v}"> |
| added | `html/body[1]/script[3]` | — | <script src="/bundles/common_locale_en-US?v={v}"> |
| added | `html/body[1]/script[4]` | — | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… |
| added | `html/body[1]/script[5]` | — | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); |

### anonymous#010.1 — GET /

Route `GET /`, step `upgrade database (disabled)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `anonymous#010.1` | GET / | not present in candidate run |

### anonymous#011.0 — GET /first-run-setup/check-upgrade-progress

Route `GET /first-run-setup/check-upgrade-progress`, step `upgrade progress (disabled)`, kinds: status, location, header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 302 | 410 |
| location | `Location` | / |  |
| header | `Cache-Control` | private | no-store, no-cache |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta charset="utf-8"> |
| added | `html/head[1]/meta[1]` | — | <meta content="IE=edge" http-equiv="X-UA-Compatible"> |
| added | `html/head[1]/meta[2]` | — | <meta content="width=device-width, initial-scale=1.0" name="viewport"> |
| added | `html/head[1]/meta[3]` | — | <meta content="none" name="msapplication-config"> |
| added | `html/head[1]/title[2]` | — | <title>First-run setup |
| added | `html/head[1]/style[1]` | — | <style type="text/css">/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdo… |
| added | `html/head[1]/link[1]` | — | <link href="/content/styles?v={v}" rel="stylesheet"> |
| added | `html/head[1]/script[1]` | — | <script src="/bundles/modernizr?v={v}"> |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Account Sign In Settings Manage Users Manage Projects Applicatio… |
| added | `html/body[1]/div[1]` | — | <div class="body-content">First-run setup First-run setup was part of the old TicketDesk application and is retired with it. This installation is … |
| added | `html/body[1]/script[1]` | — | <script type="text/javascript">window.currentCulture = 'en-US'; |
| added | `html/body[1]/script[2]` | — | <script src="/bundles/common?v={v}"> |
| added | `html/body[1]/script[3]` | — | <script src="/bundles/common_locale_en-US?v={v}"> |
| added | `html/body[1]/script[4]` | — | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… |
| added | `html/body[1]/script[5]` | — | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); |

### anonymous#011.1 — GET /

Route `GET /`, step `upgrade progress (disabled)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `anonymous#011.1` | GET / | not present in candidate run |

### anonymous#012.0 — GET /first-run-setup/legacy-migration

Route `GET /first-run-setup/legacy-migration`, step `legacy migration (disabled)`, kinds: status, location, header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 302 | 410 |
| location | `Location` | / |  |
| header | `Cache-Control` | private | no-store, no-cache |
| element | `html/head[1]/title[1]` | <title>Object moved | <meta charset="utf-8"> |
| added | `html/head[1]/meta[1]` | — | <meta content="IE=edge" http-equiv="X-UA-Compatible"> |
| added | `html/head[1]/meta[2]` | — | <meta content="width=device-width, initial-scale=1.0" name="viewport"> |
| added | `html/head[1]/meta[3]` | — | <meta content="none" name="msapplication-config"> |
| added | `html/head[1]/title[2]` | — | <title>First-run setup |
| added | `html/head[1]/style[1]` | — | <style type="text/css">/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdo… |
| added | `html/head[1]/link[1]` | — | <link href="/content/styles?v={v}" rel="stylesheet"> |
| added | `html/head[1]/script[1]` | — | <script src="/bundles/modernizr?v={v}"> |
| element | `html/body[1]/h2[1]` | <h2>Object moved to here . | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Account Sign In Settings Manage Users Manage Projects Applicatio… |
| added | `html/body[1]/div[1]` | — | <div class="body-content">First-run setup First-run setup was part of the old TicketDesk application and is retired with it. This installation is … |
| added | `html/body[1]/script[1]` | — | <script type="text/javascript">window.currentCulture = 'en-US'; |
| added | `html/body[1]/script[2]` | — | <script src="/bundles/common?v={v}"> |
| added | `html/body[1]/script[3]` | — | <script src="/bundles/common_locale_en-US?v={v}"> |
| added | `html/body[1]/script[4]` | — | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… |
| added | `html/body[1]/script[5]` | — | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); |

### anonymous#012.1 — GET /

Route `GET /`, step `legacy migration (disabled)`, kinds: missing

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| missing | `anonymous#012.1` | GET / | not present in candidate run |

### anonymous#013.0 — GET /emailtest/index/1

Route `GET /emailtest/index/{id}`, step `e-mail test page renders a ticket anonymously (S2)`, kinds: status, header, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 404 |
| header | `Content-Type` | text/plain;charset=utf-8 | — |
| body-text | `line 2` |  | (end) |

### anonymous#016.1 — GET /

Route `GET /`, step `switch culture to pt-BR`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#017.0 — GET /

Route `GET /`, step `home in pt-BR`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### anonymous#018.1 — GET /

Route `GET /`, step `switch culture back to en-US`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### sign-in#000.0 — GET /user/sign-in?ReturnUrl=%2Faccount%2Fmanage

Route `GET /user/sign-in`, step `sign-in page with return url`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### sign-in#001.0 — POST /user/sign-in?ReturnUrl=%2Faccount%2Fmanage

Route `POST /user/sign-in`, step `sign-in with wrong password`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### sign-in#002.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### sign-in#002.1 — GET /

Route `GET /`, step `sign-in`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### sign-in#003.0 — GET /account/manage

Route `GET /account/manage`, step `account manage`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### sign-in#004.0 — GET /account/change-password

Route `GET /account/change-password`, step `change password page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### sign-in#005.0 — POST /account/change-password

Route `POST /account/change-password`, step `change password with wrong current password`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### sign-in#006.0 — GET /account/edit-profile

Route `GET /account/edit-profile`, step `edit profile page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### sign-in#007.0 — POST /account/edit-profile

Route `POST /account/edit-profile`, step `save profile unchanged`, kinds: status, location, header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 200 | 302 |
| location | `Location` |  | /account/manage?Message=ProfileSaveSuccess |
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |
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

### sign-in#008.0 — GET /user/sign-out

Route `GET /user/sign-out`, step `sign out`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; path=/ | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### sign-in#008.1 — GET /

Route `GET /`, step `sign out`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### sign-in#009.1 — GET /user/sign-in?ReturnUrl=%2Ftickets

Route `GET /user/sign-in`, step `ticket center after sign-out`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### search#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### search#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### search#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### search#002.0 — GET /search

Route `GET /search`, step `search page empty`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### search#003.0 — GET /search?term=printer

Route `GET /search`, step `search by query`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]` | — | <div class="clickable panel panel-info" id="ticketDetailPanel">More Info #76 Label printer times out in building B IT Help Desk printer Low Priority Hardware Request Assigned To: Toma… |
| added | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[3]` | — | <div class="clickable panel panel-info" id="ticketDetailPanel">Closed #813 Label printer keeps disconnecting in the lab IT Help Desk printer High Priority Network Problem Assigned To:… |
| added | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[4]` | — | <div class="clickable panel panel-info" id="ticketDetailPanel">Resolved #1546 Label printer lost its settings in building B Facilities printer-040 Network Problem Assigned To: Yuki Ki… |
| added | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[5]` | — | <div class="clickable panel panel-info" id="ticketDetailPanel">Closed #2364 Printer needs access for a new hire in the warehouse Facilities printer Medium Priority Hardware Question A… |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[1]/div[1]/div[1]/div[1]/text()[1]` | Resolved | Active |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[1]/div[1]/div[1]/div[2]/text()[1]` | #3994 | #3582 |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[1]/div[1]/div[1]/div[3]/a[1]/@href` | /ticket/3994 | /ticket/3582 |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[1]/div[1]/div[1]/div[3]/a[1]/text()[1]` | Printer stopped syncing in the warehouse | Label printer shows an error in meeting room 4 |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[2]/div[1]/div[1]/div[1]/div[2]/div[1]/text()[1]` | High Priority Hardware Request | Low Priority Hardware Request |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[2]/div[1]/div[1]/div[1]/div[2]/div[2]/text()[1]` | Leo Smith | Mia Moore |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[2]/div[1]/div[1]/div[1]/div[2]/div[3]/text()[1]` | Ava Smith | Diego Patel |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[2]/div[1]/div[1]/div[1]/div[2]/div[4]/text()[1]` | Leo Smith | Mia Moore |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[8]/div[2]/div[1]/div[1]/div[2]/div[1]/text()[1]` | Several people on my team see the same thing. We tried a different cable already. Our manager approv ... | It worked fine last week. |

… 23 more diffs of this exchange in report.json

### search#004.0 — GET /search/network

Route `GET /search/{term}`, step `search by path`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[1]/text()[1]` | Resolved | Closed |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[2]/text()[1]` | #1633 | #742 |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[3]/a[1]/@href` | /ticket/1633 | /ticket/742 |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[1]/div[1]/div[1]/div[3]/a[1]/text()[1]` | Intranet page lost its settings in the warehouse | SSL certificate is very slow after the update |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[1]/div[1]/text()[1]` | Default | Finance Systems |
| added | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[1]/div[2]/div[1]` | — | <div>network-081 |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[2]/div[1]/text()[1]` | Hardware Question | Medium Priority Network Question |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[2]/div[2]/text()[1]` | Zoe Murphy | Diego Larsen |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[2]/div[3]/text()[1]` | Ines Lewis | Ravi Taylor |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[1]/div[2]/div[4]/text()[1]` | Zoe Murphy | Ravi Taylor |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[2]/div[2]/div[1]/div[1]/div[2]/div[1]/text()[1]` | We tried a different cable already. We tried a different cable already. | No urgency, whenever you have time. |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[3]/div[1]/div[1]/div[1]/div[1]/text()[1]` | Resolved | Closed |
| text | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/div[3]/div[1]/div[1]/div[1]/div[2]/text()[1]` | #3318 | #2248 |

… 61 more diffs of this exchange in report.json

### search#005.0 — GET /search?term=zzqqxxnohits

Route `GET /search`, step `search no hits`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-list#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-list#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-list#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-list#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center default list`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="yrwz4lXdQMyHQc5JbAcApA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#003.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket index redirects to the list`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="87sURh31N3SOggcjzaj92w=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#004.0 — GET /tickets/mytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `my tickets`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="lJG6oowAN2ryKgXyyQYzRA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#005.0 — GET /tickets/unassigned

Route `GET /tickets/{listName?}/{page:int?}`, step `unassigned`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="LK89KWOFE8s6rXyc3FKq6Q=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#006.0 — GET /tickets/assignedToMe

Route `GET /tickets/{listName?}/{page:int?}`, step `assigned to me`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="lEz/VdK3jIIiHbtOwZIXSg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#007.0 — GET /tickets/historytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `history`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="jqIv2lWcTigVfTT3BhwQmQ=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#008.0 — GET /tickets/opentickets

Route `GET /tickets/{listName?}/{page:int?}`, step `open tickets`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="oFzQTwrvttzBh3ysKztO0Q=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#009.0 — GET /tickets/pageList/opentickets/2

Route `GET /tickets/pageList/{listName=mytickets}/{page:int?}`, step `open tickets page 2`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[1]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[2]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[3]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[4]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[5]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[6]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[7]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[8]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[9]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[10]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[11]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[12]/span[1]/a[1]/@data-ajax-method` | — | POST |

### ticket-list#010.0 — POST /tickets/filterList

Route `POST /tickets/filterList/{listName=opentickets}/{page:int?}`, step `filter open tickets`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[1]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[2]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[3]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[4]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[5]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[6]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[7]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[8]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[9]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[10]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[11]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[12]/span[1]/a[1]/@data-ajax-method` | — | POST |

### ticket-list#011.0 — GET /tickets/sortList/opentickets?columnName=Title

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `sort by title`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[1]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[2]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[3]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[4]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[5]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[6]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[7]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[8]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[9]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[10]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[11]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[12]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[43]/@class` | clickable | clickable overdue |

### ticket-list#012.0 — GET /tickets/sortList/opentickets?columnName=Title

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `sort by title again (flip)`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[1]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[2]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[3]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[4]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[5]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[6]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[7]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[8]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[9]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[10]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[11]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[12]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |

### ticket-list#013.0 — GET /tickets/sortList/opentickets?columnName=Priority&isMultiSort=true

Route `GET /tickets/sortList/{listName=opentickets}/{page:int?}`, step `add priority to sort`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/body[1]/div[1]/div[1]/div[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[1]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[2]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[3]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[4]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[5]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[6]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[7]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[8]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[9]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[10]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[11]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/thead[1]/tr[1]/th[12]/span[1]/a[1]/@data-ajax-method` | — | POST |
| attribute | `html/body[1]/div[1]/div[2]/div[1]/table[1]/tbody[1]/tr[4]/@class` | clickable | clickable overdue |

### ticket-list#014.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `reset list settings`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="8WLSBRdQP1cUoLbKBLgIbQ=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#016.1 — GET /

Route `GET /`, step `switch to project 2`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-list#017.0 — GET /tickets/opentickets

Route `GET /tickets/{listName?}/{page:int?}`, step `open tickets in project 2`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="32Y+kPX9Prch78brDat4jw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-list#018.1 — GET /

Route `GET /`, step `switch to all projects`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-create-view#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-create-view#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-create-view#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-create-view#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="F8bx5bsmw7+3MEs9V7BS1w=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-create-view#003.1 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="OowkisNwMsEJ/vrtfPLSGQ=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: third-floor printer offline Default replay printer High Priority Software Problem Assigne… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-create-view#004.0 — GET /ticket/10001

Route `GET /ticket/{id:int}`, step `view ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="xDl0drk9hy0Ybm1BbS8qqw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: third-floor printer offline Default replay printer High Priority Software Problem Assigne… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-create-view#011.1 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `unknown ticket redirects to list`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="6HLfSPFLzGoVo51W5/bAlw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Unassigned My Assigned My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists wi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### ticket-edit#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-edit#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-edit#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-edit#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="9Ah46p1t/jEFY+TJUmsAcg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-edit#003.1 — GET /ticket/10002

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="puBL5X0PCI1dT06cqjqUGA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: laptop docking station flickers Default replay laptop High Priority Software Problem Assi… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-edit#004.0 — GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo

Route `GET /ticket-activity/load-activity`, step `load EditTicketInfo form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-comment#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-comment#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-comment#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-comment#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="MbeNEPV/NjVf5dakSEsLvg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-comment#003.1 — GET /ticket/10003

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="Q0tkriiMrLqxnOZH+jwuLg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: VPN drops every hour Default replay vpn High Priority Software Problem Assigned To: Unass… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-comment#004.0 — GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-assign#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-assign#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-assign#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-assign#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="oPFkujZr/6tm2u79s5EHuw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-assign#003.1 — GET /ticket/10004

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="5adBr35IsK+S6ZlI1R8XzA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: new hire needs an account Default replay onboarding High Priority Software Problem Assign… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-assign#004.0 — GET /ticket-activity/load-activity?ticketId=10004&activity=Assign

Route `GET /ticket-activity/load-activity`, step `load Assign form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### ticket-close#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-close#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-close#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-close#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="ycjB5u6vQa/9yk+YGNrcQg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-close#003.1 — GET /ticket/10005

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="JXWpCCEZ8/CiH05mNDEWmw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: monitor cable missing Default replay hardware High Priority Software Problem Assigned To:… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

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

Route `GET /ticket/{id:int}`, step `view closed ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="spgR3Kma8CC4RfV7l+X22g=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Closed #{var:ticketId} Replay: monitor cable missing Default replay hardware High Priority Software Problem Assigned To:… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-workflow#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### ticket-workflow#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### ticket-workflow#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### ticket-workflow#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="Fk4K2mRWIZIEoxevi/RTlw=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### ticket-workflow#003.1 — GET /ticket/10006

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="m86nRPJ/YPwQu6nzapjlCA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: shared drive permissions Default replay access High Priority Software Problem Assigned To… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

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

Route `GET /ticket/{id:int}`, step `view reopened ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="INg/yON1Vc9ZiZj4RnQamg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: shared drive permissions Default replay access High Priority Software Problem Assigned To… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### attachments#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### attachments#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### attachments#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### attachments#002.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="CQUNdWCWOZuYPfRRma4nqA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### attachments#006.0 — GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9

Route `GET /file/get-attachments-info`, step `pending attachments info`, kinds: status, body-text

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 500 | 200 |
| body-text | `line 1` |  | [{"name":"replay-note.txt","size":29,"isAttached":false,"type":"text/plain","url":"/file/{guid}/replay-note.txt"}] |

### attachments#007.1 — GET /ticket/10007

Route `GET /ticket/{id:int}`, step `create ticket with attachment`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: {secret:TD_ADMIN_NAME} My Account Log Off Settings Manage Users … | <td-root ngcspnonce="HhNUTJB7IEV/iIQnAEOv4g=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: scanned invoice attached Default replay attachment High Priority Software Problem Assigne… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### attachments#011.0 — GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments

Route `GET /ticket-activity/load-activity`, step `load ModifyAttachments form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### admin-settings#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### admin-settings#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### admin-settings#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#002.0 — GET /admin/application-settings

Route `GET /admin/application-settings/{action=index}`, step `application settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#003.0 — GET /admin/users

Route `GET /admin/users/{page:int?}`, step `users`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#005.0 — GET /admin/user/91efcdb5-506c-4b1b-af6f-08b63dee1475

Route `GET /admin/user/{id?}`, step `edit user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-settings#006.0 — GET /admin/new

Route `GET /admin/new`, step `new user page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#007.0 — GET /admin/projects

Route `GET /admin/projects`, step `projects`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#008.0 — GET /admin/project/new

Route `GET /admin/project/new`, step `new project page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-settings#009.0 — GET /admin/project/1

Route `GET /admin/project/{projectId:int}`, step `edit default project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-settings#010.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5483 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5483'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-settings#012.0 — GET /admin/search-manager

Route `GET /admin/search-manager`, step `search manager`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/div[1]/p[1]/text()[1]` | — | "Search uses SQL Server full-text indexes. SQL Server updates them after every change to a ticket or comment, whichever application made it, so there is no index to remove or rebuild here." |
| element | `html/body[1]/div[1]/div[1]/div[1]` | <div>Remove Search Index | <table class="table table-condensed" id="fullTextIndexes">Table State Indexed rows Changes waiting Last full population dbo.TicketEvents updating 45,649 27 running dbo.Tickets up… |
| removed | `html/body[1]/div[1]/div[1]/p[2]` | <p> | — |
| removed | `html/body[1]/div[1]/div[1]/div[2]` | <div>Populate Search Index | — |
| removed | `html/body[1]/div[1]/div[1]/p[3]` | <p> | — |

### admin-settings#013.0 — GET /admin/data-management/demo

Route `GET /admin/data-management/demo`, step `demo data page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### admin-changes#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### admin-changes#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#002.0 — GET /admin/application-settings

Route `GET /admin/application-settings/{action=index}`, step `application settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#003.0 — POST /admin/application-settings

Route `POST /admin/application-settings/{action=index}`, step `save application settings unchanged`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#004.0 — GET /admin/push-notification-settings

Route `GET /admin/push-notification-settings/{action=index}`, step `push notification settings`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5483 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5483'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#005.0 — POST /admin/push-notification-settings

Route `POST /admin/push-notification-settings/{action=index}`, step `save push notification settings unchanged`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/fieldset[1]/div[1]/form[1]/div[1]/div[4]/div[3]/div[1]/div[1]/input[1]/@value` | https://localhost | https://localhost:5483 |
| text | `html/body[1]/script[5]/text()[1]` | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); | $(document).ready(function () { $('#useCurrentUrlButton').on('click', function () { $('#siteRootUrl').val('https://localhost:5483'); }); //Fix local url validation var originalMethods = { url: $.validator.methods.url }; $.validator.methods.url = function (value, element) { return this.optional(element) \|\| (/^https?:\/\/\w+(\.\w+)*(:[0-9]+)?\/?(\/[.\w]*)*$/).test(value); }; }); |

### admin-changes#006.0 — GET /admin/push-notification-settings/configure-provider?providerAssemblyQualifiedName=TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider%2C%20TicketDesk.PushNotifications%2C%20Version%3D1.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull

Route `GET /admin/push-notification-settings/configure-provider`, step `configure SMTP provider`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/input[1]/@placeholder` | — | (not set: use the secret store) |
| text | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/span[2]/text()[1]` | Leave empty if authentication is not required | Leave empty if authentication is not required Stored in the secret store (PushNotifications:Secrets:Smtp:SmtpPassword), never in the database. |
| added | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#007.0 — POST /admin/push-notification-settings/configure-provider

Route `POST /admin/push-notification-settings/configure-provider`, step `save SMTP provider unchanged`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| attribute | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/input[1]/@placeholder` | — | (not set: use the secret store) |
| text | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/fieldset[1]/div[1]/div[2]/div[6]/div[1]/span[2]/text()[1]` | Leave empty if authentication is not required | Leave empty if authentication is not required Stored in the secret store (PushNotifications:Secrets:Smtp:SmtpPassword), never in the database. |
| added | `html/body[1]/div[1]/main[1]/div[1]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#008.0 — GET /admin/project/new

Route `GET /admin/project/new`, step `new project page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#009.1 — GET /admin/projects

Route `GET /admin/projects`, step `create project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[7]/a[1]` | <a href="/navigation/change-project/6">Replay Project | <form action="/navigation/change-project/6" class="td-menu-post" method="post">Replay Project |

### admin-changes#010.0 — GET /admin/projects

Route `GET /admin/projects`, step `projects`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[7]/a[1]` | <a href="/navigation/change-project/6">Replay Project | <form action="/navigation/change-project/6" class="td-menu-post" method="post">Replay Project |

### admin-changes#011.0 — GET /admin/project/6

Route `GET /admin/project/{projectId:int}`, step `edit replay project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[7]/a[1]` | <a href="/navigation/change-project/6">Replay Project | <form action="/navigation/change-project/6" class="td-menu-post" method="post">Replay Project |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#012.1 — GET /admin/projects

Route `GET /admin/projects`, step `save replay project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[7]/a[1]` | <a href="/navigation/change-project/6">Replay Project | <form action="/navigation/change-project/6" class="td-menu-post" method="post">Replay Project |

### admin-changes#013.0 — GET /admin/project/6

Route `GET /admin/project/{projectId:int}`, step `edit replay project again`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[7]/a[1]` | <a href="/navigation/change-project/6">Replay Project | <form action="/navigation/change-project/6" class="td-menu-post" method="post">Replay Project |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#014.2 — GET /admin/projects

Route `GET /admin/projects`, step `delete replay project`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#015.0 — GET /admin/new

Route `GET /admin/new`, step `new user page`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#016.1 — GET /admin/users

Route `GET /admin/users/{page:int?}`, step `create user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#017.0 — GET /admin/users

Route `GET /admin/users/{page:int?}`, step `users after create`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#018.0 — GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119

Route `GET /admin/user/{id?}`, step `edit new user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#019.1 — GET /admin/users

Route `GET /admin/users/{page:int?}`, step `save new user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### admin-changes#020.0 — GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119

Route `GET /admin/user/{id?}`, step `edit new user again`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |
| added | `html/body[1]/div[1]/main[1]/div[2]/div[1]/form[1]/input[1]` | — | <input name="__RequestVerificationToken" type="hidden" value="{antiforgery}"> |

### admin-changes#021.1 — GET /admin/users

Route `GET /admin/users/{page:int?}`, step `delete new user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### internal-user#000.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### internal-user#001.0 — POST /user/sign-in

Route `POST /user/sign-in`, step `sign-in submit`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### internal-user#001.1 — GET /

Route `GET /`, step `sign-in submit`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### internal-user#002.0 — GET /tickets

Route `GET /tickets/{listName?}/{page:int?}`, step `ticket center`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Ali Chen My Account Log Off Settings Manage Users Manage Project… | <td-root ngcspnonce="nWX+htiMNiDLKmAcoz4tnA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists will be reset, including … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### internal-user#003.0 — GET /tickets/mytickets

Route `GET /tickets/{listName?}/{page:int?}`, step `my tickets`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket Center | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Ali Chen My Account Log Off Settings Manage Users Manage Project… | <td-root ngcspnonce="oS3IA/Lg4eNnCRetcI95EA=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">My Tickets Open Tickets Ticket History Reset Lists × Are you sure you want to reset? All lists will be reset, including … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/ticketcenter?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script type="text/javascript">$(document).ready(function () { $('main').show(); $.ajaxSetup({ cache: false }); ticketCenter.filters.setupFilterForm();… | — |
| removed | `html/body[1]/script[7]` | <script type="text/javascript">$(document).ready(function () { $('[data-toggle="tooltip"]').tooltip(); }); | — |

### internal-user#004.0 — GET /tickets/unassigned

Route `GET /tickets/{listName?}/{page:int?}`, step `unassigned (help desk only)`, kinds: status, location, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| status | `status` | 500 | 302 |
| location | `Location` |  | /access-denied |
| element | `html/head[1]/meta[1]` | <meta content="width=device-width" name="viewport"> | <title>Object moved |
| removed | `html/head[1]/title[1]` | <title>Error | — |
| element | `html/body[1]/hgroup[1]` | <hgroup>Error. An error occurred while processing your request. | <h2>Object moved to here . |

### internal-user#005.1 — GET /access-denied

Route `GET /access-denied`, step `admin page is refused`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]/a[1]` | <a href="/user/sign-out">Log Off | <form action="/user/sign-out" class="td-menu-post" method="post">Log Off |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[1]/a[1]` | <a href="/navigation/change-project/0">All Projects | <form action="/navigation/change-project/0" class="td-menu-post" method="post">All Projects |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[2]/a[1]` | <a href="/navigation/change-project/1">Default | <form action="/navigation/change-project/1" class="td-menu-post" method="post">Default |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[3]/a[1]` | <a href="/navigation/change-project/3">Facilities | <form action="/navigation/change-project/3" class="td-menu-post" method="post">Facilities |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[4]/a[1]` | <a href="/navigation/change-project/5">Finance Systems | <form action="/navigation/change-project/5" class="td-menu-post" method="post">Finance Systems |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[5]/a[1]` | <a href="/navigation/change-project/4">HR Services | <form action="/navigation/change-project/4" class="td-menu-post" method="post">HR Services |
| element | `html/body[1]/nav[1]/div[3]/div[1]/div[1]/div[1]/span[1]/ul[1]/li[6]/a[1]` | <a href="/navigation/change-project/2">IT Help Desk | <form action="/navigation/change-project/2" class="td-menu-post" method="post">IT Help Desk |

### internal-user#006.0 — GET /ticket/new

Route `GET /ticket/new`, step `new ticket page`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| header | `X-Frame-Options` | SAMEORIGIN, DENY | DENY |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>New Ticket | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Ali Chen My Account Log Off Settings Manage Users Manage Project… | <td-root ngcspnonce="8IK8Tt+bqcxilinZS1VsYQ=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Create new ticket Project -- select -- Default Facilities Finance Systems HR Services IT Help Desk Title Type -- select … | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### internal-user#007.1 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `create ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Ali Chen My Account Log Off Settings Manage Users Manage Project… | <td-root ngcspnonce="trLQI7a3cl6Rr48XRUW1OQ=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: keyboard missing keys Default replay keyboard Software Problem Assigned To: Unassigned Ow… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### internal-user#008.0 — GET /ticket/10008

Route `GET /ticket/{id:int}`, step `view own ticket`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | XSRF-TOKEN; path=/; samesite=strict; secure |
| attribute | `html/@lang` | — | en |
| element | `html/head[1]/meta[2]` | <meta content="IE=edge" http-equiv="X-UA-Compatible"> | <title>TicketDesk |
| element | `html/head[1]/meta[3]` | <meta content="width=device-width, initial-scale=1.0" name="viewport"> | <base href="/app/"> |
| attribute | `html/head[1]/meta[4]/@content` | none | width=device-width, initial-scale=1 |
| attribute | `html/head[1]/meta[4]/@name` | msapplication-config | viewport |
| element | `html/head[1]/title[1]` | <title>Ticket #{var:ticketId} | <meta content="TicketDesk help desk: tickets, lists and new tickets." name="description"> |
| element | `html/head[1]/style[1]` | <style type="text/css"> | <link href="favicon.svg" rel="icon" type="image/svg+xml"> |
| attribute | `html/head[1]/link[1]/@href` | /content/styles?v={v} | styles-P43MTZNW.css |
| removed | `html/head[1]/script[1]` | <script src="/bundles/modernizr?v={v}"> | — |
| element | `html/body[1]/nav[1]` | <nav class="navbar navbar-default" id="mainNav" role="navigation">Toggle navigation Home Ticket Center New Ticket Search: Ali Chen My Account Log Off Settings Manage Users Manage Project… | <td-root ngcspnonce="1eOnt8Dt+PhUmN/C2jOpsg=="> |
| element | `html/body[1]/div[1]` | <div class="body-content">Active #{var:ticketId} Replay: keyboard missing keys Default replay keyboard Software Problem Assigned To: Unassigned Ow… | <script src="main-CYHZEDY2.js" type="module"> |
| removed | `html/body[1]/script[1]` | <script type="text/javascript">window.currentCulture = 'en-US'; | — |
| removed | `html/body[1]/script[2]` | <script src="/bundles/common?v={v}"> | — |
| removed | `html/body[1]/script[3]` | <script src="/bundles/common_locale_en-US?v={v}"> | — |
| removed | `html/body[1]/script[4]` | <script type="text/javascript">Globalize.culture(window.currentCulture); //Fix to decimal separator validation var _number = $.validator.methods.number… | — |
| removed | `html/body[1]/link[1]` | <link href="/content/editor?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[2]` | <link href="/content/select2?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[3]` | <link href="/content/summernote?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/link[4]` | <link href="/content/datepicker?v={v}" rel="stylesheet"> | — |
| removed | `html/body[1]/script[5]` | <script src="/bundles/editor?v={v}"> | — |
| removed | `html/body[1]/script[6]` | <script src="/bundles/select2?v={v}"> | — |
| removed | `html/body[1]/script[7]` | <script src="/bundles/select2_locale_en-US?v="> | — |
| removed | `html/body[1]/script[8]` | <script src="/bundles/markdown?v={v}"> | — |

… 6 more diffs of this exchange in report.json

### internal-user#010.0 — GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment

Route `GET /ticket-activity/load-activity`, step `load AddComment form`, kinds: header

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | s-maxage=0, private | no-store, no-cache |

### internal-user#012.0 — GET /user/sign-out

Route `GET /user/sign-out`, step `sign out`, kinds: set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; path=/ | .AspNetCore.Identity.Application; httponly; path=/; samesite=lax; secure |

### internal-user#012.1 — GET /

Route `GET /`, step `sign out`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### register-disabled#000.1 — GET /

Route `GET /`, step `register page redirects (disabled)`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` |  | .TicketDesk.Antiforgery; httponly; path=/; samesite=strict |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### register-disabled#001.0 — GET /user/sign-in

Route `GET /user/sign-in`, step `sign-in page for an antiforgery token`, kinds: header, set-cookie, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| set-cookie | `Set-Cookie` | __RequestVerificationToken; httponly; path=/ |  |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |

### register-disabled#002.0 — POST /user/register

Route `POST /user/register`, step `register anyway`, kinds: header, set-cookie

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | no-cache | private |
| set-cookie | `Set-Cookie` | .AspNet.ApplicationCookie; httponly; path=/; secure |  |

### register-disabled#002.1 — GET /

Route `GET /`, step `register anyway`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
| removed | `html/body[1]/nav[1]/div[3]` | <div class="container-fluid navbar-default" style="border-top: 1px solid #e7e7e7; padding: 7px;">Project: All Projects All Projects Default Facilities Finance Systems HR Services IT Help Desk Search: | — |

### register-disabled#003.0 — GET /

Route `GET /`, step `home as the registered user`, kinds: header, body-html

| Kind | Path | Baseline | Candidate |
|---|---|---|---|
| header | `Cache-Control` | private | no-store, no-cache |
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
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
| added | `html/head[1]/style[1]/text()[1]` | — | "/* S3 (Round 5): menu items that post (sign-out, language, project) look like the dropdown links they replace */ .dropdown-menu > li > form.td-menu-post { margin: 0; } .dropdown-menu > li > form.td-menu-post > button { display: block; width: 100%; padding: 3px 20px; clear: both; font: inherit; font… |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/a[1]/span[1]/text()[1]` | Replay Register | Account |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@href` | /account/manage | /user/sign-in |
| attribute | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/@id` | — | loginLink |
| text | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[1]/a[1]/text()[1]` | My Account | Sign In |
| removed | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[2]/ul[1]/li[2]` | <li>Log Off | — |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[1]/a[1]` | <a href="/language?name=en-US">English | <form action="/language?name=en-US" class="td-menu-post" method="post">English |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[2]/a[1]` | <a href="/language?name=pt-BR">Português do Brasil | <form action="/language?name=pt-BR" class="td-menu-post" method="post">Português do Brasil |
| element | `html/body[1]/nav[1]/div[2]/div[1]/ul[2]/li[4]/ul[1]/li[3]/a[1]` | <a href="/language?name=de-DE">Deutsch (Deutschland) | <form action="/language?name=de-DE" class="td-menu-post" method="post">Deutsch (Deutschland) |
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

