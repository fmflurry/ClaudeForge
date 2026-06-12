import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppealsFacade } from '../../application/facades/appeals.facade';

@Component({
  selector: 'cf-appeals-dashboard',
  standalone: true,
  providers: [AppealsFacade],
  imports: [RouterLink, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Appeals</h1>
    @if (facade.isLoadingList()) {
      <p>Loading appeals...</p>
    } @else if (facade.error(); as err) {
      <p style="color: var(--destructive)">{{ err[0].message }}</p>
    } @else {
      <div class="stats-grid">
        <div class="stat-card">
          <h3>Approval Rate</h3>
          <p class="stat-value">{{ facade.approvalRate() }}%</p>
        </div>
      </div>
      <table class="table">
        <thead>
          <tr>
            <th>Plugin ID</th>
            <th>Reason</th>
            <th>Status</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (a of facade.pendingAppeals(); track a.appealId) {
            <tr>
              <td>{{ a.pluginId }}</td>
              <td>{{ a.reason }}</td>
              <td>{{ a.status }}</td>
              <td>{{ a.createdAt | date }}</td>
              <td><a [routerLink]="['/control-center/appeals', a.appealId]">View</a></td>
            </tr>
          } @empty {
            <tr>
              <td colspan="5">No appeals found</td>
            </tr>
          }
        </tbody>
      </table>
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
export class AppealsDashboardComponent implements OnInit {
  readonly facade = inject(AppealsFacade);
  ngOnInit(): void {
    this.facade.loadAppeals();
  }
}
