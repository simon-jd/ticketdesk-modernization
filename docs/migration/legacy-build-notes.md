# Legacy TicketDesk 2.5: build and run notes (Round 0)

The legacy app runs unmodified apart from one project-file repair. Everything else is configured at
deploy time by `ops/windows/*.ps1`; the repository's `Web.config` is unchanged.

## Environment the legacy app was built and run in

| Piece | Version / setting | Where it comes from |
|---|---|---|
| OS | Windows Server 2025 Standard Evaluation, Server Core, build 26100.1742 | eval ISO, `ops/onprem/vm/` (unattended) |
| .NET Framework runtime | 4.8.1 (release key 533320); the app targets 4.6.2 | in-box |
| IIS | IIS 10, ASP.NET 4.x (Web-Asp-Net45), integrated pipeline | `Install-TdPrerequisites.ps1` |
| Database | SQL Server 2022 Express 16.0.1000.6 (RTM), instance `SQLEXPRESS`, TCP 1433, mixed auth | `Install-TdSqlExpress.ps1` |
| Build tools | Visual Studio 2022 Build Tools, MSBuild 17.14.60, web build workload, .NET Framework 4.6.2 targeting pack, 4.8 SDK | `Install-TdBuildTools.ps1` |
| Packages | nuget.exe 7.9.0 restoring `packages.config` (84 packages for the 12 projects in `TicketDesk-2.5.sln`) from nuget.org | `Build-TdLegacy.ps1` |

Build result at source revision `f510384`: 0 errors, 4 warnings (2x CS0618 `MailMessage.ReplyTo` is
obsolete in S22.Mail, 2x MSB3245 described below); file-system publish of `TicketDesk.Web.Client`
gives 840 files. Raw logs: `docs/evidence/legacy-baseline-env-20261108/msbuild/`.

## Source changes (in the repository)

| # | File | Change | Why |
|---|---|---|---|
| 1 | `TicketDesk/TicketDesk.PushNotifications.Job/TicketDesk.PushNotifications.Job.csproj` | Removed an unclosed `<Reference>` to WindowsAzure.Storage 8.5.0 and pointed the two WebJobs references back at the 1.1.2 packages (commit `61c4fe6`). | Upstream commit `40b8acb` (2018-01-29) left the XML malformed (`MSB4025: The 'Reference' start tag ... does not match the end tag of 'ItemGroup'`), so neither nuget restore nor MSBuild could load the project and the solution did not build. `packages.config` of that project still pins WebJobs 1.1.2 and Storage 8.2.0; the references now match what is restored. |

That is the only code change. Nothing else needed fixing:

- **Package feeds**: every `packages.config` entry (94 distinct id/version pairs across all project folders,
  including the two folders outside the solution) exists on nuget.org (checked 2026-11-07 against the
  v3 flat container); no dead feed, no `NuGet.config`.
- **MSB3245 warnings** (left as they are): `TicketDesk.IO.csproj` references `Microsoft.Azure.WebJobs`
  and `Microsoft.Azure.WebJobs.Host` 2.0.0 (same half-applied upgrade in `40b8acb`), but no project's
  `packages.config` restores those versions. TicketDesk.IO does not use the types, so the build
  succeeds; the references simply do not resolve.
- `TicketDesk.Search` and `TicketDesk.SearchMonitor.Job` (net452) are not in the solution and are not built.

## Deploy-time configuration (not in the repository)

`Deploy-TdApp.ps1` mirrors the publish output into `C:\inetpub\ticketdesk` (App_Data is kept) and then
sets, in the deployed `Web.config` only:

| Setting | Repository value | Deployed value | Why |
|---|---|---|---|
| `connectionStrings/TicketDesk` | `(LocalDb)\MSSQLLocalDB` with `AttachDBFilename=|DataDirectory|TicketDesk.mdf` | `Data Source=.\SQLEXPRESS;Initial Catalog=TicketDesk;Integrated Security=True;MultipleActiveResultSets=True` | LocalDB is a developer feature and does not run under an IIS app pool identity; the on-prem host uses SQL Server Express. Integrated security with `IIS AppPool\TicketDesk` (db_owner of the TicketDesk database only, created by `Set-TdIisSite.ps1`). |
| `system.web/compilation/@debug` | `true` | removed by the repo's own `Web.Release.config` transform during publish | standard Release publish |
| `appSettings/ticketdesk:SetupEnabled` | `true` | `true` until first-run setup has created the database, then `false` (`Deploy-TdApp.ps1 -SetupMode Auto`, the default) | Upstream's own comment says to disable it after setup; while it is on, the anonymous `/first-run-setup/create-database` POST stays reachable (inventory finding S1). Disabled, the wizard's routes redirect to home (`setup-disabled.txt`). |

The other app settings stay at the repository defaults:
`DemoModeEnabled=false`, `ResetDemoDataOnStartup=false`, `RegisterEnabled=false`, `HomeEnabled=true`.
The first-run wizard was completed (database via the EF6 migrations, admin user), and the demo data was
then created through TicketDesk's own admin page (`/admin/data-management/create-demo-data`).

## Azure-dependent components: what is off or local

