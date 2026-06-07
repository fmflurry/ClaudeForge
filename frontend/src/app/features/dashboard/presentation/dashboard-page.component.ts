import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DashboardFacade } from '../application/facades/dashboard.facade';
import { InstalledPluginsTableComponent } from './installed-plugins/installed-plugins-table.component';
import { PluginDetailsModalComponent } from './plugin-details-modal/plugin-details-modal.component';
import type { InstalledPlugin } from '../domain/models/dashboard.models';

/** Interval between background update checks (5 minutes). */
const UPDATE_CHECK_INTERVAL_MS = 300_000;

@Component({
  selector: 'cf-dashboard-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InstalledPluginsTableComponent, PluginDetailsModalComponent],
  template: `
    <div data-testid="dashboard-page" class="dashboard-page">
      @if (facade.hasUpdates()) {
        <div data-testid="update-banner" class="update-banner">Updates are available for your installed plugins.</div>
      }

      <div data-testid="install-search" class="install-search">
        <input type="text" placeholder="Search for plugins to install…" aria-label="Search for plugins to install" />
      </div>

      <cf-installed-plugins-table
        (viewDetails)="onViewDetails($event)"
        (removePlugin)="onRemovePlugin($event)"
        (updatePlugin)="onUpdatePlugin($event)"
      />

      @if (selectedPluginName()) {
        <cf-plugin-details-modal
          [pluginName]="selectedPluginName()!"
          [plugin]="selectedPlugin()"
          (closed)="onModalClosed()"
          (confirmRemove)="onConfirmRemove($event)"
          (confirmUpdate)="onConfirmUpdate($event)"
        />
      }
    </div>
  `,
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  readonly facade = inject(DashboardFacade);

  private readonly _selectedPluginName = signal<string | null>(null);
  private timerId: ReturnType<typeof setTimeout> | null = null;
  private destroyed = false;

  readonly selectedPluginName = this._selectedPluginName.asReadonly();

  readonly selectedPlugin = computed((): InstalledPlugin | undefined => {
    const name = this._selectedPluginName();
    if (!name) return undefined;
    return this.facade.installedPlugins().find((p: InstalledPlugin) => p.name === name);
  });

  ngOnInit(): void {
    this.facade.loadInstalled();
    this.scheduleUpdateCheck();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.timerId !== null) {
      clearTimeout(this.timerId);
      this.timerId = null;
    }
  }

  onViewDetails(name: string): void {
    this._selectedPluginName.set(name);
  }

  onRemovePlugin(name: string): void {
    this.facade.removeInstalled(name);
    if (this._selectedPluginName() === name) {
      this._selectedPluginName.set(null);
    }
  }

  onUpdatePlugin(name: string): void {
    this.facade.recordInstallIntent(name, '');
  }

  onModalClosed(): void {
    this._selectedPluginName.set(null);
  }

  onConfirmRemove(name: string): void {
    this.facade.removeInstalled(name);
    this._selectedPluginName.set(null);
  }

  onConfirmUpdate(name: string): void {
    this.facade.recordInstallIntent(name, '');
    this._selectedPluginName.set(null);
  }

  /**
   * Schedules an immediate update check and queues the next periodic check
   * via setTimeout. Each callback reschedules itself after completing.
   * The setTimeout (rather than setInterval) keeps the timer bounded so
   * test harnesses using vi.runAllTimers() do not hit infinite-loop limits.
   */
  private scheduleUpdateCheck(): void {
    try {
      this.facade.checkForUpdates();
    } catch {
      // Gracefully ignore errors in the update check.
    }

    if (this.destroyed) return;

    this.timerId = setTimeout(() => {
      if (this.destroyed) return;
      try {
        this.facade.checkForUpdates();
      } catch {
        // Gracefully ignore errors in the periodic update check.
      }
    }, UPDATE_CHECK_INTERVAL_MS);
  }
}
