import { Component, computed, effect, inject, input, resource, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { getTicket } from '../api/fn/tickets/get-ticket';
import { errorMessage } from '../core/http';
import { Session } from '../core/session';
import { attachmentUrl, datePart, dueState, fileSize, statusLabel } from './format';
import { TicketActions } from './ticket-actions';

/**
 * Ticket page (legacy /ticket/{id}): details, fields, attachments (when the user may see them, S8) and the activity log, from
 * GET /api/tickets/{id}. The ticket text comes rendered and sanitized by the server (the legacy Markdown and allow-lists) and is
 * bound with [innerHTML], so Angular's own sanitizer runs over it as well.
 */
@Component({
  selector: 'td-ticket-detail',
  imports: [RouterLink, TicketActions],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.css',
})
export class TicketDetailPage {
  private readonly api = inject(TicketDeskApi);
  protected readonly session = inject(Session);

  readonly id = input<string>();
  protected readonly ticketId = computed(() => (/^\d+$/.test(this.id() ?? '') ? Number(this.id()) : null));

  protected readonly ticket = resource({
    params: () => this.ticketId() ?? undefined,
    loader: ({ params }) => this.api.invoke(getTicket, { id: params }),
  });

  protected readonly notFound = computed(() =>
    this.ticketId() === null || (this.ticket.error() instanceof HttpErrorResponse && (this.ticket.error() as HttpErrorResponse).status === 404));
  protected readonly loadError = computed(() => (this.ticket.error() && !this.notFound() ? errorMessage(this.ticket.error()) : null));

  /** The last activity's result, announced in a status region (the ticket reloads around it). */
  protected readonly saved = signal('');

  protected readonly statusLabel = statusLabel;
  protected readonly datePart = datePart;
  protected readonly fileSize = fileSize;
  protected readonly attachmentUrl = attachmentUrl;

  constructor() {
    const title = inject(Title);
    effect(() => {
      if (this.ticket.hasValue()) title.setTitle(`#${this.ticket.value().ticketId} ${this.ticket.value().title} - TicketDesk`);
      else if (this.notFound()) title.setTitle('Ticket not found - TicketDesk');
    });
  }

  protected changed(message: string): void {
    this.saved.set(message);
    this.ticket.reload();
  }

  protected due(dueDate: string | null) {
    return this.ticket.hasValue() ? dueState(dueDate, this.ticket.value().status, this.session.info()!.today) : null;
  }
}
