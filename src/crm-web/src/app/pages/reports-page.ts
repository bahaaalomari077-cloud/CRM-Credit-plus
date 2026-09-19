import { Component, computed, inject } from '@angular/core';
import { CrmStore } from '../core/crm-store.service';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { LocaleMoneyPipe } from '../core/i18n/format.pipes';
import { DEAL_STAGES } from '../core/crm.models';

interface Slice {
  label: string;
  count: number;
  value: number;
  percent: number;
  accent: string;
}

@Component({
  selector: 'app-reports-page',
  imports: [TranslatePipe, LocaleMoneyPipe],
  templateUrl: './reports-page.html',
})
export class ReportsPage {
  protected readonly store = inject(CrmStore);
  private readonly i18n = inject(I18nService);

  private readonly accents = ['blue', 'violet', 'amber', 'emerald', 'slate'];

  protected readonly byStage = computed<Slice[]>(() => {
    const deals = this.store.deals();
    const total = deals.length || 1;
    return DEAL_STAGES.map((stage, index) => {
      const inStage = deals.filter((deal) => deal.stage === stage);
      return {
        label: this.i18n.translate(`stage.${stage}` as TranslationKey),
        count: inStage.length,
        value: inStage.reduce((sum, deal) => sum + deal.value, 0),
        percent: Math.round((inStage.length / total) * 100),
        accent: this.accents[index % this.accents.length],
      };
    }).filter((slice) => slice.count > 0);
  });

  protected readonly bySource = computed<Slice[]>(() => this.group((lead) => lead.source, 'source'));
  protected readonly byStatus = computed<Slice[]>(() => this.group((lead) => lead.status, 'status'));

  protected readonly totals = computed(() => {
    const deals = this.store.deals();
    const totalValue = deals.reduce((sum, deal) => sum + deal.value, 0);
    const closed = deals.filter((deal) => deal.stage === 'Won' || deal.stage === 'Lost');
    const won = deals.filter((deal) => deal.stage === 'Won');

    return {
      leads: this.store.leads().length,
      deals: deals.length,
      totalValue,
      average: deals.length ? Math.round(totalValue / deals.length) : 0,
      winRate: closed.length ? Math.round((won.length / closed.length) * 100) : 0,
      openTasks: this.store.openActivities().length,
    };
  });

  private group(pick: (lead: { source: string; status: string }) => string, prefix: string): Slice[] {
    const leads = this.store.leads();
    const total = leads.length || 1;
    const counts = new Map<string, number>();

    for (const lead of leads) {
      const key = pick(lead);
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }

    return [...counts.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([key, count], index) => ({
        label: this.i18n.translate(`${prefix}.${key}` as TranslationKey),
        count,
        value: 0,
        percent: Math.round((count / total) * 100),
        accent: this.accents[index % this.accents.length],
      }));
  }
}
