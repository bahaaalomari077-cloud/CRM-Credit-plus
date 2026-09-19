import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, Routes } from '@angular/router';
import { I18nService, TranslationKey } from './core/i18n/i18n.service';

/** Tab titles follow the selected language. */
const title = (key: TranslationKey) => (_route: ActivatedRouteSnapshot) =>
  `${inject(I18nService).translate(key)} · Credit Plus CRM`;

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    title: title('nav.dashboard'),
    loadComponent: () => import('./pages/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'leads',
    title: title('nav.leads'),
    loadComponent: () => import('./pages/leads-page').then((m) => m.LeadsPage),
  },
  {
    path: 'deals',
    title: title('nav.deals'),
    loadComponent: () => import('./pages/deals-page').then((m) => m.DealsPage),
  },
  {
    path: 'activities',
    title: title('nav.activities'),
    loadComponent: () => import('./pages/activities-page').then((m) => m.ActivitiesPage),
  },
  {
    path: 'reports',
    title: title('nav.reports'),
    loadComponent: () => import('./pages/reports-page').then((m) => m.ReportsPage),
  },
  {
    path: 'settings',
    title: title('nav.settings'),
    loadComponent: () => import('./pages/settings-page').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
