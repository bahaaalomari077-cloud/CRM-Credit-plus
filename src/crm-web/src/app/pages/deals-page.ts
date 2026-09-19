import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CrmStore } from '../core/crm-store.service';
import { DEAL_STAGES, Deal, DealStage, OPEN_STAGES, STAGE_LABELS } from '../core/crm.models';

interface DealForm {
  name: string;
  company: string;
  contact: string;
  value: number;
  stage: DealStage;
  probability: number;
  expectedCloseDate: string;
}

const isoDaysFromNow = (days: number): string => {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return date.toISOString().slice(0, 10);
};

const blankDeal = (): DealForm => ({
  name: '',
  company: '',
  contact: '',
  value: 10000,
  stage: 'Qualified',
  probability: 50,
  expectedCloseDate: isoDaysFromNow(14),
});

@Component({
  selector: 'app-deals-page',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './deals-page.html',
})
export class DealsPage {
  protected readonly store = inject(CrmStore);
  protected readonly stages = DEAL_STAGES;
  protected readonly boardStages = OPEN_STAGES;

  protected readonly stageFilter = signal('');
  protected readonly view = signal<'board' | 'table'>('board');
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<DealForm>(blankDeal());

  /** Deals grouped per open stage, for the kanban view. */
  protected readonly board = computed(() =>
    this.boardStages.map((stage) => {
      const deals = this.store.deals().filter((deal) => deal.stage === stage);
      return {
        stage,
        label: this.stageLabel(stage),
        deals,
        total: deals.reduce((sum, deal) => sum + deal.value, 0),
      };
    }),
  );

  protected readonly wonTotal = computed(() =>
    this.store
      .deals()
      .filter((deal) => deal.stage === 'Won')
      .reduce((sum, deal) => sum + deal.value, 0),
  );

  constructor() {
    void this.store.loadDeals();
  }

  protected stageLabel(stage: string): string {
    return STAGE_LABELS[stage as DealStage] ?? stage;
  }

  protected reload(): void {
    void this.store.loadDeals(this.stageFilter() || undefined);
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.form.set(blankDeal());
    this.showForm.set(true);
  }

  protected openEdit(deal: Deal): void {
    this.editingId.set(deal.id);
    this.form.set({
      name: deal.name,
      company: deal.company,
      contact: deal.contact,
      value: deal.value,
      stage: deal.stage,
      probability: deal.probability,
      expectedCloseDate: deal.expectedCloseDate,
    });
    this.showForm.set(true);
  }

  protected update<K extends keyof DealForm>(field: K, value: DealForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.name.trim() || !form.company.trim() || form.value <= 0) {
      return;
    }

    const id = this.editingId();
    const saved = id ? await this.store.updateDeal(id, form) : await this.store.createDeal(form);
    if (saved) {
      this.showForm.set(false);
    }
  }

  protected async move(deal: Deal, stage: string): Promise<void> {
    if (stage && stage !== deal.stage) {
      await this.store.moveDeal(deal.id, stage as DealStage);
    }
  }

  protected async remove(deal: Deal): Promise<void> {
    if (confirm(`حذف الصفقة "${deal.name}"؟`)) {
      await this.store.deleteDeal(deal.id);
    }
  }

  protected initials(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }
}
