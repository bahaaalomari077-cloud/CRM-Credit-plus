import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuditEntry,
  CreateUserRequest,
  EmailMessage,
  SendEmailRequest,
  UpdateUserRequest,
  User,
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl.replace(/\/crm$/, '');

  getUsers(): Promise<User[]> {
    return firstValueFrom(this.http.get<User[]>(`${this.base}/users`));
  }

  createUser(request: CreateUserRequest): Promise<User> {
    return firstValueFrom(this.http.post<User>(`${this.base}/users`, request));
  }

  updateUser(id: string, request: UpdateUserRequest): Promise<User> {
    return firstValueFrom(this.http.put<User>(`${this.base}/users/${id}`, request));
  }

  deleteUser(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/users/${id}`));
  }

  getAudit(filters: { entityType?: string; entityId?: string; action?: string; limit?: number }): Promise<AuditEntry[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && `${value}`.trim()) {
        params = params.set(key, `${value}`);
      }
    }
    return firstValueFrom(this.http.get<AuditEntry[]>(`${this.base}/audit`, { params }));
  }

  sendEmail(request: SendEmailRequest): Promise<EmailMessage> {
    return firstValueFrom(this.http.post<EmailMessage>(`${this.base}/emails`, request));
  }

  getEmails(relatedType?: string, relatedId?: string): Promise<EmailMessage[]> {
    let params = new HttpParams();
    if (relatedType) {
      params = params.set('relatedType', relatedType);
    }
    if (relatedId) {
      params = params.set('relatedId', relatedId);
    }
    return firstValueFrom(this.http.get<EmailMessage[]>(`${this.base}/emails`, { params }));
  }
}
