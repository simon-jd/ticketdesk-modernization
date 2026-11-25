# TicketDesk 2.5 legacy inventory

Round 0, `inventory-harness`. Source: `modernize` at `110dfed` (upstream `develop`, tag `upstream-develop-20220103`).
Method: static reading of the code, the EF6 migration snapshots (`.resx` model blobs decoded) and the config files.
Items marked **verify** are not yet confirmed against the running app or a database the app created (see §16).
The per-action table is [`routes.csv`](routes.csv) (87 rows: 77 actions in 18 controllers, plus bundles).

## 0. Corrections to the plan's numbers

| Plan says | Code says |
|---|---|
| 14 csproj, 18 controllers, 76 views | Correct (14 projects in `TicketDesk-2.5.sln`, 18 controller classes, 76 `.cshtml` under `Views/`). |
| EF6 with **58** migrations | **23 code-based migrations** in `TicketDesk.Domain` (`InitialCreate` + `Td2.5.001`…`Td2.5.022`) and 1 in `TicketDesk.Domain.Legacy` (the TD 2.0 schema, used only for upgrades). `TdIdentityContext` and `TdPushNotificationContext` use **automatic migrations** (no migration classes). Each migration has 3 files (`.cs`, `.Designer.cs`, `.resx`): 69 files, which may be where a larger number came from. |
| 2 test files | **No tests.** There is no test project; the two file names containing "test" are `EmailTestController.cs` and `SummerNoteStringExtensions.cs`. |

The README/RESULTS should use the corrected numbers.

## 1. Solution map

| Project | Role | Notable dependencies |
|---|---|---|
| `TicketDesk.Web.Client` | MVC 5.2.3 web app (OWIN startup, SimpleInjector 4.0.8, Postal 1.2, X.PagedList, jQuery 2.2.2, Bootstrap 3.3.6, Dropzone, select2, Summernote, PageDown) | everything below |
| `TicketDesk.Domain` | EF 6.1.3 `TdDomainContext`, entities, `TicketActionManager` (ticket workflow rules), user list settings | Newtonsoft.Json 9 |
| `TicketDesk.Domain.Legacy` | TD 2.0 schema + upgrade path to 2.5 (first-run "upgrade database") | EF6 |
| `TicketDesk.Web.Identity` | ASP.NET Identity 2.2.1 `TdIdentityContext`, user/role managers, user display-name cache | Identity.EntityFramework 2.2.1 |
| `TicketDesk.PushNotifications` | `TdPushNotificationContext` (schema `notifications`), delivery manager, SMTP and SendGrid providers | S22.Mail, Sendgrid 9.5.1 |
| `TicketDesk.PushNotifications.Job` | Azure WebJob (SDK 1.1.2) that runs the delivery loop on a timer | Microsoft.Azure.WebJobs |
| `TicketDesk.Search`, `.Search.Common` | `TdSearchContext`: picks Azure Search or Lucene | |
| `TicketDesk.Search.Lucene` | Lucene.Net 3.0.3 index on disk (`App_Data/ticketdesk-searchindex`) | Lucene.Net |
| `TicketDesk.Search.Azure` | Azure Search provider, queues index items | WindowsAzure.Storage 8.2 |
| `TicketDesk.SearchMonitor.Job` | Azure WebJob: `QueueTrigger("ticket-search-queue")` → Azure Search | WebJobs SDK |
| `TicketDesk.IO` | attachment storage (local disk or Azure Blob), Azure queue helper | WindowsAzure.Storage |
| `TicketDesk.Localization` | 114 `.resx` files: neutral (en-US) + `pt-BR` (38) + `de-DE` (38); `CultureHelper` | |
| `S22.Mail` | vendored library: serializable `MailMessage` | |

## 2. Request pipeline

`Startup.Configuration` (OWIN, `Microsoft.Owin.Host.SystemWeb`): global filters (`LocalizationFilter`, `HandleErrorAttribute`) → routes
(`MapMvcAttributeRoutes` then the conventional `{controller}/{action}/{id}`) → bundles → SimpleInjector container → database
initializers (`ConfigureDatabase`) → search (`ConfigureSearch`, runs `Optimize()` on the Lucene index at startup) → cookie auth →
push notifications (may start an in-process timer) → Application Insights (off unless a key is set).

