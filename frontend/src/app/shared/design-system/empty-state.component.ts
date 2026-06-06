import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'cf-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-empty-state" role="status" aria-live="polite">
      <p class="cf-empty-state__message">{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: [
    `
      .cf-empty-state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 3rem 1rem;
        text-align: center;
        color: #6b7280;
      }

      .cf-empty-state__message {
        font-size: 0.875rem;
        margin: 0;
      }
    `,
  ],
})
export class EmptyStateComponent {
  readonly message = input.required<string>();
}
