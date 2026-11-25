import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { SessionInfo } from './api/models/session-info';
import { TicketPage } from './api/models/ticket-page';
import { TicketOptions } from './api/models/ticket-options';
import { BrowserLocation } from './core/browser';
import { Session } from './core/session';

const session: SessionInfo = {
  userId: 'u-internal', displayName: 'Ina Internal', isHelpDesk: false, isAdministrator: false,
  lists: [{ name: 'mytickets', displayName: 'My tickets' }, { name: 'opentickets', displayName: 'Open tickets' }, { name: 'historytickets', displayName: 'Ticket history' }],
  defaultList: 'mytickets', selectedProjectId: null, multiProject: false, today: '2026-11-10',
  angularPages: ['tickets.index', 'ticket.view', 'ticket.new.get'],
};

const options: TicketOptions = {
  projects: [{ projectId: 1, projectName: 'Default' }], ticketTypes: ['Question', 'Problem'], categories: ['Hardware', 'Software'], priorities: ['High', 'Low'],
  internalUsers: [{ id: 'u-internal', displayName: 'Ina Internal' }], helpDeskUsers: [{ id: 'u-help', displayName: 'Hal Help' }],
  permissions: { canSetOwner: false, canSetAssignedTo: false, canSetPriority: false, canSetTags: true },
};

function page(items: number, total = items): TicketPage {
  return {
    page: 1, pageSize: 20, totalCount: total, totalPages: Math.ceil(total / 20), sort: '-lastUpdateDate,ticketId',
    items: Array.from({ length: items }, (_, i) => ({
      ticketId: 100 + i, projectId: 1, title: `Ticket ${i}`, ticketType: 'Problem', category: 'Hardware', priority: null, status: 'Active' as const,
      owner: { id: 'u-internal', displayName: 'Ina Internal' }, assignedTo: null, createdDate: '2026-11-01T10:00:00+00:00',
      lastUpdateDate: '2026-11-02T10:00:00+00:00', dueDate: i === 0 ? '2026-11-09T00:00:00+00:00' : null, targetDate: null, tagList: null,
    })),
  };
}

