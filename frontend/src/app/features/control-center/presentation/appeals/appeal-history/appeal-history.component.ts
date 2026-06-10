import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cf-appeal-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Appeal History</h1>
    <p>History of resolved appeals with metrics will appear here.</p>
  `,
})
export class AppealHistoryComponent {}
