import { Component, computed, inject, input, resource } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { listTickets } from '../api/fn/tickets/list-tickets';
import { TicketStatus } from '../api/models/ticket-status';
import { errorMessage } from '../core/http';
import { Session } from '../core/session';
import { TicketOptionsStore } from './ticket-options';
import { datePart, dueState, statusLabel } from './format';
import { ListQuery, PAGE_SIZES, ariaSort, filtersFor, fromRoute, pageWindow, rangeText, toApiParams, toUrl, toggleSort } from './list-query';

interface Column {
  field: string;
  label: string;
}

/**
 * Ticket center (legacy /tickets/{listName}/{page}): the user's lists, filters, sorting and paging over GET /api/tickets
 * (dbo.usp_TicketList). The whole query is in the URL, so a list view can be bookmarked, shared and navigated with Back.
 */
@Component({
  selector: 'td-ticket-center',
  imports: [RouterLink],
  templateUrl: './ticket-center.html',
  styleUrl: './ticket-center.css',
})
export class TicketCenterPage {
  private readonly api = inject(TicketDeskApi);
  private readonly optionsStore = inject(TicketOptionsStore);
  private readonly router = inject(Router);
  protected readonly session = inject(Session);

  // route parameters and query string (withComponentInputBinding)
  readonly listName = input<string>();
  readonly page = input<string>();
  readonly sort = input<string>();
  readonly status = input<string>();
  readonly owner = input<string>();
  readonly assignedTo = input<string>();
  readonly projectId = input<string>();
  readonly pageSize = input<string>();

  protected readonly pageSizes = PAGE_SIZES;
  protected readonly statuses: TicketStatus[] = ['Active', 'MoreInfo', 'Resolved'];
  protected readonly statusLabel = statusLabel;
  protected readonly datePart = datePart;

  protected readonly query = computed<ListQuery>(() => {
    const s = this.session.info()!;
    return fromRoute(
      { listName: this.listName(), page: this.page(), sort: this.sort(), status: this.status(), owner: this.owner(),
        assignedTo: this.assignedTo(), projectId: this.projectId(), pageSize: this.pageSize() },
      s.lists.map((l) => l.name), s.defaultList, s.selectedProjectId);
  });

  protected readonly lists = computed(() => this.session.info()!.lists);
  protected readonly listTitle = computed(() => this.lists().find((l) => l.name === this.query().list)?.displayName ?? 'Tickets');
  protected readonly filters = computed(() => filtersFor(this.query().list));

  protected readonly tickets = resource({
    params: () => this.query(),
    loader: ({ params }) => this.api.invoke(listTickets, toApiParams(params)),
  });

  /** Projects and users for the filters (loaded once). */
  protected readonly options = resource({ loader: () => this.optionsStore.get() });

  protected readonly projectNames = computed(() =>
    new Map((this.options.hasValue() ? this.options.value().projects : []).map((p) => [p.projectId, p.projectName])));

  protected readonly columns = computed<Column[]>(() => [
    { field: 'ticketId', label: '#' },
    { field: 'title', label: 'Title' },
    { field: 'status', label: 'Status' },
    { field: 'priority', label: 'Priority' },
    { field: 'ticketType', label: 'Type' },
    { field: 'category', label: 'Category' },
    { field: 'owner', label: 'Owner' },
    { field: 'assignedTo', label: 'Assigned to' },
    { field: 'lastUpdateDate', label: 'Updated' },
    { field: 'dueDate', label: 'Due' },
  ]);

  protected readonly appliedSort = computed(() => (this.tickets.hasValue() ? this.tickets.value().sort : this.query().sort));
  protected readonly range = computed(() => {
    if (!this.tickets.hasValue()) return '';
    const t = this.tickets.value();
    return rangeText(t.page, t.pageSize, t.totalCount);
  });
  protected readonly pages = computed(() => (this.tickets.hasValue() ? pageWindow(this.tickets.value().page, this.tickets.value().totalPages) : []));
  protected readonly loadError = computed(() => (this.tickets.error() ? errorMessage(this.tickets.error()) : null));

  protected ariaSort(field: string) {
    return ariaSort(this.appliedSort(), field);
  }

  protected due(dueDate: string | null, status: TicketStatus) {
    return dueState(dueDate, status, this.session.info()!.today);
  }

  /** Router link + query params for a variant of the current query (another list, page, sort or filter). */
  protected link(change: Partial<ListQuery>) {
    const q: ListQuery = { ...this.query(), ...change };
    return toUrl(q, this.session.info()!.selectedProjectId);
  }

  protected listLink(list: string) {
    return this.link({ list, page: 1, sort: '', status: '', owner: '', assignedTo: '' });
  }

  protected pageLink(page: number) {
    return this.link({ page });
  }

  protected sortBy(field: string): void {
    this.go({ sort: toggleSort(this.appliedSort(), field), page: 1 });
  }

  protected filter(field: 'status' | 'owner' | 'assignedTo' | 'projectId' | 'pageSize', value: string): void {
    const change: Partial<ListQuery> = { page: 1 };
    if (field === 'pageSize') change.pageSize = Number(value);
    else if (field === 'projectId') change.projectId = value === '' ? null : Number(value);
    else if (field === 'status') change.status = value as TicketStatus | '';
    else change[field] = value;
    this.go(change);
  }

  protected resetLink() {
    return this.link({ page: 1, sort: '', status: '', owner: '', assignedTo: '', pageSize: 20, projectId: this.session.info()!.selectedProjectId });
  }

  private go(change: Partial<ListQuery>): void {
    const { commands, queryParams } = this.link(change);
    void this.router.navigate(commands, { queryParams });
  }
}
