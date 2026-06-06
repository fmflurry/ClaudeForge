/**
 * RED tests — Task 13.1 / 13.2: Pure search/discovery rules
 *
 * Expected production file (DO NOT exist yet — tests MUST FAIL to compile):
 *   src/app/features/search/domain/rules/search-filter.rules.ts
 *
 * Production exports the coder MUST define:
 *
 *   interface SearchFilterQuery {
 *     readonly keyword?: string;
 *     readonly types?: readonly string[];
 *     readonly languages?: readonly string[];
 *     readonly useCases?: readonly string[];
 *     readonly page?: number;
 *     readonly limit?: number;
 *   }
 *
 *   interface SearchNoResultsState {
 *     readonly isEmpty: boolean;
 *     readonly keyword: string;
 *     readonly suggestions: readonly string[];
 *     readonly message: string;
 *   }
 *
 *   const DEFAULT_SEARCH_PAGE: number   (= 1)
 *   const DEFAULT_SEARCH_LIMIT: number  (= 20)
 *
 *   function buildSearchQueryParams(query: SearchFilterQuery): SearchPluginsParams
 *     → converts SearchFilterQuery to wire SearchPluginsParams; returns NEW object
 *
 *   function buildDiscoveryQueryParams(criteria: DiscoveryCriteria): DiscoverPluginsParams
 *     → converts DiscoveryCriteria to wire DiscoverPluginsParams; returns NEW object
 *
 *   function combineSearchFilters(base: SearchFilterQuery, overrides: Partial<SearchFilterQuery>): SearchFilterQuery
 *     → merges overrides onto base; returns NEW object; never mutates inputs
 *
 *   function buildNoResultsState(keyword: string, suggestions: readonly string[]): SearchNoResultsState
 *     → returns a display state indicating empty results with context
 */

import {
  buildSearchQueryParams,
  buildDiscoveryQueryParams,
  combineSearchFilters,
  buildNoResultsState,
  DEFAULT_SEARCH_PAGE,
  DEFAULT_SEARCH_LIMIT,
} from './search-filter.rules';
import type { SearchFilterQuery, SearchNoResultsState } from './search-filter.rules';
import type { DiscoveryCriteria } from '../models/search.models';
import type {
  DiscoverPluginsParams,
  SearchPluginsParams,
} from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Default constants
// ---------------------------------------------------------------------------

describe('Search filter default constants', () => {
  it('DEFAULT_SEARCH_PAGE should be 1', () => {
    expect(DEFAULT_SEARCH_PAGE).toBe(1);
  });

  it('DEFAULT_SEARCH_LIMIT should be 20', () => {
    expect(DEFAULT_SEARCH_LIMIT).toBe(20);
  });
});

// ---------------------------------------------------------------------------
// buildSearchQueryParams
// ---------------------------------------------------------------------------

describe('buildSearchQueryParams — basic field mapping', () => {
  it('should map keyword to q', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ keyword: 'jest' });
    expect(params.q).toBe('jest');
  });

  it('should map page', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ page: 3 });
    expect(params.page).toBe(3);
  });

  it('should map limit', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ limit: 10 });
    expect(params.limit).toBe(10);
  });

  it('should use DEFAULT_SEARCH_PAGE when page is not provided', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({});
    expect(params.page).toBe(DEFAULT_SEARCH_PAGE);
  });

  it('should use DEFAULT_SEARCH_LIMIT when limit is not provided', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({});
    expect(params.limit).toBe(DEFAULT_SEARCH_LIMIT);
  });

  it('should map types filter', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ types: ['formatter', 'linter'] });
    expect(params.type).toEqual(['formatter', 'linter']);
  });

  it('should map languages filter', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ languages: ['typescript'] });
    expect(params.language).toEqual(['typescript']);
  });

  it('should map useCases filter', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ useCases: ['code-quality'] });
    expect(params.useCase).toEqual(['code-quality']);
  });

  it('should omit q when keyword is undefined', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({});
    expect(params.q).toBeUndefined();
  });

  it('should return a new object each call', () => {
    const q: SearchFilterQuery = { keyword: 'test' };
    expect(buildSearchQueryParams(q)).not.toBe(buildSearchQueryParams(q));
  });

  it('should not mutate the input query', () => {
    const q: SearchFilterQuery = { keyword: 'test', types: ['formatter'] };
    const keysBefore = Object.keys(q).length;
    buildSearchQueryParams(q);
    expect(Object.keys(q)).toHaveLength(keysBefore);
  });
});

