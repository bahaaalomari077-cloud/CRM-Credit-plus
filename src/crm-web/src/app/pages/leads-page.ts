import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
  imports: [FormsModule, TranslatePipe, LocaleDatePipe],
  templateUrl: './leads-page.html',
})
export class LeadsPage {
  protected readonly store = inject(CrmStore);
  private readonly i18n = inject(I18nService);
  protected readonly statuses = LEAD_STATUSES;
  protected readonly sources = LEAD_SOURCES;

  protected readonly statusFilter = signal('');
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<LeadForm>(blankLead());

  constructor() {
    void this.store.loadLeads();
  }

  protected statusLabel(status: string): string {
    return this.i18n.translate(`status.${status}` as TranslationKey);
  }

  protected sourceLabel(source: string): string {
    return this.i18n.translate(`source.${source}` as TranslationKey);
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
}
