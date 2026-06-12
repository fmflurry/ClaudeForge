import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { NotificationsFacade } from '../../application/facades/notifications.facade';

@Component({
  selector: 'cf-notifications',
  standalone: true,
  providers: [NotificationsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="notif-bell"
      role="button"
      tabindex="0"
      (click)="open.set(!open())"
      (keydown.enter)="open.set(!open())"
      (keydown.space)="open.set(!open())"
    >
      <span>🔔</span>
      @if (facade.unreadCount() > 0) {
        <span class="badge">{{ facade.unreadCount() }}</span>
      }
    </div>
    @if (open()) {
      <div class="dropdown">
        <div class="dropdown-header">
          <h3>Notifications</h3>
          <button (click)="facade.markAllRead()" [disabled]="facade.unreadCount() === 0">Mark all read</button>
        </div>
        @if (facade.isLoading()) {
          <p>Loading...</p>
        } @else {
          @for (n of facade.notifications(); track n.id) {
            <div
              class="notif-item"
              [class.unread]="!n.isRead"
              role="button"
              tabindex="0"
              (click)="facade.markRead(n.id)"
              (keydown.enter)="facade.markRead(n.id)"
              (keydown.space)="facade.markRead(n.id)"
            >
              <p class="notif-title">{{ n.title }}</p>
              <p class="notif-msg">{{ n.message }}</p>
              <small>{{ n.createdAt }}</small>
            </div>
          } @empty {
            <p class="empty">No notifications</p>
          }
        }
      </div>
    }
  `,
  styles: [
    `
      .notif-bell {
        position: relative;
        cursor: pointer;
        font-size: 1.25rem;
      }
      .badge {
        position: absolute;
        top: -6px;
        right: -6px;
        background: var(--destructive);
        color: white;
        border-radius: 50%;
        width: 18px;
        height: 18px;
        font-size: 0.7rem;
        display: flex;
        align-items: center;
        justify-content: center;
      }
      .dropdown {
        position: absolute;
        top: 100%;
        right: 0;
        width: 320px;
        max-height: 400px;
        overflow-y: auto;
        background: var(--card);
        border: 1px solid var(--border);
        border-radius: 0.5rem;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
        z-index: 1000;
      }
      .dropdown-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.75rem;
        border-bottom: 1px solid var(--border);
      }
      .dropdown-header h3 {
        margin: 0;
      }
      .notif-item {
        padding: 0.75rem;
        border-bottom: 1px solid var(--border);
        cursor: pointer;
      }
      .notif-item:hover {
        background: var(--accent);
      }
      .notif-item.unread {
        font-weight: 600;
        background: var(--accent);
      }
      .notif-title {
        margin: 0;
        font-size: 0.875rem;
      }
      .notif-msg {
        margin: 0.25rem 0 0;
        font-size: 0.8rem;
        opacity: 0.8;
      }
      .empty {
        padding: 1rem;
        text-align: center;
        color: var(--muted-foreground);
      }
      button {
        font-size: 0.75rem;
        padding: 0.25rem 0.5rem;
        border: 1px solid var(--border);
        border-radius: 0.25rem;
        cursor: pointer;
      }
      small {
        font-size: 0.7rem;
        opacity: 0.6;
      }
    `,
  ],
})
export class NotificationsComponent implements OnInit {
  readonly facade = inject(NotificationsFacade);
  readonly open = signal(false);

  ngOnInit(): void {
    this.facade.loadNotifications();
  }
}
