import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { I18nService, Language } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { ThemeService } from '../core/theme.service';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly i18n = inject(I18nService);
  protected readonly themes = inject(ThemeService);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected setLanguage(language: Language): void {
    this.i18n.setLanguage(language);
  }

  protected async submit(): Promise<void> {
    if (this.busy() || !this.email().trim() || !this.password()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      await this.auth.login({ email: this.email().trim(), password: this.password() });
      // Return them to whatever they were trying to reach before signing in.
      const redirect = this.route.snapshot.queryParamMap.get('redirect') ?? '/dashboard';
      await this.router.navigateByUrl(redirect);
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.busy.set(false);
    }
  }

  private describe(failure: unknown): string {
    if (failure instanceof HttpErrorResponse) {
      if (failure.status === 0) {
        return this.i18n.translate('app.offline');
      }
      if (failure.status === 400 || failure.status === 401) {
        return this.i18n.translate('login.failed');
      }
      return failure.error?.detail ?? this.i18n.translate('login.failed');
    }
    return this.i18n.translate('login.failed');
  }
}
