/**
 * Domain models for the Dashboard feature.
 * Pure types — no framework dependencies.
 */

export type AddOnUpdateStatus = 'up-to-date' | 'update-available';

export interface InstalledAddOn {
  readonly name: string;
  readonly version: string;
  readonly installedAt: string;
  readonly status: AddOnUpdateStatus;
  readonly latestVersion: string | null;
}

export interface DashboardGroup {
  readonly teamId: string;
  readonly plugins: readonly InstalledAddOn[];
}

export interface RecommendedAddOn {
  readonly name: string;
  readonly latestVersion: string | null;
  readonly teamId: string;
}
