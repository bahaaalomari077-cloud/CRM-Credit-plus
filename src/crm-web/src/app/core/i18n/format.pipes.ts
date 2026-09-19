import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService } from './i18n.service';

type DateStyle = 'short' | 'time' | 'long' | 'dayTime';

const PRESETS: Record<DateStyle, Intl.DateTimeFormatOptions> = {
  short: { day: 'numeric', month: 'short' },
  long: { weekday: 'long', day: 'numeric', month: 'long' },
  time: { hour: 'numeric', minute: '2-digit' },
  dayTime: { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' },
};

/**
 * Locale-aware date formatting that follows a runtime language switch.
 * Impure because the underlying locale can change without the input changing.
 */
@Pipe({ name: 'ldate', pure: false })
export class LocaleDatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(value: string | Date | undefined, style: DateStyle = 'short'): string {
    return this.i18n.formatDate(value, PRESETS[style]);
  }
}

/** Locale-aware money formatting; see LocaleDatePipe for why it is impure. */
@Pipe({ name: 'lmoney', pure: false })
export class LocaleMoneyPipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(value: number): string {
    return this.i18n.formatMoney(value);
  }
}
