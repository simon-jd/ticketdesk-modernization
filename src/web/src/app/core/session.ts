import { Injectable, inject, signal } from '@angular/core';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { getSession } from '../api/fn/session/get-session';
import { SessionInfo } from '../api/models/session-info';
import { errorMessage } from './http';

/** The signed-in user and the pages the Angular front end answers (GET /api/session, loaded once at start-up). */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(TicketDeskApi);
  readonly info = signal<SessionInfo | null>(null);
  readonly error = signal<string | null>(null);

  async load(): Promise<void> {
    try {
      this.info.set(await this.api.invoke(getSession));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  /** The registry has this page on Angular (ids of the migration registry: tickets.index, ticket.view, ticket.new.get). */
  servesPage(pageId: string): boolean {
    return this.info()?.angularPages.includes(pageId) ?? false;
  }
}
