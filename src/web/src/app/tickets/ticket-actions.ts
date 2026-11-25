import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, output, resource, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { performTicketActivity } from '../api/fn/tickets/perform-ticket-activity';
import { TicketDetail } from '../api/models/ticket-detail';
import { errorMessage, validationErrors } from '../core/http';
import { ACTIVITY_DONE, ACTIVITY_LABELS, ActivityValues, activityRequest, fieldsFor, validateActivity } from './activity-form';
import { TicketOptionsStore } from './ticket-options';

/**
 * The workflow buttons of the ticket page (the legacy activity buttons), each opening its small form; POST
 * /api/tickets/{id}/activities, then the page reloads the ticket. Editing fields and files stays on the server page.
 */
@Component({
  selector: 'td-ticket-actions',
  imports: [FormsModule],
  templateUrl: './ticket-actions.html',
  styleUrl: './ticket-actions.css',
})
export class TicketActions {
  private readonly api = inject(TicketDeskApi);
  private readonly injector = inject(Injector);
  private readonly optionsStore = inject(TicketOptionsStore);
  readonly ticket = input.required<TicketDetail>();
  readonly changed = output<string>();

  protected readonly labels = ACTIVITY_LABELS;
  protected readonly options = resource({ loader: () => this.optionsStore.get() });
  protected readonly open = signal<string | null>(null);
  protected readonly fields = computed(() => (this.open() ? fieldsFor(this.open()!) : null));
  protected values: ActivityValues = this.blank();
  protected readonly errors = signal<Partial<Record<'comment' | 'assignedTo' | 'priority', string>>>({});
  protected readonly failure = signal<string | null>(null);
  protected readonly busy = signal(false);
  private readonly assigneeField = viewChild<ElementRef<HTMLElement>>('assigneeField');
  private readonly commentField = viewChild<ElementRef<HTMLElement>>('commentField');

  private blank(): ActivityValues {
    return { comment: '', assignedTo: '', priority: '', assignToMe: false, reactivate: false };
  }

  protected start(activity: string): void {
    this.open.set(activity);
    this.values = this.blank();
    this.errors.set({});
    this.failure.set(null);
    // the form's first field: the assignee for assignments, else the comment
    afterNextRender(() => (this.assigneeField() ?? this.commentField())?.nativeElement.focus(), { injector: this.injector });
  }

  protected cancel(): void {
    this.open.set(null);
  }

  protected async submit(): Promise<void> {
    const activity = this.open();
    if (!activity || this.busy()) return;
    const errors = validateActivity(activity, this.values);
    this.errors.set(errors);
    this.failure.set(null);
    if (Object.keys(errors).length > 0) return;
    this.busy.set(true);
    try {
      await this.api.invoke(performTicketActivity, { id: this.ticket().ticketId, body: activityRequest(activity, this.values) });
      this.open.set(null);
      this.changed.emit(ACTIVITY_DONE[activity] ?? 'Saved.');
    } catch (e) {
      const server = validationErrors(e);
      if (server) this.errors.set(Object.fromEntries(Object.entries(server).map(([k, v]) => [k, v.join(' ')])));
      else if (e instanceof HttpErrorResponse && e.status === 409) {
        this.failure.set('This is no longer possible: the ticket has changed. It has been reloaded.');
        this.open.set(null);
        this.changed.emit('');
      } else this.failure.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
