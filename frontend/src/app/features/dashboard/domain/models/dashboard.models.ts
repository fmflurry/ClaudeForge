/**
 * Domain models for the Dashboard feature.
 * Pure types — no framework dependencies.
 */

export type PluginUpdateStatus = 'up-to-date' | 'update-available';

export interface InstalledPlugin {
  readonly name: string;
  readonly version: string;
  readonly installedAt: string;
  readonly status: PluginUpdateStatus;
  readonly latestVersion: string | null;
}

export interface DashboardGroup {
  readonly teamId: string;
  readonly plugins: readonly InstalledPlugin[];
}

export interface RecommendedPlugin {
  readonly name: string;
  readonly latestVersion: string | null;
  readonly teamId: string;
}