describe('buildSearchQueryParams — empty filter arrays', () => {
  it('should omit or set empty type filter when types is empty array', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ types: [] });
    const typeVal = params.type;
    if (typeVal !== undefined) {
      expect(typeVal).toEqual([]);
    }
  });

  it('should omit or set empty language filter when languages is empty array', () => {
    const params: SearchPluginsParams = buildSearchQueryParams({ languages: [] });
    const langVal = params.language;
    if (langVal !== undefined) {
      expect(langVal).toEqual([]);
    }
  });
});

// ---------------------------------------------------------------------------
// buildDiscoveryQueryParams
// ---------------------------------------------------------------------------

describe('buildDiscoveryQueryParams', () => {
  it('should map keyword', () => {
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams({ keyword: 'linter' });
    expect(params.keyword).toBe('linter');
  });

  it('should map languages array', () => {
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams({ languages: ['python'] });
    expect(params.language).toEqual(['python']);
  });

  it('should map useCases array', () => {
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams({ useCases: ['testing'] });
    expect(params.useCase).toEqual(['testing']);
  });

  it('should map types array', () => {
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams({ types: ['linter'] });
    expect(params.type).toEqual(['linter']);
  });

  it('should omit keyword when not provided (undefined)', () => {
    const criteria: DiscoveryCriteria = { languages: ['typescript'] };
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams(criteria);
    expect(params.keyword).toBeUndefined();
  });

  it('should handle completely empty criteria', () => {
    const params: DiscoverPluginsParams = buildDiscoveryQueryParams({});
    expect(params).toBeDefined();
  });

  it('should return a new object each call', () => {
    const c: DiscoveryCriteria = { keyword: 'test' };
    expect(buildDiscoveryQueryParams(c)).not.toBe(buildDiscoveryQueryParams(c));
  });

  it('should not mutate the input criteria', () => {
    const c: DiscoveryCriteria = { keyword: 'test', languages: ['python'] };
    const keysBefore = Object.keys(c).length;
    buildDiscoveryQueryParams(c);
    expect(Object.keys(c)).toHaveLength(keysBefore);
  });
});

// ---------------------------------------------------------------------------
// combineSearchFilters — merging and immutability
// ---------------------------------------------------------------------------

