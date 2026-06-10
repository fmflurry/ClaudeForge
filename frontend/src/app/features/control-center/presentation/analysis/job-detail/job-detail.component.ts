import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'cf-job-detail',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Job Detail</h1>
    <p>Job ID: {{ jobId() }}</p>
    <p><a routerLink="/control-center/analysis">Back to Analysis</a></p>
  `,
})
export class JobDetailComponent {
  readonly jobId = input.required<string>();
}
