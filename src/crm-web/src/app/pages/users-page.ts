import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../core/admin.service';
import { ROLES, Role, User } from '../core/auth.models';
import { AuthService } from '../core/auth.service';
import { LocaleDatePipe } from '../core/i18n/format.pipes';
import { I18nService, TranslationKey } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

interface UserForm {
  email: string;
  fullName: string;
  password: string;
  role: Role;
  isActive: boolean;
}

const blankUser = (): UserForm => ({
  email: '',
  fullName: '',
  password: '',
  role: 'Agent',
  isActive: true,
});

@Component({
  selector: 'app-users-page',
  imports: [FormsModule, TranslatePipe, LocaleDatePipe],
  templateUrl: './users-page.html',
})
export class UsersPage {
  private readonly admin = inject(AdminService);
  private readonly i18n = inject(I18nService);
  protected readonly auth = inject(AuthService);

  protected readonly roles = ROLES;
  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<UserForm>(blankUser());

  constructor() {
    void this.reload();
  }

  protected roleLabel(role: string): string {
    return this.i18n.translate(`role.${role}` as TranslationKey);
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.users.set(await this.admin.getUsers());
      this.error.set(null);
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.form.set(blankUser());
    this.error.set(null);
    this.showForm.set(true);
  }

  protected openEdit(user: User): void {
    this.editingId.set(user.id);
    this.form.set({
      email: user.email,
      fullName: user.fullName,
      password: '',
      role: user.role,
      isActive: user.isActive,
    });
    this.error.set(null);
    this.showForm.set(true);
  }

  protected update<K extends keyof UserForm>(field: K, value: UserForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.fullName.trim()) {
      return;
    }

    this.loading.set(true);
    try {
      const id = this.editingId();
      if (id) {
        await this.admin.updateUser(id, {
          fullName: form.fullName,
          role: form.role,
          isActive: form.isActive,
        });
      } else {
        await this.admin.createUser({
          email: form.email,
          fullName: form.fullName,
          password: form.password,
          role: form.role,
        });
      }
      this.showForm.set(false);
      this.error.set(null);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async remove(user: User): Promise<void> {
    if (!confirm(this.i18n.translate('common.confirmDelete', { name: user.fullName }))) {
      return;
    }

    try {
      await this.admin.deleteUser(user.id);
      await this.reload();
    } catch (failure) {
      this.error.set(this.describe(failure));
    }
  }

  private describe(failure: unknown): string {
    if (failure instanceof HttpErrorResponse) {
      return failure.error?.detail ?? failure.error?.title ?? `Request failed (${failure.status}).`;
    }
    return 'Unexpected error.';
  }
}
