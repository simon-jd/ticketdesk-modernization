# legacy-adapters (round 2), 2026-11-08 20:30–21:05Z

Lab VM (Windows Server 2025, IIS 10, SQL Server 2022 Express RTM, `ops/onprem/vm/vmctl.sh`) under the shared VM lock;
gateway = TicketDesk.Web from this branch on https://localhost:5443 (`ops/dev/replay-gateway.sh`). Golden =
`tests/characterization/golden/legacy-golden.jsonl.xz` (262 exchanges, recorded on the round-1 `baseline-s`). Every replay
starts from a fresh `baseline-s` reset with the candidate deployed (`ops/dev/replay-candidate.sh`). Replay output
(`candidate.jsonl`, `gateway.jsonl`) is stored as `.xz`; `compare*/report.md` is the readable result.

| Dir | What | Result |
|---|---|---|
| `build/` | `ops/onprem/legacy-artifact.sh build 887e4e8` (net48) and `build 35d7b5e` (net48 + adapters) on the VM: `build-info-*.json`, full log, `warnings.txt`, package SHA-256, `binding-redirects-*.txt` (`ops/dev/check-binding-redirects.cs` on the published `bin` + `Web.config`) | 0 errors, the same 4 warnings as the 4.6.2 build (2× CS0618 in S22.Mail, 2× MSB3245 WebJobs 2.0.0 in TicketDesk.IO); 840 / 891 files; redirects: 12 / 21 needed, 0 problems (7 stale upstream redirects match no reference) |
| `step1-net48/` | golden vs the net48 build (`td-legacy-publish-887e4e8.zip`) | direct 262/262; gateway 262/262 |
| `step2-adapters/` | golden vs net48 + System.Web adapters (`td-legacy-publish-35d7b5e.zip`, remote-auth key deployed) | direct 262/262; gateway 259/262, the 3 differences are G6 (push-notification settings page shows the gateway URL) |
| `step2-adapters/remote-auth/` | `ops/dev/remote-auth-probe.cs`: GET `/systemweb-adapters/authenticate` as the CoreServices client sends it, for a seeded admin, help-desk and internal user | 11/11 checks: 200 + ClaimsPrincipal (name, given name, role); wrong key 400, no key 400, no migration header 400; no cookie → 302 to `https://localhost:5443/user/sign-in?ReturnUrl=…`; return URL → 302 `/ticketcenter` |
| `step2-adapters/forwarded-for/` | anonymous 500 probe (`/emailtest/index/999999999`) with `X-Forwarded-For: 127.0.0.1`, guard on / off | guard on: 268-byte generic page; `RemoteAppTrustForwardedFor=true`: 8,717-byte error page with stack trace (Request.IsLocal) |
| `step3-expand-schema/` | `td-db migrate` (9 scripts) on the VM database after deploy, app pool restarted, golden; `assigned-date-check.txt` (end state); `audit-run/` + `assigned-date-audit.txt` (test-only trigger logging every legacy write to `dbo.Tickets`); `td-db-status.txt` | direct 261/262, gateway 258/262: D1 (tag autocomplete order) + G6×3; 29 legacy writes (8 inserts, 21 updates, 10 assignee changes): AssignedDate right in all, 0 invariant violations over 10,008 tickets |
| `baseline-s-shared/` | golden vs the re-saved `baseline-s` (shared state, no deploy) | direct 261/262 (D1); gateway 258/262 (D1, G6×3) |
| `step4-filestore/` | legacy app with `AzureStorage` → Azurite 3.37.0 (`probe-unmodified/`), `diagnosis/` (exceptions with customErrors temporarily off, Azurite debug log of the 403, SDK probe script) | **blocked**: 175 exchanges, 8 journey errors: every ticket create 500. Cause 1: Application Insights adds `x-ms-request-id`/`x-ms-request-root-id` after the storage SDK signed the request → 403 (fixed in the deploy step). Cause 2: `BlobFileSystem.Azure` 0.0.7 calls `CloudBlockBlob.StartCopyFromBlob`, which WindowsAzure.Storage 8.2.0 no longer has → `MissingMethodException` when attachments are committed |

Packages: `$TD_W/vm/artifacts/legacy-adapters/td-legacy-publish-{887e4e8,35d7b5e}.zip` (+ `.sha256`, `.build-info.json`).
Shared database export: `$TD_W/vm/exports/ticketdesk-baseline-s-shared.bak` (+ `.manifest.json`).
