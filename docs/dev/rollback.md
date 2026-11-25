# Rollback after the legacy app is retired

Until Round 3 a rollback was a registry edit: setting a route to `Legacy` in `migration-registry.json` sent it back to the
legacy app behind the YARP fallback within a second (measured in Round 2: 347 ms, and 149 ms back). Round 4 (`r4-retire`)
removed the fallback, remote authentication and the `Legacy` mode, so there is no older app behind the new one any more.
What rollback means now:

| What went wrong | Rollback | Time | Notes |
|---|---|---|---|
| An Angular page misbehaves | registry edit: the page's id to `New` (its server page), no redeploy | seconds (file watch) | the only switch left in `migration-registry.json`; `Legacy` is ignored with a warning |
| A release of the new app is bad | redeploy the **previous image / commit** of the new app (`kubectl rollout undo deployment/<web>` on AKS, or the previous image tag locally) | one rollout | the previous image must run on the current schema: see below |
| The schema step of a release is bad | restore the database (point-in-time restore on Azure SQL; the restore drill is measured by the `aks` round) | restore time | `td-db` scripts are forward-only; a contract script has no "down" |
| The whole modern stack must go | not a rollback any more: bringing the legacy app back is a project (below) | hours | last resort |

## The previous image on the current schema

- An image from before `r4-retire` (the gateway build, "v1") runs standalone with `Legacy__Enabled=false` (that is how
  `ops/dev/e2e.sh --standalone` ran it in Round 3); its gateway and remote-authentication settings are simply off.
- Sign-in: such an image issues and reads the shared cookie (`.AspNet.ApplicationCookie`) when configured for it, the
  current one only `.AspNetCore.Identity.Application` (I1): each switch between them signs users out once. Passwords keep
  working both ways: the current image writes Identity V3 hashes and rehashes V2 ones at sign-in (I2); an older image in V2
  compatibility mode still verifies V3 hashes (ASP.NET Core's hasher reads both formats, `PasswordCompatibilityTests`) and
  writes V2 again for passwords changed while it runs.

- Schema: since `r4-retire` the database is contracted (td-db C0001: no AssignedDate trigger, `CK_Tickets_AssignedDate`).
  Older images of the new app still work on it: they declare the trigger to EF Core (`HasTrigger`), which only makes EF Core
  read generated values with a separate SELECT instead of `OUTPUT` (fine without a trigger), and they set `AssignedDate`
  themselves since wave 4, so the constraint holds. The current image does **not** run on an expand-only database (error
  334 on ticket updates): a fresh environment runs `td-db migrate --contract --legacy-retired` before the first rollout.

## Bringing the legacy app back (last resort)

Not a rollback but a re-deployment, in this order: (1) undo the contract on the database (drop `CK_Tickets_AssignedDate`,
create the AssignedDate trigger again: `docs/sql/schema-changes.md`); (2) deploy the legacy build (local lab VM: snapshot
`baseline-s-search`; Azure: the lift stage of `ops/azure/`); (3) deploy an image of the new app from before `r4-retire`
with its gateway, remote authentication or shared cookie settings; (4) reset the passwords of users whose hash is V3 (the
legacy app verifies V2 only; V3 hashes are the ones whose base64 text starts with `AQAAAA`, format byte 0x01). Data written by the new app since retirement stays (same tables), but the legacy app's search
index (Lucene) would have to be rebuilt and its push items are not written by the new app (N1).

## Tools that belong to the coexistence rounds

These scripts drive the gateway, the legacy app behind it or both apps at once. They stay in the repository as the record
of Rounds 1–3 and work only with a checkout from before `r4-retire` (for example `modernize` @ `c702afb`):
`ops/dev/replay-gateway.sh` (gateway replays; `--serve` now starts the new app alone and ignores the `Legacy__*`
settings), `ops/dev/replay-candidate.sh`, `ops/dev/shared-signin-probe.py`, `ops/dev/remote-auth-probe.cs`,
`ops/dev/entra-user-check.sh`, `ops/dev/container-smoke.sh`, `ops/dev/notification-coexistence.sh`,
`ops/dev/frontend-after.sh`. The legacy app itself (`TicketDesk/`, the lab VM snapshots, `ops/onprem/`, `ops/windows/`)
is untouched and stays the "before" reference.
