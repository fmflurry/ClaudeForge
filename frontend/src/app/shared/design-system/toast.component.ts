import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass } from '@angular/common';

export type ToastVariant = 'success' | 'error' | 'warning' | 'info';

@Component({
  selector: 'cf-toast',
  standalone: true,
  imports: [NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="cf-toast"
      [ngClass]="'cf-toast--' + variant()"
      role="alert"
      aria-live="polite"
    >
      <span class="cf-toast__message">{{ message() }}</span>
      <button class="cf-toast__dismiss" aria-label="Dismiss" (click)="dismiss.emit()">
        &times;
      </button>
    </div>
  `,
  styles: [
    `
      .cf-toast {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        border-radius: 0.375rem;
        min-width: 20rem;
        box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
      }

      .cf-toast--success {
        background: #d1fae5;
        color: #065f46;
      }

      .cf-toast--error {
        background: #fee2e2;
        color: #991b1b;
      }

      .cf-toast--warning {
        background: #fef3c7;
        color: #92400e;
      }

      .cf-toast--info {
        background: #dbeafe;
        color: #1e40af;
      }

      .cf-toast__message {
        flex: 1;
        font-size: 0.875rem;
      }

      .cf-toast__dismiss {
        background: transparent;
        border: none;
        cursor: pointer;
        font-size: 1rem;
        color: inherit;
        opacity: 0.7;
        padding: 0;
        line-height: 1;
      }

      .cf-toast__dismiss:hover {
        opacity: 1;
      }
    `,
  ],
})
export class ToastComponent {
  readonly message = input.required<string>();
  readonly variant = input<ToastVariant>('info');
  readonly dismiss = output<void>();
}
