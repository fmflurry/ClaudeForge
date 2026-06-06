/**
 * RED tests for localStorage ports + adapters (Task 11.4)
 *
 * Expected production files (DO NOT exist yet — tests will fail to compile/resolve):
 *   src/app/shared/domain/ports/team-context-storage.port.ts
 *   src/app/shared/domain/ports/telemetry-preference.port.ts
 *   src/app/shared/domain/ports/installed-plugins-storage.port.ts
 *   src/app/shared/infrastructure/storage/local-storage-team-context.adapter.ts
 *   src/app/shared/infrastructure/storage/local-storage-telemetry-preference.adapter.ts
 *   src/app/shared/infrastructure/storage/local-storage-installed-plugins.adapter.ts
 *   src/app/shared/infrastructure/storage/in-memory-team-context.adapter.ts
 *   src/app/shared/infrastructure/storage/in-memory-telemetry-preference.adapter.ts
 *   src/app/shared/infrastructure/storage/in-memory-installed-plugins.adapter.ts
 *
 * Domain port interfaces the coder MUST implement:
 *
 *   TeamContextStoragePort (abstract class):
 *     abstract getTeamId(): string | null
 *     abstract setTeamId(id: string): void
 *     abstract clear(): void
 *     STORAGE_KEY = 'plugin-marketplace:team'
 *
 *   TelemetryPreferencePort (abstract class):
 *     abstract isDisabled(): boolean
 *     abstract setDisabled(disabled: boolean): void
 *     abstract clear(): void
 *     STORAGE_KEY = 'plugin-marketplace:telemetry-disabled'
 *
 *   InstalledPluginRecord:
 *     name: string
 *     version: string
 *     installedAt: string  (ISO date string)
 *
 *   InstalledPluginsStoragePort (abstract class):
 *     abstract list(): InstalledPluginRecord[]
 *     abstract add(record: InstalledPluginRecord): void
 *     abstract remove(name: string): void
 *     abstract clear(): void
 *     STORAGE_KEY = 'plugin-marketplace:installed'
 *
 *   LocalStorageTeamContextAdapter implements TeamContextStoragePort
 *   LocalStorageTelemetryPreferenceAdapter implements TelemetryPreferencePort
 *   LocalStorageInstalledPluginsAdapter implements InstalledPluginsStoragePort
 *
 *   InMemoryTeamContextAdapter implements TeamContextStoragePort
 *   InMemoryTelemetryPreferenceAdapter implements TelemetryPreferencePort
 *   InMemoryInstalledPluginsAdapter implements InstalledPluginsStoragePort
 */

import { TeamContextStoragePort } from '../../domain/ports/team-context-storage.port';
import { TelemetryPreferencePort } from '../../domain/ports/telemetry-preference.port';
import {
  InstalledPluginRecord,
  InstalledPluginsStoragePort,
} from '../../domain/ports/installed-plugins-storage.port';
import { LocalStorageTeamContextAdapter } from './local-storage-team-context.adapter';
import { LocalStorageTelemetryPreferenceAdapter } from './local-storage-telemetry-preference.adapter';
import { LocalStorageInstalledPluginsAdapter } from './local-storage-installed-plugins.adapter';
import { InMemoryTeamContextAdapter } from './in-memory-team-context.adapter';
import { InMemoryTelemetryPreferenceAdapter } from './in-memory-telemetry-preference.adapter';
import { InMemoryInstalledPluginsAdapter } from './in-memory-installed-plugins.adapter';

// ---------------------------------------------------------------------------
// Helpers — use jsdom's localStorage (available in vitest + jsdom environment)
// ---------------------------------------------------------------------------

function clearStorage(): void {
  window.localStorage.clear();
}

// ---------------------------------------------------------------------------
// Storage key constants
// ---------------------------------------------------------------------------

