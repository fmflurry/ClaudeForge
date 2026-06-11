import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { AuditFacade } from '../../application/facades/audit.facade';

@Component({
  selector: 'cf-audit-log',
  standalone: true,
  providers: [AuditFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Audit Log</h1>
    <div class="filters">
      <select (change)="facade.setFilters({ type: $any($event.target).value })">
        <option value="">All Types</option>
        <option value="karma">Karma</option>
        <option value="analysis">Analysis</option>
        <option value="appeal">Appeal</option>
        <option value="approval">Approval</option>
      </select>
      <button (click)="facade.exportLogs('csv')">Export CSV</button>
      <button (click)="facade.exportLogs('json')">Export JSON</button>
    </div>
    @if (facade.isLoading()) {
      <p>Loading logs...</p>
    } @else if (facade.error(); as err) {
      <p style="color: var(--destructive)">{{ err[0].message }}</p>
    } @else {
      <table class="table">
        <thead>
          <tr><th>Timestamp</th><th>Type</th><th>Description</th><th>Actor</th></tr>
        </thead>
        <tbody>
          @for (log of facade.logs(); track log.timestamp) {
            <tr>
              <td>{{ log.timestamp }}</td>
              <td><span class="badge">{{ log.eventType }}</span></td>
              <td>{{ log.description }}</td>
              <td>{{ log.actorId ?? '-' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="4">No log entries found</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .filters { display: flex; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
    .table { width: 100%; border-collapse: collapse; }
    .table th, .table td { text-align: left; padding: 0.5rem; border-bottom: 1px solid var(--border); }
    .badge { background: var(--secondary); padding: 0.15rem 0.5rem; border-radius: 0.25rem; font-size: 0.8rem; }
    button { padding: 0.5rem 1rem; border: 1px solid var(--border); border-radius: 0.25rem; cursor: pointer; background: var(--primary); color: var(--primary-foreground); }
    select { padding: 0.5rem; border: 1px solid var(--border); border-radius: 0.25rem; }
  `],
})
export class AuditLogComponent implements OnInit {
  readonly facade = inject(AuditFacade);
  ngOnInit(): void { this.facade.loadLogs(); }
}
