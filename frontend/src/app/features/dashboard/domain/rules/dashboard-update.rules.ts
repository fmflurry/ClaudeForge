/**
 * Pure domain rules for computing plugin update status.
 * Zero framework or infrastructure dependencies.
 */

import type { InstalledPluginRecord } from '../../../../shared/domain/ports/installed-plugins-storage.port';
import type { InstalledPlugin, PluginUpdateStatus } from '../models/dashboard.models';

/**
 * Compares two semver strings numerically, segment by segment.
 * Returns negative when a < b, 0 when equal, positive when a > b.
 */
export function compareSemVer(a: string, b: string): number {
  const aParts = a.split('.').map(Number);
  const bParts = b.split('.').map(Number);
  const length = Math.max(aParts.length, bParts.length);

  for (let i = 0; i < length; i++) {
    const aSegment = aParts[i] ?? 0;
    const bSegment = bParts[i] ?? 0;
    if (aSegment !== bSegment) {
      return aSegment - bSegment;
    }
  }

  return 0;
}

/**
 * Derives update status by comparing installed version against the latest catalog version.
 * Returns 'up-to-date' when latestVersion is null, empty, or not strictly greater.
 */
export function computeUpdateStatus(installedVersion: string, latestVersion: string | null): PluginUpdateStatus {
  if (!latestVersion) {
    return 'up-to-date';
  }

  return compareSemVer(latestVersion, installedVersion) > 0 ? 'update-available' : 'up-to-date';
}

/**
 * Maps an InstalledPluginRecord (storage model) to an InstalledPlugin view-model,
 * enriching it with the computed update status.
 * Pure function — no side-effects, returns a new object every call.
 */
export function enrichInstalledPlugin(record: InstalledPluginRecord, latestVersion: string | null): InstalledPlugin {
  return {
    name: record.name,
    version: record.version,
    installedAt: record.installedAt,
    status: computeUpdateStatus(record.version, latestVersion),
    latestVersion,
  };
}
