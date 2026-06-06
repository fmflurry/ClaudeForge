/**
 * RED tests — Task 15.1: Dashboard domain pure logic
 *
 * Expected production files (DO NOT exist yet — tests WILL FAIL to compile):
 *   src/app/features/dashboard/domain/models/dashboard.models.ts
 *   src/app/features/dashboard/domain/rules/dashboard-update.rules.ts
 *   src/app/features/dashboard/domain/rules/dashboard-grouping.rules.ts
 *
 * Production types the coder MUST define:
 *
 *   // dashboard.models.ts
 *   type PluginUpdateStatus = 'up-to-date' | 'update-available';
 *
 *   type InstalledPlugin = {
 *     readonly name: string;
 *     readonly version: string;
 *     readonly installedAt: string;
 *     readonly status: PluginUpdateStatus;
 *     readonly latestVersion: string | null;
 *   };
 *
 *   type DashboardGroup = {
 *     readonly teamId: string;
 *     readonly plugins: readonly InstalledPlugin[];
 *   };
 *
 *   type RecommendedPlugin = {
 *     readonly name: string;
 *     readonly latestVersion: string | null;
 *     readonly teamId: string;
 *   };
 *
 *   // dashboard-update.rules.ts
 *   function compareSemVer(a: string, b: string): number
 *     → returns negative if a < b, 0 if equal, positive if a > b
 *
 *   function computeUpdateStatus(installedVersion: string, latestVersion: string | null): PluginUpdateStatus
 *     → 'update-available' when latestVersion > installedVersion (semver), else 'up-to-date'
 *     → 'up-to-date' when latestVersion is null
 *
 *   function enrichInstalledPlugin(
 *     record: InstalledPluginRecord,
 *     latestVersion: string | null
 *   ): InstalledPlugin
 *     → returns an InstalledPlugin view-model with status derived from computeUpdateStatus
 *     → does NOT perform any filesystem or storage operations (intent model only)
 *
 *   // dashboard-grouping.rules.ts
 *   function groupPluginsByTeam(
 *     plugins: readonly InstalledPlugin[],
 *     teamId: string
 *   ): DashboardGroup
 *     → returns a single DashboardGroup for the given teamId
 *
 *   function deriveRecommended(
 *     catalogPlugins: readonly { name: string; latestVersion: string | null }[],
 *     installedPlugins: readonly InstalledPlugin[],
 *     teamId: string
 *   ): readonly RecommendedPlugin[]
 *     → returns catalog entries NOT already installed (by name)
 */

import type { InstalledPluginRecord } from '../../../../shared/domain/ports/installed-plugins-storage.port';
import type { InstalledPlugin, DashboardGroup, PluginUpdateStatus, RecommendedPlugin } from '../models/dashboard.models';
import {
  compareSemVer,
  computeUpdateStatus,
  enrichInstalledPlugin,
} from '../rules/dashboard-update.rules';
import {
  groupPluginsByTeam,
  deriveRecommended,
} from '../rules/dashboard-grouping.rules';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeRecord(overrides: Partial<InstalledPluginRecord> = {}): InstalledPluginRecord {
  return {
    name: 'my-plugin',
    version: '1.0.0',
    installedAt: '2024-01-01T00:00:00.000Z',
    ...overrides,
  };
}

