import { Page, expect, test } from '@playwright/test';
import { ADMIN, INTERNAL } from './env';

/**
 * S3, Round 5: what used to be links that changed state by GET are buttons that post with the page's antiforgery token: Log off
 * (the Angular frame and the server pages' user menu), the language and project menus, the ticket list's sort headers and the
 * reset dialog of the server ticket center. Each click works, and the old URL by GET changes nothing.
 */
const languageMenu = (page: Page) => page.locator('#mainNav a.dropdown-toggle:has(i.fa-language)');
const culture = (page: Page) => page.evaluate(() => (window as unknown as { currentCulture: string }).currentCulture);

test.describe('as the admin', () => {
  test.use({ storageState: ADMIN.stateFile });

  test('the language menu posts the culture and returns to the page', async ({ page }) => {
    await page.goto('/about');
    expect(await culture(page)).toBe('en-US');
    await languageMenu(page).click();
    await page.getByRole('button', { name: 'Português do Brasil' }).click();
    await expect(page).toHaveURL(/\/about$/);
    expect(await culture(page)).toBe('pt-BR');
    await languageMenu(page).click();
    await page.getByRole('button', { name: 'English' }).click();
    await expect(page).toHaveURL(/\/about$/);
    expect(await culture(page)).toBe('en-US');
    // the old link by GET: 405, the culture stays
    expect((await page.request.get('/language?name=pt-BR', { maxRedirects: 0 })).status()).toBe(405);
    await page.reload();
    expect(await culture(page)).toBe('en-US');
  });

  test('the project menu posts the selection and returns to the page', async ({ page }) => {
    await page.goto('/about');
    const menu = page.locator('#dropdownMenu1');
    const items = page.locator('ul[aria-labelledby="dropdownMenu1"] form.td-menu-post button');
    await menu.click();
    const all = (await items.first().textContent())!.trim();
    const project = (await items.nth(1).textContent())!.trim();
    await items.nth(1).click();
    await expect(page).toHaveURL(/\/about$/);
    await expect(menu).toContainText(project);
    await menu.click();
    await items.first().click();
    await expect(menu).toContainText(all);
  });

  test('the server ticket center sorts by posting and resets its lists with the dialog', async ({ page }) => {
    // the server page of the ticket center (the shipped registry answers /tickets with Angular)
    await page.setExtraHTTPHeaders({ 'X-TicketDesk-Ui': 'server' });
    await page.goto('/tickets/opentickets');
    const titleHeader = page.locator('#ticketList thead a', { hasText: 'Title' });
    await expect(titleHeader).toHaveAttribute('data-ajax-method', 'POST');
    const sorted = page.waitForResponse((r) => r.url().includes('/tickets/sortList/') && r.request().method() === 'POST');
    await titleHeader.click();
    expect((await sorted).status()).toBe(200);
    await expect(page.locator('#ticketList thead th', { hasText: 'Title' }).locator('sup')).toHaveText('[1]');

    await page.locator('[data-target="#reset-confirm"]').click();
    const dialog = page.locator('#reset-confirm');
    await expect(dialog).toBeVisible();
    await dialog.locator('form[action="/tickets/reset-user-lists"] button[type="submit"]').click();
    await expect(page).toHaveURL(/\/tickets$/);
    await page.goto('/tickets/opentickets');
    await expect(page.locator('#ticketList thead th', { hasText: 'Title' }).locator('sup')).toHaveCount(0);
  });
});

test.describe('as an internal user', () => {
  test.use({ storageState: INTERNAL.stateFile });

  test('Log off in the Angular frame posts and signs out', async ({ page }) => {
    const response = await page.goto('/tickets');
    expect(response!.headers()['x-ticketdesk-backend']).toContain('ui=angular');
    await expect(page.locator('td-root form.sign-out input[name="__RequestVerificationToken"]')).toHaveValue(/.+/);
    await page.locator('td-root').getByRole('button', { name: 'Log off' }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('#loginLink')).toBeAttached(); // the anonymous user menu
    await page.goto('/tickets');
    await expect(page).toHaveURL(/\/user\/sign-in\?ReturnUrl=%2Ftickets$/);
  });

  test('a GET of the old sign-out URL shows the button and signs nobody out; the button does', async ({ page }) => {
    await page.goto('/user/sign-out');
    await expect(page.locator('#signOutForm')).toBeVisible();
    await page.goto('/about');
    await expect(page.locator('#loginLink')).toHaveCount(0); // still signed in
    await page.goto('/user/sign-out');
    await page.locator('#signOutForm button[type="submit"]').click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('#loginLink')).toBeAttached();
  });
});
