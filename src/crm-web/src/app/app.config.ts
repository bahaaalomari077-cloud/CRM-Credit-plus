import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar-JO';
import localeEn from '@angular/common/locales/en-GB';
import { ApplicationConfig, LOCALE_ID, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { I18nService } from './core/i18n/i18n.service';

registerLocaleData(localeAr);
registerLocaleData(localeEn);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(),
    // Dates and currency follow the chosen language. Angular reads LOCALE_ID once per
    // pipe instance, so the shell recreates the routed view on a language change.
    { provide: LOCALE_ID, useFactory: () => inject(I18nService).locale() },
  ],
};
