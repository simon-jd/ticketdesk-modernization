# Flaky remote-auth test (round 3, search-worker)

`MigrationRoutingTests.With_remote_authentication_a_signed_in_request_is_answered_by_the_new_app_after_asking_the_legacy_app`
failed once in three full runs on `bc80b0e` and passed alone.

**Cause.** With `Legacy:RemoteApp:Enabled` the app registers the System.Web adapters; their
`System.Web.Hosting.HostingEnvironmentAccessor` keeps the running host in a process-wide static, set when a host starts and
cleared when it stops. Two test classes start such a host (`GatewayEdgeTests.Remote_app_authentication_when_enabled_...`,
`Remote_app_without_an_api_key_refuses_to_start`, and this test in `MigrationRoutingTests`). xUnit runs test classes in
parallel, so when the two hosts overlapped, the second start threw `InvalidOperationException: Hosting environment is already
set` (`failure-message.txt`, `failure-before-run2.txt`). Production is one host per process, so the app is not affected.

**Fix.** `src/modern/tests/TicketDesk.Web.Tests/SystemWebAdaptersHost.cs`: an xUnit collection for the two classes, so their
tests never run at the same time.

**Measured** (`runs.txt`; `ops/dev` test environment against td-sql, `TicketDesk.Web.Tests` run alone, which overlaps the
two classes more often than the full solution run did): before the fix 10 of 10 runs failed (one of the two tests each time),
after it 20 of 20 passed. Raw logs (21 MB) kept outside the repo: `$W/logs/search-worker/flaky-raw/`.
