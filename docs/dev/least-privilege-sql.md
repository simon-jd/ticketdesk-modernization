# The new app's database principal (least privilege)

The new app (TicketDesk.Web) no longer connects as `sa`. It is a member of one database role, `td_modern_app_role`
(`ops/db/td_modern_app_role.sql`): `SELECT` on the tables the modules read, `INSERT`/`UPDATE`/`DELETE` only where a
migrated route writes, `SELECT`+`INSERT` on the shared Data Protection key ring (`dbo.DataProtectionKeys`), `EXECUTE` on
the four td-db procedures. No DDL, no fixed database role, no server permission, no access to the two migration journals
(`__ModernSchemaHistory`, `__MigrationHistory`) or the notification queue tables. Schema changes stay with `td-db migrate`,
a separate privileged principal used at deploy time only.

## Lab VM (SQL Server Express, SQL logins)

```bash
ops/dev/td-db-app-login.sh apply   # login td_modern_app (password generated into $TD_W/.secrets/td-modern-app.env, 0600)
ops/dev/td-db-app-login.sh check   # 6 allowed operations succeed, 10 forbidden ones are refused
TD_MODERN_DB=vm TD_MODERN_LOGIN=app ops/dev/replay-gateway.sh ...   # the gateway runs as td_modern_app
```

A VM snapshot reset drops the login (it lives in `master`); `replay-gateway.sh` re-applies it after its reset when
`TD_MODERN_LOGIN=app`. The legacy app keeps its own principal (the IIS app-pool identity, integrated security).

## Azure SQL (managed identity, no password)

Azure SQL runs Entra-only authentication (r1-azure-foundation). The app's identity — the AKS workload identity (or the
VM's managed identity for a lift-and-shift run) — becomes a contained database user and a member of the same role:

```sql
-- as the server's Entra admin, in the TicketDesk database, after td-db migrate
:r ops/db/td_modern_app_role.sql
CREATE USER [id-ticketdesk-web] FROM EXTERNAL PROVIDER;        -- the managed identity's name
ALTER ROLE td_modern_app_role ADD MEMBER [id-ticketdesk-web];
```

The connection string then carries no secret: `Server=tcp:<server>.database.windows.net;Database=TicketDesk;
Authentication=Active Directory Default;Encrypt=True` (Microsoft.Data.SqlClient picks up the workload identity). The role
script travels with the repository, not with the BACPAC, so it is re-run after an import; `td_modern_app` (the SQL login)
is never created on Azure.

When a module starts writing another table, add the grant to `ops/db/td_modern_app_role.sql` with the route it serves;
the wave-4 grants (ticket writes) are already listed for the tickets round to confirm.
