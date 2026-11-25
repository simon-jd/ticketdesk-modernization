import { Injectable, inject } from '@angular/core';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { getTicketOptions } from '../api/fn/tickets/get-ticket-options';
import { TicketOptions } from '../api/models/ticket-options';

/** GET /api/tickets/options once per page load (projects, users, value lists): the list filters and the new-ticket form share it. */
@Injectable({ providedIn: 'root' })
export class TicketOptionsStore {
  private readonly api = inject(TicketDeskApi);
  private pending: Promise<TicketOptions> | null = null;

  get(): Promise<TicketOptions> {
    this.pending ??= this.api.invoke(getTicketOptions).catch((e: unknown) => {
      this.pending = null; // a failed load is retried next time
      throw e;
    });
    return this.pending;
  }
}
