# Real Entra ID sign-in, 2026-11-25

A real person signed in to the new app (TicketDesk.Web, `src/modern`) through Microsoft Entra ID with their own account,
in the operator's own Entra tenant. The first sign-in created the TicketDesk user and gave it its role from the Entra app
role. Before this, the Entra sign-in was proven only against the local OpenID Connect test provider
(`docs/evidence/identity-20261109/step5-entra-test-idp/`); the attempt in the university tenant on 2026-11-10 was stopped by
that tenant's consent policy (`docs/evidence/identity-20261110/step8-entra-user-check/`).

## Setup

- App registration `ticketdesk-lab-web`: single tenant (`AzureADMyOrg`), web redirect URI
  `https://localhost:5443/signin-oidc`, front-channel logout `https://localhost:5443/signout-oidc`, implicit grant off,
  four app roles named like the legacy roles (TdAdministrators, TdHelpDeskUsers, TdInternalUsers, TdPendingUsers), one
  client secret valid until 2026-12-24 (kept outside the repository). The signed-in account was assigned TdAdministrators;
  assignment is not required to sign in. See `app-registration.json`.
- The app ran with `ops/dev/entra-local.sh up` (.NET 10 SDK container, Kestrel on 127.0.0.1:5443, self-signed localhost
  certificate, settings from a file outside the repository, template `ops/dev/entra-local.env.example`) against database
  `TicketDesk_entra` on td-sql: a copy of `TicketDesk` made with `ops/dev/e2e-db.sh`, migrated through the contract stage.
- The browser ran on another machine and reached the app through an SSH tunnel to `https://localhost:5443`.

## What the files show

| File | Content |
|---|---|
| `authorize-request.txt` | The app's challenge on the running instance: 302 to the tenant's `/oauth2/v2.0/authorize` with the registered client and redirect URI, `response_type=code`, PKCE `S256`, `response_mode=form_post`, scope `openid profile email`. |
| `container-log.txt` | The complete container log: start at 22:42:55Z, then at 22:47:39Z `EntraAccountLinker` "Provisioned user … (roles TdAdministrators)". |
| `db-first-sign-in.txt` | After the sign-in: one row in `IdentityUserLogins` with `LoginProvider = EntraId`, pointing to a new user row without a password; that user has role TdAdministrators; `TicketDesk_entra` has 252 users against 251 in the source database, and the new user id is not in the source. |
| `app-registration.json` | The registration's relevant settings and the role assignment (no secret values). |

So the code flow ran end to end against Entra: the callback validated the token, the account linker created the local user
on first sign-in and mapped the `roles` claim (Entra app role TdAdministrators) to the legacy role of the same name.

## Not covered

One account and one first sign-in, on a local run of the app (not on AKS). Sign-out, the other three roles and a user
without an app role were not exercised with Entra; they are covered against the test provider.

## Redactions

Tenant, client, application object, service principal, user object and local user IDs, the account's user name, e-mail
address and display name are replaced by placeholders (`<tenant-id>`, `<client-id>`, `<entra-user-object-id>`,
`<local-user-id>`, `<email>`, `<display-name>`, ...). Per-request values of the authorize URL (state, nonce, code challenge)
are replaced too. Nothing else was changed.
