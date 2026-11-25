# Replay summary

Compared 262, matched 155, mismatched 109 (missing 0, extra 2); unexplained mismatches: 0.

Answered by the new app: 255 exchanges on 75 routes (38 by its Angular front end on 3 routes); by the legacy app: 9.

| Wave | Exchanges | New app | Legacy | Mismatches | Explained | Unexplained |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 2 | 2 | 0 | 0 | 0 | 0 |
| 1 | 34 | 34 | 0 | 3 | 3 | 0 |
| 2 | 99 | 95 | 4 | 45 | 45 | 0 |
| 3 | 11 | 11 | 0 | 4 | 4 | 0 |
| 4 | 62 | 62 | 0 | 29 | 29 | 0 |
| 5 | 51 | 51 | 0 | 28 | 28 | 0 |
| retire | 5 | 0 | 5 | 0 | 0 | 0 |

## Routes answered by the new app

| Route id | Wave | Exchanges | of these by the Angular front end |
|---|---|---:|---:|
| account.change-password.get | 2 | 1 | 0 |
| account.change-password.post | 5 | 1 | 0 |
| account.edit-profile.get | 2 | 1 | 0 |
| account.edit-profile.post | 5 | 1 | 0 |
| account.manage | 2 | 2 | 0 |
| activity.add-comment | 4 | 2 | 0 |
| activity.assign | 4 | 2 | 0 |
| activity.buttons | 2 | 6 | 0 |
| activity.cancel-more-info | 4 | 1 | 0 |
| activity.close | 4 | 1 | 0 |
| activity.edit-ticket-info | 4 | 1 | 0 |
| activity.force-close | 4 | 1 | 0 |
| activity.give-up | 4 | 1 | 0 |
| activity.load | 4 | 21 | 0 |
| activity.modify-attachments | 4 | 1 | 0 |
| activity.pass | 4 | 1 | 0 |
| activity.reassign | 4 | 1 | 0 |
| activity.reopen | 4 | 1 | 0 |
| activity.request-more-info | 4 | 2 | 0 |
| activity.resolve | 4 | 1 | 0 |
| activity.supply-more-info | 4 | 1 | 0 |
| activity.take-over | 4 | 4 | 0 |
| admin.application-settings.get | 2 | 2 | 0 |
| admin.application-settings.post | 5 | 1 | 0 |
| admin.demo | 2 | 1 | 0 |
| admin.project-delete | 5 | 1 | 0 |
| admin.project-edit.get | 2 | 4 | 0 |
| admin.project-edit.post | 5 | 1 | 0 |
| admin.project-new.get | 2 | 2 | 0 |
| admin.project-new.post | 5 | 1 | 0 |
| admin.projects | 2 | 5 | 0 |
| admin.push-provider.get | 5 | 1 | 0 |
| admin.push-provider.post | 5 | 1 | 0 |
| admin.push-providers | 2 | 1 | 0 |
| admin.push-settings.get | 2 | 2 | 0 |
| admin.push-settings.post | 5 | 1 | 0 |
| admin.search-manager | 2 | 1 | 0 |
| admin.user-delete | 5 | 1 | 0 |
| admin.user-edit.get | 2 | 3 | 0 |
| admin.user-edit.post | 5 | 1 | 0 |
| admin.user-new.get | 2 | 2 | 0 |
| admin.user-new.post | 5 | 1 | 0 |
| admin.users | 2 | 7 | 0 |
| admin.users-page | 2 | 1 | 0 |
| autocomplete.tag-list | 2 | 1 | 0 |
| bundles.scripts | 0 | 1 | 0 |
| bundles.styles | 0 | 1 | 0 |
| file.attachments-info | 2 | 2 | 0 |
| file.delete | 4 | 1 | 0 |
| file.get | 2 | 1 | 0 |
| file.upload | 4 | 3 | 0 |
| home.about | 1 | 1 | 0 |
| home.access-denied | 1 | 3 | 0 |
| home.index | 1 | 1 | 0 |
| home.language | 3 | 2 | 0 |
| home.root | 1 | 29 | 0 |
| navigation.change-project | 3 | 2 | 0 |
| ticket.details | 2 | 5 | 0 |
| ticket.events | 2 | 6 | 0 |
| ticket.files | 2 | 7 | 0 |
| ticket.list-redirect | 2 | 1 | 0 |
| ticket.new.get | 4 | 8 | 8 |
| ticket.new.post | 4 | 8 | 0 |
| ticket.subscription | 3 | 2 | 0 |
| ticket.view | 2 | 14 | 14 |
| tickets.filter | 3 | 1 | 0 |
| tickets.index | 2 | 16 | 16 |
| tickets.page | 2 | 1 | 0 |
| tickets.reset-lists | 3 | 1 | 0 |
| tickets.sort | 3 | 3 | 0 |
| user.register.get | 5 | 2 | 0 |
| user.register.post | 5 | 1 | 0 |
| user.sign-in.get | 5 | 20 | 0 |
| user.sign-in.post | 5 | 14 | 0 |
| user.sign-out | 5 | 2 | 0 |