function makeInstalled(overrides: Partial<InstalledPlugin> = {}): InstalledPlugin {
  return {
    name: 'my-plugin',
    version: '1.0.0',
    installedAt: '2024-01-01T00:00:00.000Z',
    status: 'up-to-date',
    latestVersion: '1.0.0',
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// compareSemVer — ordering
// ---------------------------------------------------------------------------

describe('compareSemVer', () => {
  it('returns 0 for identical versions', () => {
    expect(compareSemVer('1.0.0', '1.0.0')).toBe(0);
  });

  it('returns negative when a < b (patch)', () => {
    expect(compareSemVer('1.0.0', '1.0.1')).toBeLessThan(0);
  });

  it('returns positive when a > b (patch)', () => {
    expect(compareSemVer('1.0.1', '1.0.0')).toBeGreaterThan(0);
  });

  it('returns negative when a < b (minor)', () => {
    expect(compareSemVer('1.0.0', '1.1.0')).toBeLessThan(0);
  });

  it('returns positive when a > b (minor)', () => {
    expect(compareSemVer('1.2.0', '1.1.0')).toBeGreaterThan(0);
  });

  it('returns negative when a < b (major)', () => {
    expect(compareSemVer('1.0.0', '2.0.0')).toBeLessThan(0);
  });

  it('returns positive when a > b (major)', () => {
    expect(compareSemVer('3.0.0', '2.9.9')).toBeGreaterThan(0);
  });

  it('handles versions with leading zeros in segment', () => {
    // 1.0.10 > 1.0.9 numerically (not lexicographically)
    expect(compareSemVer('1.0.10', '1.0.9')).toBeGreaterThan(0);
  });

  it('handles version 0.0.1 vs 0.0.2', () => {
    expect(compareSemVer('0.0.1', '0.0.2')).toBeLessThan(0);
  });

  it('returns a number (not boolean, not undefined)', () => {
    const result = compareSemVer('1.0.0', '2.0.0');
    expect(typeof result).toBe('number');
  });
});

// ---------------------------------------------------------------------------
// computeUpdateStatus
// ---------------------------------------------------------------------------

describe('computeUpdateStatus', () => {
  it('returns "up-to-date" when installed equals latest', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.2.3', '1.2.3');
    expect(status).toBe('up-to-date');
  });

  it('returns "update-available" when latest > installed', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.0.0', '1.0.1');
    expect(status).toBe('update-available');
  });

  it('returns "update-available" for a major version bump', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.0.0', '2.0.0');
    expect(status).toBe('update-available');
  });

  it('returns "update-available" for a minor version bump', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.0.0', '1.1.0');
    expect(status).toBe('update-available');
  });

  it('returns "up-to-date" when installed is ahead of latest (should not happen but is safe)', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('2.0.0', '1.9.9');
    expect(status).toBe('up-to-date');
  });

  it('returns "up-to-date" when latestVersion is null', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.0.0', null);
    expect(status).toBe('up-to-date');
  });

  it('returns "up-to-date" when latestVersion is empty string', () => {
    const status: PluginUpdateStatus = computeUpdateStatus('1.0.0', '');
    expect(status).toBe('up-to-date');
  });

  it('returns one of the two valid union values', () => {
    const statuses: PluginUpdateStatus[] = ['up-to-date', 'update-available'];
    expect(statuses).toContain(computeUpdateStatus('1.0.0', '1.0.1'));
  });
});

// ---------------------------------------------------------------------------
// enrichInstalledPlugin — intent model only, no side-effects
// ---------------------------------------------------------------------------

describe('enrichInstalledPlugin', () => {
  it('maps record name to InstalledPlugin name', () => {
    const record = makeRecord({ name: 'alpha' });
    const result = enrichInstalledPlugin(record, '1.0.0');
    expect(result.name).toBe('alpha');
  });

  it('maps record version to InstalledPlugin version', () => {
    const record = makeRecord({ version: '0.9.0' });
    const result = enrichInstalledPlugin(record, '1.0.0');
    expect(result.version).toBe('0.9.0');
  });

  it('maps record installedAt to InstalledPlugin installedAt', () => {
    const record = makeRecord({ installedAt: '2024-06-01T00:00:00.000Z' });
    const result = enrichInstalledPlugin(record, '1.0.0');
    expect(result.installedAt).toBe('2024-06-01T00:00:00.000Z');
  });

  it('sets status "up-to-date" when installed equals latest', () => {
    const record = makeRecord({ version: '1.0.0' });
    const result = enrichInstalledPlugin(record, '1.0.0');
    expect(result.status).toBe('up-to-date');
  });

  it('sets status "update-available" when latest > installed', () => {
    const record = makeRecord({ version: '1.0.0' });
    const result = enrichInstalledPlugin(record, '2.0.0');
    expect(result.status).toBe('update-available');
  });

  it('sets status "up-to-date" when latestVersion is null', () => {
    const record = makeRecord({ version: '1.0.0' });
    const result = enrichInstalledPlugin(record, null);
    expect(result.status).toBe('up-to-date');
  });

  it('sets latestVersion field on the view-model', () => {
    const record = makeRecord({ version: '1.0.0' });
    const result = enrichInstalledPlugin(record, '2.0.0');
    expect(result.latestVersion).toBe('2.0.0');
  });

  it('sets latestVersion to null when null is passed', () => {
    const record = makeRecord({ version: '1.0.0' });
    const result = enrichInstalledPlugin(record, null);
    expect(result.latestVersion).toBeNull();
  });

  it('returns a new object each call (immutability)', () => {
    const record = makeRecord();
    const r1 = enrichInstalledPlugin(record, '1.0.0');
    const r2 = enrichInstalledPlugin(record, '1.0.0');
    expect(r1).not.toBe(r2);
  });

  it('does NOT mutate the source record', () => {
    const record = makeRecord();
    const originalName = record.name;
    enrichInstalledPlugin(record, '1.0.0');
    expect(record.name).toBe(originalName);
  });

  it('INTENT MODEL: result has no filesystem or HTTP side-effect fields', () => {
    const record = makeRecord();
    const result = enrichInstalledPlugin(record, '1.0.0');
    // Only the known shape fields should exist
    expect(typeof result.name).toBe('string');
    expect(typeof result.version).toBe('string');
    expect(typeof result.installedAt).toBe('string');
    expect(['up-to-date', 'update-available']).toContain(result.status);
  });
});

// ---------------------------------------------------------------------------
// groupPluginsByTeam
// ---------------------------------------------------------------------------