- Almost every action is attribute-routed; attribute-routed actions are not reachable through the conventional route.
  **`EmailTestController` has no attribute routes and no `[Authorize]`**, so `/emailtest/index/{id}` is reachable anonymously.
- `TdAuthorizeAttribute` turns a 401 for a signed-in user into a redirect to `/access-denied`; anonymous users go to `/user/sign-in?ReturnUrl=…` (cookie middleware `LoginPath`).
- `DbSetupFilter` (added when `ticketdesk:SetupEnabled=true` and the DB is not ready) redirects every non-setup request to `/first-run-setup`.
- `customErrors mode="Off"`, `compilation debug="true"` in the shipped `Web.config` (yellow screens with stack traces; Release transforms change this).
- Response headers set in `Web.config`: `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `X-Content-Type-Options: nosniff ` (trailing space in the value), `X-Powered-By` removed, MVC version header disabled.
- No SignalR, no Web API, no OData. JSON comes from MVC actions via `JsonCamelCaseResult` (camelCase, `AllowGet` on GETs).

## 3. Controllers and actions (summary of `routes.csv`)

| Area | Routes | Auth | Notes |
|---|---|---|---|
| Home | `/`, `/index`, `/about`, `/access-denied`, `/language` | anonymous | `/` 301-redirects to `/tickets` when `HomeEnabled=false` |
| User / Account | `/user/sign-in`, `/user/register`, `/user/sign-out`, `/account/manage`, `/account/change-password`, `/account/edit-profile` | anonymous / signed in | sign-in tries e-mail when the name contains `@`, then optional LDAP |
| Ticket center | `/tickets/{listName?}/{page?}`, `/tickets/pageList/…`, `/tickets/filterList/…`, `/tickets/sortList/…`, `/tickets/reset-user-lists` | internal users, help desk, admins | filter/sort are persisted per user; partials replace `#ticketList` |
| Ticket | `/ticket/{id}`, `/ticket/new`, `/ticket/ticket-details|ticket-events|ticket-files`, `/ticket/change-ticket-subscription` | same | ticket page = shell + 4 AJAX panels |
| Ticket activity | `/ticket-activity/load-activity`, `/activity-buttons` and 15 POST actions (add-comment, assign, cancel-more-info, close, edit-ticket-info, force-close, give-up, modify-attachments, pass, reassign, request-more-info, reopen, resolve, supply-more-info, take-over) | same | AJAX form from `load-activity`; success = HTTP 200 with an empty body, validation failure = HTTP 200 with the form partial |
| Files | `/file/upload`, `/file/delete`, `/file/{id}/{fileName}`, `/file/get-attachments-info` | same | Dropzone uploads into a "pending" area keyed by a `tempId` GUID |
| Search, autocomplete | `/search`, `/search/{term}`, `/auto-complete/tag-list` | same | |
| Admin | `/admin/users…`, `/admin/user/{id}`, `/admin/new`, `/admin/projects`, `/admin/project/…`, `/admin/application-settings`, `/admin/push-notification-settings…`, `/admin/search-manager…`, `/admin/data-management…` | `TdAdministrators` | |
| Setup | `/first-run-setup…` | anonymous while `SetupEnabled=true` | |
| Navigation | `/navigation/change-project/{id}` (+ child action `ProjectMenu`) | **no `[Authorize]`** | |

Counts from `routes.csv`: 33 POST endpoints — 19 validate the antiforgery token, 8 render a token but do not validate it
(`[ValidateAntiForgeryToken]` missing: account change-password/edit-profile, application settings, project new/edit, push
settings, user edit, first-run create-database), 6 neither render nor validate one (file upload/delete, project delete, user
delete, configure provider, change-ticket-subscription). 17 GET/ANY actions change state (culture cookie, list filter/sort,
demo data wipe, index remove/rebuild, setup steps, sign-out).

AJAX callers (all jQuery): `edit-ticket.js` (`$.get` for the 4 ticket panels, activity form and buttons; `$.ajax` POST for the
subscription toggle), `ticket-file-uploader.js` (Dropzone POST, `$.get` attachment info, `$.ajax` delete), `ticket-tags.js`
(select2 → tag-list JSON), `jquery.unobtrusive-ajax` for the activity forms, the filter form, column-sort links and both pagers.
Pages call `$.ajaxSetup({ cache: false })`, so every AJAX GET carries a `_=<timestamp>` cache-buster.

