import { ChangeDetectionStrategy, Component, computed, inject, output, Signal } from '@angular/core';
import { DashboardFacade } from '../../application/facades/dashboard.facade';
import type { InstalledPlugin } from '../../domain/models/dashboard.models';

@Component({
  selector: 'cf-installed-plugins-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  template: `
    @if (isLoading()) {
      <div data-testid="loading" aria-busy="true" class="loading">Loading plugins…</div>
    } @else if (hasError()) {
      <div data-testid="error-message" role="alert" class="error">Failed to load plugins. Please try again.</div>
    } @else if (plugins().length === 0) {
      <div data-testid="empty-state" role="status" class="empty-state">No plugins installed yet.</div>
    } @else {
      <table data-testid="plugins-table" class="plugins-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Version</th>
            <th>Installed</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (plugin of plugins(); track plugin.name) {
            <tr>
              <td>{{ plugin.name }}</td>
              <td>{{ plugin.version }}</td>
              <td>{{ plugin.installedAt }}</td>
              <td>
                @if (plugin.status === 'update-available') {
                  <span data-testid="update-badge" class="update-badge">
                    Update available: {{ plugin.latestVersion }}
                  </span>
                } @else {
                  <span class="up-to-date">Up to date</span>
                }
              </td>
              <td>
                <button
                  data-testid="details-btn"
                  class="details-btn"
                  aria-label="Details {{ plugin.name }}"
                  (click)="onViewDetails(plugin.name)"
                >
                  Details
                </button>
                <button
                  data-testid="remove-btn"
                  class="remove-btn"
                  aria-label="Remove {{ plugin.name }}"
                  (click)="onRemove(plugin.name)"
                >
                  Remove
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class InstalledPluginsTableComponent {
  private readonly facade = inject(DashboardFacade);

  // Derived signals from facade
  readonly plugins: Signal<InstalledPlugin[]> = computed(() => this.facade.installedPlugins());
  readonly isLoading: Signal<boolean> = computed(() => this.facade.isLoading());
  readonly hasError: Signal<boolean> = computed(() => this.facade.error() !== undefined);
  readonly hasUpdates: Signal<boolean> = computed(() => this.facade.hasUpdates());

  // Outputs
  readonly removePlugin = output<string>();
  readonly viewDetails = output<string>();
  readonly updatePlugin = output<string>();

  onRemove(name: string): void {
    this.facade.removeInstalled(name);
    this.removePlugin.emit(name);
  }

  onViewDetails(name: string): void {
    this.viewDetails.emit(name);
  }

  onUpdate(name: string): void {
    this.updatePlugin.emit(name);
  }
}
