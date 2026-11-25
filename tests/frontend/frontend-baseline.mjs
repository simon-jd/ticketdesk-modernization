// Lighthouse (performance, accessibility, best practices; desktop preset) and axe-core on the key legacy TicketDesk pages.
//
//   node frontend-baseline.mjs --base https://localhost:8443 --out DIR [--runs 3] [--env-file $TD_W/.secrets/legacy-lab.env]
//
// Credentials (TD_ADMIN_EMAIL, TD_ADMIN_PASSWORD, TD_ADMIN_NAME) are read from the env file, not from the shell, because
// values with spaces (the admin's display name) do not survive `set -a; . file`.
//
// Chrome: CHROME_PATH or /usr/bin/google-chrome, headless, --ignore-certificate-errors (lab CA); Lighthouse drives a puppeteer page of the same browser. The sign-in page is
// measured signed out; the other pages after signing in as the lab admin through the real form (Lighthouse runs with
// disableStorageReset so the session cookie stays). Each page: N Lighthouse runs (the run with the median performance score
// is saved), one axe-core run (all rules axe enables by default). Secret values (credentials, the admin's name/e-mail) are
// replaced by {secret:NAME} in everything written, and screenshot audits are dropped from the saved reports.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import puppeteer from 'puppeteer-core';
import lighthouse from 'lighthouse';
import desktopConfig from 'lighthouse/core/config/desktop-config.js';