describe('Storage key constants', () => {
  it('TeamContextStoragePort.STORAGE_KEY should equal "plugin-marketplace:team"', () => {
    expect(TeamContextStoragePort.STORAGE_KEY).toBe('plugin-marketplace:team');
  });

  it('TelemetryPreferencePort.STORAGE_KEY should equal "plugin-marketplace:telemetry-disabled"', () => {
    expect(TelemetryPreferencePort.STORAGE_KEY).toBe('plugin-marketplace:telemetry-disabled');
  });

  it('InstalledPluginsStoragePort.STORAGE_KEY should equal "plugin-marketplace:installed"', () => {
    expect(InstalledPluginsStoragePort.STORAGE_KEY).toBe('plugin-marketplace:installed');
  });
});

// ---------------------------------------------------------------------------
// TeamContextStoragePort — LocalStorageTeamContextAdapter
// ---------------------------------------------------------------------------

describe('LocalStorageTeamContextAdapter', () => {
  let adapter: TeamContextStoragePort;

  beforeEach(() => {
    clearStorage();
    adapter = new LocalStorageTeamContextAdapter();
  });

  it('should return null when no team ID is set', () => {
    expect(adapter.getTeamId()).toBeNull();
  });

  it('should persist and retrieve a team ID', () => {
    adapter.setTeamId('frontend-team');
    expect(adapter.getTeamId()).toBe('frontend-team');
    // confirm the value is under the correct key
    expect(window.localStorage.getItem(TeamContextStoragePort.STORAGE_KEY)).toBe('frontend-team');
  });

  it('should overwrite the team ID when set again', () => {
    adapter.setTeamId('team-a');
    adapter.setTeamId('team-b');
    expect(adapter.getTeamId()).toBe('team-b');
  });

  it('should return null after clear()', () => {
    adapter.setTeamId('some-team');
    adapter.clear();
    expect(adapter.getTeamId()).toBeNull();
    expect(window.localStorage.getItem(TeamContextStoragePort.STORAGE_KEY)).toBeNull();
  });

  it('should not throw when clear() is called with no value stored', () => {
    expect(() => adapter.clear()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// TeamContextStoragePort — InMemoryTeamContextAdapter (swappability)
// ---------------------------------------------------------------------------

describe('InMemoryTeamContextAdapter (TeamContextStoragePort)', () => {
  let adapter: TeamContextStoragePort;

  beforeEach(() => {
    adapter = new InMemoryTeamContextAdapter();
  });

  it('should return null when nothing is set', () => {
    expect(adapter.getTeamId()).toBeNull();
  });

  it('should persist and retrieve a team ID in-memory', () => {
    adapter.setTeamId('in-memory-team');
    expect(adapter.getTeamId()).toBe('in-memory-team');
  });

  it('should return null after clear()', () => {
    adapter.setTeamId('team');
    adapter.clear();
    expect(adapter.getTeamId()).toBeNull();
  });

  it('should NOT touch window.localStorage', () => {
    clearStorage();
    adapter.setTeamId('isolated');
    expect(window.localStorage.getItem(TeamContextStoragePort.STORAGE_KEY)).toBeNull();
  });

  it('should be assignable to TeamContextStoragePort (structural compatibility)', () => {
    const port: TeamContextStoragePort = adapter;
    expect(port).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// TelemetryPreferencePort — LocalStorageTelemetryPreferenceAdapter
// ---------------------------------------------------------------------------

describe('LocalStorageTelemetryPreferenceAdapter', () => {
  let adapter: TelemetryPreferencePort;

  beforeEach(() => {
    clearStorage();
    adapter = new LocalStorageTelemetryPreferenceAdapter();
  });

  it('should return false (not disabled) when nothing is stored', () => {
    expect(adapter.isDisabled()).toBe(false);
  });

  it('should persist disabled=true', () => {
    adapter.setDisabled(true);
    expect(adapter.isDisabled()).toBe(true);
    expect(window.localStorage.getItem(TelemetryPreferencePort.STORAGE_KEY)).toBe('true');
  });

  it('should persist disabled=false', () => {
    adapter.setDisabled(true);
    adapter.setDisabled(false);
    expect(adapter.isDisabled()).toBe(false);
  });

  it('should return false after clear()', () => {
    adapter.setDisabled(true);
    adapter.clear();
    expect(adapter.isDisabled()).toBe(false);
    expect(window.localStorage.getItem(TelemetryPreferencePort.STORAGE_KEY)).toBeNull();
  });

  it('should not throw when clear() is called with no value stored', () => {
    expect(() => adapter.clear()).not.toThrow();
  });

  it('should handle corrupted value gracefully (return false, not throw)', () => {
    // Simulate corruption
    window.localStorage.setItem(TelemetryPreferencePort.STORAGE_KEY, 'NOT_A_BOOLEAN');
    expect(() => adapter.isDisabled()).not.toThrow();
    expect(adapter.isDisabled()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// TelemetryPreferencePort — InMemoryTelemetryPreferenceAdapter (swappability)
// ---------------------------------------------------------------------------

describe('InMemoryTelemetryPreferenceAdapter (TelemetryPreferencePort)', () => {
  let adapter: TelemetryPreferencePort;

  beforeEach(() => {
    adapter = new InMemoryTelemetryPreferenceAdapter();
  });

  it('should return false when not set', () => {
    expect(adapter.isDisabled()).toBe(false);
  });

  it('should persist in-memory', () => {
    adapter.setDisabled(true);
    expect(adapter.isDisabled()).toBe(true);
  });

  it('should return false after clear()', () => {
    adapter.setDisabled(true);
    adapter.clear();
    expect(adapter.isDisabled()).toBe(false);
  });

  it('should NOT touch window.localStorage', () => {
    clearStorage();
    adapter.setDisabled(true);
    expect(window.localStorage.getItem(TelemetryPreferencePort.STORAGE_KEY)).toBeNull();
  });

  it('should be assignable to TelemetryPreferencePort (structural compatibility)', () => {
    const port: TelemetryPreferencePort = adapter;
    expect(port).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// InstalledPluginsStoragePort — LocalStorageInstalledPluginsAdapter
// ---------------------------------------------------------------------------

describe('LocalStorageInstalledPluginsAdapter', () => {
  let adapter: InstalledPluginsStoragePort;
  const baseRecord: InstalledPluginRecord = {
    name: 'my-plugin',
    version: '1.0.0',
    installedAt: '2024-01-15T10:00:00.000Z',
  };

  beforeEach(() => {
    clearStorage();
    adapter = new LocalStorageInstalledPluginsAdapter();
  });

  it('should return empty array when nothing is stored', () => {
    expect(adapter.list()).toEqual([]);
  });

  it('should add a record and return it in list()', () => {
    adapter.add(baseRecord);
    expect(adapter.list()).toHaveLength(1);
    expect(adapter.list()[0]).toEqual(baseRecord);
  });

  it('should persist records across adapter instances (via real localStorage)', () => {
    adapter.add(baseRecord);
    // New instance reads from the same storage
    const adapter2 = new LocalStorageInstalledPluginsAdapter();
    expect(adapter2.list()).toHaveLength(1);
    expect(adapter2.list()[0].name).toBe('my-plugin');
  });

  it('should add multiple records without overwriting', () => {
    adapter.add({ name: 'plugin-a', version: '1.0.0', installedAt: '2024-01-01T00:00:00.000Z' });
    adapter.add({ name: 'plugin-b', version: '2.0.0', installedAt: '2024-01-02T00:00:00.000Z' });
    expect(adapter.list()).toHaveLength(2);
  });

  it('should remove a record by name', () => {
    adapter.add({ name: 'plugin-a', version: '1.0.0', installedAt: '2024-01-01T00:00:00.000Z' });
    adapter.add({ name: 'plugin-b', version: '2.0.0', installedAt: '2024-01-02T00:00:00.000Z' });
    adapter.remove('plugin-a');
    const remaining = adapter.list();
    expect(remaining).toHaveLength(1);
    expect(remaining[0].name).toBe('plugin-b');
  });

  it('should not throw when removing a name that does not exist', () => {
    expect(() => adapter.remove('nonexistent')).not.toThrow();
  });

  it('should return empty list after clear()', () => {
    adapter.add(baseRecord);
    adapter.clear();
    expect(adapter.list()).toEqual([]);
    expect(window.localStorage.getItem(InstalledPluginsStoragePort.STORAGE_KEY)).toBeNull();
  });

  it('should produce immutable list — mutating the returned array must not affect store', () => {
    adapter.add(baseRecord);
    const result = adapter.list();
    // Attempt to mutate
    (result as InstalledPluginRecord[]).push({ name: 'injected', version: '0.0.1', installedAt: '2024-01-01T00:00:00.000Z' });
    expect(adapter.list()).toHaveLength(1);
  });

  it('should handle corrupted JSON in localStorage gracefully (return [], not throw)', () => {
    window.localStorage.setItem(InstalledPluginsStoragePort.STORAGE_KEY, '{NOT VALID JSON}');
    expect(() => adapter.list()).not.toThrow();
    expect(adapter.list()).toEqual([]);
  });

  it('should handle missing key in localStorage gracefully (return [])', () => {
    window.localStorage.removeItem(InstalledPluginsStoragePort.STORAGE_KEY);
    expect(adapter.list()).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// InstalledPluginsStoragePort — InMemoryInstalledPluginsAdapter (swappability)
// ---------------------------------------------------------------------------

describe('InMemoryInstalledPluginsAdapter (InstalledPluginsStoragePort)', () => {
  let adapter: InstalledPluginsStoragePort;
  const record: InstalledPluginRecord = {
    name: 'test-plugin',
    version: '1.2.3',
    installedAt: '2024-06-01T00:00:00.000Z',
  };

  beforeEach(() => {
    adapter = new InMemoryInstalledPluginsAdapter();
  });

  it('should return empty array initially', () => {
    expect(adapter.list()).toEqual([]);
  });

  it('should add and list in-memory', () => {
    adapter.add(record);
    expect(adapter.list()).toHaveLength(1);
  });

  it('should remove by name', () => {
    adapter.add(record);
    adapter.remove('test-plugin');
    expect(adapter.list()).toHaveLength(0);
  });

  it('should return empty after clear()', () => {
    adapter.add(record);
    adapter.clear();
    expect(adapter.list()).toEqual([]);
  });

  it('should NOT touch window.localStorage', () => {
    clearStorage();
    adapter.add(record);
    expect(window.localStorage.getItem(InstalledPluginsStoragePort.STORAGE_KEY)).toBeNull();
  });

  it('should be assignable to InstalledPluginsStoragePort (structural compatibility)', () => {
    const port: InstalledPluginsStoragePort = adapter;
    expect(port).toBeDefined();
  });

  it('two instances should have isolated state', () => {
    const a = new InMemoryInstalledPluginsAdapter();
    const b = new InMemoryInstalledPluginsAdapter();
    a.add(record);
    expect(b.list()).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Port contracts — adapters implement ports (type-level verification)
// ---------------------------------------------------------------------------

describe('Port contract compliance', () => {
  it('LocalStorageTeamContextAdapter must implement TeamContextStoragePort', () => {
    const adapter = new LocalStorageTeamContextAdapter();
    expect(adapter).toBeInstanceOf(LocalStorageTeamContextAdapter);
    expect(typeof adapter.getTeamId).toBe('function');
    expect(typeof adapter.setTeamId).toBe('function');
    expect(typeof adapter.clear).toBe('function');
  });

  it('LocalStorageTelemetryPreferenceAdapter must implement TelemetryPreferencePort', () => {
    const adapter = new LocalStorageTelemetryPreferenceAdapter();
    expect(typeof adapter.isDisabled).toBe('function');
    expect(typeof adapter.setDisabled).toBe('function');
    expect(typeof adapter.clear).toBe('function');
  });

  it('LocalStorageInstalledPluginsAdapter must implement InstalledPluginsStoragePort', () => {
    const adapter = new LocalStorageInstalledPluginsAdapter();
    expect(typeof adapter.list).toBe('function');
    expect(typeof adapter.add).toBe('function');
    expect(typeof adapter.remove).toBe('function');
    expect(typeof adapter.clear).toBe('function');
  });
});
