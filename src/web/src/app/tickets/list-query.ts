import { ListTickets$Params } from '../api/fn/tickets/list-tickets';
import { TicketStatus } from '../api/models/ticket-status';

/** Everything that selects one page of a ticket list. It lives in the URL: /tickets/{list}/{page}?sort=&status=&... */
export interface ListQuery {
  list: string;
  page: number;
  pageSize: number;
  /** Empty: the list's default sort (the server applies it). Otherwise e.g. "-lastUpdateDate,title". */
  sort: string;
  status: TicketStatus | '';
  owner: string;
  assignedTo: string;
  projectId: number | null;
}

export interface SortKey {
  field: string;
  descending: boolean;
}

/** The page sizes the legacy filter bar offered. */
export const PAGE_SIZES = [10, 20, 30, 50, 100] as const;
export const DEFAULT_PAGE_SIZE = 20;
const STATUSES: TicketStatus[] = ['Active', 'MoreInfo', 'Resolved', 'Closed'];

/** Columns the list can be sorted by (a subset of the API's whitelist). */
export const SORTABLE = ['ticketId', 'title', 'status', 'priority', 'owner', 'assignedTo', 'ticketType', 'category', 'lastUpdateDate', 'dueDate', 'targetDate', 'createdDate'] as const;

export type RouteInputs = Partial<Record<'listName' | 'page' | 'sort' | 'status' | 'owner' | 'assignedTo' | 'projectId' | 'pageSize', string | undefined>>;

function positiveInt(value: string | undefined): number | null {
  if (!value || !/^\d+$/.test(value)) return null;
  const n = Number(value);
  return n >= 1 && Number.isSafeInteger(n) ? n : null;
}

/** Reads the URL into a query; anything unknown falls back to the defaults (the server validates again). */
export function fromRoute(r: RouteInputs, lists: string[], defaultList: string, selectedProjectId: number | null): ListQuery {
  const list = lists.find((l) => l.toLowerCase() === (r.listName ?? '').toLowerCase()) ?? defaultList;
  const size = positiveInt(r.pageSize);
  const sort = parseSort(r.sort ?? '').filter((k) => (SORTABLE as readonly string[]).includes(k.field));
  const project = r.projectId === 'all' ? null : (positiveInt(r.projectId) ?? selectedProjectId);
  return {
    list,
    page: positiveInt(r.page) ?? 1,
    pageSize: size && (PAGE_SIZES as readonly number[]).includes(size) ? size : DEFAULT_PAGE_SIZE,
    sort: formatSort(sort),
    status: STATUSES.find((s) => s === r.status) ?? '',
    owner: r.owner ?? '',
    assignedTo: r.assignedTo ?? '',
    projectId: project,
  };
}

/** URL of a query: path segments and the query string (defaults left out, so the plain list URL stays /tickets/{list}). */
export function toUrl(q: ListQuery, selectedProjectId: number | null): { commands: (string | number)[]; queryParams: Record<string, string> } {
  const commands: (string | number)[] = ['/tickets', q.list];
  if (q.page > 1) commands.push(q.page);
  const queryParams: Record<string, string> = {};
  if (q.sort) queryParams['sort'] = q.sort;
  if (q.status) queryParams['status'] = q.status;
  if (q.owner) queryParams['owner'] = q.owner;
  if (q.assignedTo) queryParams['assignedTo'] = q.assignedTo;
  if (q.pageSize !== DEFAULT_PAGE_SIZE) queryParams['pageSize'] = String(q.pageSize);
  if (q.projectId !== selectedProjectId) queryParams['projectId'] = q.projectId === null ? 'all' : String(q.projectId);
  return { commands, queryParams };
}

export function toApiParams(q: ListQuery): ListTickets$Params {
  const p: ListTickets$Params = { List: q.list, Page: q.page, PageSize: q.pageSize };
  if (q.sort) p.Sort = q.sort;
  if (q.status) p.Status = [q.status];
  if (q.owner) p.Owner = q.owner;
  if (q.assignedTo) p.AssignedTo = q.assignedTo;
  if (q.projectId !== null) p.ProjectId = q.projectId;
  return p;
}

export function parseSort(sort: string): SortKey[] {
  return sort
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
    .map((s) => (s.startsWith('-') ? { field: s.slice(1), descending: true } : { field: s, descending: false }));
}

export function formatSort(keys: SortKey[]): string {
  return keys.map((k) => (k.descending ? '-' : '') + k.field).join(',');
}

/**
 * A click on a column header: the same first column flips its direction, another column becomes the only sort key
 * (ascending; dates newest first). The server adds the ticket id as the last key.
 */
export function toggleSort(applied: string, field: string): string {
  const first = parseSort(applied)[0];
  if (first && first.field === field) return formatSort([{ field, descending: !first.descending }]);
  return formatSort([{ field, descending: field.endsWith('Date') }]);
}

/** aria-sort of a column header from the sort the server applied. */
export function ariaSort(applied: string, field: string): 'ascending' | 'descending' | 'none' {
  const first = parseSort(applied)[0];
  if (!first || first.field !== field) return 'none';
  return first.descending ? 'descending' : 'ascending';
}

/** Page numbers around the current page (at most `width`), for the pager. */
export function pageWindow(current: number, totalPages: number, width = 5): number[] {
  if (totalPages <= 0) return [];
  const half = Math.floor(width / 2);
  let start = Math.max(1, current - half);
  const end = Math.min(totalPages, start + width - 1);
  start = Math.max(1, end - width + 1);
  return Array.from({ length: end - start + 1 }, (_, i) => start + i);
}

/** "Tickets 21–40 of 57". */
export function rangeText(page: number, pageSize: number, total: number): string {
  if (total === 0) return 'No tickets';
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return from > total ? `No tickets on page ${page} (${total} in all)` : `Tickets ${from}–${to} of ${total}`;
}

/** Filters the legacy filter bar disabled per list (the list already fixes that column). */
export function filtersFor(list: string): { status: boolean; owner: boolean; assignedTo: boolean } {
  return {
    status: list !== 'historytickets',
    owner: list !== 'mytickets',
    assignedTo: list !== 'unassigned' && list !== 'assignedToMe',
  };
}
