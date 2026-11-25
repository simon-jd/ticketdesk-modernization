# Moving the database to Azure SQL Database: path, tooling, local rehearsal

Source: SQL Server 2022 Express on the legacy Windows VM (database `TicketDesk`, the legacy app writing to it).
Target: the Azure SQL Database free offer (`sqldb-ticketdesk`, GP_S_Gen5_2 serverless, Entra-only, public access off,
private endpoint), created by Terraform's `replatform` stage. Round 2 sql-data chose the path, built the reconciliation
tool and rehearsed locally (sections below); **Round 2 azure-sql ran it on Azure** ("The Azure run", at the end).

## Options

| Path | Works from Express? | Downtime | Notes |
|---|---|---|---|
| **BACPAC: SqlPackage export → import** | yes | writes frozen for export + import + check | schema and data in one file; export validates Azure SQL compatibility; the import sets edition and service objective; Query Store, logins and SQL Agent jobs are not carried |
| Schema-only dacpac, then bulk copy per table (bcp / SqlBulkCopy, in parallel) | yes | same shape, can be faster for large tables | more moving parts; foreign keys, identity values and triggers need care during the load |
| Azure Database Migration Service / Azure SQL migration extension, offline mode | yes | same as BACPAC | a managed service and a self-hosted integration runtime for one small database |
| Transactional replication (online) | **no**: Express can only be a subscriber, not a publisher | minutes | would need the source upgraded first |
| Managed Instance link, log shipping | no: targets Azure SQL Managed Instance, not SQL Database | - | wrong target |

**Recommendation: BACPAC with a short write freeze.** The legacy database is small (the lab copy has 39 tickets;
even scale M, 100,000 tickets and 1.87 million rows in total, moved in 31 s locally), Express rules out the online
options, and one SqlPackage export/import pair is easy to rehearse, time and roll back. If a production-sized copy ever
makes the window too long, switch to schema dacpac + parallel bulk copy; the reconciliation step stays the same.

## Runbook

Before the window:
1. `td-db migrate` on the source (expand scripts and procedures in place; both apps keep working).
2. Terraform: logical server, database, private endpoint, firewall, Key Vault secret for the new connection string.
3. Dry run: `sqlpackage /Action:Export` against a copy; fix anything the Azure compatibility validation rejects.

In the window (what `ops/dev/move-rehearsal.sh` times):
1. **Freeze**: stop the legacy app pool, then `ALTER DATABASE [TicketDesk] SET READ_ONLY WITH ROLLBACK IMMEDIATE`.
2. **Export**: `sqlpackage /Action:Export /SourceServerName:... /TargetFile:ticketdesk.bacpac`.
3. **Import**: `sqlpackage /Action:Import /SourceFile:ticketdesk.bacpac /TargetServerName:<server>.database.windows.net
   /TargetDatabaseName:TicketDesk /p:DatabaseEdition=... /p:DatabaseServiceObjective=...` (Entra token or SQL login from
   Key Vault; credentials in a 0600 response file, never on the command line).
4. **Reconcile**: `TD_DB_SOURCE=... TD_DB_TARGET=... td-db reconcile --json reconcile.json`. Exit code 0 is the go
   signal; 1 stops the cutover.
5. **Re-apply what a BACPAC does not carry**: Query Store on with the same settings; the forced plan for
   `usp_TicketList`'s tag count (or its Query Store hint, `docs/sql/tuning.md`); database users for the app's managed
   identity.
6. **Switch**: point both apps' connection strings (Key Vault) at Azure SQL, start the legacy app pool, smoke tests.

Rollback: the source was only set read-only. `SET READ_WRITE`, connection strings back, start the pool.

## Reconciliation (`td-db reconcile`)