Quirks a characterization test will capture (fix or keep is a decision, see §13):
- `GET /file/get-attachments-info?tempId=…` throws once a pending file exists: it builds URLs with `Url.RouteUrl("GetPendingFile")`, a route name that does not exist.
- `POST /admin/project/delete/{id}` always redirects to the edit page (missing `return` before `RedirectToAction("Index")`); the edit page then bounces to the list because the project is gone.
- `POST /ticket-activity/resolve` re-renders a failed form as `ReOpen`.
- Editing the tag list (`TdDomainContext.PrePopulateModifiedTicket`) uses an inverted condition: it re-adds tags that are already present and never adds new ones; tag rows only come right on ticket creation. **verify** with a recording.
- The SearchController class route is `[Route("{action=index")]` (missing brace); harmless because no action uses it.
- `/language` works only because `ActionLink("Index", "language")` renders `/language?name=…` through the conventional route, which the attribute route then matches.

## 4. Data model (EF6)

Three `DbContext`s share connection string `TicketDesk` and one database; `__MigrationHistory` separates them by `ContextKey`:

| Context | ContextKey | Migrations | Tables |
|---|---|---|---|
| `TdDomainContext` | `TicketDesk.Domain.Migrations.Configuration` (default) | 23 code-based | `Tickets`, `TicketEvents`, `TicketEventNotifications`, `TicketSubscribers`, `TicketTags`, `Projects`, `UserSettings`, `ApplicationSettings` |
| `TdIdentityContext` | `TicketDeskIdentity` | automatic, data loss allowed | `IdentityUsers`, `IdentityRoles`, `IdentityUserRoles`, `IdentityUserClaims`, `IdentityUserLogins` |
| `TdPushNotificationContext` | `TicketDeskPushNotifications` | automatic, data loss allowed | `notifications.PushNotificationItems`, `notifications.TicketPushNotificationItems`, `notifications.PushNotificationDestinations`, `notifications.ApplicationPushNotificationSettings`, `notifications.SubscriberPushNotificationSettings` |

User references are **strings without foreign keys** (`Tickets.Owner/AssignedTo/CreatedBy/LastUpdateBy/CurrentStatusSetBy`,
`TicketEvents.EventBy`, `TicketSubscribers.SubscriberId`, `UserSettings.UserId` hold `IdentityUsers.Id` GUID strings).

### 4.1 Final domain schema (model snapshot of `Td2.5.022`)

| Table | Key | Columns (type, null) | Relationships / indexes |
|---|---|---|---|
| `Projects` | `ProjectId` identity | `ProjectName` nvarchar(100), `ProjectDescription` nvarchar(500) null, `Version` rowversion | row `Default` inserted by 017 |
| `Tickets` | `TicketId` identity | `ProjectId` int (default 1), `TicketType` nvarchar(50), `Category` nvarchar(50), `Title` nvarchar(500), `Details` nvarchar(max), `IsHtml` bit, `TagList` nvarchar(100) null, `CreatedBy` nvarchar(256), `CreatedDate` datetimeoffset (default `SYSDATETIMEOFFSET()`), `TargetDate`/`ResolutionDate`/`DueDate` datetimeoffset null, `EstimatedDuration`/`ActualDuration` decimal(18,2) null, `Owner` nvarchar(256), `AssignedTo` nvarchar(256) null, `TicketStatus` int (0 Active, 1 MoreInfo, 2 Resolved, 3 Closed), `CurrentStatusDate` datetimeoffset, `CurrentStatusSetBy` nvarchar(256), `LastUpdateBy` nvarchar(256), `LastUpdateDate` datetimeoffset, `Priority` nvarchar(25) null, `AffectsCustomer` bit, `Version` rowversion | FK → Projects (`IX_ProjectId`) |
| `TicketEvents` | (`TicketId`, `EventId` identity) | `EventDescription` nvarchar(500) null, `Comment` nvarchar(max) null, `IsHtml` bit, `EventBy` nvarchar(256), `EventDate` datetimeoffset (default `SYSDATETIMEOFFSET()`), `ForActivity` int (flags enum `TicketActivity`, default 16384), `Version` rowversion | FK → Tickets cascade |
| `TicketSubscribers` | (`TicketId`, `SubscriberId` nvarchar(256)) | — | FK → Tickets cascade |
| `TicketEventNotifications` | (`TicketId`, `EventId`, `SubscriberId`) | `IsNew` bit, `IsRead` bit, `Version` rowversion | FK → TicketEvents cascade; FK → TicketSubscribers (no cascade); 4 indexes |
| `TicketTags` | `TicketTagId` identity | `TagName` nvarchar(100), `TicketId` int | FK → Tickets cascade |
| `UserSettings` | `UserId` nvarchar(256) | `ListSettingsJson` nvarchar(max) null, `SelectedProjectId` int null | — |
| `ApplicationSettings` | `ApplicationName` nvarchar(128) (`TicketDesk`) | `PermissionsSettingsJson`, `SelectListSettingsJson`, `SecuritySettingsJson`, `ClientSettingsJson` nvarchar(max) null | — |

