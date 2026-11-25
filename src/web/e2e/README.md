# Playwright end-to-end tests

Against a running TicketDesk.Web (`TD_E2E_BASE_URL`, default `https://localhost:5463`): `ops/dev/e2e.sh` starts one (the new
app alone on a fresh, contracted copy of the baseline data; until Round 3 also the gateway with the legacy VM behind it) and
runs the suite.
Credentials are read from `$TD_W/.secrets/legacy-lab.env` and `td-seed.env`. Output (`results.json`, failure screenshots) goes
to `TD_E2E_OUT` (default `e2e-out/`, git-ignored); sign-in state to `e2e/.auth/` (git-ignored, deleted by `ops/dev/e2e.sh`).
