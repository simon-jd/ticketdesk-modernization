# Evidence: route migration, waves 0–3 (Round 2, `routes`)

All VM runs on lab-host against the shared lab VM (`ops/onprem/vm/vmctl.sh`, legacy TicketDesk at https://localhost:8443),
snapshot `baseline-s` = the shared state from legacy-adapters (net48 build with the System.Web adapters server side, the
expanded schema, attachments in the Blob store on Azurite). Each run held the shared VM lock (`plan/locks/vm.lock`, owner
`routes`) and started from a `baseline-s` reset (`ops/onprem/baseline/baseline-s.sh`, which also empties Azurite). The
gateway is `TicketDesk.Web` (Release) on https://localhost:5443 with **remote authentication on**
(`Legacy:RemoteApp:*`, key from `.secrets/legacy-remoteapp.env`), the **VM's database** (`TD_MODERN_DB=vm`, shared with the
legacy app) and the **shared Blob store** (`.secrets/legacy-filestore.env`). Golden: `b8284a4` (re-recorded on that state).

| Folder | What | Command |
|---|---|---|
| `replay-waves-0/`, `-01/`, `-012/`, `-0123/` | golden replay through the gateway with the registry cut to waves 0; 0–1; 0–2; 0–3 | `TD_LOCK_OWNER=routes TD_MODERN_DB=vm ops/dev/replay-gateway.sh --from tests/characterization/golden/legacy-golden.jsonl.xz --out-dir DIR --waves 0,1,... --remote-auth` |
| `.../summary.md` | which app answered each exchange, per wave; every mismatch with its expected-difference ids | `ops/dev/replay-summary.py` (rules: `docs/migration/replay-explanations.json`) |
| `.../compare-golden-gateway/report.md` | td-replay's full comparison | `td-replay compare` |
| `.../gateway.jsonl.xz` | the candidate recording (xz -9: over 512 KB raw) | |
| `switch-back/switch-back.txt` | `home.about` switched to Legacy and back by editing the registry file of the running gateway (no restart) | `/tmp` script, steps in the file |
| `perf/` | k6 mix, legacy direct vs the gateway with waves 0–3 and remote auth, 3 runs each, back to back, host load per run | `TD_LOCK_OWNER=routes TD_MODERN_DB=vm TD_GATEWAY_ARGS="--waves 0,1,2,3 --remote-auth" ops/dev/perf-baseline.sh --out-dir DIR` (gateway at `cfce29b`: the file routes added later are not in the mix) |
| `tests/dotnet-test.txt` | the solution's tests (SQL Server and Azurite integration tests included) | header of the file |
| `docker/docker-build.txt` | image build from the repository root and a container smoke test | header of the file |
| `round1-golden-without-remote-auth/` | first runs (2026-11-08, round-1 golden, remote auth not yet available): waves 0 and 0–1 replays and the perf pair in which every signed-in request was still answered by the legacy app (rule G7) | |
