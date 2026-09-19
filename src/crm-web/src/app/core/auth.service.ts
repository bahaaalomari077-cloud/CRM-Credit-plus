import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ADMIN_ROLES,
  AUDIT_ROLES,
  ChangePasswordRequest,
  LoginRequest,
  LoginResponse,
  Role,
  User,
  WRITE_ROLES,
} from './auth.models';

const TOKEN_KEY = 'crm.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = environment.apiBaseUrl.replace(/\/crm$/, '');

  readonly user = signal<User | null>(null);
  readonly token = signal<string | null>(this.readToken());
  readonly ready = signal(false);

  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly role = computed<Role | null>(() => this.user()?.role ?? null);
  readonly canWrite = computed(() => this.hasRole(WRITE_ROLES));
  readonly canReadAudit = computed(() => this.hasRole(AUDIT_ROLES));
  readonly canManageUsers = computed(() => this.hasRole(ADMIN_ROLES));

  /** Restores a session from a stored token; called once before the first route resolves. */
  async restore(): Promise<void> {
    const token = this.token();
    if (!token) {
      this.ready.set(true);
      return;
    }

    try {
      this.user.set(await firstValueFrom(this.http.get<User>(`${this.base}/auth/me`)));
    } catch {
      // An expired or revoked token should not leave the app half signed-in.
      this.clear();
    } finally {
      this.ready.set(true);
    }
  }

  async login(request: LoginRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${this.base}/auth/login`, request),
    );
    this.store(response.accessToken);
    this.user.set(response.user);
  }

  changePassword(request: ChangePasswordRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.base}/auth/change-password`, request));
  }

  logout(): void {
    this.clear();
    void this.router.navigateByUrl('/login');
  }

  private hasRole(roles: Role[]): boolean {
    const role = this.user()?.role;
    return role !== undefined && roles.includes(role);
  }

  private store(token: string): void {
    this.token.set(token);
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch {
      // Private windows can refuse storage; the session then lasts this tab only.
    }
  }

  private clear(): void {
    this.token.set(null);
    this.user.set(null);
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      // Nothing to clean up if storage is unavailable.
    }
  }

  private readToken(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }
}
