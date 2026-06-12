/**
 * Pure filter rules for the Search & Discovery domain.
 * No Angular or infrastructure dependencies — zero side effects.
 */

import type { DiscoverAddOnsParams, SearchAddOnsParams } from '../../../../shared/infrastructure/http/api-client.types';
import type { DiscoveryCriteria } from '../models/search.models';

export interface SearchFilterQuery {
  readonly keyword?: string;
  readonly types?: readonly string[];
  readonly languages?: readonly string[];
  readonly useCases?: readonly string[];
  readonly page?: number;
  readonly limit?: number;
}

export interface SearchNoResultsState {
  readonly isEmpty: boolean;
  readonly keyword: string;
  readonly suggestions: readonly string[];
  readonly message: string;
}

export const DEFAULT_SEARCH_PAGE = 1;
export const DEFAULT_SEARCH_LIMIT = 20;

/**
 * Converts a SearchFilterQuery to the wire SearchAddOnsParams shape.
 * Returns a NEW object each call — never mutates inputs.
 */
export function buildSearchQueryParams(query: SearchFilterQuery): SearchAddOnsParams {
  const result: SearchAddOnsParams = {
    page: query.page ?? DEFAULT_SEARCH_PAGE,
    limit: query.limit ?? DEFAULT_SEARCH_LIMIT,
  };

  if (query.keyword !== undefined) {
    result.q = query.keyword;
  }

  if (query.types !== undefined) {
    result.type = [...query.types];
  }

  if (query.languages !== undefined) {
    result.language = [...query.languages];
  }

  if (query.useCases !== undefined) {
    result.useCase = [...query.useCases];
  }

  return result;
}

/**
 * Converts a DiscoveryCriteria to the wire DiscoverAddOnsParams shape.
 * Returns a NEW object each call — never mutates inputs.
 */
export function buildDiscoveryQueryParams(criteria: DiscoveryCriteria): DiscoverAddOnsParams {
  const result: DiscoverAddOnsParams = {};

  if (criteria.keyword !== undefined) {
    result.keyword = criteria.keyword;
  }

  if (criteria.languages !== undefined) {
    result.language = [...criteria.languages];
  }

  if (criteria.useCases !== undefined) {
    result.useCase = [...criteria.useCases];
  }

  if (criteria.types !== undefined) {
    result.type = [...criteria.types];
  }

  return result;
}

/**
 * Merges overrides onto a base SearchFilterQuery.
 * Returns a NEW object — never mutates inputs.
 */
export function combineSearchFilters(
  base: SearchFilterQuery,
  overrides: Partial<SearchFilterQuery>,
): SearchFilterQuery {
  return { ...base, ...overrides };
}

/**
 * Builds a display state for empty search results.
 * Returns a NEW object each call.
 */
export function buildNoResultsState(keyword: string, suggestions: readonly string[]): SearchNoResultsState {
  return {
    isEmpty: true,
    keyword,
    suggestions: [...suggestions],
    message: `No results found${keyword ? ` for "${keyword}"` : ''}. Try different search terms or explore suggestions.`,
  };
}
