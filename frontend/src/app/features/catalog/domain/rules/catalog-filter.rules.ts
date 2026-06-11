/**
 * Pure filter/sort rules for the Catalog domain.
 * No Angular or infrastructure dependencies — zero side effects.
 */

import type { ListAddOnsParams } from '../../../../shared/infrastructure/http/api-client.types';
import type { AddOnSummary } from '../models/catalog.models';

export interface CatalogFilterQuery {
  readonly types?: readonly string[];
  readonly languages?: readonly string[];
  readonly useCases?: readonly string[];
  readonly sort?: string;
  readonly order?: 'asc' | 'desc';
  readonly page?: number;
  readonly limit?: number;
}

export const DEFAULT_SORT = 'downloadCount';
export const DEFAULT_ORDER: 'asc' | 'desc' = 'desc';
export const DEFAULT_PAGE = 1;
export const DEFAULT_LIMIT = 20;

/**
 * Builds a complete CatalogFilterQuery by filling missing fields with defaults.
 * Returns a NEW object — never mutates the input.
 */
export function buildFilterQuery(partial: Partial<CatalogFilterQuery>): CatalogFilterQuery {
  return {
    types: partial.types ?? [],
    languages: partial.languages ?? [],
    useCases: partial.useCases ?? [],
    sort: partial.sort ?? DEFAULT_SORT,
    order: partial.order ?? DEFAULT_ORDER,
    page: partial.page ?? DEFAULT_PAGE,
    limit: partial.limit ?? DEFAULT_LIMIT,
  };
}

/**
 * Returns true when the plugin matches the query.
 * Logic: AND across dimensions, OR within each dimension.
 * Empty filter arrays are ignored (dimension is inactive).
 */
export function filterMatches(addOn: AddOnSummary, query: CatalogFilterQuery): boolean {
  const activeTypes = query.types ?? [];
  const activeLanguages = query.languages ?? [];
  const activeUseCases = query.useCases ?? [];

  if (activeTypes.length > 0) {
    const hasTypeMatch = activeTypes.some((t) => addOn.types.includes(t));
    if (!hasTypeMatch) return false;
  }

  if (activeLanguages.length > 0) {
    const hasLangMatch = activeLanguages.some((l) => addOn.languages.includes(l));
    if (!hasLangMatch) return false;
  }

  if (activeUseCases.length > 0) {
    const hasUseCaseMatch = activeUseCases.some((u) => addOn.useCaseTags.includes(u));
    if (!hasUseCaseMatch) return false;
  }

  return true;
}

/**
 * Returns the sort/order params from a query, falling back to defaults.
 * Returns a NEW object each call.
 */
export function composeSortParams(query: CatalogFilterQuery): {
  sort: string;
  order: 'asc' | 'desc';
} {
  return {
    sort: query.sort ?? DEFAULT_SORT,
    order: query.order ?? DEFAULT_ORDER,
  };
}

/**
 * Converts a CatalogFilterQuery to the wire ListAddOnsParams shape.
 * Returns a NEW object each call.
 */
export function toListAddOnsParams(query: CatalogFilterQuery): ListAddOnsParams {
  const sortParams = composeSortParams(query);
  const result: ListAddOnsParams = {
    page: query.page ?? DEFAULT_PAGE,
    limit: query.limit ?? DEFAULT_LIMIT,
    sort: sortParams.sort,
    order: sortParams.order,
  };

  const types = query.types ?? [];
  const languages = query.languages ?? [];
  const useCases = query.useCases ?? [];

  if (types.length > 0) {
    result.type = [...types];
  }

  if (languages.length > 0) {
    result.language = [...languages];
  }

  if (useCases.length > 0) {
    result.useCase = [...useCases];
  }

  return result;
}
