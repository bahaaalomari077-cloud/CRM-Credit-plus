import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Alert,
  OperationsReport,
  CreateKycDocumentRequest,
  CreateLoanRequest,
  CreateTicketRequest,
  CustomerOverview,
  KycDocument,
  KycSummary,
  LoanApplication,
  LoanStatus,
  Ticket,
  UpdateTicketRequest,
} from './banking.models';

@Injectable({ providedIn: 'root' })
export class BankingService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getAlerts(limit = 50): Promise<Alert[]> {
    return firstValueFrom(
      this.http.get<Alert[]>(`${this.base}/alerts`, { params: new HttpParams().set('limit', limit) }),
    );
  }

  getOperationsReport(): Promise<OperationsReport> {
    return firstValueFrom(this.http.get<OperationsReport>(`${this.base}/reports/operations`));
  }

  /**
   * Downloads a CSV. The browser cannot set an Authorization header on a plain
   * link, so the file is fetched as a blob and saved from memory.
   */
  async exportCsv(dataset: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.get(`${this.base}/export/${dataset}`, { responseType: 'blob', observe: 'response' }),
    );

    const blob = response.body;
    if (!blob) {
      return;
    }

    const disposition = response.headers.get('content-disposition') ?? '';
    const match = /filename=([^;]+)/i.exec(disposition);
    const name = match ? match[1].trim().replace(/^"|"$/g, '') : `${dataset}.csv`;

    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
  }

  getCustomer(leadId: string): Promise<CustomerOverview> {
    return firstValueFrom(this.http.get<CustomerOverview>(`${this.base}/customers/${leadId}`));
  }

  getKyc(leadId: string): Promise<KycSummary> {
    return firstValueFrom(this.http.get<KycSummary>(`${this.base}/kyc/${leadId}`));
  }

  addKycDocument(request: CreateKycDocumentRequest): Promise<KycDocument> {
    return firstValueFrom(this.http.post<KycDocument>(`${this.base}/kyc/documents`, request));
  }

  verifyKycDocument(id: string, status: string, notes: string): Promise<KycDocument> {
    return firstValueFrom(
      this.http.patch<KycDocument>(`${this.base}/kyc/documents/${id}/verify`, { status, notes }),
    );
  }

  deleteKycDocument(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/kyc/documents/${id}`));
  }

  getLoans(filters: { status?: string; leadId?: string; search?: string } = {}): Promise<LoanApplication[]> {
    return firstValueFrom(
      this.http.get<LoanApplication[]>(`${this.base}/loans`, { params: this.params(filters) }),
    );
  }

  createLoan(request: CreateLoanRequest): Promise<LoanApplication> {
    return firstValueFrom(this.http.post<LoanApplication>(`${this.base}/loans`, request));
  }

  moveLoan(id: string, status: LoanStatus, notes = ''): Promise<LoanApplication> {
    return firstValueFrom(
      this.http.patch<LoanApplication>(`${this.base}/loans/${id}/status`, { status, notes }),
    );
  }

  deleteLoan(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/loans/${id}`));
  }

  getTickets(
    filters: { status?: string; priority?: string; leadId?: string; search?: string } = {},
  ): Promise<Ticket[]> {
    return firstValueFrom(this.http.get<Ticket[]>(`${this.base}/tickets`, { params: this.params(filters) }));
  }

  createTicket(request: CreateTicketRequest): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>(`${this.base}/tickets`, request));
  }

  updateTicket(id: string, request: UpdateTicketRequest): Promise<Ticket> {
    return firstValueFrom(this.http.put<Ticket>(`${this.base}/tickets/${id}`, request));
  }

  deleteTicket(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/tickets/${id}`));
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
