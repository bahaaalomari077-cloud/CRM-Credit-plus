export type Role = 'Viewer' | 'Agent' | 'Manager' | 'Admin';

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  role: Role;
}

export interface UpdateUserRequest {
  fullName: string;
  role: Role;
  isActive: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface AuditEntry {
  id: number;
  occurredAt: string;
  userId: string | null;
  userEmail: string;
  action: string;
  entityType: string;
  entityId: string;
  summary: string | null;
  changes: string | null;
  ipAddress: string | null;
}

export interface EmailMessage {
  id: string;
  toAddress: string;
  toName: string;
  subject: string;
  body: string;
  status: 'Queued' | 'Sent' | 'Failed';
  error: string | null;
  relatedType: string;
  relatedId: string | null;
  sentByUserId: string;
  sentByName: string;
  createdAt: string;
  sentAt: string | null;
}

export interface SendEmailRequest {
  toAddress: string;
  toName: string;
  subject: string;
  body: string;
  relatedType?: string;
  relatedId?: string | null;
}

export const ROLES: Role[] = ['Viewer', 'Agent', 'Manager', 'Admin'];

/** Mirrors the API's authorization policies so the UI hides what would be refused. */
export const WRITE_ROLES: Role[] = ['Agent', 'Manager', 'Admin'];
export const AUDIT_ROLES: Role[] = ['Manager', 'Admin'];
export const ADMIN_ROLES: Role[] = ['Admin'];

export const AUDIT_ACTIONS = [
  'Created',
  'Updated',
  'Deleted',
  'StageChanged',
  'LoggedIn',
  'LoginFailed',
  'EmailSent',
  'PasswordChanged',
];