Model/database drift: the snapshot declares `Ticket → Project` with cascade delete, but `Td2.5.017` created the FK with
`cascadeDelete: false` (EF6 never reconciles it). **verify** in the real database.

### 4.2 Migration history

| Migration | Change |
|---|---|
| `201410100421345_InitialCreate` | TD 2.5 baseline: `Settings` (key/value, seeded with lists and flags), `Tickets`, `TicketComments`, `TicketAttachments` (file bytes in the DB), `TicketEventNotifications` (e-mail queue columns), `TicketTags` |
| `Td2.5.001` | `UserSettings` (`ListSettingsJson`) |
| `Td2.5.002` | `TicketComments` → `TicketEvents`; `CommentId` → `EventId`, `CommentEvent` → `EventDescription`, `CommentedBy` → `EventBy`, `CommentedDate` → `EventDate` |
| `Td2.5.003` | `ApplicationSettings` with JSON columns, seeded from the old lists |
| `Td2.5.004` | drop `Settings` |
| `Td2.5.005` | drop the old notification queue table; temporary `TicketEvents.NotificationsSent` |
| `Td2.5.006` | user-name columns widened to nvarchar(256); `UserSettings` PK rebuilt |
| `Td2.5.007` | `TicketSubscribers`; **drop `TicketAttachments`** (files move out of the DB) |
| `Td2.5.008` | subscriber key → `SubscriberUserName`; drop `NotificationsSent` |
| `Td2.5.009` | new `TicketEventNotifications` (per subscriber, `IsNew/IsRead/PushNotificationPending`) |
| `Td2.5.010` | user names → user ids: `SubscriberUserName` → `SubscriberId` |
| `Td2.5.011` | `PushNotificationSettingsJson` on ApplicationSettings and UserSettings |
| `Td2.5.012` | drop `PushNotificationPending` |
| `Td2.5.013` | drop both `PushNotificationSettingsJson` columns (moved to the notifications context) |
| `Td2.5.014` | `ntext` → nvarchar(max) for `TicketEvents.Comment`, `Tickets.Details` |
| `Td2.5.015` | `ApplicationSettings.SecuritySettingsJson` |
| `Td2.5.016` | `ApplicationSettings.ClientSettingsJson` |
| `Td2.5.017` | `Projects` + `Default` row; `Tickets.ProjectId` (default 1, FK, index); rowversion on notifications |
| `Td2.5.018` | `UserSettings.SelectedProjectId` |
| `Td2.5.019` | `TicketEvents.ForActivity` (default 16384 = EditTicketInfo for old rows) |
| `Td2.5.020` | `Tickets.DueDate` |
| `Td2.5.021` | `Tickets.EstimatedDuration`, `ActualDuration` decimal(18,2) |
| `Td2.5.022` | `Tickets.TargetDate`, `ResolutionDate` |

### 4.3 Settings stored in the database

- `ApplicationSettings` row `TicketDesk`: `Permissions` (`AllowInternalUsersToEditPriority`, `AllowInternalUsersToEditTags`),
  `SelectLists` (`CategoryList`, `PriorityList`, `TicketTypesList`), `SecuritySettings` (`DefaultNewUserRoles` — default
  `["TdPendingUsers"]`, `DefaultLogonDomain` for LDAP), `ClientSettings.Settings` dictionary (`DefaultTextEditorType`
  markdown/summernote, `DefaultSiteRootUrl` for e-mail links). Mapped as EF6 complex types with a `Serialized` JSON property.
- `notifications.ApplicationPushNotificationSettings.PushNotificationSettingsJson`: enabled flag, background queue, delivery
  interval, retries, anti-noise consolidation, broadcast mode, and per-provider configuration **including SMTP password /
  SendGrid key in plain text**.
