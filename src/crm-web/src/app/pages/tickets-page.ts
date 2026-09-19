import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../core/auth.service';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  Ticket,
  TicketPriority,
  TicketStatus,
} from '../core/banking.models';
import { BankingService } from '../core/banking.service';
import { CrmStore } from '../core/crm-store.service';
import { LocaleDatePipe } from '../core/i18n/format.pipes';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

interface TicketForm {
  leadId: string;
  customerName: string;
  subject: string;
  description: string;
  category: string;
  priority: TicketPriority;
  status: TicketStatus;
  assignedTo: string;
  resolution: string;
}

const blankTicket = (): TicketForm => ({
  leadId: '',
  customerName: '',
  subject: '',
  description: '',
  category: 'Inquiry',
  priority: 'Normal',
  status: 'Open',
  assignedTo: '',
  resolution: '',
});

@Component({
  selector: 'app-tickets-page',
  imports: [FormsModule, TranslatePipe, LocaleDatePipe],
  templateUrl: './tickets-page.html',
})
export class TicketsPage {
  private readonly banking = inject(BankingService);
  private readonly i18n = inject(I18nService);
  protected readonly store = inject(CrmStore);
  protected readonly auth = inject(AuthService);

  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly categories = TICKET_CATEGORIES;

  protected readonly tickets = signal<Ticket[]>([]);
  protected readonly statusFilter = signal('');
  protected readonly priorityFilter = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<TicketForm>(blankTicket());

  protected readonly openCount = computed(
    () => this.tickets().filter((ticket) => ticket.status !== 'Resolved' && ticket.status !== 'Closed').length,
  );

  protected readonly breachedCount = computed(
    () => this.tickets().filter((ticket) => ticket.isBreached).length,
  );

  constructor() {
    void this.reload();
    void this.store.loadLeads();
  }

  protected statusLabel(status: string): string {
    return this.i18n.translate(`ticketStatus.${status}` as TranslationKey);
  }

  protected priorityLabel(priority: string): string {
    return this.i18n.translate(`priority.${priority}` as TranslationKey);
  }

  protected categoryLabel(category: string): string {
    return this.i18n.translate(`category.${category}` as TranslationKey);
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.tickets.set(
        await this.banking.getTickets({
          status: this.statusFilter() || undefined,
          priority: this.priorityFilter() || undefined,
        }),
      );
      this.error.set(null);
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.form.set(blankTicket());
    this.error.set(null);
    this.showForm.set(true);
  }

  protected openEdit(ticket: Ticket): void {
    this.editingId.set(ticket.id);
    this.form.set({
      leadId: ticket.leadId ?? '',
      customerName: ticket.customerName,
      subject: ticket.subject,
      description: ticket.description,
      category: ticket.category,
      priority: ticket.priority,
      status: ticket.status,
      assignedTo: ticket.assignedTo,
      resolution: ticket.resolution ?? '',
    });
    this.error.set(null);
    this.showForm.set(true);
  }

  protected update<K extends keyof TicketForm>(field: K, value: TicketForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  /** Closing needs a resolution; the UI says so before the API refuses. */
  protected readonly needsResolution = computed(() => {
    const form = this.form();
    return (
      (form.status === 'Resolved' || form.status === 'Closed') && !form.resolution.trim()
    );
  });

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.subject.trim() || !form.description.trim()) {
      return;
    }

    this.loading.set(true);
    try {
      const id = this.editingId();
      if (id) {
        await this.banking.updateTicket(id, {
          subject: form.subject,
          description: form.description,
          category: form.category,
          priority: form.priority,
          status: form.status,
          assignedTo: form.assignedTo,
          resolution: form.resolution,
        });
      } else {
        await this.banking.createTicket({
          leadId: form.leadId || null,
          customerName: form.customerName,
          subject: form.subject,
          description: form.description,
          category: form.category,
          priority: form.priority,
        });
      }
      this.showForm.set(false);
      this.error.set(null);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async remove(ticket: Ticket): Promise<void> {
    if (!confirm(this.i18n.translate('common.confirmDelete', { name: ticket.reference }))) {
      return;
    }

    try {
      await this.banking.deleteTicket(ticket.id);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    }
  }

  private describe(failure: unknown): string {
    if (failure instanceof HttpErrorResponse) {
      return failure.error?.detail ?? failure.error?.title ?? `Request failed (${failure.status}).`;
    }
    return 'Unexpected error.';
  }
}
