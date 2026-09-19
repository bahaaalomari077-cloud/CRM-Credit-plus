import { Component, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { CrmStore } from '../core/crm-store.service';
import { I18nService, Language } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { Theme, ThemeService } from '../core/theme.service';

@Component({
  selector: 'app-settings-page',
  imports: [TranslatePipe],
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  protected readonly store = inject(CrmStore);
  protected readonly i18n = inject(I18nService);
  protected readonly themes = inject(ThemeService);

  protected readonly apiUrl = environment.apiBaseUrl;
  protected readonly checking = signal(false);

  protected setLanguage(language: Language): void {
    this.i18n.setLanguage(language);
  }

  protected setTheme(theme: Theme): void {
    this.themes.setTheme(theme);
  }

  protected async checkConnection(): Promise<void> {
    this.checking.set(true);
    await this.store.refreshAll();
    this.checking.set(false);
  }
}