- `UserSettings.ListSettingsJson`: each user's ticket-center lists (columns, filters, sort, page size). Created on first visit.
- `notifications.SubscriberPushNotificationSettings` / `PushNotificationDestinations`: per user (and the pseudo-subscriber
  `new ticket broadcast`) e-mail destinations.

### 4.4 EF6 behaviour the new data layer must reproduce

- `TdDomainContext.SaveChanges[Async]` override: for new tickets sets `CreatedBy`, `CreatedDate`, `TicketStatus=Active`,
  status fields, tags from `TagList`, the Create/CreateOnBehalfOf event, subscribers; for changed tickets sets
  `LastUpdateBy/Date`, status date/by when the status changed, tag diff, subscribers; creates one `TicketEventNotification`
  per subscriber for every new event; when a project is deleted resets users' `SelectedProjectId`; then raises the static
  events `TicketsCreated`, `TicketsChanged`, `NotificationsCreated` that drive search indexing and push notifications.
- `ValidateEntity` blocks ticket creation for users without the Create permission (security check inside EF).
- `CreatedDate` and `EventDate` are `DatabaseGenerated(Computed)`: EF6 never writes them, the column default does. In EF Core
  they must be "default on insert, never updated", not computed columns.
- Timestamps come from `DateTime.Now` (server local time) stored in `datetimeoffset` with the server's offset; the new app
  will run in UTC containers, so offsets will differ unless decided.
- `Version` rowversion columns are concurrency tokens on Projects, Tickets, TicketEvents, TicketEventNotifications.
- The ticket center builds **Entity SQL** strings (`ObjectQuery.Where("it.Col = @p")`, `OrderBy("it.Col DESC")`) from the
  user's saved list settings; the sort column name comes straight from the `columnName` query parameter of `sortList` and is
  persisted. EF Core has no Entity SQL: needs expression trees with a column whitelist (and the injection hole closes).
- Identity's role table is shared by `IdentityRole` and `TicketDeskRole` (TPH, `Discriminator` column) **verify**.

## 5. Identity, OWIN and passwords

- Cookie: `UseCookieAuthentication` with `AuthenticationType = ApplicationCookie` → cookie `.AspNet.ApplicationCookie`,
  `LoginPath=/user/sign-in`, defaults otherwise (14-day sliding expiration; persistent only with "remember me"),
  `SecurityStampValidator` every 30 min, `GivenName` claim = display name. No `machineKey` in `Web.config`: the ticket is
  protected with the IIS auto-generated machine key, so nothing outside this IIS app can read it today.
- Roles: `TdAdministrators`, `TdHelpDeskUsers`, `TdInternalUsers`, `TdPendingUsers` (display names/descriptions localized).
  Admin implies help desk implies internal (`TicketDeskUserManager.IsTd*`).
- `TicketDeskUserManager`: unique e-mail, any characters in user names, password ≥ 5 characters with no other rules,
  lockout enabled for new users, 5 failures → 5 minutes. "Disabled" users are encoded as `LockoutEndDateUtc = DateTime.MaxValue`.
- Password hash: Identity 2.2.1 default `PasswordHasher` = `Crypto.HashPassword`: base64 of `0x00 | 16-byte salt | 32-byte
  PBKDF2-HMAC-SHA1 (1000 iterations)` (68 characters). No custom hasher anywhere.
- TD 2.0 upgrade (`LegacySecurityMigrator`): copies `aspnet_Membership` users; clear-text passwords are re-hashed, others get no
  password (must reset). Dropped once migrated.
- Optional LDAP: when `DefaultLogonDomain` is set, sign-in falls back to an LDAP bind with `System.DirectoryServices`
  (Windows only).

What ASP.NET Core Identity needs for compatibility (Round 2):
- **Hashes**: `PasswordHasher<TUser>` verifies the v2 format and returns `SuccessRehashNeeded`. With the default (v3) mode
  it then rewrites the hash as PBKDF2-HMAC-SHA512 — which the legacy app cannot verify. While both apps share the database,
  set `PasswordHasherOptions.CompatibilityMode = IdentityV2`; switch to v3 after the legacy app is retired.
  `tools/Seed` tests prove both directions (`PasswordHashTests`).
