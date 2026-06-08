import { ChangeDetectionStrategy, Component, computed, inject, output, Signal } from '@angular/core';
import { DashboardFacade } from '../../application/facades/dashboard.facade';
import type { InstalledPlugin } from '../../domain/models/dashboard.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-installed-plugins-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  template: `
    @if (isLoading()) {
      <div data-testid="loading" aria-busy="true" class="loading">{{ i18n.t('dashboard.loading') }}</div>
    } @else if (hasError()) {
      <div data-testid="error-message" role="alert" class="error">{{ i18n.t('dashboard.error-load') }}</div>
    } @else if (plugins().length === 0) {
      <div data-testid="empty-state" role="status" class="empty-state">{{ i18n.t('dashboard.empty-state') }}</div>
    } @else {
      <table data-testid="plugins-table" class="plugins-table">
        <thead>
          <tr>
            <th>{{ i18n.t('dashboard.col-name') }}</th>
            <th>{{ i18n.t('dashboard.col-version') }}</th>
            <th>{{ i18n.t('dashboard.col-installed') }}</th>
            <th>{{ i18n.t('dashboard.col-status') }}</th>
            <th>{{ i18n.t('dashboard.col-actions') }}</th>
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
                    {{ i18n.t('dashboard.update-available', { version: plugin.latestVersion }) }}
                  </span>
                } @else {
                  <span class="up-to-date">{{ i18n.t('dashboard.up-to-date') }}</span>
                }
              </td>
              <td>
                <button
                  data-testid="details-btn"
                  class="details-btn"
                  [attr.aria-label]="i18n.t('dashboard.details-btn-aria', { name: plugin.name })"
                  (click)="onViewDetails(plugin.name)"
                >
                  {{ i18n.t('dashboard.details-btn') }}
                </button>
                <button
                  data-testid="remove-btn"
                  class="remove-btn"
                  [attr.aria-label]="i18n.t('dashboard.remove-btn-aria', { name: plugin.name })"
                  (click)="onRemove(plugin.name)"
                >
                  {{ i18n.t('dashboard.remove-btn') }}
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
  protected readonly i18n = inject(I18nFacade);

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
