import { Routes, UrlMatchResult, UrlSegment } from '@angular/router';
import { angularPage, leaveForServer } from './core/pages';
import { ServerPage } from './core/server-page';

/**
 * The pages the Angular front end has, at their legacy URLs, each with the migration-registry id that switches it
 * (src/modern/TicketDesk.Web/migration-registry.json, mode Angular). Everything else is a server page.
 */
export const routes: Routes = [
  {
    // /tickets, /tickets/{listName}, /tickets/{listName}/{page}: one route, so paging and list changes reuse the page
    matcher: ticketCenterUrl,
    canMatch: [angularPage('tickets.index')],
    loadComponent: () => import('./tickets/ticket-center').then((m) => m.TicketCenterPage),
    title: 'Ticket center',
  },
  {
    path: 'ticket/new',
    canMatch: [angularPage('ticket.new.get')],
    loadComponent: () => import('./tickets/new-ticket').then((m) => m.NewTicketPage),
    title: 'New ticket',
  },
  {
    path: 'ticket/:id',
    canMatch: [angularPage('ticket.view')],
    loadComponent: () => import('./tickets/ticket-detail').then((m) => m.TicketDetailPage),
    title: 'Ticket',
  },
  { path: '**', canMatch: [leaveForServer], component: ServerPage, title: 'TicketDesk' },
];

/** The legacy route template tickets/{listName?}/{page:int?}. */
export function ticketCenterUrl(segments: UrlSegment[]): UrlMatchResult | null {
  if (segments.length === 0 || segments.length > 3 || segments[0].path !== 'tickets') return null;
  if (segments.length === 3 && !/^\d+$/.test(segments[2].path)) return null;
  const posParams: Record<string, UrlSegment> = {};
  if (segments[1]) posParams['listName'] = segments[1];
  if (segments[2]) posParams['page'] = segments[2];
  return { consumed: segments, posParams };
}