- **Schema**: map to the existing table names; add `NormalizedUserName`, `NormalizedEmail` (backfill `UPPER()`),
  `ConcurrencyStamp`, `LockoutEnd` (datetimeoffset, from `LockoutEndDateUtc`; keep both during coexistence), role
  `NormalizedName`, `RoleClaims` and `UserTokens` tables — as expand-only changes the EF6 app ignores. Automatic migrations
  of `TdIdentityContext` are on with data loss allowed: the legacy app must not start with a model that differs from the
  expanded schema (**verify** how EF6 reacts to extra columns; it should ignore them).
- **Cookie**: the new app cannot read `.AspNet.ApplicationCookie`. Options: System.Web adapters remote authentication
  (new app asks the legacy app), or move the legacy app to `Microsoft.Owin.Security.Interop` with a shared Data Protection
  key ring. Security-stamp claim type is the same in both stacks.
- Reset/confirmation tokens (`DataProtectorTokenProvider` over the OWIN data protector) are not portable; in-flight tokens
  die at cutover.

## 6. Session, TempData, cache

- **No `Session` and no `TempData` use anywhere** (checked all `.cs`/`.cshtml`). Session state is default InProc and unused,
  so System.Web adapters *remote session* is not needed — only remote authentication.
- `MemoryCache.Default["user-info-cache"]`: id → display name/e-mail for all users, 15-minute absolute expiry. Two apps
  will show stale names for up to 15 minutes after an edit.
- No `OutputCache`. Process-wide statics: `TdSearchContext.Current`, `TicketDeskFileStore`, push-notification delivery
  providers, the `TdDomainContext` static events.
- Several admin actions call `HttpRuntime.UnloadAppDomain()` (push settings save, demo data) to reload configuration.

## 7. Background work, WebJobs and queues

- In-process: `HostingEnvironment.QueueBackgroundWorkItem` for search indexing, notification creation, new-user notification
  settings; `InProcessPushNotificationScheduler` (a `System.Threading.Timer` every `DeliveryIntervalMinutes`) when push
  notifications are enabled and the app is not an Azure Web App (`WEBSITE_SITE_NAME` unset).
- `TicketDesk.PushNotifications.Job`: WebJobs SDK 1.1.2 console job, same delivery loop on a timer (Azure deployments).
- `TicketDesk.SearchMonitor.Job`: `QueueTrigger("ticket-search-queue")` (Azure Storage queue) → Azure Search.
- Both jobs need `AzureWebJobsStorage`/`AzureStorage`; on-prem neither runs (legacy-env documents what is off).

## 8. Search

`TdSearchContext` uses Azure Search when its connection string/app settings are present, otherwise Lucene.Net 3.0.3 with
an `FSDirectory` under `App_Data/ticketdesk-searchindex` (RAM directory when there is no `DataDirectory`). Index item:
`Id, ProjectId, Title, Details, Status, LastUpdateDate, Tags[], Events[]` (event comments). Indexing happens after every
ticket save in a background work item; `/admin/search-manager/populate-index` rebuilds everything synchronously.
Results are filtered by the user's selected project. The Lucene index has a single-writer lock: two apps cannot write it.

## 9. E-mail

New ticket and ticket-event notifications: `TicketEventNotification` rows → background step builds
`TicketPushNotificationItem`s (consolidation window 2–10 min, exclude own events) → Postal renders
`Views/Emails/Ticket.{Html,Text}.cshtml` into an S22 `SerializableMailMessage`, serialized with **`BinaryFormatter`** into
`notifications.PushNotificationItems.MessageContent` → delivery loop sends via `SmtpClient` or SendGrid, with retries.
`BinaryFormatter` is removed from .NET 9+: queued items cannot be read by the new app (drain or re-render before cutover).
New-ticket broadcast goes to all help-desk staff or a custom address. Identity e-mail/SMS services are no-ops.

## 10. Localization

Resource classes per view/controller in `TicketDesk.Localization` (en-US neutral, pt-BR, de-DE). Culture comes from the
`_culture` cookie (set by `/language?name=…`, 1 year) via `LocalizationFilter`/`CultureHelper`, default from
`<globalization>` (en-US). Client side: per-culture bundles (`common_locale_*`, `select2_locale_*`, `summernote_locale_*`)
with Globalize/CLDR. Dates in forms (`DueDateAsString` etc.) are parsed with `DateTime.TryParse` in the request culture and
rendered with `ToShortDateString()`: the same input means different dates in en-US and pt-BR/de-DE.

## 11. File attachments

