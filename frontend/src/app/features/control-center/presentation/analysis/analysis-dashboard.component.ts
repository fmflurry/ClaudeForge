import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AnalysisFacade } from '../../application/facades/analysis.facade';

@Component({
  selector: 'cf-analysis-dashboard',
  standalone: true,
  providers: [AnalysisFacade],
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Analysis Pipeline</h1>
    @if (facade.isLoading()) {
      <p>Loading queue status...</p>
    } @else {
      <div class="stats-grid">
        <div class="stat-card">
          <h3>Queued</h3>
          <p class="stat-value">{{ facade.queueStatus().queued }}</p>
        </div>
        <div class="stat-card">
          <h3>Processing</h3>
          <p class="stat-value">{{ facade.queueStatus().processing }}</p>
        </div>
      </div>
      <p><a routerLink="/control-center">Back to Overview</a></p>
    }
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
    .stat-card { background: var(--card); border: 1px solid var(--border); border-radius: 0.5rem; padding: 1.25rem; }
    .stat-value { font-size: 2rem; font-weight: 700; margin: 0; }
  `],
})
export class AnalysisDashboardComponent implements OnInit {
  readonly facade = inject(AnalysisFacade);
  ngOnInit(): void { this.facade.loadQueue(); }
}
