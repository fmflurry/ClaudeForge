/**
 * Facade for the Dashboard domain.
 * Components interact with this facade only — no direct store, use-case, or port access.
 * Install intent is written to browser storage only (InstalledPluginsStoragePort).
 * No HTTP writes ever happen in this facade.
 */

import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { DashboardStore, DashboardStoreEnum } from '../store/dashboard.store';
import { InstalledPluginsStoragePort } from '../../../../shared/domain/ports/installed-plugins-storage.port';
import { CatalogLatestVersionPort } from '../../domain/ports/catalog-latest-version.port';
import { TeamContextFacade } from '../../../team-context/application/facades/team-context.facade';
import { enrichInstalledPlugin } from '../../domain/rules/dashboard-update.rules';
import { groupPluginsByTeam } from '../../domain/rules/dashboard-grouping.rules';
import type { DashboardGroup, InstalledPlugin, RecommendedPlugin } from '../../domain/models/dashboard.models';

@Injectable()
export class DashboardFacade {
  private readonly store = inject(DashboardStore);
  private readonly storagePort = inject(InstalledPluginsStoragePort);
  private readonly catalogPort = inject(CatalogLatestVersionPort);
  private readonly teamContextFacade = inject(TeamContextFacade);
  private readonly destroyRef = inject(DestroyRef);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  /** Enriched list of installed plugins with update status. */
  get installedPlugins(): Signal<InstalledPlugin[]> {
    return computed(() => this.store.get(DashboardStoreEnum.INSTALLED_PLUGINS)().data ?? []);
  }

  /** True when at least one installed plugin has status 'update-available'. */
  get hasUpdates(): Signal<boolean> {
    return computed(() => this.installedPlugins().some((p) => p.status === 'update-available'));
  }

  /** Single DashboardGroup for the currently active team. */
  get groupsByTeam(): Signal<DashboardGroup> {
    return computed(() => {
      const teamId = this.teamContextFacade.teamId() ?? 'unknown';
      return groupPluginsByTeam(this.installedPlugins(), teamId);
    });
  }

  /** Catalog plugins not yet installed — empty until catalog is loaded externally. */
  get recommendedPlugins(): Signal<readonly RecommendedPlugin[]> {
    return computed(() => []);
  }

  /** True while any async operation is in flight. */
  get isLoading(): Signal<boolean> {
    return computed(
      () =>
        (this.store.get(DashboardStoreEnum.INSTALLED_PLUGINS)().isLoading ?? false) ||
        (this.store.get(DashboardStoreEnum.UPDATE_CHECKS)().isLoading ?? false),
    );
  }

  /** Current error array, or undefined when no error. */
  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(
      () =>
        this.store.get(DashboardStoreEnum.INSTALLED_PLUGINS)().errors ??
        this.store.get(DashboardStoreEnum.UPDATE_CHECKS)().errors,
    );
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  /**
   * Reads installed plugin records from browser storage and enriches them
   * with the last-known update status from the UPDATE_CHECKS slice.
   * Pure storage read — no HTTP call.
   */
  loadInstalled(): void {
    const records = this.storagePort.list();
    const updateChecks = this.store.get(DashboardStoreEnum.UPDATE_CHECKS)().data ?? {};

    const plugins = records.map((r) => enrichInstalledPlugin(r, updateChecks[r.name] ?? null));

    this.store.update(DashboardStoreEnum.INSTALLED_PLUGINS, {
      data: plugins,
      status: 'Success',
      isLoading: false,
      errors: undefined,
    });
  }

  /**
   * Records an install intent in browser storage (localStorage adapter).
   * Does NOT write to the filesystem or make any HTTP request.
   */
  recordInstallIntent(name: string, version: string): void {
    this.storagePort.add({
      name,
      version,
      installedAt: new Date().toISOString(),
    });
  }

  /**
   * Removes an installed plugin from browser storage.
   */
  removeInstalled(name: string): void {
    this.storagePort.remove(name);
  }

  /**
   * Queries the CatalogLatestVersionPort for each installed plugin to determine
   * update availability. Updates the store with enriched status.
   * Gracefully handles errors — sets error state rather than throwing.
   */
  checkForUpdates(): void {
    const currentPlugins = this.installedPlugins();

    if (currentPlugins.length === 0) {
      this.store.update(DashboardStoreEnum.UPDATE_CHECKS, {
        data: {},
        status: 'Success',
        isLoading: false,
        errors: undefined,
      });
      return;
    }

    const versionRequests = currentPlugins.map((plugin) => this.catalogPort.getLatestVersion(plugin.name));

    forkJoin(versionRequests)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError((err: unknown) => {
          const message = err instanceof Error ? err.message : 'Update check failed';
          this.store.update(DashboardStoreEnum.UPDATE_CHECKS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'UPDATE_CHECK_ERROR', message }],
          });
          return of(null);
        }),
      )
      .subscribe((versions) => {
        if (versions === null) return; // error already handled in catchError

        const updateChecks: Record<string, string | null> = {};
        currentPlugins.forEach((plugin, index) => {
          updateChecks[plugin.name] = versions[index] ?? null;
        });

        this.store.update(DashboardStoreEnum.UPDATE_CHECKS, {
          data: updateChecks,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });

        // Re-enrich installed plugins with the new update checks.
        const records = this.storagePort.list();
        const enrichedPlugins = records.map((r) => enrichInstalledPlugin(r, updateChecks[r.name] ?? null));

        this.store.update(DashboardStoreEnum.INSTALLED_PLUGINS, {
          data: enrichedPlugins,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      });
  }
}
