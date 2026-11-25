import fs from 'node:fs';
import path from 'node:path';

/**
 * Where the suite runs and who signs in. Credentials come from the lab's secret files (read, never sourced or printed):
 * $TD_W/.secrets/legacy-lab.env (TD_ADMIN_EMAIL, TD_ADMIN_PASSWORD) and td-seed.env (TD_SEED_USER_PASSWORD, the seeded users).
 */
const TD_W = process.env['TD_W'] ?? path.join(process.env['HOME'] ?? '', 'Desktop/ticketdesk-modernization');

function readEnv(file: string): Record<string, string> {
  const text = fs.readFileSync(path.join(TD_W, '.secrets', file), 'utf8');
  return Object.fromEntries(
    text.split('\n').map((l) => l.trim()).filter((l) => l && !l.startsWith('#') && l.includes('='))
      .map((l) => [l.slice(0, l.indexOf('=')).trim(), l.slice(l.indexOf('=') + 1).trim().replace(/^['"]|['"]$/g, '')]),
  );
}

const lab = readEnv('legacy-lab.env');
const seed = readEnv('td-seed.env');

export const BASE_URL = (process.env['TD_E2E_BASE_URL'] ?? 'https://localhost:5463').replace(/\/$/, '');
export const OUTPUT = process.env['TD_E2E_OUT'] ?? 'e2e-out';
/** The running app's registry file, for the switch-back test (skipped without it). */
export const REGISTRY = process.env['TD_E2E_REGISTRY'] ?? '';

export interface User {
  key: string;
  userName: string;
  password: string;
  stateFile: string;
}

export const ADMIN: User = { key: 'admin', userName: lab['TD_ADMIN_EMAIL']!, password: lab['TD_ADMIN_PASSWORD']!, stateFile: 'e2e/.auth/admin.json' };
/** The seeded internal (not help-desk) user of the golden recording's internal-user journey. */
export const INTERNAL: User = {
  key: 'internal', userName: 'ali.chen.00238@seed.ticketdesk.test', password: seed['TD_SEED_USER_PASSWORD']!, stateFile: 'e2e/.auth/internal.json',
};

export function unique(prefix: string): string {
  return `${prefix} ${new Date().toISOString().replace(/[-:.TZ]/g, '').slice(0, 14)}-${Math.random().toString(36).slice(2, 7)}`;
}
