export type DealStage = 'Qualified' | 'Proposal' | 'Negotiation' | 'Won' | 'Lost';
export type LeadStatus = 'New' | 'Contacted' | 'Qualified' | 'Converted' | 'Lost';

export interface Lead {
  id: string;
  fullName: string;
  company: string;
  email: string;
  phone: string;
  source: string;
  status: LeadStatus;
  owner: string;
  createdAt: string;
  kycStatus: string;
  riskRating: string;
}

export interface Deal {
  id: string;
  name: string;
  company: string;
  contact: string;
  value: number;
  stage: DealStage;
  probability: number;
  expectedCloseDate: string;
  owner: string;
  createdAt: string;
}

export interface Activity {
  id: string;
  title: string;
  type: string;
  relatedTo: string;
  dueAt: string;
  isCompleted: boolean;
  owner: string;
  createdAt: string;
}

export interface PipelineStage {
  id: string;
  name: DealStage;
  value: number;
  dealCount: number;
  accent: string;
}

export interface DashboardSummary {
  newLeads: number;
  totalLeads: number;
  pipelineValue: number;
  wonThisMonth: number;
  conversionRate: number;
  openActivities: number;
  pipeline: PipelineStage[];
  deals: Deal[];
  activities: Activity[];
}

export interface CreateLeadRequest {
  fullName: string;
  company: string;
  email: string;
  phone: string;
  source: string;
}

export interface CreateDealRequest {
  name: string;
  company: string;
  contact: string;
  value: number;
  stage: DealStage;
  probability: number;
  expectedCloseDate: string;
}

export interface CreateActivityRequest {
  title: string;
  type: string;
  relatedTo: string;
  dueAt: string;
}

export const DEAL_STAGES: DealStage[] = ['Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];
export const OPEN_STAGES: DealStage[] = ['Qualified', 'Proposal', 'Negotiation'];
export const LEAD_STATUSES: LeadStatus[] = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];
export const ACTIVITY_TYPES = ['Call', 'Email', 'Meeting', 'Task'];
export const LEAD_SOURCES = ['Website', 'Referral', 'Campaign', 'Social media', 'Event'];
