# Evidence: Round 4 pipelines (Azure DevOps), 2026-11-11

Project `TicketDesk` (private) in the user's Azure DevOps organization; self-hosted agent `lab-host-1` (pool `lab-host`)
on lab-host. Collected with `ops/ado/*.sh` (REST API with the user's PAT, never written anywhere). Tenant, subscription and
client ids are replaced by placeholders; the user's name and e-mail are removed.

| File | What | Command |
|---|---|---|
| `repo.txt` | push of `modernize` (upstream history), default branch, branches, direct push refused | `git push`, `_apis/git/repositories` |
| `agent.txt` | agent package/sha256, status, the listener's clean environment, "no inherited Azure login" step | `ops/ado/agent.sh status`, `/proc/<pid>/environ` (keys only) |
| `runs.csv`, `runs-summary.txt` | every pipeline run: result, reason, queue wait, duration | `ops/ado/evidence.sh runs` |
| `runs/run-N.txt` | stage/job/task timeline (state, result, seconds, error messages) | `ops/ado/evidence.sh timeline` |
| `gates/` | the three gate demos: blocked PR builds, refused completions, fixes, completions (`gates.md`) | `ops/ado/pr.sh` |
| `service-connection.txt` | Entra app (no secret), federated credential, service connection, token exchange run | `az ad app`, `_apis/serviceendpoint`, pipeline `sc-check` |
| `cd-kind/` | CD to kind: approval (requested, decided, wait), deployment, smoke | `ops/ado/evidence.sh approvals`, `cd-kind.sh smoke` |