For every user table in either database: the table exists on both sides; the columns match by name, type, length,
precision, scale, nullability, identity and computed flags; the row counts match; and a content hash matches. The hash
is SHA-256 over the rows in primary-key order, each row reduced **on the server** to
`HASHBYTES('SHA2_256', CONCAT(<length>:<text>, '|', ...))` with an explicit, lossless text form per type
(`float` style 3, `datetimeoffset` style 121 so an offset change is a difference even for the same instant,
`varbinary` hex, NULL as a token distinct from the empty string), so only 32 bytes per row cross the network.
`rowversion` columns are excluded and listed (an import always assigns new values). Tables are compared four at a time.
Exit codes: 0 match, 1 mismatch, 2 error. The tests check that identical databases match even after rowversions
change, and that a changed title, a deleted tag row, a shifted offset, NULL vs empty and an extra column each make
exactly that table mismatch (`ReconcilerTests`).

## Local rehearsal (td-sql → td-sql)

`ops/dev/move-rehearsal.sh S|M`: fresh source at the scale (tools/Seed, seed 42) with the td-db migrations, then the
window as above with both databases in td-sql (SQL Server 2022 CU25), SqlPackage 170.5.96. Host load average 5.8 to
5.9 (32 cores, shared). Evidence: `docs/evidence/sql-move-20261107/` (`rehearsal-*.txt`, `timings-*.json`,
`reconcile-*.json`).

| Scale | Rows (all tables) | Freeze | Export | BACPAC size | Import | Reconcile | Window | Result |
|---|---|---|---|---|---|---|---|---|
| S (10,000 tickets) | 186,990 | 0.13 s | 5.14 s | 3.6 MB | 8.41 s | 1.22 s | **14.9 s** | 15 tables match |
| M (100,000 tickets) | 1,869,659 | 0.25 s | 7.32 s | 44.4 MB | 12.26 s | 11.34 s | **31.2 s** | 15 tables match |

What this does and does not predict for Azure: export time will be similar (it runs next to the source); the import
adds the upload and is bounded by the target's service objective (log rate), so it will be slower than a local import;
reconciliation streams 32 bytes per row from each side, so it grows with row count and network latency. The Azure round records the
real window; at the lab database's size it should be well under a minute.

## The Azure run (Round 2 azure-sql)

Driver `ops/azure/replatform.sh` (lab-host) and `ops/azure/Invoke-TdAzureProvision.ps1` (on the VM, through Run
Command); evidence `docs/evidence/azure-sql-20261109/` (cycle R1: SQL Express baseline, rehearsals) and
`docs/evidence/azure-sql-20261110/` (cycles R2/R3: rebuild, measured cutover, golden, latency, rotation).

### No passwords: who connects as whom

| Principal | What it does | How it authenticates |
|---|---|---|
| VM system-assigned identity `vm-tdlab-legacy` | the legacy app's database user: **contained user** `CREATE USER ... WITH SID = <client id>, TYPE = E`, `db_datareader` + `db_datawriter` only | EF6 interceptor `ManagedIdentitySqlTokenInterceptor` (TicketDesk.Domain) sets `SqlConnection.AccessToken` from IMDS (`https://database.windows.net/`); on when `ticketdesk:SqlAuthentication = ManagedIdentity` |
| `id-tdlab-sqldeploy` (user-assigned, on the VM) | the server's Entra admin: BACPAC import, contained user, `td-db migrate`, `td-db reconcile`, plan capture | `Authentication=Active Directory Managed Identity;User Id=<client id>` (SqlPackage, td-db with Microsoft.Data.SqlClient.Extensions.Azure), IMDS token by client id (PowerShell) |
| operator (lab-host) | Terraform, Key Vault, blob uploads | `az` sign-in; cannot reach the database at all (public access off) |

