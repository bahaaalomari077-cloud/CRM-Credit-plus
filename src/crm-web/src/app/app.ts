import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CrmStore } from './core/crm-store.service';

interface NavItem {
  name: string;
  icon: string;
  path: string;
  badge?: () => number;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly store = inject(CrmStore);
  protected readonly showQuickAdd = signal(false);

  protected readonly nav: NavItem[] = [
    { name: 'الرئيسية', icon: '⌂', path: '/dashboard' },
    { name: 'العملاء المحتملون', icon: '◉', path: '/leads', badge: () => this.store.leads().length },
    { name: 'الصفقات', icon: '◇', path: '/deals', badge: () => this.store.deals().length },
    { name: 'الأنشطة', icon: '✓', path: '/activities', badge: () => this.store.openActivities().length },
  ];

  constructor() {
    void this.store.refreshAll();
  }

  protected onSearch(value: string): void {
    this.store.search.set(value);
  }
}