`TicketDeskFileStore`: Azure Blob when an `AzureStorage` connection string exists, otherwise local disk
`App_Data/filestore/ticketdesk-attachments/`. Committed files live in a folder per ticket id; pending uploads live in
`pending/` as `{fileName}.{tempId}` until the ticket is saved (`CommitPendingAttachments` moves/renames them, adding `(n)` on
name clashes). Downloads are always `application/octet-stream`. No file-type or size checks besides IIS limits
(default `maxRequestLength` 4 MB). No per-ticket authorization on download. Nothing in the database since `Td2.5.007`.
Coexistence needs one shared store (SMB share or Azure Files/Blob) for both apps.

## 12. Configuration

`appSettings`: `ticketdesk:SetupEnabled` (true), `ticketdesk:DemoModeEnabled` (false), `ticketdesk:ResetDemoDataOnStartup`
(false), `ticketdesk:RegisterEnabled` (false), `ticketdesk:HomeEnabled` (true), optional `ticketdesk:InsightsInstrumentationKey`;
connection strings `TicketDesk` (LocalDB `|DataDirectory|TicketDesk.mdf` by default) and optional `AzureStorage`;
`Web.azure.config`/`Web.Release.config` transforms; `PublishScripts/` for Azure Web Apps.

## 13. Security observations (characterize first, then decide)

| # | Finding | Evidence | Decision needed |
|---|---|---|---|
| S1 | `POST /first-run-setup/create-database` is anonymous while `SetupEnabled=true` (shipped default) and resets the password of any existing user name + grants `TdAdministrators` | `FirstRunSetupController.CreateDatabase`, `WhenSetupEnabledAttribute` checks only the app setting | legacy-env should set `SetupEnabled=false` after setup; new app drops the wizard |
| S2 | `/emailtest/index/{id}` renders any ticket anonymously | no `[Authorize]`, conventional route | drop in the new app (characterize as 200/500) |
| S3 | 14 POSTs without antiforgery validation, 17 state-changing GETs | §3 | new app validates all; replay diffs will show it (expected mismatch list) |
| S4 | `POST /user/register` ignores `RegisterEnabled` | `UserController.Register` | keep pending-role behaviour or block |
| S5 | Entity SQL built from the `columnName` query parameter and persisted per user | `UserSettingExtensions.ApplyToQuery` | whitelist columns in the new list query |
| S6 | SMTP password / SendGrid key in plain text in the database | push settings JSON | Key Vault in the new app |
| S7 | `/navigation/change-project/{id}` has no `[Authorize]` | `NavigationController` | require auth |
| S8 | Attachments downloadable by any internal user, file name from the URL | `FileController.GetFile` | per-ticket check, path normalization |
| S9 | `customErrors Off` / `debug=true` in the shipped config | `Web.config` | Release transform on the VM (legacy-env) |
| S10 | An Azure Storage account key (account `ticketdesk2store`) committed in plain text in `TicketDesk.SearchMonitor.Job/App.config` (upstream, public since 2015-06; found 2026-11-09) | `App.config` connection strings | left as upstream has it (already public, presumably revoked; the lab never uses it); the new app takes storage access from managed identity / Key Vault, and no connection string with a key is committed |
| S11 | User display names are rendered unencoded on the ticket page (`Html.Raw(string.Format(Index.AssignedTo/OwnedBy/UpdatedBy, ...DisplayName))`); users edit their own display name on the profile page (found 2026-11-09, r2b-tickets) | `Views/Ticket/_TicketDetails.cshtml` | the new app encodes them (expected-differences S11) |
| S13 | Any internal user can open any ticket by its number (`ticket/{id}`, its panels, `GET /api/tickets/{id}`), although only the tickets they own, created, are assigned to or subscribe to show their files (S8) (found 2026-11-10, r3-search-worker) | `TicketController` / `TicketQueries.GetAsync` check the role only, like the legacy `TdAuthorize` | **kept as legacy behaviour** (coordinator, 2026-11-10: the migration preserves behaviour), and the new search follows it too (the legacy search does not filter per ticket either); the tighter rule exists in the new search behind `Search:RestrictToOwnTickets` (off, measured with td-db V0012); follow-up after the migration: decide whether search and the ticket page get the S8 rule |

## 14. Ranked migration risks

