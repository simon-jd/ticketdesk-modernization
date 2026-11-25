# Gate demos (2026-11-11): branch policy on `modernize` blocks, then the fix completes

Policy (created 2026-11-11T05:21:29Z, `_apis/policy/configurations` id 1): **Build** validation, required (blocking), pipeline
`ci` (`azure-pipelines/ci.yml`), expires after 720 min, on `refs/heads/modernize` (exact). With a policy on the branch Azure
Repos refuses direct pushes: `TF402455: Pushes to this branch are not permitted; you must use a pull request` (`../repo.txt`).

| PR | Breaking commit | PR build (CI run) | Gate | Completion attempt while red | Fix commit | Build after fix | Completed (rebase + fast-forward) |
|---|---|---|---|---|---|---|---|
| 2 | `9708533` GateDemoTests: `Assert.Equal(4, 2 + 3)` | run 6 failed: `.NET - restore, build, tests` (1 failed, failed again on the retry) | tests | HTTP 403 `GitPullRequestUpdateRejectedByPolicyException`: "CI (azure-pipelines/ci.yml) must succeed to update modernize." (`gate1-blocked.txt`) | `ac82e30` (revert) | run 7 succeeded | 05:28:24Z, modernize = `ac82e30` |
| 3 | `4b08fa8` Newtonsoft.Json 12.0.3 (GHSA-5crp-9r3c-p9vr, high) | run 9 failed: `Static gates / Vulnerable packages` ("NuGet restore failed") and `.NET restore` (`error NU1903: Warning As Error`, `gate2-nu1903.txt`) | vulnerable package | HTTP 403, same message (`gate2-blocked.txt`) | `8466a17` (revert); run 10 then failed on a CI script bug (`images.sh` pruned its own tag), fixed by `bb09bcc` | run 11 succeeded | 08:51:06Z, modernize = `bb09bcc` |
| 4 | `4957aab` `terraform fmt` drift in `envs/lab/outputs.tf` | run 13 failed: `Static gates / terraform fmt -check` (diff in `gate3-fmt-diff.txt`) | terraform fmt | HTTP 403, same message (`gate3-blocked.txt`) | `5c70418` (revert) | run 14 succeeded | 08:59:52Z, modernize = `5c70418` |

Each demo's breaking commit and its revert are both in `modernize` (net change none); the PRs also carried the pipeline work
(`ws/pipelines`). The CI on `modernize` after each completion: runs 8, 12, 15 succeeded. Per-run timelines: `../runs/run-N.txt`.
