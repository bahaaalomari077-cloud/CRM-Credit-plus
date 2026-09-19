import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CrmApiService } from './crm-api.service';
import {
  Activity,
  CreateActivityRequest,
  CreateDealRequest,
  CreateLeadRequest,
  DashboardSummary,
  Deal,
  DealStage,
  Lead,
  LeadStatus,
} from './crm.models';

const emptyDashboard: DashboardSummary = {
  newLeads: 0,
  totalLeads: 0,
  pipelineValue: 0,
  wonThisMonth: 0,
  conversionRate: 0,
  openActivities: 0,
  pipeline: [],
  deals: [],
  activities: [],
};

/** Single source of truth for the shell and every page; wraps the API in signals. */
@Injectable({ providedIn: 'root' })
export class CrmStore {
  private readonly api = inject(CrmApiService);

  readonly dashboard = signal<DashboardSummary>(emptyDashboard);
  readonly leads = signal<Lead[]>([]);
  readonly deals = signal<Deal[]>([]);
  readonly activities = signal<Activity[]>([]);

  readonly search = signal('');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly offline = signal(false);

  readonly openActivities = computed(() => this.activities().filter((activity) => !activity.isCompleted));

  async refreshAll(): Promise<void> {
    await this.run(async () => {
      const [dashboard, leads, deals, activities] = await Promise.all([
        this.api.getDashboard(),
        this.api.getLeads(),
        this.api.getDeals(),
        this.api.getActivities(),
      ]);
      this.dashboard.set(dashboard);
      this.leads.set(leads);
      this.deals.set(deals);
      this.activities.set(activities);
    });
  }

  async refreshDashboard(): Promise<void> {
    await this.run(async () => this.dashboard.set(await this.api.getDashboard()));
  }

  async loadLeads(status?: string): Promise<void> {
    await this.run(async () => this.leads.set(await this.api.getLeads(this.search(), status)));
  }

  async createLead(request: CreateLeadRequest): Promise<boolean> {
    return this.mutate(async () => {
      const lead = await this.api.createLead(request);
      this.leads.update((current) => [lead, ...current]);
    });
  }

  async updateLead(id: string, request: CreateLeadRequest & { status: LeadStatus }): Promise<boolean> {
    return this.mutate(async () => {
      const lead = await this.api.updateLead(id, request);
      this.leads.update((current) => current.map((item) => (item.id === id ? lead : item)));
    });
  }

  async deleteLead(id: string): Promise<boolean> {
    return this.mutate(async () => {
      await this.api.deleteLead(id);
      this.leads.update((current) => current.filter((item) => item.id !== id));
    });
  }

  async loadDeals(stage?: string): Promise<void> {
    await this.run(async () => this.deals.set(await this.api.getDeals(this.search(), stage)));
  }

  async createDeal(request: CreateDealRequest): Promise<boolean> {
    return this.mutate(async () => {
      const deal = await this.api.createDeal(request);
      this.deals.update((current) => [deal, ...current]);
    });
  }

  async updateDeal(id: string, request: CreateDealRequest): Promise<boolean> {
    return this.mutate(async () => {
      const deal = await this.api.updateDeal(id, request);
      this.deals.update((current) => current.map((item) => (item.id === id ? deal : item)));
    });
  }

  async moveDeal(id: string, stage: DealStage): Promise<boolean> {
    return this.mutate(async () => {
      const deal = await this.api.moveDeal(id, stage);
      this.deals.update((current) => current.map((item) => (item.id === id ? deal : item)));
    });
  }

  async deleteDeal(id: string): Promise<boolean> {
    return this.mutate(async () => {
      await this.api.deleteDeal(id);
      this.deals.update((current) => current.filter((item) => item.id !== id));
    });
  }

  async loadActivities(completed?: boolean): Promise<void> {
    await this.run(async () => this.activities.set(await this.api.getActivities(completed)));
  }

  async createActivity(request: CreateActivityRequest): Promise<boolean> {
    return this.mutate(async () => {
      const activity = await this.api.createActivity(request);
      this.activities.update((current) => [activity, ...current]);
    });
  }

  async toggleActivity(activity: Activity): Promise<boolean> {
    return this.mutate(async () => {
      const updated = await this.api.setActivityCompletion(activity.id, !activity.isCompleted);
      this.activities.update((current) => current.map((item) => (item.id === activity.id ? updated : item)));
    });
  }

  async deleteActivity(id: string): Promise<boolean> {
    return this.mutate(async () => {
      await this.api.deleteActivity(id);
      this.activities.update((current) => current.filter((item) => item.id !== id));
    });
  }

  dismissError(): void {
    this.error.set(null);
  }

  /** Runs a read; failures flip the offline banner rather than blanking the screen. */
  private async run(work: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    try {
      await work();
      this.offline.set(false);
      this.error.set(null);
    } catch (failure) {
      this.offline.set(true);
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  /** Runs a write and reports whether it succeeded, so dialogs know to stay open. */
  private async mutate(work: () => Promise<void>): Promise<boolean> {
    this.loading.set(true);
    try {
      await work();
      this.error.set(null);
      this.offline.set(false);
      // Any write can change the headline numbers, so pull them again.
      this.dashboard.set(await this.api.getDashboard());
      return true;
    } catch (failure) {
      this.error.set(this.describe(failure));
      return false;
    } finally {
      this.loading.set(false);
    }
  }

  private describe(failure: unknown): string {
    if (failure instanceof HttpErrorResponse) {
      if (failure.status === 0) {
        return 'تعذر الاتصال بالـ API. شغّل المشروع على المنفذ 5233 ثم حاول مرة أخرى.';
      }
      return failure.error?.detail ?? failure.error?.title ?? `فشل الطلب (${failure.status}).`;
    }
    return 'حدث خطأ غير متوقع.';
  }
}
