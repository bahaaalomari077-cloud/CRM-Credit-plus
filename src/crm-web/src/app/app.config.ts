import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar-JO';
import localeEn from '@angular/common/locales/en-GB';
import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { I18nService } from './core/i18n/i18n.service';

registerLocaleData(localeAr);
registerLocaleData(localeEn);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
      // Binds route parameters to component input() signals.
      withComponentInputBinding(),
    ),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Restore any stored session before the first route resolves, so a refresh
    // does not bounce a signed-in user to the login page.
    provideAppInitializer(() => inject(AuthService).restore()),
    // Dates and currency follow the chosen language. Angular reads LOCALE_ID once per
    // pipe instance, so formatting goes through the ldate/lmoney pipes instead.
    { provide: LOCALE_ID, useFactory: () => inject(I18nService).locale() },
  ],
};
