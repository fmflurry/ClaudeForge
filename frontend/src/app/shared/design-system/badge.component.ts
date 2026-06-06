import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgClass } from '@angular/common';

export type BadgeVariant = 'default' | 'success' | 'warning' | 'error' | 'info';

@Component({
  selector: 'cf-badge',
  standalone: true,
  imports: [NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="cf-badge" [ngClass]="'cf-badge--' + variant()">
      <ng-content />
    </span>
  `,
  styles: [
    `
      .cf-badge {
        display: inline-flex;
        align-items: center;
        padding: 0.125rem 0.5rem;
        border-radius: 9999px;
        font-size: 0.75rem;
        font-weight: 500;
        line-height: 1.25rem;
      }

      .cf-badge--default {
        background: #e5e7eb;
        color: #374151;
      }

      .cf-badge--success {
        background: #d1fae5;
        color: #065f46;
      }

      .cf-badge--warning {
        background: #fef3c7;
        color: #92400e;
      }

      .cf-badge--error {
        background: #fee2e2;
        color: #991b1b;
      }

      .cf-badge--info {
        background: #dbeafe;
        color: #1e40af;
      }
    `,
  ],
})
export class BadgeComponent {
  readonly variant = input<BadgeVariant>('default');
}
