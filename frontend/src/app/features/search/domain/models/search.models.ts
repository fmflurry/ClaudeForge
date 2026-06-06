/**
 * Immutable domain models for the Search & Discovery feature.
 *
 * Note: `type` aliases are used here (instead of `interface`) because tests
 * may cast these to `Record<string, unknown>` — a pattern TypeScript strict
 * mode only permits for `type` aliases, not `interface` declarations.
 * The ESLint consistent-type-definitions rule is disabled for this file only.
 */

/* eslint-disable @typescript-eslint/consistent-type-definitions */

export type SearchResult = {
  readonly pluginId: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly author: string;
  readonly types: readonly string[];
  readonly languages: readonly string[];
  readonly useCases: readonly string[];
  readonly downloadCount: number;
  readonly latestVersion: string | null;
  readonly relevanceScore: number;
};

export type DiscoveryResult = {
  readonly pluginId: string;
  readonly name: string;
  readonly slug: string;
  readonly description: string;
  readonly author: string;
  readonly types: readonly string[];
  readonly languages: readonly string[];
  readonly matchedLanguages: readonly string[];
  readonly maturityIndicator: string;
  readonly relevanceScore: number;
  readonly lastUpdated?: string;
};

export type SearchResultsPage = {
  readonly items: readonly SearchResult[];
  readonly totalCount: number;
  readonly page: number;
  readonly limit: number;
  readonly totalPages: number;
  readonly categorySuggestions: readonly string[];
};

export type DiscoveryResults = {
  readonly items: readonly DiscoveryResult[];
  readonly criteriaEchoed: DiscoveryCriteria;
};

export type DiscoveryCriteria = {
  readonly keyword?: string;
  readonly languages?: readonly string[];
  readonly useCases?: readonly string[];
  readonly types?: readonly string[];
};