The server is reachable only through the private endpoint, so every administrative step runs on the VM inside the
VNet. The user is created by SID because a service-principal admin cannot resolve names in Entra (that would need
Directory Readers on the server's identity). In production the admin steps would run from a separate deployment agent
in the VNet that holds the deploy identity; on this lab VM any local process could ask IMDS for it.

Two legacy behaviours had to be handled for a contained user, both found on Azure, both fixed in code:
1. EF 6.1's `Database.Exists()` (startup check, migrations initializer) always probes `master`; a contained user cannot
   log in there and EF throws "credentials missing for master connection" (HTTP 500 at start). The interceptor opens
   such a probe against the app's own database, where `sys.databases` lists it too.
2. ASP.NET Identity 2's `IdentityDbContext(name, throwIfV1Schema: true)` checks for the Identity 1.0 schema with its own
   `SqlConnection`, outside EF6 interception, so without a token (HTTP 500 at sign-in). `TdIdentityContext` now passes
   `false`: the check looks for `AspNetUsers`, which TicketDesk never uses (`IdentityUsers`), so it never found anything.

Attachments: the legacy Blob SDK (WindowsAzure.Storage 8.2.0) needs a key or SAS and touches container-level APIs on
every path, so they live in their own account with shared keys allowed; the app gets an account SAS (blob only,
container + object, 8 days, re-issued weekly by Terraform) from Key Vault through the VM identity. The ops account
(artifacts, backups) stays Entra-only.

### Freeze writes without killing reads

`DENY INSERT, UPDATE, DELETE TO [IIS AppPool\TicketDesk]` on the source (permission checks apply at once; no connection
is killed, unlike `READ_ONLY ... ROLLBACK IMMEDIATE`, which cost 11 failed reads in the lift round), then wait until the
app has no open transaction. A write then fails with error 229 (HTTP 500 for the client); reads go on. Rollback is the
`REVOKE`. A BACPAC is for Azure SQL Database and cannot carry Windows users, and the source's Windows user owns the schema
EF created: the export runs from a `COPY_ONLY` copy (`TicketDeskExport`) whose schemas are given to `dbo` and whose
Windows users are dropped; the source is untouched.

### The window (one Run Command)

`azsql-wake` (first query, resumes a paused serverless database before the freeze) -> `freeze` -> `export` (copy +
SqlPackage) -> `import` (into the empty free-offer database only: SqlPackage would create a paid one otherwise) ->
`azsql-users` -> `reconcile` (`td-db reconcile`, exit 1 = no-go, rollback) -> `switch`. The switch is a config-only
deploy: one atomic `File.Replace` of Web.config (connection string to Azure SQL, `ticketdesk:SqlAuthentication`), so
ASP.NET starts a new app domain while IIS queues requests, and a warm-up. One Run Command keeps its ~30 s start-up out of
the window. The probe (`ops/azure/cutover_probe.py`, lab-host) reads `/` and `/tickets` and writes one comment per
interval on its own thread through the whole window.

### Results (cycle R3, 2026-11-10; RESULTS.md has every row)

| | Rehearsal 08:08Z | Measured 08:15Z |
|---|---|---|
| target before | online, GP_S_Gen5_2, free limit | same (`azsql-wake` 0.3 s) |
| freeze (DENY + no open app transaction) | 0.0 s | 0.0 s |
| copy without Windows users (COPY_ONLY backup + restore) | 8.0 s | 7.8 s |
| SqlPackage export (BACPAC) | 20.4 s | 21.1 s |
| import into Azure SQL | 37.2 s | 37.2 s |
| contained user / reconciliation | 0.2 s / 4.0 s, **20/20 MATCH** | 0.2 s / 4.0 s, **20/20 MATCH** |
| switch (Web.config replace + warm-up) | 5.4 s | 5.6 s |
| **write freeze** (DENY -> switched) | 75.4 s | **76.2 s** (probe: 77.5 s without a successful write) |
| probe reads failed | 0 of 190 | **0 of 172** |
| probe writes failed | 42 of 135 | 47 of 120 (all inside the window) |

After the switch: smoke 9/9; `td-db migrate` applied V0006 and the hinted `usp_TicketList` on Azure SQL; the tag count
runs in its parallel shape without any forced plan or Query Store hint; golden replay on Azure SQL 259/262 with only G6.
Latency (k6 mix, 5 ops/s, from lab-host, same VM): SQL Express p50 221.5 / p95 311.8 ms, Azure SQL p50 244.4 /
p95 323.5 ms, 0 errors. The free-offer database pauses after 60 minutes without connections; a paused database answered
error 40613 for at least 52 s on its first connection (R1), which is why the window starts with `azsql-wake`.

Where the time goes: the import (37 s for 3.6 MB) is bounded by the serverless target, the export by SqlPackage's
model build on a 2-vCPU VM; the freeze itself costs nothing. For a bigger database the same window would grow with
import time (schema + parallel bulk copy is the documented alternative above).
