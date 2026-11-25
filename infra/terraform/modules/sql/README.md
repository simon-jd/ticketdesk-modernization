# sql module

Azure SQL logical server plus one database on the **Azure SQL Database free offer**, private only.

- **Server**: Entra-only authentication (`azuread_authentication_only = true`), administrator is the signed-in
  user; no SQL login or password exists. TLS 1.2 minimum. `public_network_access_enabled = false`.
- **Database**: General Purpose serverless Gen5, 2 vCores max, 0.5 min, 32 GB, local backup redundancy,
  `useFreeLimit = true` and `freeLimitExhaustionBehavior = AutoPause`: when the monthly free allowance
  (100,000 vCore-seconds, 32 GB data, 32 GB backup per database) is used up, the database pauses until the
  next month instead of billing. Created with `azapi_resource` because azurerm 5.x has no free-offer arguments.
- **Private endpoint** in the `snet-pe` subnet (`sqlServer` sub-resource) and the
  `privatelink.database.windows.net` private DNS zone linked to the VNet, so
  `<server>.database.windows.net` resolves to the private IP inside the VNet. From outside the VNet the
  server is unreachable (public access off) — connect from the VM, from AKS, or not at all.

## Auto-pause and open connections

Serverless auto-pause (after `autoPauseDelay` = 60 minutes idle) only happens when **no session is open**.
An open connection — an app's idle connection pool, a forgotten `sqlcmd`/SSMS window, a health check that polls
the database — keeps the database online and keeps consuming the free vCore-seconds. Once the free allowance is
gone the database pauses for the rest of the month (it does not bill), which then breaks the app. So: close
tools when done, keep health checks off the database or infrequent, and set `Min Pool Size=0` /
`Connection Lifetime` in the app's connection string.

## Cost

Free DB: $0 within the allowance; with `AutoPause` it never bills. Private endpoint $0.01/h plus $0.01/GB
processed; private DNS zone $0.50 per zone-month. Unit prices and their source are in
`docs/dev/azure-spend.md`.

## Connection string

`Server=tcp:<server>.database.windows.net,1433;Database=sqldb-ticketdesk;Authentication=Active Directory Default;Encrypt=True`
(output `connection_string`; no secret inside — Entra authentication with a managed identity or developer sign-in).
Users for managed identities are created in T-SQL (`CREATE USER [<identity>] FROM EXTERNAL PROVIDER`) by the
Entra admin from inside the VNet.
