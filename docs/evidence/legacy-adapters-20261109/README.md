# legacy-adapters step 4 (shared file store) and the re-recorded golden, 2026-11-09 01:39–01:54Z

Coordinator decision (DECISIONS.md, option A): a provider wrapper in legacy `TicketDesk.IO`. Lab VM under the shared VM lock;
Azurite 3.37.0 on lab-host (`ops/onprem/filestore/azurite.sh`, reached from the VM as 10.0.2.2); gateway = TicketDesk.Web
from this branch on https://localhost:5443. Package `td-legacy-publish-b4edcc3.zip` (sha256 `f1408b12…74cf6`). Replay output
is stored as `.xz`; `compare*/report.md` is the readable result.

| Dir | What | Result |
|---|---|---|
| `step4-blob/build/` | `ops/onprem/legacy-artifact.sh build b4edcc3` | 0 errors, the same 4 warnings; 891 files |
| `step4-blob/wrapper-test/` | `ops/windows/Test-TdBlobStore.ps1` on the VM: the deployed `TicketDeskBlobStorageProvider` against Azurite (the legacy solution has no test projects). `deploy.txt` is the first real run of Deploy-TdApp's App Insights exclusion | 12/12: save, list, commit rename pending → ticket folder, content and size kept, rename onto an existing file and from a missing file refused, rename within a folder, folder rename, read back, delete |
| `step4-blob/golden-blob/` | golden (round-1 recording) vs the app on the blob store, fresh `baseline-s` + empty Azurite per run (`replay-candidate.sh --prepare 'azurite.sh reset'`) | 0 journey errors; direct 261/262 (D1), gateway 258/262 (D1, G6×3); the attachments journey leaves `10007/replay-note.txt` and `10007/replay-added.txt` in the container |
| `step4-blob/move/` | `disk-golden/`: golden with the same package on the disk store (disk mode unchanged: 261/262, D1). Then `move-filestore.sh` (`ops/windows/Move-TdFileStore.ps1`) twice, the app switched to blob, 6 synthetic binary files added on disk (0 B to 5 MiB; tickets 1, 2, 3, 42, 9999), a third move, and `check-attachments.py` reading every moved file back through the app | run 1: 2 files uploaded, 2/2 match; run 2: 0 uploaded, 2 skipped; run 3: 6 uploaded, 2 skipped, 8/8 match (9,643,226 bytes); read back through the app: 8/8 same size and MD5. (The synthetic `réseau-diagram.png` was written as `rÃ©seau…` because Windows PowerShell 5.1 read the generating script as ANSI; that name round-trips too) |
| `baseline-s-install/` | `baseline-s.sh install`: installed-clean → package (key + blob store) → restore `ticketdesk-baseline-s.bak` → td-db migrate → reindex → export `ticketdesk-baseline-s-shared.bak` → snapshot `baseline-s`; then `reset` | 96 s; snapshot `baseline-s-legacy` untouched; state file FILESTORE=blob; reset 27 s including the Azurite reset |
| `replay-legacy/` | golden re-recorded on that state: `record-legacy.sh` (reset, run A, reset, run B, compare); old golden vs run A | A vs B 262/262; old vs new 261/262, the one difference is D1. Run A is the new `tests/characterization/golden/legacy-golden.jsonl.xz` (same bytes) |
| `replay-gateway/` | the new golden through the gateway (`replay-gateway.sh`, fresh reset) | 259/262; the 3 differences are G6 |
