import { NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CrmStore } from '../core/crm-store.service';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { LocaleDatePipe, LocaleMoneyPipe } from '../core/i18n/format.pipes';
import { Activity } from '../core/crm.models';

type Period = 'week' | 'month' | 'quarter';

@Component({
  selector: 'app-dashboard-page',
  imports: [NgClass, RouterLink, TranslatePipe, LocaleDatePipe, LocaleMoneyPipe],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  protected readonly store = inject(CrmStore);
  protected readonly i18n = inject(I18nService);

  protected readonly today = new Date();
  protected readonly period = signal<Period>('week');

  protected readonly periods: { id: Period; key: TranslationKey }[] = [
    { id: 'week', key: 'dashboard.periodWeek' },
    { id: 'month', key: 'dashboard.periodMonth' },
    { id: 'quarter', key: 'dashboard.periodQuarter' },
  ];

  /** The period buttons filter open deals by how soon they are expected to close. */
  protected readonly visibleDeals = computed(() => {
    const term = this.store.search().trim().toLowerCase();
    const horizon = { week: 7, month: 31, quarter: 92 }[this.period()];
    const limit = new Date();
    limit.setDate(limit.getDate() + horizon);

    return this.store.dashboard().deals.filter((deal) => {
      const withinPeriod = new Date(deal.expectedCloseDate) <= limit;
      if (!withinPeriod) {
        return false;
      }
      if (!term) {
        return true;
      }
      return `${deal.company} ${deal.contact} ${deal.name} ${this.stageLabel(deal.stage)}`
        .toLowerCase()
        .includes(term);
    });
  });

  protected readonly stats = computed(() => {
    const dashboard = this.store.dashboard();
    return [
      { key: 'dashboard.statNewLeads' as TranslationKey, value: dashboard.newLeads.toString(), unit: '', icon: '♙', tone: 'sky' },
      { key: 'dashboard.statPipeline' as TranslationKey, value: this.money(dashboard.pipelineValue), unit: 'JOD', icon: '⌁', tone: 'violet' },
      { key: 'dashboard.statWon' as TranslationKey, value: this.money(dashboard.wonThisMonth), unit: 'JOD', icon: '✦', tone: 'mint' },
      { key: 'dashboard.statConversion' as TranslationKey, value: `${dashboard.conversionRate}%`, unit: '', icon: '◎', tone: 'amber' },
    ];
  });

  /** Widest stage sets the 100% mark so the bars stay comparable at any scale. */
  protected readonly maxStageValue = computed(() =>
    Math.max(1, ...this.store.dashboard().pipeline.map((stage) => stage.value)),
  );

  protected barWidth(value: number): number {
    return Math.round((value / this.maxStageValue()) * 100);
  }

  protected stageLabel(stage: string): string {
    return this.i18n.translate(`stage.${stage}` as TranslationKey);
  }

  protected typeIcon(type: string): string {
    return type === 'Call' ? '☎' : type === 'Email' ? '✉' : type === 'Meeting' ? '◍' : '□';
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

  protected toggle(activity: Activity): void {
    void this.store.toggleActivity(activity).then(() => this.store.refreshDashboard());
  }

  protected money(value: number): string {
    return new Intl.NumberFormat(this.i18n.locale(), {
      notation: 'compact',
      maximumFractionDigits: 1,
    }).format(value);
  }
}