## Mismatches

| Key | Request | Route | Answered by | Kinds | Expected difference |
|---|---|---|---|---|---|
| `anonymous#003.1` | GET /user/sign-in?ReturnUrl=%2Ftickets | user.sign-in.get | new | header, set-cookie | S3 |
| `anonymous#004.1` | GET /user/sign-in?ReturnUrl=%2Fticket%2F1 | user.sign-in.get | new | header | S3 |
| `anonymous#005.1` | GET /user/sign-in?ReturnUrl=%2Fadmin%2Fusers | user.sign-in.get | new | header | S3 |
| `anonymous#006.0` | GET /user/sign-in | user.sign-in.get | new | header | S3 |
| `sign-in#000.0` | GET /user/sign-in?ReturnUrl=%2Faccount%2Fmanage | user.sign-in.get | new | header, set-cookie | S3 |
| `sign-in#001.0` | POST /user/sign-in?ReturnUrl=%2Faccount%2Fmanage | user.sign-in.post | new | header | S3 |
| `sign-in#004.0` | GET /account/change-password | account.change-password.get | new | header | S3 |
| `sign-in#005.0` | POST /account/change-password | account.change-password.post | new | header | S3 |
| `sign-in#006.0` | GET /account/edit-profile | account.edit-profile.get | new | header | S3 |
| `sign-in#007.0` | POST /account/edit-profile | account.edit-profile.post | new | status, location, header, set-cookie, body-html | L5, S3 |
| `sign-in#007.1` | GET /account/manage?Message=ProfileSaveSuccess | account.manage | new | extra | L5 |
| `sign-in#009.1` | GET /user/sign-in?ReturnUrl=%2Ftickets | user.sign-in.get | new | header | S3 |
| `search#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-list#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-list#002.0` | GET /tickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#003.1` | GET /tickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#004.0` | GET /tickets/mytickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#005.0` | GET /tickets/unassigned | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#006.0` | GET /tickets/assignedToMe | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#007.0` | GET /tickets/historytickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#008.0` | GET /tickets/opentickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#009.0` | GET /tickets/pageList/opentickets/2 | tickets.page | new | header, body-html | S3 |
| `ticket-list#010.0` | POST /tickets/filterList | tickets.filter | new | header, body-html | S3 |
| `ticket-list#011.0` | GET /tickets/sortList/opentickets?columnName=Title | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#012.0` | GET /tickets/sortList/opentickets?columnName=Title | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#013.0` | GET /tickets/sortList/opentickets?columnName=Priority&isMultiSort=true | tickets.sort | new | header, body-html | S3, S5 |
| `ticket-list#014.1` | GET /tickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-list#017.0` | GET /tickets/opentickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-create-view#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-create-view#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-create-view#003.1` | GET /ticket/10001 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-create-view#004.0` | GET /ticket/10001 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-create-view#011.1` | GET /tickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `ticket-edit#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-edit#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-edit#003.1` | GET /ticket/10002 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-edit#004.0` | GET /ticket-activity/load-activity?ticketId=10002&activity=EditTicketInfo | activity.load | new | header | S3 |
| `ticket-comment#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-comment#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-comment#003.1` | GET /ticket/10003 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-comment#004.0` | GET /ticket-activity/load-activity?ticketId=10003&activity=AddComment | activity.load | new | header | S3 |
| `ticket-assign#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-assign#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-assign#003.1` | GET /ticket/10004 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-assign#004.0` | GET /ticket-activity/load-activity?ticketId=10004&activity=Assign | activity.load | new | header | S3 |
| `ticket-close#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-close#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-close#003.1` | GET /ticket/10005 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-close#004.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=TakeOver | activity.load | new | header | S3 |
| `ticket-close#006.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Resolve | activity.load | new | header | S3 |
| `ticket-close#008.0` | GET /ticket-activity/load-activity?ticketId=10005&activity=Close | activity.load | new | header | S3 |
| `ticket-close#010.0` | GET /ticket/10005 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `ticket-workflow#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `ticket-workflow#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `ticket-workflow#003.1` | GET /ticket/10006 | ticket.view | angular | header, set-cookie, body-html | A2 |
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
| `ticket-workflow#030.0` | GET /ticket/10006 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `attachments#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `attachments#002.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `attachments#006.0` | GET /file/get-attachments-info?tempId=da6a5d4c-8f66-4aac-a60f-361c41d9abe9 | file.attachments-info | new | status, body-text | L1 |
| `attachments#007.1` | GET /ticket/10007 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `attachments#011.0` | GET /ticket-activity/load-activity?ticketId=10007&activity=ModifyAttachments | activity.load | new | header | S3 |
| `admin-settings#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `admin-settings#002.0` | GET /admin/application-settings | admin.application-settings.get | new | header | S3 |
| `admin-settings#005.0` | GET /admin/user/91efcdb5-506c-4b1b-af6f-08b63dee1475 | admin.user-edit.get | new | header, body-html | S3 |
| `admin-settings#006.0` | GET /admin/new | admin.user-new.get | new | header | S3 |
| `admin-settings#008.0` | GET /admin/project/new | admin.project-new.get | new | header | S3 |
| `admin-settings#009.0` | GET /admin/project/1 | admin.project-edit.get | new | header, body-html | S3 |
| `admin-settings#010.0` | GET /admin/push-notification-settings | admin.push-settings.get | new | header, body-html | G6, S3 |
| `admin-changes#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `admin-changes#002.0` | GET /admin/application-settings | admin.application-settings.get | new | header | S3 |
| `admin-changes#003.0` | POST /admin/application-settings | admin.application-settings.post | new | header | S3 |
| `admin-changes#004.0` | GET /admin/push-notification-settings | admin.push-settings.get | new | header, body-html | G6, S3 |
| `admin-changes#005.0` | POST /admin/push-notification-settings | admin.push-settings.post | new | header, body-html | G6, S3 |
| `admin-changes#006.0` | GET /admin/push-notification-settings/configure-provider?providerAssemblyQualifiedName=TicketDesk.PushNotifications.Delivery.SmtpDeliveryProvider%2C%20TicketDesk.PushNotifications%2C%20Version%3D1.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Dnull | admin.push-provider.get | new | header, body-html | S3, S6 |
| `admin-changes#007.0` | POST /admin/push-notification-settings/configure-provider | admin.push-provider.post | new | header, body-html | S3, S6 |
| `admin-changes#008.0` | GET /admin/project/new | admin.project-new.get | new | header | S3 |
| `admin-changes#011.0` | GET /admin/project/6 | admin.project-edit.get | new | header, body-html | S3 |
| `admin-changes#013.0` | GET /admin/project/6 | admin.project-edit.get | new | header, body-html | S3 |
| `admin-changes#015.0` | GET /admin/new | admin.user-new.get | new | header | S3 |
| `admin-changes#018.0` | GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119 | admin.user-edit.get | new | header, body-html | S3 |
| `admin-changes#020.0` | GET /admin/user/aa1ae412-2e19-43e4-9b39-fc2ada47c119 | admin.user-edit.get | new | header, body-html | S3 |
| `internal-user#000.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `internal-user#002.0` | GET /tickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `internal-user#003.0` | GET /tickets/mytickets | tickets.index | angular | header, set-cookie, body-html | A1 |
| `internal-user#004.0` | GET /tickets/unassigned | tickets.index | angular | status, location, body-html | L2 |
| `internal-user#004.1` | GET /access-denied | home.access-denied | new | extra | L2 |
| `internal-user#006.0` | GET /ticket/new | ticket.new.get | angular | header, set-cookie, body-html | A3 |
| `internal-user#007.1` | GET /ticket/10008 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `internal-user#008.0` | GET /ticket/10008 | ticket.view | angular | header, set-cookie, body-html | A2 |
| `internal-user#010.0` | GET /ticket-activity/load-activity?ticketId=10008&activity=AddComment | activity.load | new | header | S3 |
| `register-disabled#001.0` | GET /user/sign-in | user.sign-in.get | new | header, set-cookie | S3 |
| `register-disabled#002.0` | POST /user/register | user.register.post | new | header, set-cookie | S4 |
| `register-disabled#002.1` | GET / | home.root | new | body-html | S4 |
| `register-disabled#003.0` | GET / | home.root | new | body-html | S4 |
| `register-disabled#004.0` | GET /tickets | tickets.index | angular | header, location, body-text | S4 |
| `register-disabled#004.1` | GET /access-denied | user.sign-in.get | new | header, body-html | S3, S4 |