describe('pages', () => {
  let http: HttpTestingController;
  let assigned: string[];

  beforeEach(() => {
    assigned = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: BrowserLocation, useValue: { assign: (url: string) => assigned.push(url), pathAndQuery: '/tickets' } },
      ],
    });
    TestBed.inject(Session).info.set(structuredClone(session));
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  const tick = () => new Promise((r) => setTimeout(r, 5));

  /** The next request matching; resources hold the app "unstable" until their request is answered, so poll instead of whenStable. */
  async function request(match: string | ((r: HttpRequest<unknown>) => boolean), method = 'GET'): Promise<TestRequest> {
    for (let i = 0; i < 400; i++) {
      const found = http.match((r) => r.method === method && (typeof match === 'string' ? r.urlWithParams === match || r.url === match : match(r)));
      if (found.length) return found[0];
      await tick();
    }
    throw new Error(`no ${method} request for ${match}`);
  }

  async function render(harness: RouterTestingHarness) {
    for (let i = 0; i < 5; i++) {
      await tick();
      harness.fixture.detectChanges();
    }
  }

  async function open(url: string): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    void harness.navigateByUrl(url);
    return harness;
  }

  it('ticket center asks the API for the list in the URL and renders the rows with sort state', async () => {
    const harness = await open('/tickets/opentickets?status=Active');
    const req = await request((r) => r.url === '/api/tickets');
    expect(req.request.params.get('List')).toBe('opentickets');
    expect(req.request.params.getAll('Status')).toEqual(['Active']);
    expect(req.request.params.get('PageSize')).toBe('20');
    (await request('/api/tickets/options')).flush(options);
    req.flush(page(3));
    await render(harness);

    const el = harness.routeNativeElement!;
    expect(el.querySelector('h1')!.textContent).toContain('Open tickets');
    expect(el.querySelectorAll('tbody tr').length).toBe(3);
    expect(el.querySelector('tbody tr a')!.getAttribute('href')).toBe('/ticket/100');
    expect(el.querySelector('th[aria-sort="descending"]')!.textContent).toContain('Updated');
    expect(el.querySelector('.due-overdue')!.textContent).toContain('Overdue');
    expect(el.querySelector('[role="status"]')!.textContent).toContain('Tickets 1–3 of 3');
    expect(el.querySelector('.list-tabs [aria-current="page"]')!.textContent).toContain('Open tickets');
    expect(el.querySelector('#filter-owner')).not.toBeNull();
  });

  it('a click on a column header sorts through the URL', async () => {
    const harness = await open('/tickets');
    (await request('/api/tickets/options')).flush(options);
    (await request((r) => r.url === '/api/tickets')).flush(page(2));
    await render(harness);
    expect(harness.routeNativeElement!.querySelector('#filter-owner')).toBeNull(); // "My tickets" fixes the owner
    const title = [...harness.routeNativeElement!.querySelectorAll<HTMLButtonElement>('th button')].find((b) => b.textContent!.includes('Title'))!;
    title.click();
    const req = await request((r) => r.url === '/api/tickets');
    expect(req.request.params.get('Sort')).toBe('title');
    expect(req.request.params.get('List')).toBe('mytickets');
    req.flush({ ...page(2), sort: 'title,ticketId' });
    await render(harness);
    expect(harness.routeNativeElement!.querySelector('th[aria-sort="ascending"]')!.textContent).toContain('Title');
  });

  it('a page switched back in the registry is loaded from the server', async () => {
    TestBed.inject(Session).info.set({ ...session, angularPages: ['tickets.index'] });
    const harness = await open('/tickets');
    (await request('/api/tickets/options')).flush(options);
    (await request((r) => r.url === '/api/tickets')).flush(page(1));
    await render(harness);
    void harness.navigateByUrl('/ticket/100');
    await render(harness);
    expect(assigned).toEqual(['/ticket/100']);
    void harness.navigateByUrl('/admin/users?page=2');
    await render(harness);
    expect(assigned).toEqual(['/ticket/100', '/admin/users?page=2']);
    http.expectNone('/api/tickets/100');
  });

  it('new ticket: fields follow the permissions, client checks run first, server errors land on the fields', async () => {
    const harness = await open('/ticket/new');
    (await request('/api/tickets/options')).flush(options);
    await render(harness);
    const el = harness.routeNativeElement!;
    expect(el.querySelector('#field-priority')).toBeNull(); // canSetPriority false
    expect(el.querySelector('#field-owner')).toBeNull();
    expect(el.querySelector('#field-projectId')).toBeNull(); // one project
    expect(el.querySelector('#field-tagList')).not.toBeNull();

    el.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await render(harness);
    http.expectNone({ method: 'POST', url: '/api/tickets' });
    expect(el.querySelector('.error-summary')!.textContent).toContain('Title: Enter a title.');
    expect(el.querySelector('#field-title')!.getAttribute('aria-invalid')).toBe('true');
    expect(el.querySelector('#field-title')!.getAttribute('aria-describedby')).toBe('title-error');

    fill(el, { '#field-title': 'Printer jams', '#field-details': 'It **jams**.', '#field-ticketType': 'Problem', '#field-category': 'Hardware' });
    el.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    const post = await request('/api/tickets', 'POST');
    expect(post.request.body).toMatchObject({ title: 'Printer jams', ticketType: 'Problem', category: 'Hardware', isHtml: false, projectId: 1 });
    expect((post.request.body as Record<string, unknown>)['owner']).toBeUndefined();
    post.flush({ title: 'One or more validation errors occurred.', errors: { category: ['Unknown category.'] } }, { status: 400, statusText: 'Bad Request' });
    await render(harness);
    expect(el.querySelector('#category-error')!.textContent).toContain('Unknown category.');
  });

  it('new ticket: a created ticket opens its page', async () => {
    const harness = await open('/ticket/new');
    (await request('/api/tickets/options')).flush(options);
    await render(harness);
    const el = harness.routeNativeElement!;
    fill(el, { '#field-title': 'x', '#field-details': 'y', '#field-ticketType': 'Question', '#field-category': 'Software' });
    el.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    (await request('/api/tickets', 'POST')).flush({ ticketId: 4242 }, { status: 201, statusText: 'Created' });
    (await request('/api/tickets/4242')).flush(null, { status: 404, statusText: 'Not Found' });
    await render(harness);
    expect(harness.routeNativeElement!.querySelector('h1')!.textContent).toContain('Ticket not found');
  });
});

function fill(el: HTMLElement, values: Record<string, string>): void {
  for (const [selector, value] of Object.entries(values)) {
    const input = el.querySelector<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
  }
}
