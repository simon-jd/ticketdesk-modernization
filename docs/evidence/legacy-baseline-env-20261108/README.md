# Legacy baseline environment, 2026-11-08 (UTC)

Legacy TicketDesk 2.5 built and deployed on the "on-prem" Windows Server 2025 VM (round 0, `legacy-env`).

## Environment
- Host: lab-host (32 cores, 123 GB RAM), qemu 10.2.1 with KVM (`-cpu host`), user-mode networking,
  forwards on 127.0.0.1: 2222->22, 8080->80, 8443->443, 13389->3389, 11433->1433.
- Guest: Windows Server 2025 Standard Evaluation (Server Core) 26100, 4 vCPU, 8 GB RAM, 80 GB qcow2,
  IIS 10, SQL Server 2022 Express 16.0.1000.6, VS 2022 Build Tools (MSBuild 17.14.60). `vm-info.txt`.
- Source built: `ws/legacy-env` at `f510384` (upstream `110dfed` + the project-file fix `61c4fe6`).

## Commands (from lab-host, repo root of the worktree)
```
ops/onprem/vm/vmctl.sh install                      # unattended install (qemu-kvm.txt, firstboot.txt)
ops/onprem/new-lab-cert.sh localhost tdlegacy01     # lab CA + server cert (secrets dir)
ops/onprem/provision-legacy.sh copy-ops prereqs sql site buildtools source build deploy backup-task
python3 -I ops/onprem/smoke/legacy_smoke.py setup   # first-run setup  -> first-run-setup.*
python3 -I ops/onprem/smoke/legacy_smoke.py demo    # demo data        -> demo-data.*
python3 -I ops/onprem/smoke/legacy_smoke.py smoke   # smoke test       -> smoke-1.*, smoke-after-restore.*
ops/onprem/export-schema.sh                         # $W/vm/exports/ticketdesk-schema.sql + .dacpac
```

## Files
| File | What it shows |
|---|---|
| `provision/01..12-*.txt` | every provisioning run in order, including the failures and their fixes: 02 (SQL setup DPAPI failure), 05 (MSB4025 build failure), 04/08/12 (second runs: no CHANGE lines) |
| `msbuild/{restore,build,publish}.txt`, `msbuild/build-info.json` | nuget restore (84 packages), MSBuild (0 errors, 4 warnings), publish (840 files) |
| `first-run-setup.*` | TicketDesk first-run wizard driven over HTTPS: database created, admin user |
| `demo-data.*` | demo data created through `/admin/data-management/create-demo-data` (37 tickets) |
| `smoke-1.*` | smoke test: HTTPS home, sign-in, create ticket, view it, add comment, comment visible: 9/9 checks |
| `restore-drill.txt` | scheduled-task backup restored with `Restore-TdSite.ps1` (DB + site files) in 4.4 s; ticket count back to the backup's 38 |
| `smoke-after-restore.*` | smoke test after the restore: 9/9 |
| `provision/13-final-build-deploy.txt` | final pass of every step with the committed scripts at `31754a5` (all SKIP except the redeploy) |
| `provision/15-refresh-installed-clean.txt`, `demo-data.*`, `smoke-final.*` | snapshot `installed-clean` refreshed (wizard off), demo data recreated (4/4), final smoke test 9/9 |
| `provision/14-deploy-setup-off.txt`, `setup-disabled.txt` | redeploy with `-SetupMode Auto`: wizard off once the DB has tables; anonymous create-database POST now redirects home and creates nothing |
| `qemu-kvm.txt`, `firstboot.txt` | KVM install: qemu start 00:07:31Z, first-boot script done 00:11:17Z, ssh answered 210 s after the install started |
| `qemu-tcg-attempt.txt` | the earlier TCG install (before KVM access was granted); disk discarded |

The `*.json` files are the smoke script's machine-readable summaries (every request with status and ms).
No secrets are in these files (checked by grepping for every password in `$W/.secrets/*.env`).
