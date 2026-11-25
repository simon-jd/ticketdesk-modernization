import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import { ADMIN, REGISTRY } from './env';

/**
 * Switch-back through the running app's registry file (it watches the file): ticket.view goes from Angular to the server
 * page (New), then back to Angular. (Until Round 3 it also went to Legacy, the legacy app behind the gateway; that mode is
 * retired.) Skipped without TD_E2E_REGISTRY.
 */
test.describe('switch-back', () => {
  test.skip(!REGISTRY, 'TD_E2E_REGISTRY (the running app\'s registry file) is not set');
  test.use({ storageState: ADMIN.stateFile });

  const original = REGISTRY ? fs.readFileSync(REGISTRY, 'utf8') : '';
  function setMode(mode: string) {
    const reg = JSON.parse(original);
    reg.Migration.Routes['ticket.view'] = mode;
    fs.writeFileSync(`${REGISTRY}.tmp`, JSON.stringify(reg, null, 2));
    fs.renameSync(`${REGISTRY}.tmp`, REGISTRY);
  }
  test.afterAll(() => {
    if (!REGISTRY) return;
    fs.writeFileSync(`${REGISTRY}.tmp`, original);
    fs.renameSync(`${REGISTRY}.tmp`, REGISTRY);
  });

  async function backendOf(page: import('@playwright/test').Page, path: string, until: (b: string) => boolean) {
    let backend = '';
    await expect.poll(async () => {
      backend = (await page.request.get(path, { maxRedirects: 0 })).headers()['x-ticketdesk-backend'] ?? '';
      return until(backend);
    }, { timeout: 15_000 }).toBe(true);
    return backend;
  }

  test('a ticket page switched back is answered by the server page, then by Angular again', async ({ page }) => {
    await page.goto('/tickets/opentickets');
    const link = page.locator('table.tickets tbody tr td.title a').first();
    const href = (await link.getAttribute('href'))!;

    setMode('New');
    expect(await backendOf(page, href, (b) => b === 'modern; route=ticket.view')).toBe('modern; route=ticket.view');
    // the Angular list (loaded before the switch) still links there: its session says ticket.view is Angular, so the router
    // navigates in the page; a fresh list page knows better and lets the browser load the server page
    await page.goto('/tickets/opentickets');
    await page.locator(`a[href="${href}"]`).first().click();
    await expect(page).toHaveURL(new RegExp(`${href}$`));
    await expect(page.locator('#mainNav')).toBeAttached(); // the server (Razor) layout

    setMode('Angular');
    expect(await backendOf(page, href, (b) => b.endsWith('ui=angular'))).toBe('modern; route=ticket.view; ui=angular');
    await page.goto(href);
    await expect(page.locator('td-root h1')).toBeVisible();
  });
});
