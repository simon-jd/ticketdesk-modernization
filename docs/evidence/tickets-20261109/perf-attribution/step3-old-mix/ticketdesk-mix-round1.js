// k6 request mix for the legacy TicketDesk baseline (and the same mix through the gateway).
//
// Fixed arrival rate of "operations" (TD_RATE per second, default 10) for TD_WARMUP_S + TD_MEASURE_S seconds; requests
// made in the first TD_WARMUP_S seconds are tagged phase=warmup and left out of the reported numbers.
// Operation mix (read-heavy), picked per iteration with a per-VU seeded generator (mulberry32, seed 42000 + VU id;
// k6 2.x no longer has the randomSeed option), so a VU makes the same sequence of choices in every run:
//   list    35%  GET  /tickets/opentickets                                    (1 request)
//   view    35%  GET  /ticket/{id} + AJAX ticket-details + ticket-events       (3 requests; id uniform over the 10,000 seeded tickets)
//   filter  15%  POST /tickets/filterList (assignedTo anyone|unassigned)        (1 request)
//   comment 10%  GET  load-activity AddComment + POST add-comment              (2 requests; ticket from the VU's last open list)
//   create   5%  GET  /ticket/new + POST /ticket/new + GET the new ticket       (3 requests)
// Every VU signs in once (op=signin, not reported) as one of the 37 seeded help-desk users (same password, from the env)
// and keeps its cookies across iterations (noCookiesReset). Redirects are not followed and only 200 counts as a success
// (302 only where it is the expected answer), so a lost session shows up as failed requests instead of fast sign-in pages.
//
//   k6 run -e TD_TARGET=https://localhost:8443 -e TD_USERS_FILE=users-helpdesk.json -e TD_PASSWORD=... \
//          -e TD_SUMMARY=summary.json tests/perf/ticketdesk-mix.js
import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';

const BASE = (__ENV.TD_TARGET || 'https://localhost:8443').replace(/\/$/, '');
const USERS = JSON.parse(open(__ENV.TD_USERS_FILE || './users-helpdesk.json'));
const PASSWORD = __ENV.TD_PASSWORD;
const RATE = parseInt(__ENV.TD_RATE || '10', 10);
const WARMUP_S = parseInt(__ENV.TD_WARMUP_S || '30', 10);
const MEASURE_S = parseInt(__ENV.TD_MEASURE_S || '60', 10);
const TICKETS = parseInt(__ENV.TD_TICKETS || '10000', 10);
const OPS = ['list', 'view', 'filter', 'comment', 'create'];

http.setResponseCallback(http.expectedStatuses(200));
const EXPECT_302 = http.expectedStatuses(302);