describe('combineSearchFilters', () => {
  it('should return the base fields when overrides is empty', () => {
    const base: SearchFilterQuery = { keyword: 'test', page: 2, limit: 10 };
    const result = combineSearchFilters(base, {});
    expect(result.keyword).toBe('test');
    expect(result.page).toBe(2);
    expect(result.limit).toBe(10);
  });

  it('should override keyword', () => {
    const base: SearchFilterQuery = { keyword: 'old' };
    const result = combineSearchFilters(base, { keyword: 'new' });
    expect(result.keyword).toBe('new');
  });

  it('should override page', () => {
    const base: SearchFilterQuery = { keyword: 'test', page: 1 };
    const result = combineSearchFilters(base, { page: 5 });
    expect(result.page).toBe(5);
  });

  it('should override types filter', () => {
    const base: SearchFilterQuery = { types: ['formatter'] };
    const result = combineSearchFilters(base, { types: ['linter'] });
    expect(result.types).toEqual(['linter']);
  });

  it('should override languages filter', () => {
    const base: SearchFilterQuery = { languages: ['typescript'] };
    const result = combineSearchFilters(base, { languages: ['python'] });
    expect(result.languages).toEqual(['python']);
  });

  it('should not mutate the base query', () => {
    const base: SearchFilterQuery = { keyword: 'test', page: 1 };
    combineSearchFilters(base, { page: 99 });
    expect(base.page).toBe(1);
  });

  it('should not mutate the overrides object', () => {
    const overrides: Partial<SearchFilterQuery> = { page: 3 };
    combineSearchFilters({ keyword: 'test' }, overrides);
    expect(overrides.page).toBe(3);
  });

  it('should return a new object each call', () => {
    const base: SearchFilterQuery = { keyword: 'test' };
    const r1 = combineSearchFilters(base, {});
    const r2 = combineSearchFilters(base, {});
    expect(r1).not.toBe(r2);
  });

  it('should handle combining all filter dimensions', () => {
    const base: SearchFilterQuery = {
      keyword: 'test',
      types: ['formatter'],
      languages: ['typescript'],
      useCases: ['code-quality'],
      page: 1,
      limit: 20,
    };
    const overrides: Partial<SearchFilterQuery> = {
      types: ['linter'],
      page: 2,
    };
    const result = combineSearchFilters(base, overrides);
    expect(result.keyword).toBe('test');
    expect(result.types).toEqual(['linter']);
    expect(result.languages).toEqual(['typescript']);
    expect(result.useCases).toEqual(['code-quality']);
    expect(result.page).toBe(2);
    expect(result.limit).toBe(20);
  });
});

// ---------------------------------------------------------------------------
// buildNoResultsState — empty results display helper
// ---------------------------------------------------------------------------

describe('buildNoResultsState', () => {
  let result: SearchNoResultsState;

  beforeEach(() => {
    result = buildNoResultsState('eslint', ['formatter', 'linter']);
  });

  it('should set isEmpty to true', () => {
    expect(result.isEmpty).toBe(true);
  });

  it('should echo the keyword', () => {
    expect(result.keyword).toBe('eslint');
  });

  it('should include the provided suggestions', () => {
    expect(result.suggestions).toEqual(['formatter', 'linter']);
  });

  it('should provide a non-empty message string', () => {
    expect(result.message.length).toBeGreaterThan(0);
  });

  it('should handle empty suggestions array', () => {
    const r = buildNoResultsState('missing', []);
    expect(r.isEmpty).toBe(true);
    expect(r.suggestions).toEqual([]);
  });

  it('should handle empty keyword string', () => {
    const r = buildNoResultsState('', ['formatter']);
    expect(r.isEmpty).toBe(true);
    expect(r.keyword).toBe('');
  });

  it('should return a new object each call', () => {
    const r1 = buildNoResultsState('test', []);
    const r2 = buildNoResultsState('test', []);
    expect(r1).not.toBe(r2);
  });

  it('should produce independent suggestions array', () => {
    const suggestions = ['formatter', 'linter'];
    const r = buildNoResultsState('test', suggestions);
    (r.suggestions as string[]).push('injected');
    expect(suggestions).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Edge cases — special characters and Unicode in keyword
// ---------------------------------------------------------------------------

describe('buildSearchQueryParams — special character handling', () => {
  it('should pass through keyword with special characters unchanged', () => {
    const params = buildSearchQueryParams({ keyword: 'café & ☕' });
    expect(params.q).toBe('café & ☕');
  });

  it('should pass through keyword with SQL-like characters unchanged', () => {
    const params = buildSearchQueryParams({ keyword: "'; DROP TABLE plugins; --" });
    expect(params.q).toBe("'; DROP TABLE plugins; --");
  });

  it('should handle empty string keyword', () => {
    const params = buildSearchQueryParams({ keyword: '' });
    // An empty string keyword should be passed through (the port/HTTP layer will omit or send it)
    expect(params.q === '' || params.q === undefined).toBe(true);
  });
});
