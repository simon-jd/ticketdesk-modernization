# S9 on the lab VM: customErrors RemoteOnly, baseline-s re-saved

2026-11-08 15:52–16:02Z, VM lock held by `baseline`.

1. Reset to the old `baseline-s`; DB checksums (row counts + `CHECKSUM_AGG(BINARY_CHECKSUM(*))` for all 20 tables)
   → `db-checksums-before.txt`; Web.config had `<customErrors mode="Off" />`, `<compilation>` without `debug`
   (`webconfig-before.txt`).
2. `provision-legacy.sh copy-ops deploy` with the new `-CustomErrorsMode RemoteOnly` (`deploy.txt`; second run
   `deploy-again.txt` ends in the same state). Anonymous 500 probe `GET /emailtest/index/999999999` (no such ticket):
   before 8,997 bytes with exception, source lines and stack trace; after 268 bytes, the generic "Error" page
   (`500-check.txt`, `500-before.html`, `500-after.html`).
3. **The deploy also deleted the Lucene index**: robocopy `/MIR` with `/XD <site>\App_Data` still mirrored the publish
   folder's `App_Data` and purged `App_Data\ticketdesk-searchindex` (`deploy.txt`, 3 extra files, 3.99 MB). A first
   snapshot saved in that state recorded empty search results; it was replaced. Fix in `Deploy-TdApp.ps1`: `/XD App_Data`.
4. Reset, `legacy_smoke.py reindex` (`reindex.txt`), deploy with the fixed script (`deploy-fixed.txt`): the index
   keeps its 4,192,580 bytes and 'printer' finds 20 tickets (`index-check.txt`); Web.config `RemoteOnly`
   (`webconfig-after.txt`).
5. DB checksums after: identical to step 1 (`db-compare.txt`), so `ticketdesk-baseline-s.bak` was not re-exported.
6. Graceful stop, snapshot `baseline-s` replaced (`snapshots.txt`), `qemu-img check` clean, start.
7. Golden re-recorded (`../replay-legacy/`). Old golden vs new run 1: 262 compared, 260 matched; the 2 differences
   are `attachments#006` (`GET /file/get-attachments-info`, 500) and `internal-user#004` (`GET /tickets/unassigned`,
   500): error page (and its headers) only (`compare-old-golden-vs-new/report.md`).

Perf and Lighthouse were not re-run: only the error pages changed, and none of the measured requests returns an error.
