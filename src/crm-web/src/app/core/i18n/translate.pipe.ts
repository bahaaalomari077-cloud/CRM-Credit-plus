import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService, TranslationKey } from './i18n.service';

/**
 * Usage: {{ 'nav.deals' | t }} or {{ 'deals.count' | t: { count: 4 } }}.
 * Impure so a language change re-renders every translated binding.
 */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(key: TranslationKey | string, params?: Record<string, string | number>): string {
    return this.i18n.translate(key as TranslationKey, params);
  }
}
