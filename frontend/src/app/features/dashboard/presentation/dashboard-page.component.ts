import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cf-dashboard-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Dashboard</h2>`,
})
export class DashboardPageComponent {}