const require = createRequire(import.meta.url);
const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, all) => (v.startsWith('--') ? [...a, [v.slice(2), all[i + 1]]] : a), []));
const BASE = (args.base || 'https://localhost:8443').replace(/\/$/, '');
const OUT = args.out || 'out';
const RUNS = parseInt(args.runs || '3', 10);
const PAGES = [
  { key: 'sign-in', path: '/user/sign-in', auth: false },
  { key: 'ticket-center', path: '/tickets', auth: true },
  { key: 'ticket-detail', path: '/ticket/1', auth: true },
  { key: 'new-ticket', path: '/ticket/new', auth: true },
  { key: 'admin-users', path: '/admin/users', auth: true },
];
const ENV_FILE = args['env-file'] || path.join(process.env.TD_W || path.join(process.env.HOME, 'Desktop/ticketdesk-modernization'), '.secrets/legacy-lab.env');
const CRED = Object.fromEntries(fs.readFileSync(ENV_FILE, 'utf8').split('\n').map((l) => l.trim())
  .filter((l) => l && !l.startsWith('#') && l.includes('=')).map((l) => [l.slice(0, l.indexOf('=')).trim(), l.slice(l.indexOf('=') + 1).trim().replace(/^['"]|['"]$/g, '')]));
const SECRETS = Object.entries(CRED).filter(([k, v]) => /^TD_ADMIN_(EMAIL|PASSWORD|NAME)$/.test(k) && v && v.length >= 4);
const redact = (text) => SECRETS.reduce((t, [k, v]) => t.split(v).join(`{secret:${k}}`), text);
const DROP_AUDITS = ['final-screenshot', 'screenshot-thumbnails', 'full-page-screenshot'];

fs.mkdirSync(OUT, { recursive: true });
const browser = await puppeteer.launch({
  executablePath: process.env.CHROME_PATH || '/usr/bin/google-chrome',
  headless: true,
  args: ['--ignore-certificate-errors', '--no-first-run', '--no-default-browser-check', '--disable-extensions'],
  defaultViewport: null,
});
const chromeVersion = await browser.version();

async function signIn() {
  const page = await browser.newPage();
  await page.goto(`${BASE}/user/sign-in`, { waitUntil: 'networkidle0' });
  await page.type('input[name="UserNameOrEmail"]', CRED.TD_ADMIN_EMAIL);
  await page.type('input[name="Password"]', CRED.TD_ADMIN_PASSWORD);
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('form[action*="sign-in"] button[type="submit"], form[action*="sign-in"] input[type="submit"]')]);
  const ok = (await page.content()).includes('sign-out');
  await page.close();
  if (!ok) throw new Error('sign-in through the form failed');
}

async function axeRun(url) {
  const page = await browser.newPage();
  await page.setViewport({ width: 1350, height: 940 });
  // the injected axe script is inline: pages with a Content-Security-Policy (the Angular pages) would block it. Bypassing CSP
  // only lets the checker in; it changes nothing axe looks at (the legacy pages send no CSP, so their runs are unaffected)
  await page.setBypassCSP(true);
  await page.goto(url, { waitUntil: 'networkidle0' });
  await page.addScriptTag({ content: fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8') });
  const result = await page.evaluate(async () => {
    const r = await window.axe.run(document, { resultTypes: ['violations'] });
    return { testEngine: r.testEngine, url: r.url, violations: r.violations.map((v) => ({ id: v.id, impact: v.impact, help: v.help, helpUrl: v.helpUrl, tags: v.tags, nodes: v.nodes.map((n) => ({ target: n.target, html: n.html.slice(0, 300) })) })) };
  });
  await page.close();
  return result;
}

const summary = { base: BASE, chrome: chromeVersion, lighthouse: require('lighthouse/package.json').version, axe: require('axe-core/package.json').version, preset: 'desktop', runs: RUNS, pages: {} };
let signedIn = false;
for (const p of PAGES) {
  if (p.auth && !signedIn) { await signIn(); signedIn = true; }
  const url = BASE + p.path;
  const runs = [];
  for (let i = 0; i < RUNS; i++) {
    const page = await browser.newPage();
    const r = await lighthouse(url, { output: 'json', logLevel: 'error', disableStorageReset: true, onlyCategories: ['performance', 'accessibility', 'best-practices'] }, desktopConfig, page);
    await page.close();
    runs.push(r.lhr);
  }
  const scored = runs.map((lhr, i) => ({ i, perf: lhr.categories.performance.score })).sort((a, b) => a.perf - b.perf);
  const lhr = runs[scored[Math.floor(scored.length / 2)].i];
  if (lhr.finalDisplayedUrl && !lhr.finalDisplayedUrl.startsWith(url)) console.error(`warning: ${url} ended at ${lhr.finalDisplayedUrl}`);
  for (const a of DROP_AUDITS) delete lhr.audits[a];
  fs.writeFileSync(path.join(OUT, `lighthouse-${p.key}.json`), redact(JSON.stringify(lhr, null, 1)));
  const axe = await axeRun(url);
  fs.writeFileSync(path.join(OUT, `axe-${p.key}.json`), redact(JSON.stringify(axe, null, 1)));
  const byImpact = {};
  for (const v of axe.violations) {
    byImpact[v.impact] ??= { rules: 0, nodes: 0 };
    byImpact[v.impact].rules += 1; byImpact[v.impact].nodes += v.nodes.length;
  }
  const failedA11y = Object.values(lhr.audits).filter((a) => lhr.categories.accessibility.auditRefs.some((r) => r.id === a.id && r.weight > 0) && a.score === 0).map((a) => a.id);
  summary.pages[p.key] = {
    url: p.path, final_url: lhr.finalDisplayedUrl.replace(BASE, ''),
    performance_runs: runs.map((x) => x.categories.performance.score * 100),
    performance: lhr.categories.performance.score * 100, accessibility: lhr.categories.accessibility.score * 100,
    best_practices: lhr.categories['best-practices'].score * 100,
    lcp_ms: lhr.audits['largest-contentful-paint'].numericValue, fcp_ms: lhr.audits['first-contentful-paint'].numericValue,
    tbt_ms: lhr.audits['total-blocking-time'].numericValue, cls: lhr.audits['cumulative-layout-shift'].numericValue,
    transfer_kb: Math.round(lhr.audits['total-byte-weight'].numericValue / 1024),
    lighthouse_failed_a11y_audits: failedA11y,
    axe_violations: axe.violations.length, axe_by_impact: byImpact, axe_rules: axe.violations.map((v) => `${v.id} (${v.impact}, ${v.nodes.length})`),
  };
  console.log(`${p.key}: perf ${summary.pages[p.key].performance} a11y ${summary.pages[p.key].accessibility} bp ${summary.pages[p.key].best_practices} axe ${axe.violations.length} ${JSON.stringify(byImpact)}`);
}
await browser.close();
fs.writeFileSync(path.join(OUT, 'summary.json'), redact(JSON.stringify(summary, null, 2)));
