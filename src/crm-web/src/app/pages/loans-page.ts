import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import {
  LOAN_PRODUCTS,
  LOAN_STATUSES,
  LOAN_TRANSITIONS,
  LoanApplication,
  LoanStatus,
} from '../core/banking.models';
import { BankingService } from '../core/banking.service';
import { CrmStore } from '../core/crm-store.service';
import { LocaleDatePipe, LocaleMoneyPipe } from '../core/i18n/format.pipes';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

interface LoanForm {
  leadId: string;
  product: string;
  amount: number;
  termMonths: number;
  interestRate: number;
}

const blankLoan = (): LoanForm => ({
  leadId: '',
  product: 'WorkingCapital',
  amount: 25000,
  termMonths: 36,
  interestRate: 7.5,
});

@Component({
  selector: 'app-loans-page',
  imports: [FormsModule, RouterLink, TranslatePipe, LocaleDatePipe, LocaleMoneyPipe],
  templateUrl: './loans-page.html',
})
export class LoansPage {
  private readonly banking = inject(BankingService);
  private readonly i18n = inject(I18nService);
  protected readonly store = inject(CrmStore);
  protected readonly auth = inject(AuthService);

  protected readonly statuses = LOAN_STATUSES;
  protected readonly products = LOAN_PRODUCTS;

  protected readonly loans = signal<LoanApplication[]>([]);
  protected readonly statusFilter = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly form = signal<LoanForm>(blankLoan());

  protected readonly moveTarget = signal<LoanApplication | null>(null);
  protected readonly moveNotes = signal('');

  protected readonly totalRequested = computed(() =>
    this.loans().reduce((sum, loan) => sum + loan.amount, 0),
  );

  protected readonly totalApproved = computed(() =>
    this.loans()
      .filter((loan) => loan.status === 'Approved' || loan.status === 'Disbursed')
      .reduce((sum, loan) => sum + loan.amount, 0),
  );

  constructor() {
    void this.reload();
    void this.store.loadLeads();
  }

  protected statusLabel(status: string): string {
    return this.i18n.translate(`loanStatus.${status}` as TranslationKey);
  }

  protected productLabel(product: string): string {
    return this.i18n.translate(`product.${product}` as TranslationKey);
  }

  /** Only the moves the API would accept are offered. */
  protected movesFor(loan: LoanApplication): LoanStatus[] {
    return LOAN_TRANSITIONS[loan.status] ?? [];
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.loans.set(await this.banking.getLoans({ status: this.statusFilter() || undefined }));
      this.error.set(null);
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.form.set({ ...blankLoan(), leadId: this.store.leads()[0]?.id ?? '' });
    this.error.set(null);
    this.showForm.set(true);
  }

  protected update<K extends keyof LoanForm>(field: K, value: LoanForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.leadId || form.amount <= 0) {
      return;
    }

    this.loading.set(true);
    try {
      await this.banking.createLoan(form);
      this.showForm.set(false);
      this.error.set(null);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected openMove(loan: LoanApplication): void {
    this.moveTarget.set(loan);
    this.moveNotes.set('');
    this.error.set(null);
  }

  protected async move(status: LoanStatus): Promise<void> {
    const loan = this.moveTarget();
    if (!loan) {
      return;
    }

    this.loading.set(true);
    try {
      await this.banking.moveLoan(loan.id, status, this.moveNotes());
      this.moveTarget.set(null);
      this.error.set(null);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async remove(loan: LoanApplication): Promise<void> {
    if (!confirm(this.i18n.translate('common.confirmDelete', { name: loan.reference }))) {
      return;
    }

    try {
      await this.banking.deleteLoan(loan.id);
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