| Component | State on the on-prem host | How it is decided in code |
|---|---|---|
| Azure Search | off; **Lucene.Net on disk** in `App_Data\ticketdesk-searchindex` | `Startup.Search.cs`: `AzureIndexProvider.IsConfigured` is false without `Azure.Search.ServiceName`/`ApiKey` app settings, so the Lucene provider is used |
| Azure Storage (blobs) | off; attachments go to `App_Data\filestore` (created on the first upload) | `TicketDeskFileStorage` uses `DataDirectory\filestore` when no `AzureStorage` connection string/app setting exists |
| Azure Storage queues | off | `AzureQueueProvider.IsConfigured` is false without `AzureStorage` |
| WebJobs (`TicketDesk.PushNotifications.Job`) | not running. The WebJobs publish targets copy it to `App_Data\jobs\continuous\TicketDesk-PushNotifications-Job`; nothing on IIS starts it | Only Azure App Service runs WebJobs |
| Push notifications (email) | disabled (`notifications.ApplicationPushNotificationSettings`: `IsEnabled=false`, no delivery providers, no SMTP) | default settings; when enabled outside App Service, the in-process scheduler (`InProcessPushNotificationScheduler`) would deliver them |
| Application Insights | telemetry disabled | no `ticketdesk:InsightsInstrumentationKey` |
| AD/LDAP sign-in fallback | not configured | `dbo.ApplicationSettings.SecuritySettingsJson`: `"DefaultLogonDomain":null` |

## Legacy behaviour found while bringing it up (relevant for the migration)

- **First-run mode sticks if SQL is late.** `DatabaseConfig.RegisterDatabase()` checks the database once
  when the app domain starts; if SQL Server is not reachable at that moment, it installs `DbSetupFilter`
  and every request is redirected to `/first-run-setup` until the app pool recycles (seen right after a
  VM reboot while the pool was AlwaysRunning). The host scripts work around it (W3SVC depends on the
  local SQL service, pool starts on demand); the modern app should not copy this behaviour.
- **Two of the three EF6 contexts use automatic migrations.** After first-run setup
  `__MigrationHistory` holds 23 code-based migrations for `TicketDeskCore` and one
  `<timestamp>_AutomaticMigration` row each for `TicketDeskIdentity` and `TicketDeskPushNotifications`,
  stamped with the time setup ran (so the history differs between installs).
- **The `notifications` schema is owned by the app pool user** (EF created it while connected as
  `IIS AppPool\TicketDesk`). `ops/onprem/export-schema.sh` rewrites that to `dbo` in the exported script.
- **SQL Express defaults**: the database has `AUTO_CLOSE ON`, recovery model SIMPLE, compatibility level 160.
- **Antiforgery and form posts**: unchecked checkboxes must be omitted (not sent empty) or model binding
  fails with only a generic "Unable to create ticket" message (no field error is rendered for
  `AffectsCustomer`); the smoke script mimics a browser here.

## Host-side issues solved in the scripts (not app changes)

- SQL Server 2022 setup fails over an ssh key-auth or WinRM session with
  `There was an error generating the XML document` / `CryptographicException: Access is denied`
  (setup protects `/SAPWD` with DPAPI, which has no user credentials in a network logon).
  `Install-TdSqlExpress.ps1` runs setup as SYSTEM through a one-shot scheduled task
  (`Invoke-TdProcessAsSystem` in `TdOps.psm1`). Azure Run Command runs as SYSTEM anyway.
- Windows PowerShell 5.1 leaves `$PSScriptRoot` empty in `param()` defaults; scripts resolve it in the body.
- The in-box `OpenSSH-Server-In-TCP` firewall rule only covers the Private profile; the lab NIC came up
  as Public (handled in `ops/onprem/vm/firstboot.ps1`).

## Round 2 (legacy-adapters): net48, System.Web adapters, shared schema

