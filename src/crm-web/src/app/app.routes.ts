import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, Routes } from '@angular/router';
import { ADMIN_ROLES, AUDIT_ROLES } from './core/auth.models';
import { authGuard, roleGuard } from './core/auth.guards';
import { I18nService, TranslationKey } from './core/i18n/i18n.service';

/** Tab titles follow the selected language. */
const title = (key: TranslationKey) => (_route: ActivatedRouteSnapshot) =>
  `${inject(I18nService).translate(key)} · Credit Plus CRM`;

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    title: title('login.title'),
    loadComponent: () => import('./pages/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'dashboard',
    title: title('nav.dashboard'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'leads',
    title: title('nav.leads'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/leads-page').then((m) => m.LeadsPage),
  },
  {
    path: 'deals',
    title: title('nav.deals'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/deals-page').then((m) => m.DealsPage),
  },
  {
    path: 'activities',
    title: title('nav.activities'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/activities-page').then((m) => m.ActivitiesPage),
  },
  {
    path: 'reports',
    title: title('nav.reports'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reports-page').then((m) => m.ReportsPage),
  },
  {
    path: 'audit',
    title: title('nav.audit'),
    canActivate: [roleGuard(AUDIT_ROLES)],
    loadComponent: () => import('./pages/audit-page').then((m) => m.AuditPage),
  },
  {
    path: 'users',
    title: title('nav.users'),
    canActivate: [roleGuard(ADMIN_ROLES)],
    loadComponent: () => import('./pages/users-page').then((m) => m.UsersPage),
  },
  {
    path: 'settings',
    title: title('nav.settings'),
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings-page').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
