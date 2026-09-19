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

/** Arabic labels for the API's English enum values, kept in one place. */
export const STAGE_LABELS: Record<DealStage, string> = {
  Qualified: 'مؤهلة',
  Proposal: 'عرض سعر',
  Negotiation: 'تفاوض',
  Won: 'مغلقة بنجاح',
  Lost: 'مغلقة خاسرة',
};

export const LEAD_STATUS_LABELS: Record<LeadStatus, string> = {
  New: 'جديد',
  Contacted: 'تم التواصل',
  Qualified: 'مؤهل',
  Converted: 'تم التحويل',
  Lost: 'خسارة',
};

export const ACTIVITY_TYPE_LABELS: Record<string, string> = {
  Call: 'مكالمة',
  Email: 'بريد',
  Meeting: 'اجتماع',
  Task: 'مهمة',
};

export const DEAL_STAGES: DealStage[] = ['Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];
export const OPEN_STAGES: DealStage[] = ['Qualified', 'Proposal', 'Negotiation'];
export const LEAD_STATUSES: LeadStatus[] = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];
export const ACTIVITY_TYPES = ['Call', 'Email', 'Meeting', 'Task'];
