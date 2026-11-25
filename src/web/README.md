# TicketDesk web (Angular 22)

The Angular front end of the new TicketDesk app: standalone components, signals, no NgRx. It replaces server pages one by one;
the migration registry (`src/modern/TicketDesk.Web/migration-registry.json`, mode `Angular`) decides per route whether the
Angular page, the server (Razor) page or the legacy app answers.

- `npm run build` writes `../modern/TicketDesk.Web/wwwroot/app` (git-ignored). TicketDesk.Web serves it under `/app` and answers
  the page URLs (`/tickets`, `/ticket/{id}`, `/ticket/new`) with `index.html`; the build of TicketDesk.Web runs it when the
  sources changed (`-p:TdBuildWeb=false` skips it).
- `npm run generate:api` generates the typed client (`src/app/api`, git-ignored) from `../../docs/api/openapi.json` with
  ng-openapi-gen; `build` and `test` run it first.
- `npm test`: unit tests (Vitest). `npm run e2e`: Playwright against a running gateway (`TD_E2E_BASE_URL`, see `e2e/README.md`).

Node.js 24 LTS (`$TD_W/.tools/node`, on PATH through `$TD_W/.tools/env.sh`).
