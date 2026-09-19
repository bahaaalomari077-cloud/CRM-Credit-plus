import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../core/admin.service';
import { AUDIT_ACTIONS, AuditEntry } from '../core/auth.models';
import { LocaleDatePipe } from '../core/i18n/format.pipes';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

const ENTITY_TYPES = ['Lead', 'Deal', 'Activity', 'User', 'Email'];

@Component({
  selector: 'app-audit-page',
  imports: [FormsModule, TranslatePipe, LocaleDatePipe],
  templateUrl: './audit-page.html',
})
export class AuditPage {
  private readonly admin = inject(AdminService);
  private readonly i18n = inject(I18nService);

  protected readonly actions = AUDIT_ACTIONS;
  protected readonly entityTypes = ENTITY_TYPES;

  protected readonly entries = signal<AuditEntry[]>([]);
  protected readonly loading = signal(false);
  protected readonly actionFilter = signal('');
  protected readonly entityFilter = signal('');
  protected readonly expanded = signal<number | null>(null);

  constructor() {
    void this.reload();
  }

  protected actionLabel(action: string): string {
    return this.i18n.translate(`action.${action}` as TranslationKey);
  }

  protected entityLabel(entity: string): string {
    return this.i18n.translate(`entity.${entity}` as TranslationKey);
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.entries.set(
        await this.admin.getAudit({
          action: this.actionFilter() || undefined,
          entityType: this.entityFilter() || undefined,
          limit: 200,
        }),
      );
    } finally {
      this.loading.set(false);
    }
  }

  protected toggle(entry: AuditEntry): void {
    this.expanded.update((current) => (current === entry.id ? null : entry.id));
  }

  /** Pretty-prints the stored JSON diff; falls back to the raw text if it will not parse. */
  protected formatChanges(changes: string | null): string {
    if (!changes) {
      return '';
    }
    try {
      return JSON.stringify(JSON.parse(changes), null, 2);
    } catch {
      return changes;
    }
  }

  protected toneFor(action: string): string {
    if (action === 'Deleted' || action === 'LoginFailed') {
      return 'lost';
    }
    if (action === 'Created') {
      return 'converted';
    }
    return 'new';
  }
}
