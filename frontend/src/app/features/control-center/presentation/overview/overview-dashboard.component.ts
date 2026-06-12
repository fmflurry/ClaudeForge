import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { OverviewFacade } from '../../application/facades/overview.facade';

@Component({
  selector: 'cf-overview-dashboard',
  standalone: true,
  providers: [OverviewFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Overview Dashboard</h1>
    @if (facade.isLoading()) {
      <p>Loading metrics...</p>
    } @else if (facade.error(); as err) {
      <p style="color: var(--destructive)">{{ err[0].message }}</p>
    } @else {
      <div class="stats-grid">
        <div class="stat-card">
          <h3>Total Analyzed</h3>
          <p class="stat-value">{{ facade.totalAnalyzed() }}</p>
        </div>
        <div class="stat-card">
          <h3>Pass Rate</h3>
          <p class="stat-value">{{ facade.passRate() }}%</p>
        </div>
        <div class="stat-card">
          <h3>Queue Length</h3>
          <p class="stat-value">{{ facade.queueLength() }}</p>
        </div>
        <div class="stat-card">
          <h3>Pending Appeals</h3>
          <p class="stat-value">{{ facade.pendingAppeals() }}</p>
        </div>
      </div>
      <div class="section">
        <h2>Recent Activity</h2>
        <p>{{ facade.recentAnalyses() }} analyses in last 24h</p>
      </div>
      @if (facade.topFindings().length > 0) {
        <div class="section">
          <h2>Top Findings</h2>
          <table class="table">
            <thead>
              <tr>
                <th>Finding</th>
                <th>Count</th>
              </tr>
            </thead>
            <tbody>
              @for (f of facade.topFindings(); track f.finding) {
                <tr>
                  <td>{{ f.finding }}</td>
                  <td>{{ f.count }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
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
export class OverviewDashboardComponent implements OnInit {
  readonly facade = inject(OverviewFacade);
  ngOnInit(): void {
    this.facade.loadMetrics();
  }
}
