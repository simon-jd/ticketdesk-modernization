import { defineConfig, devices } from '@playwright/test';
import { BASE_URL, OUTPUT } from './e2e/env';

/**
 * End-to-end tests of the Angular pages through a running TicketDesk.Web (the new app alone since Round 4; the gateway with
 * the legacy app behind it before). One worker and no retries: the suite shares one database, and a flaky test must show as a failure.
 * No traces (they would record the typed passwords); screenshots only of failures.
 */
export default defineConfig({
  testDir: './e2e',
  outputDir: `${OUTPUT}/test-results`,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['json', { outputFile: `${OUTPUT}/results.json` }]],
  use: {
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true, // lab CA
    trace: 'off',
    screenshot: 'only-on-failure',
    locale: 'en-US',
  },
  projects: [
    { name: 'sign-in', testMatch: /auth\.setup\.ts/ },
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, dependencies: ['sign-in'] },
  ],
});
