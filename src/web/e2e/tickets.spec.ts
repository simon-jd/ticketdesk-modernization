import { Page, expect, test } from '@playwright/test';
import { ADMIN, INTERNAL, unique } from './env';

const h1 = (page: Page) => page.getByRole('heading', { level: 1 });
const rows = (page: Page) => page.locator('table.tickets tbody tr');

/** With several projects the form asks for one (unless the user picked one in the project menu). */
async function pickProject(page: Page): Promise<void> {
  const project = page.getByLabel('Project');
  if ((await project.count()) > 0 && !(await project.inputValue())) await project.selectOption({ index: 1 });
}

async function createTicket(page: Page, title: string, file?: { name: string; body: string }): Promise<string> {
  await page.goto('/ticket/new');
  await expect(h1(page)).toHaveText('New ticket');
  await expect(page.getByLabel('Title')).toBeVisible(); // the form renders once its choices are loaded
  await pickProject(page);
  await page.getByLabel('Title').fill(title);
  await page.getByLabel('Type').selectOption({ index: 1 });
  await page.getByLabel('Category').selectOption({ index: 1 });
  await page.getByLabel('Details').fill('The **printer** on floor 3 jams.\n\n- tried a restart');
  if (file) await page.getByLabel('Attachments').setInputFiles({ name: file.name, mimeType: 'text/plain', buffer: Buffer.from(file.body) });
  await page.getByRole('button', { name: 'Create ticket' }).click();
  await expect(page).toHaveURL(/\/ticket\/\d+$/);
  await expect(h1(page)).toContainText(title);
  return page.url().match(/\/ticket\/(\d+)$/)![1]!;
}

