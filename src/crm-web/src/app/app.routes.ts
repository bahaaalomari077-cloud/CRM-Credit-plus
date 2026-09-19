import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    title: 'الرئيسية · Credit Plus CRM',
    loadComponent: () => import('./pages/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'leads',
    title: 'العملاء المحتملون · Credit Plus CRM',
    loadComponent: () => import('./pages/leads-page').then((m) => m.LeadsPage),
  },
  {
    path: 'deals',
    title: 'الصفقات · Credit Plus CRM',
    loadComponent: () => import('./pages/deals-page').then((m) => m.DealsPage),
  },
  {
    path: 'activities',
    title: 'الأنشطة · Credit Plus CRM',
    loadComponent: () => import('./pages/activities-page').then((m) => m.ActivitiesPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
