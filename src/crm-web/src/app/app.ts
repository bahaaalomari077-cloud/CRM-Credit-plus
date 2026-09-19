import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CrmStore } from './core/crm-store.service';
import { I18nService, Language } from './core/i18n/i18n.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { ThemeService } from './core/theme.service';
import { TranslationKey } from './core/i18n/i18n.service';

interface NavItem {
  key: TranslationKey;
  icon: string;
  path: string;
  badge?: () => number;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly store = inject(CrmStore);
  protected readonly i18n = inject(I18nService);
  protected readonly themes = inject(ThemeService);
  private readonly router = inject(Router);

  protected readonly showQuickAdd = signal(false);
  protected readonly searchOpen = signal(false);

  protected readonly mainNav: NavItem[] = [
    { key: 'nav.dashboard', icon: '⌂', path: '/dashboard' },
    { key: 'nav.leads', icon: '◉', path: '/leads', badge: () => this.store.leads().length },
    { key: 'nav.deals', icon: '◇', path: '/deals', badge: () => this.store.deals().length },
    { key: 'nav.activities', icon: '✓', path: '/activities', badge: () => this.store.openActivities().length },
  ];

  protected readonly adminNav: NavItem[] = [
    { key: 'nav.reports', icon: '▥', path: '/reports' },
    { key: 'nav.settings', icon: '⚙', path: '/settings' },
  ];

  /** Search matches across all three collections and drives the results dropdown. */
  protected readonly searchResults = computed(() => {
    const term = this.store.search().trim().toLowerCase();
    if (term.length < 2) {
      return [];
    }

    const leads = this.store
      .leads()
      .filter((lead) => `${lead.fullName} ${lead.company} ${lead.email}`.toLowerCase().includes(term))
      .slice(0, 4)
      .map((lead) => ({ icon: '◉', label: lead.fullName, sub: lead.company, path: '/leads' }));

    const deals = this.store
      .deals()
      .filter((deal) => `${deal.name} ${deal.company} ${deal.contact}`.toLowerCase().includes(term))
      .slice(0, 4)
      .map((deal) => ({ icon: '◇', label: deal.name, sub: deal.company, path: '/deals' }));

    const activities = this.store
      .activities()
      .filter((activity) => `${activity.title} ${activity.relatedTo}`.toLowerCase().includes(term))
      .slice(0, 4)
      .map((activity) => ({ icon: '✓', label: activity.title, sub: activity.relatedTo, path: '/activities' }));

    return [...leads, ...deals, ...activities];
  });

  constructor() {
    void this.store.refreshAll();
  }

  protected onSearch(value: string): void {
    this.store.search.set(value);
    this.searchOpen.set(value.trim().length >= 2);
  }

  protected goTo(path: string): void {
    this.searchOpen.set(false);
    void this.router.navigateByUrl(path);
  }

  protected setLanguage(language: Language): void {
    this.i18n.setLanguage(language);
  }

  protected closeMenus(): void {
    this.showQuickAdd.set(false);
    this.searchOpen.set(false);
  }
}
