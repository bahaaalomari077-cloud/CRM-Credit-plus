import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
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

@Injectable({ providedIn: 'root' })
export class CrmApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getDashboard(): Promise<DashboardSummary> {
    return firstValueFrom(this.http.get<DashboardSummary>(`${this.base}/dashboard`));
  }

  getLeads(search?: string, status?: string): Promise<Lead[]> {
    return firstValueFrom(
      this.http.get<Lead[]>(`${this.base}/leads`, { params: this.params({ search, status }) }),
    );
  }

  createLead(request: CreateLeadRequest): Promise<Lead> {
    return firstValueFrom(this.http.post<Lead>(`${this.base}/leads`, request));
  }

  updateLead(id: string, request: CreateLeadRequest & { status: LeadStatus }): Promise<Lead> {
    return firstValueFrom(this.http.put<Lead>(`${this.base}/leads/${id}`, request));
  }

  deleteLead(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/leads/${id}`));
  }

  getDeals(search?: string, stage?: string): Promise<Deal[]> {
    return firstValueFrom(
      this.http.get<Deal[]>(`${this.base}/deals`, { params: this.params({ search, stage }) }),
    );
  }

  createDeal(request: CreateDealRequest): Promise<Deal> {
    return firstValueFrom(this.http.post<Deal>(`${this.base}/deals`, request));
  }

  updateDeal(id: string, request: CreateDealRequest): Promise<Deal> {
    return firstValueFrom(this.http.put<Deal>(`${this.base}/deals/${id}`, request));
  }

  moveDeal(id: string, stage: DealStage): Promise<Deal> {
    return firstValueFrom(this.http.patch<Deal>(`${this.base}/deals/${id}/stage`, { stage }));
  }

  deleteDeal(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/deals/${id}`));
  }

  getActivities(completed?: boolean): Promise<Activity[]> {
    const params = completed === undefined ? undefined : new HttpParams().set('completed', completed);
    return firstValueFrom(this.http.get<Activity[]>(`${this.base}/activities`, { params }));
  }

  createActivity(request: CreateActivityRequest): Promise<Activity> {
    return firstValueFrom(this.http.post<Activity>(`${this.base}/activities`, request));
  }

  setActivityCompletion(id: string, isCompleted: boolean): Promise<Activity> {
    const action = isCompleted ? 'complete' : 'reopen';
    return firstValueFrom(this.http.patch<Activity>(`${this.base}/activities/${id}/${action}`, {}));
  }

  deleteActivity(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/activities/${id}`));
  }

  private params(values: Record<string, string | undefined>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value?.trim()) {
        params = params.set(key, value.trim());
      }
    }
    return params;
  }
}
