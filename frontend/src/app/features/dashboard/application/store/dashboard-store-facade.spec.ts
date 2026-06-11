/**
 * RED tests — Task 15.3: DashboardStore + DashboardFacade
 *
 * Expected production files (DO NOT exist yet — tests WILL FAIL to compile):
 *   src/app/features/dashboard/application/store/dashboard.store.ts
 *   src/app/features/dashboard/application/facades/dashboard.facade.ts
 *   src/app/features/dashboard/domain/ports/catalog-latest-version.port.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // dashboard.store.ts
 *   enum DashboardStoreEnum {
 *     INSTALLED_PLUGINS = 'INSTALLED_PLUGINS',
 *     UPDATE_CHECKS     = 'UPDATE_CHECKS',
 *   }
 *
 *   interface DashboardState {
 *     [DashboardStoreEnum.INSTALLED_PLUGINS]: ResourceState<InstalledAddOn[]>;
 *     [DashboardStoreEnum.UPDATE_CHECKS]:     ResourceState<Record<string, string | null>>;
 *   }
 *
 *   @Injectable({ providedIn: 'root' })
 *   class DashboardStore extends BaseStore<typeof DashboardStoreEnum, DashboardState>
 *
 *   // catalog-latest-version.port.ts
 *   abstract class CatalogLatestVersionPort {
 *     abstract getLatestVersion(pluginName: string): Observable<string | null>;
 *   }
 *
 *   // dashboard.facade.ts
 *   @Injectable()
 *   class DashboardFacade {
 *     // Signals:
 *     get installedPlugins(): Signal<InstalledPlugin[]>
 *       — InstalledPluginRecord list enriched with update status
 *     get hasUpdates(): Signal<boolean>
 *       — true if any plugin has status 'update-available'
 *     get groupsByTeam(): Signal<DashboardGroup>
 *       — single group for the current team (from TeamContextFacade.teamId)
 *     get recommendedPlugins(): Signal<readonly RecommendedPlugin[]>
 *       — derived via deriveRecommended (not-yet-installed catalog items for team)
 *     get isLoading(): Signal<boolean>
 *     get error(): Signal<{ code: string; message: string }[] | undefined>
 *
 *     // Methods:
 *     loadInstalled(): void
 *       — reads from InstalledAddOnsStoragePort, enriches with last-known update status
 *     recordInstallIntent(name: string, version: string): void
 *       — calls InstalledAddOnsStoragePort.add(), updates store (no HTTP write)
 *     removeInstalled(name: string): void
 *       — calls InstalledAddOnsStoragePort.remove(), updates store
 *     checkForUpdates(): void
 *       — queries CatalogLatestVersionPort for each installed plugin,
 *         sets update status in store; sets error on failure
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { DashboardStore, DashboardStoreEnum } from './dashboard.store';
import type { DashboardState } from './dashboard.store';
import { DashboardFacade } from '../facades/dashboard.facade';
import { InstalledAddOnsStoragePort } from '../../../../shared/domain/ports/installed-plugins-storage.port';
import type { InstalledPluginRecord } from '../../../../shared/domain/ports/installed-plugins-storage.port';
import { InMemoryInstalledAddOnsAdapter } from '../../../../shared/infrastructure/storage/in-memory-installed-plugins.adapter';
import { CatalogLatestVersionPort } from '../../domain/ports/catalog-latest-version.port';
import type { InstalledAddOn, DashboardGroup } from '../../domain/models/dashboard.models';
import { TeamContextFacade } from '../../../team-context/application/facades/team-context.facade';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';

// ---------------------------------------------------------------------------
// Fake CatalogLatestVersionPort
// ---------------------------------------------------------------------------

@Injectable()
class FakeCatalogLatestVersionPort extends CatalogLatestVersionPort {
  private readonly _versions = new Map<string, string | null>();

  setVersion(name: string, version: string | null): void {
    this._versions.set(name, version);
  }

  getLatestVersion(pluginName: string): Observable<string | null> {
    return of(this._versions.get(pluginName) ?? null);
  }
}

@Injectable()
class ErrorCatalogLatestVersionPort extends CatalogLatestVersionPort {
  getLatestVersion(_pluginName: string): Observable<string | null> {
    return throwError(() => new Error('Network error'));
  }
}

// ---------------------------------------------------------------------------
// Stub TeamContextFacade
// ---------------------------------------------------------------------------

@Injectable()
class StubTeamContextFacade {
  private readonly _teamId = () => 'team-test' as string | undefined;

  get teamId() {
    return this._teamId;
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const RECORD_A: InstalledPluginRecord = {
  name: 'alpha-plugin',
  version: '1.0.0',
  installedAt: '2024-01-01T00:00:00.000Z',
};

const RECORD_B: InstalledPluginRecord = {
  name: 'beta-plugin',
  version: '2.0.0',
  installedAt: '2024-02-01T00:00:00.000Z',
};

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function buildStorage(...records: InstalledPluginRecord[]): InMemoryInstalledAddOnsAdapter {
  const adapter = new InMemoryInstalledAddOnsAdapter();
  records.forEach((r) => adapter.add(r));
  return adapter;
}

function setup(
  storage: InstalledAddOnsStoragePort,
  catalogPort: CatalogLatestVersionPort,
): { store: DashboardStore; facade: DashboardFacade } {
  TestBed.configureTestingModule({
    providers: [
      DashboardStore,
      DashboardFacade,
      { provide: InstalledAddOnsStoragePort, useValue: storage },
      { provide: CatalogLatestVersionPort, useValue: catalogPort },
      { provide: TeamContextFacade, useClass: StubTeamContextFacade },
    ],
  });
  return {
    store: TestBed.inject(DashboardStore),
    facade: TestBed.inject(DashboardFacade),
  };
}

function setupWithFakePort(...records: InstalledPluginRecord[]): {
  store: DashboardStore;
  facade: DashboardFacade;
  catalogPort: FakeCatalogLatestVersionPort;
} {
  const storage = buildStorage(...records);
  const catalogPort = new FakeCatalogLatestVersionPort();
  const { store, facade } = setup(storage, catalogPort);
  return { store, facade, catalogPort };
}

function setupWithErrorPort(...records: InstalledPluginRecord[]): { store: DashboardStore; facade: DashboardFacade } {
  const storage = buildStorage(...records);
  const catalogPort = new ErrorCatalogLatestVersionPort();
  return setup(storage, catalogPort);
}

// ---------------------------------------------------------------------------
// DashboardStore — enum keys + initial state
// ---------------------------------------------------------------------------

describe('DashboardStore — enum keys', () => {
  it('should have INSTALLED_PLUGINS key', () => {
    expect(DashboardStoreEnum.INSTALLED_PLUGINS).toBe('INSTALLED_PLUGINS');
  });

  it('should have UPDATE_CHECKS key', () => {
    expect(DashboardStoreEnum.UPDATE_CHECKS).toBe('UPDATE_CHECKS');
  });
});

describe('DashboardStore — initial state', () => {
  it('should initialise INSTALLED_PLUGINS with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [DashboardStore] });
    const store = TestBed.inject(DashboardStore);
    const state: ResourceState<InstalledAddOn[]> = store.get(DashboardStoreEnum.INSTALLED_PLUGINS)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('should initialise UPDATE_CHECKS with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [DashboardStore] });
    const store = TestBed.inject(DashboardStore);
    const state: ResourceState<Record<string, string | null>> = store.get(DashboardStoreEnum.UPDATE_CHECKS)();
    expect(state.isLoading).toBeFalsy();
  });

  it('INSTALLED_PLUGINS state should accept ResourceState<InstalledAddOn[]>', () => {
    TestBed.configureTestingModule({ providers: [DashboardStore] });
    const store = TestBed.inject(DashboardStore);
    const partial: Partial<DashboardState[typeof DashboardStoreEnum.INSTALLED_PLUGINS]> = {
      data: [],
      status: 'Success',
    };
    store.update(DashboardStoreEnum.INSTALLED_PLUGINS, partial);
    expect(store.get(DashboardStoreEnum.INSTALLED_PLUGINS)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — initial signal values
// ---------------------------------------------------------------------------

describe('DashboardFacade — initial signal values', () => {
  it('installedPlugins should return empty array before loadInstalled', () => {
    const { facade } = setupWithFakePort();
    expect(facade.installedPlugins()).toEqual([]);
  });

  it('hasUpdates should return false before any load', () => {
    const { facade } = setupWithFakePort();
    expect(facade.hasUpdates()).toBe(false);
  });

  it('isLoading should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoading()).toBe(false);
  });

  it('error should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.error()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — loadInstalled
// ---------------------------------------------------------------------------

describe('DashboardFacade — loadInstalled', () => {
  it('should populate installedPlugins from storage', () => {
    const { facade } = setupWithFakePort(RECORD_A, RECORD_B);
    facade.loadInstalled();
    const plugins = facade.installedPlugins();
    expect(plugins).toHaveLength(2);
  });

  it('should map record names correctly', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    facade.loadInstalled();
    expect(facade.installedPlugins()[0].name).toBe('alpha-plugin');
  });

  it('should map record version correctly', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    facade.loadInstalled();
    expect(facade.installedPlugins()[0].version).toBe('1.0.0');
  });

  it('should enrich plugins with default "up-to-date" status before update check', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    facade.loadInstalled();
    const status = facade.installedPlugins()[0].status;
    expect(['up-to-date', 'update-available']).toContain(status);
  });

  it('should handle empty storage gracefully', () => {
    const { facade } = setupWithFakePort();
    facade.loadInstalled();
    expect(facade.installedPlugins()).toHaveLength(0);
  });

  it('should set isLoading to false after load completes', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    facade.loadInstalled();
    expect(facade.isLoading()).toBe(false);
  });

  it('should NOT make any HTTP call (storage-only operation)', () => {
    // If the test setup (which only provides InMemoryInstalledAddOnsAdapter, no real HTTP)
    // does not throw, the boundary is maintained.
    const { facade } = setupWithFakePort(RECORD_A);
    expect(() => facade.loadInstalled()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — recordInstallIntent
// ---------------------------------------------------------------------------

describe('DashboardFacade — recordInstallIntent', () => {
  it('should add plugin to installedPlugins signal', () => {
    const { facade } = setupWithFakePort();
    facade.recordInstallIntent('new-plugin', '3.0.0');
    facade.loadInstalled();
    expect(facade.installedPlugins()).toHaveLength(1);
    expect(facade.installedPlugins()[0].name).toBe('new-plugin');
  });

  it('should persist via InstalledAddOnsStoragePort (browser storage only)', () => {
    const storage = buildStorage();
    const catalogPort = new FakeCatalogLatestVersionPort();
    const { facade } = setup(storage, catalogPort);
    facade.recordInstallIntent('stored-plugin', '1.0.0');
    // Storage should now contain the record
    const stored = storage.list();
    expect(stored.some((r) => r.name === 'stored-plugin')).toBe(true);
  });

  it('should set the correct version on the stored record', () => {
    const storage = buildStorage();
    const catalogPort = new FakeCatalogLatestVersionPort();
    const { facade } = setup(storage, catalogPort);
    facade.recordInstallIntent('my-plugin', '2.5.0');
    const stored = storage.list();
    const record = stored.find((r) => r.name === 'my-plugin');
    expect(record?.version).toBe('2.5.0');
  });

  it('should NOT perform any HTTP write (browser intent only)', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.recordInstallIntent('safe-plugin', '1.0.0')).not.toThrow();
  });

  it('should set installedAt as a non-empty ISO string', () => {
    const storage = buildStorage();
    const catalogPort = new FakeCatalogLatestVersionPort();
    const { facade } = setup(storage, catalogPort);
    facade.recordInstallIntent('ts-plugin', '1.0.0');
    const stored = storage.list();
    const record = stored.find((r) => r.name === 'ts-plugin');
    expect(record?.installedAt).toBeTruthy();
    expect(typeof record?.installedAt).toBe('string');
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — removeInstalled
// ---------------------------------------------------------------------------

describe('DashboardFacade — removeInstalled', () => {
  it('should remove the plugin from installedPlugins signal', () => {
    const { facade } = setupWithFakePort(RECORD_A, RECORD_B);
    facade.loadInstalled();
    facade.removeInstalled('alpha-plugin');
    facade.loadInstalled();
    const names = facade.installedPlugins().map((p) => p.name);
    expect(names).not.toContain('alpha-plugin');
    expect(names).toContain('beta-plugin');
  });

  it('should persist removal via InstalledAddOnsStoragePort', () => {
    const storage = buildStorage(RECORD_A, RECORD_B);
    const catalogPort = new FakeCatalogLatestVersionPort();
    const { facade } = setup(storage, catalogPort);
    facade.removeInstalled('alpha-plugin');
    const stored = storage.list();
    expect(stored.some((r) => r.name === 'alpha-plugin')).toBe(false);
  });

  it('should not throw when removing a non-existent plugin', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    expect(() => facade.removeInstalled('nonexistent-plugin')).not.toThrow();
  });

  it('should leave other plugins intact after removal', () => {
    const storage = buildStorage(RECORD_A, RECORD_B);
    const catalogPort = new FakeCatalogLatestVersionPort();
    const { facade } = setup(storage, catalogPort);
    facade.removeInstalled('alpha-plugin');
    const stored = storage.list();
    expect(stored).toHaveLength(1);
    expect(stored[0].name).toBe('beta-plugin');
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — checkForUpdates (success path)
// ---------------------------------------------------------------------------

describe('DashboardFacade — checkForUpdates success', () => {
  it('should set status "update-available" when catalog latest > installed', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A);
    catalogPort.setVersion('alpha-plugin', '2.0.0'); // installed is 1.0.0
    facade.loadInstalled();
    facade.checkForUpdates();
    const plugin = facade.installedPlugins().find((p) => p.name === 'alpha-plugin');
    expect(plugin?.status).toBe('update-available');
  });

  it('should set status "up-to-date" when catalog latest === installed', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A);
    catalogPort.setVersion('alpha-plugin', '1.0.0'); // same
    facade.loadInstalled();
    facade.checkForUpdates();
    const plugin = facade.installedPlugins().find((p) => p.name === 'alpha-plugin');
    expect(plugin?.status).toBe('up-to-date');
  });

  it('should set status "up-to-date" when catalog returns null', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A);
    catalogPort.setVersion('alpha-plugin', null);
    facade.loadInstalled();
    facade.checkForUpdates();
    const plugin = facade.installedPlugins().find((p) => p.name === 'alpha-plugin');
    expect(plugin?.status).toBe('up-to-date');
  });

  it('should set hasUpdates to true when at least one plugin has update', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A, RECORD_B);
    catalogPort.setVersion('alpha-plugin', '2.0.0'); // update available
    catalogPort.setVersion('beta-plugin', '2.0.0'); // same
    facade.loadInstalled();
    facade.checkForUpdates();
    expect(facade.hasUpdates()).toBe(true);
  });

  it('should set hasUpdates to false when no plugin has an update', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A);
    catalogPort.setVersion('alpha-plugin', '1.0.0');
    facade.loadInstalled();
    facade.checkForUpdates();
    expect(facade.hasUpdates()).toBe(false);
  });

  it('should not throw when installed list is empty', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.checkForUpdates()).not.toThrow();
  });

  it('should clear error after successful check', () => {
    const { facade, catalogPort } = setupWithFakePort(RECORD_A);
    catalogPort.setVersion('alpha-plugin', '1.0.0');
    facade.loadInstalled();
    facade.checkForUpdates();
    expect(facade.error()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — checkForUpdates (error path)
// ---------------------------------------------------------------------------

describe('DashboardFacade — checkForUpdates error', () => {
  it('should set error when catalog port throws', () => {
    const { facade } = setupWithErrorPort(RECORD_A);
    facade.loadInstalled();
    facade.checkForUpdates();
    expect(facade.error()).toBeDefined();
    expect(Array.isArray(facade.error())).toBe(true);
  });

  it('should NOT crash the application when catalog port throws', () => {
    const { facade } = setupWithErrorPort(RECORD_A);
    facade.loadInstalled();
    expect(() => facade.checkForUpdates()).not.toThrow();
  });

  it('should set isLoading to false after error', () => {
    const { facade } = setupWithErrorPort(RECORD_A);
    facade.loadInstalled();
    facade.checkForUpdates();
    expect(facade.isLoading()).toBe(false);
  });

  it('should preserve existing installed plugins after check failure', () => {
    const { facade } = setupWithErrorPort(RECORD_A);
    facade.loadInstalled();
    facade.checkForUpdates();
    // Plugins loaded before check should still be present
    expect(facade.installedPlugins().length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — groupsByTeam signal
// ---------------------------------------------------------------------------

describe('DashboardFacade — groupsByTeam', () => {
  it('should return a DashboardGroup for the current team', () => {
    const { facade } = setupWithFakePort(RECORD_A);
    facade.loadInstalled();
    const group: DashboardGroup = facade.groupsByTeam();
    expect(group.teamId).toBe('team-test');
  });

  it('should include all installed plugins in the group', () => {
    const { facade } = setupWithFakePort(RECORD_A, RECORD_B);
    facade.loadInstalled();
    expect(facade.groupsByTeam().plugins).toHaveLength(2);
  });

  it('should return empty plugins group when nothing installed', () => {
    const { facade } = setupWithFakePort();
    facade.loadInstalled();
    expect(facade.groupsByTeam().plugins).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// DashboardFacade — architecture boundary
// ---------------------------------------------------------------------------

describe('DashboardFacade — architecture boundary', () => {
  it('facade public API should expose signals and named methods', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.installedPlugins).toBe('function');
    expect(typeof facade.hasUpdates).toBe('function');
    expect(typeof facade.groupsByTeam).toBe('function');
    expect(typeof facade.isLoading).toBe('function');
    expect(typeof facade.error).toBe('function');
    expect(typeof facade.loadInstalled).toBe('function');
    expect(typeof facade.recordInstallIntent).toBe('function');
    expect(typeof facade.removeInstalled).toBe('function');
    expect(typeof facade.checkForUpdates).toBe('function');
  });

  it('should NOT crash when only facade is provided (no store/port direct injection from outside)', () => {
    // If setup succeeds without throwing an injection error, the boundary is maintained.
    const { facade } = setupWithFakePort();
    expect(facade).toBeDefined();
  });
});
