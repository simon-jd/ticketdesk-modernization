# Front end before/after

Lighthouse 12.8.2 (desktop preset, median of 3 runs by performance), axe-core 4.14.0 (default rules), Chrome/156.0.8078.47. Axe: rules / nodes (by impact: rules/nodes).

## ticket-center (`/tickets`)

| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |
|---|---:|---:|---:|---|---:|---:|---|
| Round 1 legacy (2026-11-08) | 100 | 65 | 100 | 10 / 75 (critical 3/6, serious 6/68, moderate 1/1) | 445 | 226 | aria-required-children, button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list, listitem, select-name |
| Legacy now | 100 | 65 | 100 | 10 / 75 (critical 3/6, serious 6/68, moderate 1/1) | 445 | 226 | aria-required-children, button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list, listitem, select-name |
| Server pages (Razor) | 100 | 65 | 100 | 10 / 75 (critical 3/6, serious 6/68, moderate 1/1) | 644 | 512 | aria-required-children, button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list, listitem, select-name |
| Angular | 100 | 100 | 100 | 0 / 0 (none) | 339 | 130 | - |

## ticket-detail (`/ticket/1`)

| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |
|---|---:|---:|---:|---|---:|---:|---|
| Round 1 legacy (2026-11-08) | 100 | 71 | 100 | 7 / 15 (critical 1/1, serious 5/13, moderate 1/1) | 524 | 330 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Legacy now | 100 | 71 | 100 | 7 / 15 (critical 1/1, serious 5/13, moderate 1/1) | 524 | 330 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Server pages (Razor) | 98 | 71 | 100 | 7 / 15 (critical 1/1, serious 5/13, moderate 1/1) | 922 | 780 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Angular | 100 | 100 | 100 | 0 / 0 (none) | 385 | 138 | - |

## new-ticket (`/ticket/new`)

| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |
|---|---:|---:|---:|---|---:|---:|---|
| Round 1 legacy (2026-11-08) | 100 | 67 | 100 | 9 / 43 (critical 2/16, serious 6/26, moderate 1/1) | 576 | 338 | button-name, color-contrast, html-has-lang, label, link-name, link-in-text-block, list, tabindex |
| Legacy now | 100 | 67 | 100 | 9 / 43 (critical 2/16, serious 6/26, moderate 1/1) | 579 | 338 | button-name, color-contrast, html-has-lang, label, link-name, link-in-text-block, list, tabindex |
| Server pages (Razor) | 97 | 67 | 100 | 9 / 43 (critical 2/16, serious 6/26, moderate 1/1) | 976 | 798 | button-name, color-contrast, html-has-lang, label, link-name, link-in-text-block, list, tabindex |
| Angular | 100 | 100 | 100 | 0 / 0 (none) | 353 | 135 | - |

## sign-in (`/user/sign-in`)

| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |
|---|---:|---:|---:|---|---:|---:|---|
| Round 1 legacy (2026-11-08) | 100 | 66 | 100 | 7 / 14 (serious 6/13, moderate 1/1) | 402 | 187 | color-contrast, html-has-lang, link-name, link-in-text-block, list, tabindex |
| Legacy now | 100 | 66 | 100 | 7 / 14 (serious 6/13, moderate 1/1) | 402 | 187 | color-contrast, html-has-lang, link-name, link-in-text-block, list, tabindex |
| Server pages (Razor) | 100 | 66 | 100 | 7 / 14 (serious 6/13, moderate 1/1) | 568 | 412 | color-contrast, html-has-lang, link-name, link-in-text-block, list, tabindex |
| Angular | 100 | 66 | 100 | 7 / 14 (serious 6/13, moderate 1/1) | 561 | 412 | color-contrast, html-has-lang, link-name, link-in-text-block, list, tabindex |

## admin-users (`/admin/users`)

| Run | Performance | Accessibility | Best practices | Axe violations | LCP ms | Transfer KB | Failed a11y audits |
|---|---:|---:|---:|---|---:|---:|---|
| Round 1 legacy (2026-11-08) | 100 | 73 | 100 | 7 / 50 (critical 1/1, serious 5/48, moderate 1/1) | 402 | 208 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Legacy now | 100 | 73 | 100 | 7 / 50 (critical 1/1, serious 5/48, moderate 1/1) | 402 | 208 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Server pages (Razor) | 100 | 73 | 100 | 7 / 50 (critical 1/1, serious 5/48, moderate 1/1) | 601 | 454 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |
| Angular | 100 | 73 | 100 | 7 / 50 (critical 1/1, serious 5/48, moderate 1/1) | 601 | 454 | button-name, color-contrast, html-has-lang, link-name, link-in-text-block, list |

## The three replaced pages together

| Run | Axe rule violations | Axe nodes |
|---|---:|---:|
| Round 1 legacy (2026-11-08) | 26 | 133 |
| Legacy now | 26 | 133 |
| Server pages (Razor) | 26 | 133 |
| Angular | 0 | 0 |
