import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CrmStore } from '../core/crm-store.service';
import { ACTIVITY_TYPES, ACTIVITY_TYPE_LABELS, Activity } from '../core/crm.models';

interface ActivityForm {
  title: string;
  type: string;
  relatedTo: string;
  dueAt: string;
}

/** datetime-local wants a local "YYYY-MM-DDTHH:mm" string, not an ISO/UTC one. */
const toLocalInput = (date: Date): string => {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const blankActivity = (): ActivityForm => {
  const due = new Date();
  due.setHours(due.getHours() + 1, 0, 0, 0);
  return { title: '', type: 'Call', relatedTo: '', dueAt: toLocalInput(due) };
};

@Component({
  selector: 'app-activities-page',
  imports: [FormsModule, DatePipe],
  templateUrl: './activities-page.html',
})
export class ActivitiesPage {
  protected readonly store = inject(CrmStore);
  protected readonly types = ACTIVITY_TYPES;

  protected readonly filter = signal<'all' | 'open' | 'done'>('open');
  protected readonly showForm = signal(false);
  protected readonly form = signal<ActivityForm>(blankActivity());

  protected readonly visible = computed(() => {
    const activities = this.store.activities();
    const filter = this.filter();
    if (filter === 'open') {
      return activities.filter((activity) => !activity.isCompleted);
    }
    if (filter === 'done') {
      return activities.filter((activity) => activity.isCompleted);
    }
    return activities;
  });

  protected readonly overdueCount = computed(() => {
    const now = Date.now();
    return this.store
      .activities()
      .filter((activity) => !activity.isCompleted && new Date(activity.dueAt).getTime() < now).length;
  });

  constructor() {
    void this.store.loadActivities();
  }

  protected typeLabel(type: string): string {
    return ACTIVITY_TYPE_LABELS[type] ?? type;
  }

  protected typeIcon(type: string): string {
    return type === 'Call' ? '☎' : type === 'Email' ? '✉' : type === 'Meeting' ? '◍' : '□';
  }

  protected isOverdue(activity: Activity): boolean {
    return !activity.isCompleted && new Date(activity.dueAt).getTime() < Date.now();
  }

  protected openCreate(): void {
    this.form.set(blankActivity());
    this.showForm.set(true);
  }

  protected update<K extends keyof ActivityForm>(field: K, value: ActivityForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(): Promise<void> {
    const form = this.form();
    if (!form.title.trim()) {
      return;
    }

    // The input yields a local time with no zone; let Date attach the browser's offset.
    const saved = await this.store.createActivity({
      ...form,
      dueAt: new Date(form.dueAt).toISOString(),
    });
    if (saved) {
      this.showForm.set(false);
    }
  }

  protected async toggle(activity: Activity): Promise<void> {
    await this.store.toggleActivity(activity);
  }

  protected async remove(activity: Activity): Promise<void> {
    if (confirm(`حذف المهمة "${activity.title}"؟`)) {
      await this.store.deleteActivity(activity.id);
    }
  }
}
