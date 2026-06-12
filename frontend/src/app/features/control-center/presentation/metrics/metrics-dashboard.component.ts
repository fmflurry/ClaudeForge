import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { MetricsFacade } from '../../application/facades/metrics.facade';

@Component({
  selector: 'cf-metrics-dashboard',
  standalone: true,
  providers: [MetricsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Metrics Dashboard</h1>
    @if (facade.isLoading()) {
      <p>Loading metrics...</p>
    } @else {
      <div class="stats-grid">
        <div class="stat-card">
          <h3>Analyzed</h3>
          <p class="stat-value">{{ facade.analysisMetrics().totalAnalyzed }}</p>
        </div>
        <div class="stat-card">
          <h3>Passed</h3>
          <p class="stat-value">{{ facade.analysisMetrics().totalPassed }}</p>
        </div>
        <div class="stat-card">
          <h3>Failed</h3>
          <p class="stat-value">{{ facade.analysisMetrics().totalFailed }}</p>
        </div>
        <div class="stat-card">
          <h3>In Review</h3>
          <p class="stat-value">{{ facade.analysisMetrics().totalInReview }}</p>
        </div>
      </div>

      <div class="section">
        <h2>Appeal Metrics</h2>
        <p><strong>Pending:</strong> {{ facade.appealMetrics().pendingAppeals }}</p>
        <p><strong>Avg Resolution:</strong> {{ facade.appealMetrics().avgResolutionTimeHours }}h</p>
      </div>

      <div class="section">
        <h2>Security Findings</h2>
        <select (change)="facade.dateRange.set($any($event.target).value)" [value]="facade.dateRange()">
          <option value="7d">Last 7 days</option>
          <option value="30d">Last 30 days</option>
          <option value="90d">Last 90 days</option>
        </select>
        @if (facade.securityMetrics().length > 0) {
          <table class="table">
            <thead>
              <tr>
                <th>Finding</th>
                <th>Count</th>
              </tr>
            </thead>
            <tbody>
              @for (f of facade.securityMetrics(); track f.finding) {
                <tr>
                  <td>{{ f.finding }}</td>
                  <td>{{ f.count }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
  `,
  styles: [
    `
      .stats-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
        gap: 1rem;
        margin-bottom: 2rem;
      }
      .stat-card {
        background: var(--card);
        border: 1px solid var(--border);
        border-radius: 0.5rem;
        padding: 1.25rem;
      }
      .stat-value {
        font-size: 2rem;
        font-weight: 700;
        margin: 0;
      }
      .section {
        margin-bottom: 2rem;
      }
      .table {
        width: 100%;
        border-collapse: collapse;
      }
      .table th,
      .table td {
        text-align: left;
        padding: 0.5rem;
        border-bottom: 1px solid var(--border);
      }
    `,
  ],
})
export class MetricsDashboardComponent implements OnInit {
  readonly facade = inject(MetricsFacade);
  ngOnInit(): void {
    this.facade.loadMetrics();
  }
}