describe('groupPluginsByTeam', () => {
  it('returns a DashboardGroup with the given teamId', () => {
    const plugins = [makeInstalled({ name: 'alpha' }), makeInstalled({ name: 'beta' })];
    const group: DashboardGroup = groupPluginsByTeam(plugins, 'team-acme');
    expect(group.teamId).toBe('team-acme');
  });

  it('returns a DashboardGroup containing all provided plugins', () => {
    const plugins = [makeInstalled({ name: 'alpha' }), makeInstalled({ name: 'beta' })];
    const group = groupPluginsByTeam(plugins, 'team-acme');
    expect(group.plugins).toHaveLength(2);
  });

  it('returns plugins in the same order as input', () => {
    const plugins = [makeInstalled({ name: 'alpha' }), makeInstalled({ name: 'beta' })];
    const group = groupPluginsByTeam(plugins, 'team-acme');
    expect(group.plugins[0].name).toBe('alpha');
    expect(group.plugins[1].name).toBe('beta');
  });

  it('handles empty plugin list gracefully', () => {
    const group = groupPluginsByTeam([], 'team-empty');
    expect(group.teamId).toBe('team-empty');
    expect(group.plugins).toHaveLength(0);
  });

  it('returns a new object each call (immutability)', () => {
    const plugins = [makeInstalled()];
    const g1 = groupPluginsByTeam(plugins, 'team-a');
    const g2 = groupPluginsByTeam(plugins, 'team-a');
    expect(g1).not.toBe(g2);
  });

  it('does NOT mutate the input plugins array', () => {
    const plugins = [makeInstalled({ name: 'alpha' })];
    const originalLength = plugins.length;
    groupPluginsByTeam(plugins, 'team-a');
    expect(plugins).toHaveLength(originalLength);
  });

  it('handles a single plugin', () => {
    const plugin = makeInstalled({ name: 'only-one' });
    const group = groupPluginsByTeam([plugin], 'team-solo');
    expect(group.plugins).toHaveLength(1);
    expect(group.plugins[0].name).toBe('only-one');
  });
});

// ---------------------------------------------------------------------------
// deriveRecommended
// ---------------------------------------------------------------------------

describe('deriveRecommended', () => {
  it('returns catalog plugins not already installed', () => {
    const catalog = [
      { name: 'not-installed', latestVersion: '1.0.0' },
      { name: 'already-installed', latestVersion: '2.0.0' },
    ];
    const installed = [makeInstalled({ name: 'already-installed' })];
    const result: readonly RecommendedPlugin[] = deriveRecommended(catalog, installed, 'team-a');
    expect(result).toHaveLength(1);
    expect(result[0].name).toBe('not-installed');
  });

  it('returns empty array when all catalog plugins are installed', () => {
    const catalog = [{ name: 'plugin-a', latestVersion: '1.0.0' }];
    const installed = [makeInstalled({ name: 'plugin-a' })];
    const result = deriveRecommended(catalog, installed, 'team-a');
    expect(result).toHaveLength(0);
  });

  it('returns all catalog plugins when none are installed', () => {
    const catalog = [
      { name: 'plugin-a', latestVersion: '1.0.0' },
      { name: 'plugin-b', latestVersion: null },
    ];
    const result = deriveRecommended(catalog, [], 'team-a');
    expect(result).toHaveLength(2);
  });

  it('attaches the teamId to each recommended entry', () => {
    const catalog = [{ name: 'new-plugin', latestVersion: '3.0.0' }];
    const result = deriveRecommended(catalog, [], 'team-beta');
    expect(result[0].teamId).toBe('team-beta');
  });

  it('preserves the latestVersion from the catalog entry', () => {
    const catalog = [{ name: 'fresh', latestVersion: '4.2.0' }];
    const result = deriveRecommended(catalog, [], 'team-a');
    expect(result[0].latestVersion).toBe('4.2.0');
  });

  it('handles null latestVersion in catalog gracefully', () => {
    const catalog = [{ name: 'unknown-version', latestVersion: null }];
    const result = deriveRecommended(catalog, [], 'team-a');
    expect(result[0].latestVersion).toBeNull();
  });

  it('returns empty array when catalog is empty', () => {
    const result = deriveRecommended([], [makeInstalled()], 'team-a');
    expect(result).toHaveLength(0);
  });

  it('returns a new array each call (immutability)', () => {
    const catalog = [{ name: 'p', latestVersion: '1.0.0' }];
    const r1 = deriveRecommended(catalog, [], 'team-a');
    const r2 = deriveRecommended(catalog, [], 'team-a');
    expect(r1).not.toBe(r2);
  });

  it('does NOT mutate catalog or installed arrays', () => {
    const catalog = [{ name: 'p', latestVersion: '1.0.0' }];
    const installed = [makeInstalled({ name: 'other' })];
    const origCatalogLen = catalog.length;
    const origInstalledLen = installed.length;
    deriveRecommended(catalog, installed, 'team-a');
    expect(catalog).toHaveLength(origCatalogLen);
    expect(installed).toHaveLength(origInstalledLen);
  });
});
