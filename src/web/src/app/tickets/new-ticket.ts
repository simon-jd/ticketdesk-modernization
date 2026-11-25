import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, resource, signal, viewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { TicketDeskApi } from '../api/ticket-desk-api';
import { createTicket } from '../api/fn/tickets/create-ticket';
import { errorMessage, validationErrors } from '../core/http';
import { Session } from '../core/session';
import { TicketOptionsStore } from './ticket-options';
import { FieldErrors, NewTicketValues, emptyValues, fromServerErrors, summary, toRequest, validate } from './new-ticket-form';

/**
 * New ticket (legacy /ticket/new) over POST /api/tickets. Fields follow the legacy form and the user's permissions (owner,
 * assignee, priority, tags only when allowed). The details are Markdown, rendered on the server like the legacy editor's text.
 */
@Component({
  selector: 'td-new-ticket',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './new-ticket.html',
  styleUrl: './new-ticket.css',
})
export class NewTicketPage {
  private readonly api = inject(TicketDeskApi);
  private readonly optionsStore = inject(TicketOptionsStore);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  protected readonly session = inject(Session);
  private readonly summaryBox = viewChild<ElementRef<HTMLElement>>('summary');
  private readonly injector = inject(Injector);

  protected readonly options = resource({ loader: () => this.optionsStore.get() });
  protected readonly permissions = computed(() => (this.options.hasValue() ? this.options.value().permissions : null));
  protected readonly multiProject = computed(() => (this.options.hasValue() ? this.options.value().projects.length > 1 : false));

  protected readonly form = inject(NonNullableFormBuilder).group(emptyValues(null, ''));
  protected readonly errors = signal<FieldErrors>({});
  protected readonly general = signal<string[]>([]);
  protected readonly submitting = signal(false);
  /** Files to attach: uploaded as pending files (POST /file/upload?tempId=, the legacy upload widget's endpoint) on submit. */
  protected readonly files = signal<File[]>([]);
  protected readonly fileNames = computed(() => this.files().map((f) => f.name).join(', '));
  protected readonly errorSummary = computed(() => summary(this.errors()));

  constructor() {
    // defaults once the choices are known: the project picked in the legacy project menu (or the only one), the user as owner
    effect(() => {
      if (!this.options.hasValue()) return;
      const projects = this.options.value().projects;
      const selected = this.session.info()?.selectedProjectId ?? null;
      const project = projects.length === 1 ? projects[0].projectId : projects.some((p) => p.projectId === selected) ? selected : null;
      this.form.reset(emptyValues(project, this.session.info()?.userId ?? ''));
    });
  }

  protected error(field: keyof NewTicketValues): string | undefined {
    return this.errors()[field];
  }

  protected describedBy(field: keyof NewTicketValues, hint?: string): string | null {
    const ids = [hint, this.error(field) ? `${field}-error` : undefined].filter((x) => !!x);
    return ids.length ? ids.join(' ') : null;
  }

  protected pickFiles(event: Event): void {
    this.files.set([...((event.target as HTMLInputElement).files ?? [])]);
  }

  protected focusField(event: Event, field: keyof NewTicketValues): void {
    event.preventDefault();
    document.getElementById(`field-${field}`)?.focus();
  }

  protected async submit(): Promise<void> {
    const values = this.form.getRawValue();
    const p = this.permissions();
    if (!p || this.submitting()) return;
    const errors = validate(values, this.multiProject());
    this.errors.set(errors);
    this.general.set([]);
    if (Object.keys(errors).length > 0) {
      this.showSummary();
      return;
    }
    this.submitting.set(true);
    try {
      const tempId = this.files().length > 0 ? await this.uploadFiles() : undefined;
      const created = await this.api.invoke(createTicket, { body: toRequest(values, p, tempId) });
      await this.router.navigate(['/ticket', created.ticketId]);
    } catch (e) {
      const server = validationErrors(e);
      if (server) {
        const mapped = fromServerErrors(server);
        this.errors.set(mapped.fields);
        this.general.set(mapped.general);
      } else {
        this.general.set([errorMessage(e)]);
      }
      this.showSummary();
    } finally {
      this.submitting.set(false);
    }
  }

  /** Pending uploads under a new tempId; the create call attaches them to the ticket. */
  private async uploadFiles(): Promise<string> {
    const tempId = crypto.randomUUID();
    for (const file of this.files()) {
      const body = new FormData();
      body.append('file', file, file.name);
      await firstValueFrom(this.http.post('/file/upload', body, { params: { tempId } }));
    }
    return tempId;
  }

  private showSummary(): void {
    // after the summary has rendered: screen readers announce it, and its links lead to the fields
    afterNextRender(() => this.summaryBox()?.nativeElement.focus(), { injector: this.injector });
  }
}