| Rank | Risk | Why it is hard | Mitigation | Round |
|---|---|---|---|---|
| 1 | Shared sign-in during coexistence | OWIN cookie bound to the IIS machine key; ASP.NET Core cannot read it; sign-in POST must stay on one side | System.Web adapters remote auth first; move sign-in last; or Owin.Security.Interop + shared key ring | 2 |
| 2 | Password hash coexistence | default Core hasher upgrades v2 → v3 on login, which the legacy app cannot verify | `CompatibilityMode = IdentityV2` until retirement (tests in `tools/Seed`) | 2 |
| 3 | Antiforgery token formats differ | MVC 5 and Core tokens are not interchangeable; forms are loaded from one app and posted to the other (activity partials, new ticket) | migrate GET+POST pairs together (`load-activity` with all activity POSTs; `ticket/new` GET+POST) | 2 |
| 4 | EF6 → EF Core behaviour | SaveChanges side effects, static domain events, `ValidateEntity` permission check, computed defaults, local-time offsets, Entity SQL list queries, complex-type JSON settings, TPH roles, cascade drift | characterization replay of every ticket workflow; port SaveChanges logic to explicit domain services with tests | 2 |
| 5 | `BinaryFormatter` messages in the notification queue | unreadable on .NET 10 | drain queue before switching delivery; store rendered MIME/JSON instead | 2–3 |
| 6 | Schema changes while both apps share the DB | Identity needs new columns; EF6 automatic migrations with data loss allowed run at legacy startup | expand/contract only; disable legacy initializers (`SetInitializer(null)`) on the VM; test legacy start against the expanded schema | 2 |
| 7 | Background work tied to IIS | `QueueBackgroundWorkItem`, in-process timer, AppDomain unloads for config reload | Worker Service; config reload via options; make delivery single-instance | 3 |
| 8 | Lucene index and local attachment folders | single-writer index; files on the VM disk | SQL full-text (plan) and one shared file store before the first write route moves | 2–3 |
| 9 | Security debt (§13) | fixing it changes responses | keep an "intended differences" list next to the replay report | 2 |
| 10 | Localization and dates | three cultures, culture-dependent parsing/formatting, resx per view | reuse resx via `IStringLocalizer`; replay in en-US and pt-BR | 3 |
| 11 | Razor + jQuery partial protocol | panels/partials expect exact HTML fragments and "empty body = success" | keep the protocol for migrated server-rendered routes; Angular pages later replace whole pages | 2–3 |
| 12 | LDAP sign-in (`System.DirectoryServices`) | Windows-only, not available in Linux containers | replaced by Entra ID OIDC (plan) | 2 |

## 15. Proposed route migration order

Waves are the `wave` column of `routes.csv`. Each wave moves only when the replay suite shows no unexpected mismatches for it.

| Wave | Routes | Content | Prerequisite |
|---|---|---|---|
| 0 | 2 | static bundles/content behind the gateway (YARP passes everything else through) | gateway |
| 1 | 4 | anonymous read-only: home, about, access-denied | layout parity |
| 2 | 29 | authenticated **read-only**: ticket page and its 4 panels, ticket center list + paging, search, tag autocomplete, file download/info, account and admin GET pages, child menus | remote authentication; EF Core read model; shared file store (reads) |
| 3 | 6 | small per-user writes: list filter/sort/reset, change project, subscription toggle, culture | wave 2 list code; antiforgery on new endpoints |
| 4 | 20 | ticket writes: new ticket (GET+POST), `load-activity` + all 15 activity POSTs, file upload/delete, attachments | EF Core write path reproducing SaveChanges side effects; notifications + indexing hooks |
| 5 | 19 | identity and admin writes: sign-in/out, register, password/profile, user/project/settings admin, push settings, search manager | hash compatibility mode; settings in Key Vault; cookie issuance moves to the new app |
| retire | 9 | first-run setup (5 rows), demo data (2), email test, TD 2.0 upgrade | replaced by `td-seed`, migrations and ops scripts |

Within a wave, order by risk (`L` before `M` before `H`).

## 16. To verify against the real legacy app / database

When `DB_READY` / `APP_READY` arrive (see the handoff for what was done): TPH `Discriminator` and nullability on
`IdentityRoles`/`IdentityUsers.DisplayName`; the Ticket→Project FK cascade setting; constraint names after the
`TicketComments` rename; the `notifications.*` schema; `__MigrationHistory` context keys; the tag-edit bug; the
`get-attachments-info` 500; anonymous `/emailtest/index/{id}`; register-while-disabled; what a fresh first-run setup inserts.
