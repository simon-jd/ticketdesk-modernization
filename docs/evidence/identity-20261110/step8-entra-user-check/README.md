# Step 8: one real Entra ID sign-in (2026-11-10)

Setup: `ops/dev/entra-user-check.sh` on `modernize` @ `967b8ca`. The VM was reset to `baseline-s-identity` and the gateway ran on
https://localhost:5443 with waves 0–5 on the new app and the shared cookie, reached from the user's Mac through an SSH tunnel.
The app registration is `ticketdesk-lab-web`, single tenant (`AzureADMyOrg`, the Azure for Students tenant), using the code
flow with PKCE and scopes `openid profile email`.

Result: **blocked by the tenant's consent policy, not by the app.**
- The user authenticated at the University sign-in page. Entra then showed "Need admin approval" for `ticketdesk-lab-web`,
  listing *View users' basic profile* and *Maintain access to data you have given it access to*. User consent is disabled in
  this tenant, so the only choices are "Cancel" and "Request approval" (which goes to the university's IT admins).
- The user chose Cancel twice, and Entra returned `access_denied` / `AADSTS65004` to `/signin-oidc` both times
  (`gateway-failures.txt`, failure lines only).
- `entra-user.txt` is empty: no `EntraId` login was created in `IdentityUserLogins`, as expected.

What this run proves live: the authority, client id, redirect URI and PKCE code request are accepted by Entra, and its error
response comes back to the app's callback. What it cannot prove live: code redemption, the `roles` claim from the tenant, and
provisioning or linking of the user. Those are covered only against the local test IdP (`EntraSignInTests`, 10/10).

Bug found: the remote failure was not handled. `AuthenticationFailureException` reached the exception handler, so the user
saw the error page instead of the sign-in page with a message. The fix goes in its own commit.
