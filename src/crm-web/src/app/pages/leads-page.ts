import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminService } from '../core/admin.service';
import { EmailMessage } from '../core/auth.models';
import { AuthService } from '../core/auth.service';
import { CrmStore } from '../core/crm-store.service';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { LocaleDatePipe } from '../core/i18n/format.pipes';
import { LEAD_SOURCES, LEAD_STATUSES, Lead, LeadStatus } from '../core/crm.models';

interface LeadForm {
  fullName: string;
  company: string;
  email: string;
  phone: string;
  source: string;
  status: LeadStatus;
}

const blankLead = (): LeadForm => ({
  fullName: '',
  company: '',
  email: '',
  phone: '',
  source: 'Website',
  status: 'New',
});

@Component({
  selector: 'app-leads-page',
  imports: [FormsModule, RouterLink, TranslatePipe, LocaleDatePipe],
  templateUrl: './leads-page.html',
})
export class LeadsPage {
  protected readonly store = inject(CrmStore);
  protected readonly auth = inject(AuthService);
  private readonly admin = inject(AdminService);
  private readonly i18n = inject(I18nService);
  protected readonly statuses = LEAD_STATUSES;
  protected readonly sources = LEAD_SOURCES;

  protected readonly statusFilter = signal('');
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<LeadForm>(blankLead());

  protected readonly showEmail = signal(false);
  protected readonly emailTarget = signal<Lead | null>(null);
  protected readonly emailForm = signal({ subject: '', body: '' });
  protected readonly emailHistory = signal<EmailMessage[]>([]);
  protected readonly emailBusy = signal(false);
  protected readonly emailError = signal<string | null>(null);

  constructor() {
    void this.store.loadLeads();
  }

  protected statusLabel(status: string): string {
    return this.i18n.translate(`status.${status}` as TranslationKey);
  }

  protected kycLabel(status: string): string {
    return this.i18n.translate(`kycStatus.${status}` as TranslationKey);
  }

  protected sourceLabel(source: string): string {
    return this.i18n.translate(`source.${source}` as TranslationKey);
  }

  protected emailStatusLabel(status: string): string {
    return this.i18n.translate(`email.status${status}` as TranslationKey);
  }

  protected reload(): void {
    void this.store.loadLeads(this.statusFilter() || undefined);
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.form.set(blankLead());
    this.showForm.set(true);
  }

  protected openEdit(lead: Lead): void {
    this.editingId.set(lead.id);
    this.form.set({
      fullName: lead.fullName,
      company: lead.company,
      email: lead.email,
      phone: lead.phone,
      source: lead.source,
      status: lead.status,
    });
    this.showForm.set(true);
  }

  protected update<K extends keyof LeadForm>(field: K, value: LeadForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.fullName.trim() || !form.company.trim()) {
      return;
    }

    const id = this.editingId();
    const saved = id ? await this.store.updateLead(id, form) : await this.store.createLead(form);
    if (saved) {
      this.showForm.set(false);
    }
  }

  protected async remove(lead: Lead): Promise<void> {
    if (confirm(this.i18n.translate('common.confirmDelete', { name: lead.fullName }))) {
      await this.store.deleteLead(lead.id);
    }
  }

  protected async openEmail(lead: Lead): Promise<void> {
    this.emailTarget.set(lead);
    this.emailForm.set({ subject: '', body: '' });
    this.emailError.set(null);
    this.emailHistory.set([]);
    this.showEmail.set(true);

    try {
      this.emailHistory.set(await this.admin.getEmails('Lead', lead.id));
    } catch {
      // History is supplementary; a failure here must not block composing.
    }
  }

  protected updateEmail(field: 'subject' | 'body', value: string): void {
    this.emailForm.update((current) => ({ ...current, [field]: value }));
  }

  protected async sendEmail(): Promise<void> {
    const lead = this.emailTarget();
    const form = this.emailForm();
    if (!lead || !form.subject.trim() || !form.body.trim() || this.emailBusy()) {
      return;
    }

    this.emailBusy.set(true);
    this.emailError.set(null);

    try {
      await this.admin.sendEmail({
        toAddress: lead.email,
        toName: lead.fullName,
        subject: form.subject,
        body: form.body,
        relatedType: 'Lead',
        relatedId: lead.id,
      });
      this.emailHistory.set(await this.admin.getEmails('Lead', lead.id));
      this.emailForm.set({ subject: '', body: '' });
    } catch (failure) {
      this.emailError.set(this.describe(failure));
    } finally {
      this.emailBusy.set(false);
    }
  }

  private describe(failure: unknown): string {
    if (failure instanceof HttpErrorResponse) {
      return failure.error?.detail ?? failure.error?.title ?? `Request failed (${failure.status}).`;
    }
    return 'Unexpected error.';
  }
}
