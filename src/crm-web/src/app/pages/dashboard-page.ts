import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CrmStore } from '../core/crm-store.service';
import { ACTIVITY_TYPE_LABELS, Activity, STAGE_LABELS } from '../core/crm.models';

@Component({
  selector: 'app-dashboard-page',
  imports: [NgClass, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './dashboard-page.html',
})
export class DashboardPage {
  protected readonly store = inject(CrmStore);

  protected readonly periods = ['هذا الأسبوع', 'هذا الشهر', 'ربع سنوي'];
  protected readonly today = new Date();

  protected readonly visibleDeals = computed(() => {
    const term = this.store.search().trim().toLowerCase();
    const deals = this.store.dashboard().deals;
    return term
      ? deals.filter((deal) =>
          `${deal.company} ${deal.contact} ${deal.name} ${this.stageLabel(deal.stage)}`
            .toLowerCase()
            .includes(term),
        )
      : deals;
  });

  protected readonly stats = computed(() => {
    const dashboard = this.store.dashboard();
    return [
      { label: 'عملاء محتملون جدد', value: dashboard.newLeads.toString(), unit: '', icon: '♙', tone: 'sky' },
      { label: 'قيمة خط المبيعات', value: this.money(dashboard.pipelineValue), unit: 'د.أ', icon: '⌁', tone: 'violet' },
      { label: 'صفقات مغلقة هذا الشهر', value: this.money(dashboard.wonThisMonth), unit: 'د.أ', icon: '✦', tone: 'mint' },
      { label: 'معدل التحويل', value: `${dashboard.conversionRate}%`, unit: '', icon: '◎', tone: 'amber' },
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
    return STAGE_LABELS[stage as keyof typeof STAGE_LABELS] ?? stage;
  }

  protected typeLabel(type: string): string {
    return ACTIVITY_TYPE_LABELS[type] ?? type;
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
    return new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
  }
}