test.describe('as an internal user', () => {
  test.use({ storageState: INTERNAL.stateFile });

  test('list, open a ticket, create one, see it in the list', async ({ page }) => {
    const response = await page.goto('/tickets');
    expect(response!.headers()['x-ticketdesk-backend']).toBe('modern; route=tickets.index; ui=angular');
    await expect(h1(page)).toHaveText('My Tickets');

    // open: the first open ticket
    await page.getByRole('navigation', { name: 'Ticket lists' }).getByRole('link', { name: 'Open Tickets' }).click();
    await expect(page).toHaveURL(/\/tickets\/opentickets$/);
    await expect(h1(page)).toHaveText('Open Tickets');
    const first = rows(page).first().locator('td.title a');
    const title = (await first.textContent())!.trim();
    await first.click();
    await expect(page).toHaveURL(/\/ticket\/\d+$/);
    await expect(h1(page)).toContainText(title);
    await expect(page.getByRole('heading', { name: 'Activity log' })).toBeVisible();
    await expect(page.locator('ol.events > li').first()).toBeVisible();

    // create
    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'New ticket' }).click();
    const newTitle = unique('E2E printer jam');
    await expect(page.getByLabel('Title')).toBeVisible();
    await pickProject(page);
    await page.getByLabel('Title').fill(newTitle);
    await page.getByLabel('Type').selectOption({ index: 1 });
    await page.getByLabel('Category').selectOption({ index: 1 });
    await page.getByLabel('Details').fill('The **printer** on floor 3 jams.');
    await expect(page.getByLabel('Owner')).toHaveCount(0); // internal users create for themselves
    await page.getByRole('button', { name: 'Create ticket' }).click();
    await expect(page).toHaveURL(/\/ticket\/\d+$/);
    await expect(h1(page)).toContainText(newTitle);
    await expect(page.locator('.ticket-text strong').first()).toHaveText('printer');
    const id = page.url().match(/\/ticket\/(\d+)$/)![1]!;

    // see it in the list: My tickets, newest first
    await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Ticket center' }).click();
    await expect(h1(page)).toHaveText('My Tickets');
    await page.getByRole('button', { name: '#', exact: true }).click();
    await expect(page.locator('th[aria-sort="ascending"]')).toContainText('#');
    await page.getByRole('button', { name: '#', exact: true }).click();
    await expect(page.locator('th[aria-sort="descending"]')).toContainText('#');
    await expect(rows(page).first().locator('td.num')).toHaveText(id);
    await expect(rows(page).first()).toContainText(newTitle);
  });

  test('has only the internal lists; the help-desk lists are refused', async ({ page }) => {
    await page.goto('/tickets');
    await expect(page.getByRole('navigation', { name: 'Ticket lists' }).getByRole('link')).toHaveText(['My Tickets', 'Open Tickets', 'Ticket History']);
    await page.goto('/tickets/unassigned');
    await expect(page).toHaveURL(/\/access-denied$/);
  });

  test('cannot see the attachments of someone else\'s ticket, and the API needs the antiforgery header', async ({ page, browser }) => {
    // the admin creates a ticket with a file (owner: the admin, so the internal user is not on it)
    const admin = await browser.newContext({ storageState: ADMIN.stateFile, ignoreHTTPSErrors: true });
    const adminPage = await admin.newPage();
    const id = await createTicket(adminPage, unique('E2E scanned invoice'), { name: 'invoice.txt', body: 'invoice 42' });
    await expect(adminPage.getByRole('heading', { name: 'Attachments' })).toBeVisible();
    const link = adminPage.getByRole('link', { name: 'invoice.txt' });
    expect((await adminPage.request.get((await link.getAttribute('href'))!)).status()).toBe(200);
    await admin.close();

    // the internal user opens the same ticket: legacy parity, the ticket is visible, but its files are not (S8)
    await page.goto(`/ticket/${id}`);
    await expect(h1(page)).toContainText('E2E scanned invoice');
    await expect(page.getByRole('heading', { name: 'Activity log' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Attachments' })).toHaveCount(0);
    expect((await page.request.get(`/file/${id}/invoice.txt`)).status()).toBe(404);
    const api = await (await page.request.get(`/api/tickets/${id}`)).json();
    expect(api.attachmentsVisible).toBe(false);
    expect(api.attachments).toBeNull();

    // state-changing API calls without the token header are refused even with the sign-in cookie
    const forged = await page.request.post('/api/tickets', { data: { title: 'forged', details: 'x', ticketType: 'Problem', category: 'Hardware' } });
    expect(forged.status()).toBe(400);
  });
});

test('workflow on the ticket page: take over, comment, resolve (help desk), close (owner)', async ({ browser }) => {
  const internal = await browser.newContext({ storageState: INTERNAL.stateFile, ignoreHTTPSErrors: true });
  const admin = await browser.newContext({ storageState: ADMIN.stateFile, ignoreHTTPSErrors: true });
  const owner = await internal.newPage();
  const title = unique('E2E monitor flickers');
  const id = await createTicket(owner, title);
  const actions = (page: Page) => page.getByRole('region', { name: 'Actions' });
  await expect(actions(owner).getByRole('button')).toHaveText(['Add comment', 'Force close']);

  const staff = await admin.newPage();
  await staff.goto(`/ticket/${id}`);
  await actions(staff).getByRole('button', { name: 'Take over' }).click();
  const form = staff.getByRole('form', { name: 'Take over' });
  await expect(form.getByLabel('Comment')).toBeFocused();
  await form.getByLabel('Priority').selectOption({ index: 1 });
  await form.getByRole('button', { name: 'Take over' }).click();
  await expect(staff.getByRole('status').filter({ hasText: 'You took over the ticket.' })).toBeVisible();
  await expect(staff.locator('dt:text-is("Assigned to") + dd')).not.toHaveText('Unassigned');

  await actions(staff).getByRole('button', { name: 'Resolve' }).click();
  await staff.getByRole('form', { name: 'Resolve' }).getByRole('button', { name: 'Resolve' }).click();
  await expect(staff.getByRole('form', { name: 'Resolve' }).getByText('Enter a comment.')).toBeVisible(); // required, checked before the call
  await staff.getByRole('form', { name: 'Resolve' }).getByLabel('Comment').fill('Replaced the **cable**.');
  await staff.getByRole('form', { name: 'Resolve' }).getByRole('button', { name: 'Resolve' }).click();
  await expect(staff.locator('.head-meta .badge')).toHaveText('Resolved');
  await expect(staff.locator('ol.events > li').first()).toContainText('resolved');
  await expect(staff.locator('ol.events > li').first().locator('strong', { hasText: 'cable' })).toBeVisible();

  await owner.reload();
  await actions(owner).getByRole('button', { name: 'Close' }).click();
  await owner.getByRole('form', { name: 'Close' }).getByRole('button', { name: 'Close' }).click();
  await expect(owner.locator('.head-meta .badge')).toHaveText('Closed');
  await expect(actions(owner).getByRole('button')).toHaveText(['Reopen']);
  await internal.close();
  await admin.close();
});

test.describe('as an administrator (help desk)', () => {
  test.use({ storageState: ADMIN.stateFile });

  test('help-desk lists, filters, sorting and paging', async ({ page }) => {
    await page.goto('/tickets');
    await expect(h1(page)).toHaveText('Unassigned');
    await expect(page.getByRole('navigation', { name: 'Ticket lists' }).getByRole('link')).toHaveCount(5);
    await page.getByRole('navigation', { name: 'Ticket lists' }).getByRole('link', { name: 'Open Tickets' }).click();
    await expect(h1(page)).toHaveText('Open Tickets');

    await page.getByLabel('Status').selectOption('MoreInfo');
    await expect(page).toHaveURL(/status=MoreInfo/);
    await expect(rows(page).first()).toBeVisible();
    for (const badge of await rows(page).locator('td .badge[class*="status-"]').allTextContents()) expect(badge).toBe('More info');

    await page.getByRole('button', { name: 'Title' }).click();
    await expect(page).toHaveURL(/sort=title/);
    await expect(page.locator('th[aria-sort="ascending"]')).toContainText('Title');
    const titles = (await rows(page).locator('td.title').allTextContents()).map((t) => t.trim());
    expect(titles).toEqual([...titles].sort((a, b) => a.localeCompare(b, 'en', { sensitivity: 'base' })) );

    await page.getByLabel('Status').selectOption('');
    const pager = page.getByRole('navigation', { name: 'Pages' });
    await pager.getByRole('link', { name: 'Next' }).click();
    await expect(page).toHaveURL(/\/tickets\/opentickets\/2\?sort=title$/);
    await expect(pager).toContainText('Page 2 of');
    await expect(pager.locator('[aria-current="page"]')).toContainText('2');
  });

  test('the new-ticket form explains what is missing', async ({ page }) => {
    await page.goto('/ticket/new');
    await page.getByRole('button', { name: 'Create ticket' }).click();
    const summary = page.getByRole('alert').filter({ hasText: 'The ticket was not created' });
    await expect(summary).toBeFocused();
    // (and "Project" first when there are several projects and none is picked in the project menu)
    expect(await summary.getByRole('link').allTextContents()).toEqual(expect.arrayContaining(
      ['Title: Enter a title.', 'Type: Choose a type.', 'Category: Choose a category.', 'Details: Describe the problem or request.']));
    await summary.getByRole('link', { name: /^Title/ }).click();
    await expect(page.getByLabel('Title')).toBeFocused();
    await expect(page.getByLabel('Title')).toHaveAttribute('aria-invalid', 'true');
  });
});

test('anonymous visitors are sent to the sign-in page', async ({ browser }) => {
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  await page.goto('/tickets');
  await expect(page).toHaveURL(/\/user\/sign-in\?ReturnUrl=%2Ftickets$/);
  await context.close();
});
