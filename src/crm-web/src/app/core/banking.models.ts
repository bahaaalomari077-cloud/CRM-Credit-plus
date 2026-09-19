import { Activity, Deal, Lead } from './crm.models';
import { AuditEntry, EmailMessage } from './auth.models';

export type KycStatus = 'NotStarted' | 'Pending' | 'Verified' | 'Rejected' | 'Expired';
export type RiskRating = 'Low' | 'Medium' | 'High';
export type LoanStatus = 'Draft' | 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Disbursed';
export type TicketStatus = 'Open' | 'InProgress' | 'Waiting' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export interface KycDocument {
  id: string;
  leadId: string;
  documentType: string;
  reference: string;
  status: KycStatus;
  issuedOn: string | null;
  expiresOn: string | null;
  notes: string | null;
  verifiedBy: string;
  verifiedAt: string | null;
  createdAt: string;
}

export interface KycSummary {
  leadId: string;
  status: KycStatus;
  riskRating: RiskRating;
  verified: number;
  pending: number;
  expired: number;
  missing: number;
  documents: KycDocument[];
}

export interface LoanApplication {
  id: string;
  reference: string;
  leadId: string;
  customerName: string;
  company: string;
  product: string;
  amount: number;
  termMonths: number;
  interestRate: number;
  status: LoanStatus;
  decision: string | null;
  decisionNotes: string | null;
  owner: string;
  createdAt: string;
  decidedAt: string | null;
  monthlyPayment: number;
}

export interface Ticket {
  id: string;
  reference: string;
  leadId: string | null;
  customerName: string;
  subject: string;
  description: string;
  category: string;
  priority: TicketPriority;
  status: TicketStatus;
  assignedTo: string;
  resolution: string | null;
  createdAt: string;
  resolvedAt: string | null;
  dueAt: string;
  isBreached: boolean;
}

export interface CustomerOverview {
  lead: Lead;
  score: number;
  scoreBand: 'Hot' | 'Warm' | 'Cold';
  scoreReasons: string[];
  kyc: KycSummary;
  deals: Deal[];
  loans: LoanApplication[];
  tickets: Ticket[];
  activities: Activity[];
  emails: EmailMessage[];
  history: AuditEntry[];
  totalDealValue: number;
  totalLoanValue: number;
}

export interface CreateKycDocumentRequest {
  leadId: string;
  documentType: string;
  reference: string;
  issuedOn: string | null;
  expiresOn: string | null;
  notes: string;
}

export interface CreateLoanRequest {
  leadId: string;
  product: string;
  amount: number;
  termMonths: number;
  interestRate: number;
}

export interface CreateTicketRequest {
  leadId: string | null;
  customerName: string;
  subject: string;
  description: string;
  category: string;
  priority: TicketPriority;
}

export interface UpdateTicketRequest {
  subject: string;
  description: string;
  category: string;
  priority: TicketPriority;
  status: TicketStatus;
  assignedTo: string;
  resolution: string;
}

export const KYC_STATUSES: KycStatus[] = ['NotStarted', 'Pending', 'Verified', 'Rejected', 'Expired'];
export const RISK_RATINGS: RiskRating[] = ['Low', 'Medium', 'High'];
export const KYC_DOCUMENT_TYPES = [
  'NationalId',
  'Passport',
  'ProofOfAddress',
  'IncomeStatement',
  'CommercialRegister',
  'TaxNumber',
];

export const LOAN_STATUSES: LoanStatus[] = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'Disbursed',
];
export const LOAN_PRODUCTS = [
  'WorkingCapital',
  'EquipmentFinance',
  'TradeFinance',
  'PersonalLoan',
  'MortgageLoan',
];

/** Mirrors the API's transition table so the UI only offers legal moves. */
export const LOAN_TRANSITIONS: Record<LoanStatus, LoanStatus[]> = {
  Draft: ['Submitted'],
  Submitted: ['UnderReview', 'Rejected'],
  UnderReview: ['Approved', 'Rejected'],
  Approved: ['Disbursed', 'Rejected'],
  Rejected: [],
  Disbursed: [],
};

export const TICKET_STATUSES: TicketStatus[] = ['Open', 'InProgress', 'Waiting', 'Resolved', 'Closed'];
export const TICKET_PRIORITIES: TicketPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
export const TICKET_CATEGORIES = [
  'Complaint',
  'Inquiry',
  'LoanIssue',
  'AccountIssue',
  'DocumentRequest',
  'Other',
];

export type AlertSeverity = 'Critical' | 'Warning' | 'Info';

export interface Alert {
  kind: string;
  severity: AlertSeverity;
  title: string;
  detail: string;
  entityType: string;
  entityId: string;
  link: string | null;
  dueAt: string | null;
}

export interface CountAndValue {
  label: string;
  count: number;
  value: number;
}

export interface OperationsReport {
  totalLoans: number;
  totalRequested: number;
  totalApproved: number;
  totalDisbursed: number;
  approvalRate: number;
  averageLoanSize: number;
  loansByStatus: CountAndValue[];
  loansByProduct: CountAndValue[];
  totalTickets: number;
  openTickets: number;
  breachedTickets: number;
  slaComplianceRate: number;
  averageResolutionHours: number;
  ticketsByCategory: CountAndValue[];
  ticketsByPriority: CountAndValue[];
  kycByStatus: CountAndValue[];
  customersFullyVerified: number;
}

export const EXPORT_DATASETS = ['leads', 'deals', 'loans', 'tickets', 'kyc', 'audit'];
