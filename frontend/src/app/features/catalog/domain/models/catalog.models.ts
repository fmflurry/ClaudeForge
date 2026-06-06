/**
 * Immutable domain models for the Catalog feature.
 *
 * Note: `type` aliases are used here (instead of `interface`) because the test
 * suite casts PluginSummary to `Record<string, unknown>` — a pattern that
 * TypeScript strict mode permits only for `type` aliases, not `interface`
 * declarations (TS2352: index signature missing in interface).
 * The ESLint consistent-type-definitions rule is disabled for this file only.
 */

/* eslint-disable @typescript-eslint/consistent-type-definitions */

export type PluginVersion = {
  readonly pluginId: string;
  readonly version: string;
  readonly isLatest: boolean;
  readonly downloadCount: number;
  readonly releaseNotes: string;
  readonly createdAt: Date;
};

export type PluginSummary = {
  readonly pluginId: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly author: string;
  readonly types: readonly string[];
  readonly languages: readonly string[];
  readonly useCaseTags: readonly string[];
  readonly downloadCount: number;
  readonly latestVersion: string | null;
  readonly createdAt: Date;
  readonly updatedAt: Date;
};

export type PluginDetail = PluginSummary & {
  readonly versions: readonly PluginVersion[];
};

export type CategoryValue = {
  readonly value: string;
  readonly displayName: string;
  readonly description: string;
  readonly count: number;
};

export type Categories = {
  readonly types: readonly CategoryValue[];
  readonly languages: readonly CategoryValue[];
  readonly useCases: readonly CategoryValue[];
};

export type PaginationMeta = {
  readonly totalCount: number;
  readonly page: number;
  readonly limit: number;
  readonly totalPages: number;
};