export const options = {
  insecureSkipTLSVerify: true, // lab CA; the target is fixed by TD_TARGET
  noCookiesReset: true, // k6 clears the cookie jar every iteration by default, which would drop the sign-in
  maxRedirects: 0,
  discardResponseBodies: false,
  scenarios: {
    mix: {
      executor: 'constant-arrival-rate',
      rate: RATE, timeUnit: '1s', duration: `${WARMUP_S + MEASURE_S}s`,
      preAllocatedVUs: 20, maxVUs: USERS.length,
    },
  },
  summaryTrendStats: ['min', 'med', 'avg', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'],
  // thresholds only make k6 compute these sub-metrics for the summary; they never fail the run
  thresholds: Object.fromEntries([
    ['http_req_duration{phase:measure,mix:yes}', ['max>=0']],
    ['http_req_failed{phase:measure,mix:yes}', ['rate>=0']],
    ['checks{phase:measure}', ['rate>=0']],
    ...OPS.map((op) => [`http_req_duration{phase:measure,op:${op}}`, ['max>=0']]),
    ...OPS.map((op) => [`http_req_failed{phase:measure,op:${op}}`, ['rate>=0']]),
  ]),
};

const state = { signedIn: false, openIds: [], seed: null };

// mulberry32: small, fast, seedable PRNG (Math.random cannot be seeded in k6 2.x)
function rnd() {
  if (state.seed === null) state.seed = 42000 + exec.vu.idInTest;
  state.seed = (state.seed + 0x6D2B79F5) | 0;
  let t = state.seed;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

function phase() { return exec.instance.currentTestRunDuration < WARMUP_S * 1000 ? 'warmup' : 'measure'; }
function tags(op) { return { op, mix: op === 'signin' ? 'no' : 'yes', phase: phase() }; }
const AJAX = { 'X-Requested-With': 'XMLHttpRequest' };

function token(body, formMatch) {
  const form = formMatch ? (body.match(new RegExp(`<form[^>]*${formMatch}[\\s\\S]*?</form>`)) || [''])[0] : body;
  const m = form.match(/name="__RequestVerificationToken" type="hidden" value="([^"]+)"/);
  return m ? m[1] : null;
}

function signIn() {
  const user = USERS[(exec.vu.idInTest - 1) % USERS.length];
  const page = http.get(`${BASE}/user/sign-in`, { tags: tags('signin') });
  const res = http.post(`${BASE}/user/sign-in`,
    { __RequestVerificationToken: token(page.body, 'sign-in'), UserNameOrEmail: user, Password: PASSWORD, RememberMe: 'false' },
    { tags: tags('signin'), responseCallback: EXPECT_302 });
  state.signedIn = check(res, { 'signed in (302 to /)': (r) => r.status === 302 && r.headers.Location === '/' }, tags('signin'));
}

function pickOp() {
  const r = rnd();
  return r < 0.35 ? 'list' : r < 0.70 ? 'view' : r < 0.85 ? 'filter' : r < 0.95 ? 'comment' : 'create';
}

function list() {
  const res = http.get(`${BASE}/tickets/opentickets`, { tags: tags('list') });
  check(res, { 'list 200': (r) => r.status === 200 }, tags('list'));
  const ids = [...res.body.matchAll(/href="\/ticket\/(\d+)"/g)].map((m) => m[1]);
  if (ids.length) state.openIds = ids;
}

function view() {
  const id = 1 + Math.floor(rnd() * TICKETS);
  const page = http.get(`${BASE}/ticket/${id}`, { tags: tags('view') });
  http.get(`${BASE}/ticket/ticket-details?ticketId=${id}`, { headers: AJAX, tags: tags('view') });
  http.get(`${BASE}/ticket/ticket-events?ticketId=${id}`, { headers: AJAX, tags: tags('view') });
  check(page, { 'view 200': (r) => r.status === 200 }, tags('view'));
}

function filter() {
  const assignedTo = rnd() < 0.5 ? 'anyone' : 'unassigned';
  const res = http.post(`${BASE}/tickets/filterList`, { owner: 'anyone', assignedTo, pageSize: '20' }, { headers: AJAX, tags: tags('filter') });
  check(res, { 'filter 200': (r) => r.status === 200 }, tags('filter'));
}

function comment() {
  if (!state.openIds.length) list();
  const id = state.openIds[Math.floor(rnd() * state.openIds.length)];
  const form = http.get(`${BASE}/ticket-activity/load-activity?ticketId=${id}&activity=AddComment`, { headers: AJAX, tags: tags('comment') });
  const res = http.post(`${BASE}/ticket-activity/add-comment?ticketId=${id}`,
    { __RequestVerificationToken: token(form.body, 'add-comment'), ticketId: id, comment: 'Load test comment.' },
    { headers: AJAX, tags: tags('comment') });
  // success is an empty 200; a re-rendered form (200 with a body) means the activity was refused (e.g. ticket in MoreInfo)
  check(res, { 'comment 200': (r) => r.status === 200, 'comment saved': (r) => r.status === 200 && r.body.length === 0 }, tags('comment'));
}

function create() {
  const page = http.get(`${BASE}/ticket/new`, { tags: tags('create') });
  const form = (page.body.match(/<form[^>]*action="\/ticket\/new"[\s\S]*?<\/form>/) || [''])[0];
  const owner = (form.match(/<select[^>]*name="Owner"[\s\S]*?<option selected="selected" value="([^"]+)"/) || [])[1] || '';
  const tempId = (form.match(/name="tempId" value="([^"]+)"/) || [])[1] || '';
  const res = http.post(`${BASE}/ticket/new`, {
    __RequestVerificationToken: token(form), TicketId: '0', IsHtml: 'True', tempId, ProjectId: '1',
    Title: 'Load test: printer offline', TicketType: 'Problem', Category: 'Hardware', Priority: 'Medium',
    TagList: 'loadtest', Details: 'Created by the k6 baseline mix.', Owner: owner, AssignedTo: '',
  }, { tags: tags('create'), responseCallback: EXPECT_302 });
  const ok = check(res, { 'create 302 to the ticket': (r) => r.status === 302 && /\/ticket\/\d+$/.test(r.headers.Location || '') }, tags('create'));
  if (ok && res.headers.Location) {
    const view = http.get(`${BASE}${res.headers.Location.replace(/^https?:\/\/[^/]+/, '')}`, { tags: tags('create') });
    check(view, { 'created ticket 200': (r) => r.status === 200 }, tags('create'));
  }
}

export default function () {
  if (!state.signedIn) { signIn(); if (!state.signedIn) return; }
  ({ list, view, filter, comment, create })[pickOp()]();
}

function sub(metrics, name) {
  const m = metrics[name];
  return m ? m.values : null;
}

export function handleSummary(data) {
  const m = data.metrics;
  const out = {
    target: BASE, rate_per_s: RATE, warmup_s: WARMUP_S, measure_s: MEASURE_S, users: USERS.length,
    all: { duration_ms: sub(m, 'http_req_duration{phase:measure,mix:yes}'), failed: sub(m, 'http_req_failed{phase:measure,mix:yes}') },
    checks: sub(m, 'checks{phase:measure}'),
    by_op: Object.fromEntries(OPS.map((op) => [op, {
      duration_ms: sub(m, `http_req_duration{phase:measure,op:${op}}`), failed: sub(m, `http_req_failed{phase:measure,op:${op}}`),
    }])),
    iterations: sub(m, 'iterations'), dropped_iterations: sub(m, 'dropped_iterations'), vus_max: sub(m, 'vus_max'),
    root_group_checks: data.root_group && data.root_group.checks,
  };
  const files = { stdout: JSON.stringify({ all: out.all, dropped: out.dropped_iterations }, null, 1) + '\n' };
  if (__ENV.TD_SUMMARY) files[__ENV.TD_SUMMARY] = JSON.stringify(out, null, 2);
  return files;
}