| # | Change | Why / result |
|---|---|---|
| 2 | `TargetFrameworkVersion` v4.6.2 → v4.8 in the 12 projects of `TicketDesk-2.5.sln`, App.config `supportedRuntime` sku, Web.config `compilation` and `httpRuntime targetFramework="4.8"` (commit `887e4e8`) | the adapters' FrameworkServices package needs net472+. `httpRuntime` had no `targetFramework`, so ASP.NET ran in 4.0 compatibility mode; 4.8 switches on the 4.5+ behaviours (task-friendly synchronization context, 4.5 request validation). No package version changed, the 4.8 targeting pack was already on the build host (`Install-TdBuildTools.ps1` now requires it). Build: 0 errors, the same 4 warnings. Golden: 262/262 direct and through the gateway. `TicketDesk.Search` and `TicketDesk.SearchMonitor.Job` (net452, not in the solution) are left as they are |
| 3 | `Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices` 2.3.0 in `TicketDesk.Web.Client` (commit `35d7b5e`): 50 packages in `packages.config` (the net48 closure, computed with an SDK-style probe project), HintPath references, 10 binding redirects, `SystemWebAdapterModule`, `netstandard` in `compilation/assemblies` | packages.config has no transitive restore, so every dependency is listed. Changed versions: System.Diagnostics.DiagnosticSource 4.4.0 → 8.0.1 and System.ValueTuple 4.4.0 → 4.5.0 (required minimums of the closure). `ops/dev/check-binding-redirects.cs` checks the published `bin` against Web.config: 21 redirects needed, 0 missing (it also lists 7 stale upstream redirects that no reference uses). Same version as the new app's CoreServices 2.3.0 |
| 4 | `Global.asax` (new; the app had only an OWIN Startup) → `App_Start/SystemWebAdaptersConfig.cs`: `HttpApplicationHost.RegisterHost` with `AddProxySupport(UseForwardedHeaders)` and, when `appSettings ticketdesk:RemoteAppApiKey` is a GUID, `AddRemoteAppServer` + `AddAuthenticationServer`; logging to the event log only | the adapters' host must exist before IIS initializes the modules, i.e. in `Application_Start`. No remote session: no Session/TempData anywhere (inventory §6), so no session endpoint and no PUT handler change (the package's install transform re-registers `ExtensionlessUrlHandler` for all verbs; not applied). Remote auth works through OWIN: the module sets the handler at PostMapRequestHandler, OWIN's cookie middleware authenticates at PreHandlerExecute, a 401 becomes OWIN's sign-in redirect |
| 5 | `Infrastructure/ForwardedForGuardModule.cs`: X-Forwarded-For is removed before the adapters' proxy module runs (opt out: `ticketdesk:RemoteAppTrustForwardedFor=true`) | the proxy module copies X-Forwarded-For into REMOTE_ADDR unchecked; `Request.IsLocal` then turns `customErrors RemoteOnly` (S9) into full error pages: measured 268 → 8,717 bytes with one header. The gateway on the same host forwards `127.0.0.1` |
| 6 | Deploy step: `Deploy-TdApp.ps1` writes `ticketdesk:RemoteAppApiKey` from `TD_REMOTEAPP_API_KEY` and `connectionStrings/AzureStorage` from `TD_AZURE_STORAGE_CONNECTION` (environment only, not logged); keeps Application Insights' correlation headers off the blob endpoint | secrets never in the repo (`.secrets/legacy-remoteapp.env`, `.secrets/legacy-filestore.env`; Key Vault on Azure). The headers (`x-ms-request-id`, `x-ms-request-root-id`) are added after the storage SDK signed a request and break SharedKey auth (403); `ApplicationInsights.config` excludes only `*.core.windows.net`, localhost and 127.0.0.1 |
| 7 | Expand schema on the VM (`td-db migrate`), `baseline-s` re-saved as the shared state; round-1 state = snapshot `baseline-s-legacy` | `ops/onprem/baseline/baseline-s.sh upgrade-shared`; see `docs/sql/schema-changes.md` |
| 8 | `TicketDesk.IO/FileStorage/TicketDeskBlobStorageProvider.cs` (commit `b4edcc3`): the Blob file store delegates to BlobFileSystem.Azure's `AzureBlobStorageProvider` except `RenameFile`/`RenameFolder`, which use WindowsAzure.Storage 8.2.0 (`StartCopy`, wait for the copy, delete the source); `TicketDeskFileStore` uses it when an `AzureStorage` connection string exists | option A (DECISIONS.md). Disk mode unchanged (golden on disk 261/262 = D1 only, before the re-record). Tested with `ops/windows/Test-TdBlobStore.ps1` on the VM (12/12) and the golden with the blob store (0 journey errors) |
| 9 | Attachments move with `ops/windows/Move-TdFileStore.ps1` (lab driver `ops/onprem/filestore/move-filestore.sh`): idempotent copy App_Data → container `ticketdesk-attachments`, size + Content-MD5 before and after, CSV manifest; disk files untouched | cutover: run while the app is on disk, stop the pool, run again (delta), deploy with the connection string, start. Uses the WindowsAzure.Storage assembly in the site's bin, so it also runs on the Azure VM with a Key Vault connection string |
| 10 | `baseline-s` = shared state with the blob store, rebuilt end to end by `baseline-s.sh install` from installed-clean; `reset` empties Azurite (`--restart unless-stopped`, so it is back after a host reboot; `azurite.sh reset` starts it if not); golden re-recorded on it | `tests/characterization/golden/README.md` |

Packaging: `ops/onprem/legacy-artifact.sh build REV` builds once on the VM into `td-legacy-publish-<rev>.zip` (+ SHA-256; same naming as
`ops/azure/lift.sh`), `deploy ZIP` deploys a package with the deploy secrets. Note for drivers: `powershell -Command -` over ssh runs stdin line
by line and silently drops an unfinished multi-line block, and `vmctl.sh ssh` reads stdin (inside `bash -s` scripts give it `< /dev/null`).

Attachments in Azure Blob storage did not work with the legacy code as shipped: `BlobFileSystem.Azure` 0.0.7 (built against WindowsAzure.Storage 3.x) calls `CloudBlockBlob.StartCopyFromBlob`, which the shipped WindowsAzure.Storage 8.2.0 no longer has, so committing uploaded attachments threw `MissingMethodException` (Azurite and real storage alike). Change 8 fixes it.
