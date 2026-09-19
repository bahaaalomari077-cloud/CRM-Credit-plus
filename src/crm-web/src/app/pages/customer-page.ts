import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import {
  KYC_DOCUMENT_TYPES,
  CustomerOverview,
  KycDocument,
} from '../core/banking.models';
import { BankingService } from '../core/banking.service';
import { LocaleDatePipe, LocaleMoneyPipe } from '../core/i18n/format.pipes';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

type Tab = 'kyc' | 'deals' | 'loans' | 'tickets' | 'tasks' | 'emails' | 'history';

interface DocumentForm {
  documentType: string;
  reference: string;
  issuedOn: string;
  expiresOn: string;
  notes: string;
}

const blankDocument = (): DocumentForm => ({
  documentType: 'NationalId',
  reference: '',
  issuedOn: '',
  expiresOn: '',
  notes: '',
});

@Component({
  selector: 'app-customer-page',
  imports: [FormsModule, RouterLink, TranslatePipe, LocaleDatePipe, LocaleMoneyPipe],
  templateUrl: './customer-page.html',
})
export class CustomerPage {
  /** Bound from the route parameter. */
  readonly id = input.required<string>();

  private readonly banking = inject(BankingService);
  private readonly i18n = inject(I18nService);
  protected readonly auth = inject(AuthService);

  protected readonly documentTypes = KYC_DOCUMENT_TYPES;

  protected readonly overview = signal<CustomerOverview | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly tab = signal<Tab>('kyc');
  protected readonly showDocumentForm = signal(false);
  protected readonly documentForm = signal<DocumentForm>(blankDocument());

  protected readonly tabs: { id: Tab; key: TranslationKey; count: () => number }[] = [
    { id: 'kyc', key: 'customer.tabKyc', count: () => this.overview()?.kyc.documents.length ?? 0 },
    { id: 'deals', key: 'customer.tabDeals', count: () => this.overview()?.deals.length ?? 0 },
    { id: 'loans', key: 'customer.tabLoans', count: () => this.overview()?.loans.length ?? 0 },
    { id: 'tickets', key: 'customer.tabTickets', count: () => this.overview()?.tickets.length ?? 0 },
    { id: 'tasks', key: 'customer.tabTasks', count: () => this.overview()?.activities.length ?? 0 },
    { id: 'emails', key: 'customer.tabEmails', count: () => this.overview()?.emails.length ?? 0 },
    { id: 'history', key: 'customer.tabHistory', count: () => this.overview()?.history.length ?? 0 },
  ];

  /** Score drives a ring; convert to a stroke offset for the SVG. */
  protected readonly scoreOffset = computed(() => {
    const score = this.overview()?.score ?? 0;
    const circumference = 2 * Math.PI * 34;
    return circumference - (score / 100) * circumference;
  });

  constructor() {
    // Reload whenever the route id changes, including the first time it binds.
    effect(() => {
      const id = this.id();
      if (id) {
        void this.reload();
      }
    });
  }

  protected label(prefix: string, value: string): string {
    return this.i18n.translate(`${prefix}.${value}` as TranslationKey);
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.overview.set(await this.banking.getCustomer(this.id()));
      this.error.set(null);
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected openDocumentForm(): void {
    this.documentForm.set(blankDocument());
    this.error.set(null);
    this.showDocumentForm.set(true);
  }

  protected updateDocument<K extends keyof DocumentForm>(field: K, value: DocumentForm[K]): void {
    this.documentForm.update((current) => ({ ...current, [field]: value }));
  }

  protected async saveDocument(): Promise<void> {
    const form = this.documentForm();
    this.loading.set(true);

    try {
      await this.banking.addKycDocument({
        leadId: this.id(),
        documentType: form.documentType,
        reference: form.reference,
        issuedOn: form.issuedOn || null,
        expiresOn: form.expiresOn || null,
        notes: form.notes,
      });
      this.showDocumentForm.set(false);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async verify(document: KycDocument, status: 'Verified' | 'Rejected'): Promise<void> {
    this.loading.set(true);
    try {
      await this.banking.verifyKycDocument(document.id, status, '');
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async removeDocument(document: KycDocument): Promise<void> {
    if (!confirm(this.i18n.translate('common.confirmDelete', { name: document.documentType }))) {
      return;
    }

    try {
      await this.banking.deleteKycDocument(document.id);
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
