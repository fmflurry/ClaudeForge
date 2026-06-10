import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type { Notification, NotificationPreferences } from '../../domain/models/control-center.models';

@Injectable()
export class NotificationsFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get notifications(): Signal<Notification[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.NOTIFICATIONS)().data ?? []);
  }

  get unreadCount(): Signal<number> {
    return computed(() => this.notifications().filter((n) => !n.isRead).length);
  }

  get isLoading(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.NOTIFICATIONS)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.NOTIFICATIONS)().errors);
  }

  loadNotifications(unreadOnly?: boolean): void {
    this.store.startLoading(ControlCenterStoreEnum.NOTIFICATIONS);
    this.port.getNotifications(unreadOnly).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.store.update(ControlCenterStoreEnum.NOTIFICATIONS, {
          data: [...response.items],
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(ControlCenterStoreEnum.NOTIFICATIONS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'LOAD_ERROR', message }],
        });
      },
    });
  }

  markRead(notificationId: string): void {
    this.port.markNotificationRead(notificationId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadNotifications();
      },
    });
  }

  markAllRead(): void {
    this.port.markAllNotificationsRead().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadNotifications();
      },
    });
  }

  updatePreferences(prefs: NotificationPreferences): void {
    this.port.updateNotificationPreferences(prefs).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }
}
