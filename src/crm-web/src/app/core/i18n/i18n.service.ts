import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { ar } from './ar';
import { en } from './en';

export type Language = 'ar' | 'en';
export type TranslationKey = keyof typeof ar;

const DICTIONARIES: Record<Language, Record<TranslationKey, string>> = { ar, en };
const STORAGE_KEY = 'crm.lang';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly document = inject(DOCUMENT);

  readonly language = signal<Language>(this.initial());
  readonly direction = computed<'rtl' | 'ltr'>(() => (this.language() === 'ar' ? 'rtl' : 'ltr'));
  readonly locale = computed(() => (this.language() === 'ar' ? 'ar-JO' : 'en-GB'));

  constructor() {
    // Keep <html lang/dir> in step so the browser applies the right text direction.
    effect(() => {
      const root = this.document.documentElement;
      root.setAttribute('lang', this.language());
      root.setAttribute('dir', this.direction());
      try {
        localStorage.setItem(STORAGE_KEY, this.language());
      } catch {
        // Private windows can refuse storage; the choice simply will not persist.
      }
    });
  }

  /** Looks up a key, substituting {placeholders} from params. */
  translate(key: TranslationKey, params?: Record<string, string | number>): string {
    const template = DICTIONARIES[this.language()][key] ?? key;
    if (!params) {
      return template;
    }

    return Object.entries(params).reduce(
      (text, [name, value]) => text.replaceAll(`{${name}}`, String(value)),
      template,
    );
  }

  /**
   * Angular's date/currency pipes read LOCALE_ID once per injector, so they cannot
   * follow a runtime language switch. These format through Intl against the current
   * signal instead, which makes every call reactive.
   */
  formatDate(value: string | Date | undefined, options: Intl.DateTimeFormatOptions): string {
    if (!value) {
      return '';
    }
    const date = value instanceof Date ? value : new Date(value);
    return new Intl.DateTimeFormat(this.locale(), options).format(date);
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat(this.locale(), {
      style: 'currency',
      currency: 'JOD',
      maximumFractionDigits: 0,
    }).format(value ?? 0);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale()).format(value ?? 0);
  }

  setLanguage(language: Language): void {
    this.language.set(language);
  }

  toggle(): void {
    this.language.set(this.language() === 'ar' ? 'en' : 'ar');
  }

  private initial(): Language {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'ar' || stored === 'en') {
        return stored;
      }
    } catch {
      // Fall through to the default.
    }
    return 'ar';
  }
}
