/**
 * Pure domain rules for grouping and recommending plugins.
 * Zero framework or infrastructure dependencies.
 */

import type { DashboardGroup, InstalledPlugin, RecommendedPlugin } from '../models/dashboard.models';

/**
 * Groups an array of installed plugins under a single DashboardGroup for the given team.
 * Returns a new object every call (immutable).
 */
export function groupPluginsByTeam(
  plugins: readonly InstalledPlugin[],
  teamId: string,
): DashboardGroup {
  return {
    teamId,
    plugins: [...plugins],
  };
}

/**
 * Derives the list of catalog plugins not already installed by the user.
 * Returns a new array every call (immutable).
 */
export function deriveRecommended(
  catalogPlugins: readonly { name: string; latestVersion: string | null }[],
  installedPlugins: readonly InstalledPlugin[],
  teamId: string,
): readonly RecommendedPlugin[] {
  const installedNames = new Set(installedPlugins.map((p) => p.name));

  return catalogPlugins
    .filter((c) => !installedNames.has(c.name))
    .map((c) => ({
      name: c.name,
      latestVersion: c.latestVersion,
      teamId,
    }));
}
