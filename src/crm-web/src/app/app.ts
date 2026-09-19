import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { Role } from './core/auth.models';
import { AuthService } from './core/auth.service';
import { CrmStore } from './core/crm-store.service';
import { I18nService, Language, TranslationKey } from './core/i18n/i18n.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { ThemeService } from './core/theme.service';

interface NavItem {
  key: TranslationKey;
  icon: string;
  path: string;
  badge?: () => number;
  roles?: Role[];
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly store = inject(CrmStore);
  protected readonly auth = inject(AuthService);
  protected readonly i18n = inject(I18nService);
  protected readonly themes = inject(ThemeService);
  private readonly router = inject(Router);

  protected readonly showQuickAdd = signal(false);
  protected readonly showUserMenu = signal(false);
  protected readonly searchOpen = signal(false);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** The sign-in page renders on its own, without the sidebar or header. */
  protected readonly isLoginRoute = computed(() => this.url().startsWith('/login'));

  private readonly allNav: NavItem[] = [
    { key: 'nav.dashboard', icon: '⌂', path: '/dashboard' },
    { key: 'nav.leads', icon: '◉', path: '/leads', badge: () => this.store.leads().length },
    { key: 'nav.deals', icon: '◇', path: '/deals', badge: () => this.store.deals().length },
    { key: 'nav.activities', icon: '✓', path: '/activities', badge: () => this.store.openActivities().length },
    { key: 'nav.loans', icon: '⛁', path: '/loans' },
    { key: 'nav.tickets', icon: '⛉', path: '/tickets' },
  ];

  private readonly allAdminNav: NavItem[] = [
    { key: 'nav.reports', icon: '▥', path: '/reports' },
    { key: 'nav.audit', icon: '⧉', path: '/audit', roles: ['Manager', 'Admin'] },
    { key: 'nav.users', icon: '☷', path: '/users', roles: ['Admin'] },
    { key: 'nav.settings', icon: '⚙', path: '/settings' },
  ];

  protected readonly mainNav = computed(() => this.allNav.filter((item) => this.permitted(item)));
  protected readonly adminNav = computed(() => this.allAdminNav.filter((item) => this.permitted(item)));

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
    // Data loads only once a session exists; the guard handles the rest.
    void this.loadWhenSignedIn();
  }

  protected roleLabel(role: string): string {
    return this.i18n.translate(`role.${role}` as TranslationKey);
  }

  protected initials(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  protected onSearch(value: string): void {
    this.store.search.set(value);
    this.searchOpen.set(value.trim().length >= 2);
  }

  protected goTo(path: string): void {
    this.closeMenus();
    void this.router.navigateByUrl(path);
  }

  protected setLanguage(language: Language): void {
    this.i18n.setLanguage(language);
  }

  protected signOut(): void {
    this.closeMenus();
    this.auth.logout();
  }

  protected closeMenus(): void {
    this.showQuickAdd.set(false);
    this.showUserMenu.set(false);
    this.searchOpen.set(false);
  }

  private permitted(item: NavItem): boolean {
    if (!item.roles) {
      return true;
    }
    const role = this.auth.role();
    return role !== null && item.roles.includes(role);
  }

  private async loadWhenSignedIn(): Promise<void> {
    if (!this.auth.ready()) {
      await this.auth.restore();
    }
    if (this.auth.isAuthenticated()) {
      await this.store.refreshAll();
    }
  }
}
