# Front-end baseline: Lighthouse and axe-core on legacy pages

`tests/frontend/frontend-baseline.mjs`, 2026-11-08 15:13Z, VM reset to `baseline-s` first. Chrome/156.0.8078.47 (headless), Lighthouse 12.8.2 (desktop preset, categories performance / accessibility / best practices, 3 runs per page, the run with the median performance score is saved), axe-core 4.14.0 (default rules, one run per page). Signed-out sign-in page; the others signed in as the lab admin through the real form. Chrome ran with `--ignore-certificate-errors` (lab CA). Screenshots are dropped and secrets replaced by `{secret:NAME}` in the saved JSON.

| Page | URL | Performance (3 runs) | Accessibility | Best practices | LCP ms | Transfer KB | axe rules violated | critical (rules/nodes) | serious | moderate |
|---|---|---|---:|---:|---:|---:|---:|---|---|---|
| sign-in | `/user/sign-in` | 100 / 100 / 100 | 66 | 100 | 402 | 187 | 7 | 0/0 | 6/13 | 1/1 |
| ticket-center | `/tickets` | 100 / 100 / 100 | 65 | 100 | 445 | 226 | 10 | 3/6 | 6/68 | 1/1 |
| ticket-detail | `/ticket/1` | 100 / 100 / 100 | 71 | 100 | 524 | 330 | 7 | 1/1 | 5/13 | 1/1 |
| new-ticket | `/ticket/new` | 100 / 100 / 100 | 67 | 100 | 576 | 338 | 9 | 2/16 | 6/26 | 1/1 |
| admin-users | `/admin/users` | 100 / 100 / 100 | 73 | 100 | 402 | 208 | 7 | 1/1 | 5/48 | 1/1 |

Totals over the 5 pages: 40 rule violations on 197 nodes — critical 7 rules / 24 nodes, serious 28 rules / 168 nodes, moderate 5 rules / 5 nodes

Rules per page (impact, nodes):

- sign-in: color-contrast (serious, 8), html-has-lang (serious, 1), link-in-text-block (serious, 1), link-name (serious, 1), list (serious, 1), page-has-heading-one (moderate, 1), tabindex (serious, 1)
- ticket-center: aria-required-children (critical, 1), button-name (critical, 1), color-contrast (serious, 38), html-has-lang (serious, 1), link-in-text-block (serious, 21), link-name (serious, 1), list (serious, 1), listitem (serious, 6), page-has-heading-one (moderate, 1), select-name (critical, 4)
- ticket-detail: button-name (critical, 1), color-contrast (serious, 9), html-has-lang (serious, 1), link-in-text-block (serious, 1), link-name (serious, 1), list (serious, 1), page-has-heading-one (moderate, 1)
- new-ticket: button-name (critical, 13), color-contrast (serious, 8), html-has-lang (serious, 1), label (critical, 3), link-in-text-block (serious, 1), link-name (serious, 1), list (serious, 1), page-has-heading-one (moderate, 1), tabindex (serious, 14)
- admin-users: button-name (critical, 1), color-contrast (serious, 44), html-has-lang (serious, 1), link-in-text-block (serious, 1), link-name (serious, 1), list (serious, 1), page-has-heading-one (moderate, 1)

Lighthouse accessibility audits failed per page:

- sign-in: color-contrast, html-has-lang, link-name, link-in-text-block, list, tabindex
- ticket-center: aria-required-children, button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list, listitem, select-name
- ticket-detail: button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list
- new-ticket: button-name, color-contrast, html-has-lang, label, link-name, link-in-text-block, list, tabindex
- admin-users: button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list

Files: `summary.json` (all numbers), `lighthouse-<page>.json` (full report of the median run), `axe-<page>.json` (violations with nodes), `run.txt`.
