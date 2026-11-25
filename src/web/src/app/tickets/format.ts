import { TicketStatus } from '../api/models/ticket-status';

const STATUS_LABELS: Record<TicketStatus, string> = { Active: 'Active', MoreInfo: 'More info', Resolved: 'Resolved', Closed: 'Closed' };

export function statusLabel(status: TicketStatus): string {
  return STATUS_LABELS[status] ?? status;
}

/** Due-date marker of the legacy lists: overdue (before the server's today) or due today; only for tickets that are not closed. */
export function dueState(dueDate: string | null | undefined, status: TicketStatus, today: string): 'overdue' | 'today' | null {
  if (!dueDate || status === 'Closed' || status === 'Resolved') return null;
  const due = dueDate.slice(0, 10);
  if (due < today) return 'overdue';
  return due === today ? 'today' : null;
}

/** The date part of an ISO timestamp as the server wrote it (dates are stored at the server's midnight). */
export function datePart(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

/** "1.5 KB", "2 MB": attachment sizes. */
export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}

/** Download URL of an attachment (legacy route file/{ticketId}/{fileName}, served by the new app). */
export function attachmentUrl(ticketId: number, name: string): string {
  return `/file/${ticketId}/${encodeURIComponent(name)}`;
}
