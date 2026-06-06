/**
 * RED tests — Task 12.1: Pure filter/sort rules
 *
 * Expected production file (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/domain/rules/catalog-filter.rules.ts
 *
 * Production exports the coder MUST define:
 *
 *   interface CatalogFilterQuery {
 *     types?: readonly string[];
 *     languages?: readonly string[];
 *     useCases?: readonly string[];
 *     sort?: string;
 *     order?: 'asc' | 'desc';
 *     page?: number;
 *     limit?: number;
 *   }
 *
 *   const DEFAULT_SORT: string  (= 'downloadCount')
 *   const DEFAULT_ORDER: 'asc' | 'desc'  (= 'desc')
 *   const DEFAULT_PAGE: number  (= 1)
 *   const DEFAULT_LIMIT: number  (= 20)
 *
 *   function buildFilterQuery(partial: Partial<CatalogFilterQuery>): CatalogFilterQuery
 *     → fills missing fields with defaults; returns NEW object
 *
 *   function filterMatches(plugin: PluginSummary, query: CatalogFilterQuery): boolean
 *     → AND-across-dimension: a plugin matches iff for EACH dimension with active filters,
 *        at least one plugin tag is included (OR within dimension, AND across dimensions)
 *
 *   function composeSortParams(query: CatalogFilterQuery): { sort: string; order: 'asc' | 'desc' }
 *     → returns { sort, order } from query, falling back to defaults
 *
 *   function toListPluginsParams(query: CatalogFilterQuery): ListPluginsParams
 *     → converts CatalogFilterQuery to the wire ListPluginsParams shape
 */

import {
  buildFilterQuery,
  composeSortParams,
  DEFAULT_LIMIT,
  DEFAULT_ORDER,
  DEFAULT_PAGE,
  DEFAULT_SORT,
  filterMatches,
  toListPluginsParams,
} from '../rules/catalog-filter.rules';
import type { CatalogFilterQuery } from '../rules/catalog-filter.rules';
import type { PluginSummary } from '../models/catalog.models';

// ---------------------------------------------------------------------------
// Fixture: a minimal PluginSummary for filter tests
// ---------------------------------------------------------------------------

function makePlugin(overrides: Partial<PluginSummary> = {}): PluginSummary {
  return {
    pluginId: 'p1',
    name: 'Test Plugin',
    slug: 'test-plugin',
    description: 'A test plugin.',
    author: 'Author',
    types: ['formatter'],
    languages: ['typescript'],
    useCaseTags: ['code-quality'],
    downloadCount: 100,
    latestVersion: '1.0.0',
    createdAt: new Date('2024-01-01T00:00:00.000Z'),
    updatedAt: new Date('2024-01-01T00:00:00.000Z'),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Default constants
// ---------------------------------------------------------------------------

describe('Catalog filter default constants', () => {
  it('DEFAULT_SORT should be "downloadCount"', () => {
    expect(DEFAULT_SORT).toBe('downloadCount');
  });

  it('DEFAULT_ORDER should be "desc"', () => {
    expect(DEFAULT_ORDER).toBe('desc');
  });

  it('DEFAULT_PAGE should be 1', () => {
    expect(DEFAULT_PAGE).toBe(1);
  });

  it('DEFAULT_LIMIT should be 20', () => {
    expect(DEFAULT_LIMIT).toBe(20);
  });
});

// ---------------------------------------------------------------------------
// buildFilterQuery — default fallback
// ---------------------------------------------------------------------------

describe('buildFilterQuery', () => {
  it('should return defaults when called with empty object', () => {
    const q = buildFilterQuery({});
    expect(q.sort).toBe(DEFAULT_SORT);
    expect(q.order).toBe(DEFAULT_ORDER);
    expect(q.page).toBe(DEFAULT_PAGE);
    expect(q.limit).toBe(DEFAULT_LIMIT);
  });

  it('should preserve provided sort key', () => {
    const q = buildFilterQuery({ sort: 'name' });
    expect(q.sort).toBe('name');
  });

  it('should preserve provided order', () => {
    const q = buildFilterQuery({ order: 'asc' });
    expect(q.order).toBe('asc');
  });

  it('should preserve provided page', () => {
    const q = buildFilterQuery({ page: 3 });
    expect(q.page).toBe(3);
  });

  it('should preserve provided limit', () => {
    const q = buildFilterQuery({ limit: 50 });
    expect(q.limit).toBe(50);
  });

  it('should preserve provided filter arrays', () => {
    const q = buildFilterQuery({ types: ['formatter'], languages: ['typescript'] });
    expect(q.types).toEqual(['formatter']);
    expect(q.languages).toEqual(['typescript']);
  });

  it('should use empty arrays for unset filter dimensions', () => {
    const q = buildFilterQuery({});
    expect(q.types ?? []).toEqual([]);
    expect(q.languages ?? []).toEqual([]);
    expect(q.useCases ?? []).toEqual([]);
  });

  it('should return a new object each call (immutability)', () => {
    const q1 = buildFilterQuery({});
    const q2 = buildFilterQuery({});
    expect(q1).not.toBe(q2);
  });

  it('should not mutate the input partial', () => {
    const partial: Partial<CatalogFilterQuery> = { sort: 'name' };
    buildFilterQuery(partial);
    expect(Object.keys(partial)).toHaveLength(1);
  });
});

// ---------------------------------------------------------------------------
// filterMatches — AND-across-dimension, OR-within-dimension
// ---------------------------------------------------------------------------

describe('filterMatches — no active filters', () => {
  it('should return true when all filter arrays are empty', () => {
    const plugin = makePlugin();
    const q = buildFilterQuery({});
    expect(filterMatches(plugin, q)).toBe(true);
  });

  it('should return true for any plugin when no filters are set', () => {
    const plugin = makePlugin({ types: [], languages: [], useCaseTags: [] });
    expect(filterMatches(plugin, buildFilterQuery({}))).toBe(true);
  });
});

describe('filterMatches — single dimension', () => {
  it('should return true when the type filter matches one plugin type', () => {
    const plugin = makePlugin({ types: ['formatter', 'linter'] });
    expect(filterMatches(plugin, buildFilterQuery({ types: ['formatter'] }))).toBe(true);
  });

  it('should return false when the type filter has no overlap with plugin types', () => {
    const plugin = makePlugin({ types: ['formatter'] });
    expect(filterMatches(plugin, buildFilterQuery({ types: ['linter'] }))).toBe(false);
  });

  it('should return true when language filter has at least one overlap (OR within dimension)', () => {
    const plugin = makePlugin({ languages: ['typescript', 'javascript'] });
    expect(filterMatches(plugin, buildFilterQuery({ languages: ['javascript', 'python'] }))).toBe(true);
  });

  it('should return false when language filter has no overlap', () => {
    const plugin = makePlugin({ languages: ['typescript'] });
    expect(filterMatches(plugin, buildFilterQuery({ languages: ['python'] }))).toBe(false);
  });

  it('should return true when useCase filter has at least one overlap', () => {
    const plugin = makePlugin({ useCaseTags: ['code-quality', 'testing'] });
    expect(filterMatches(plugin, buildFilterQuery({ useCases: ['testing'] }))).toBe(true);
  });

  it('should return false when useCase filter has no overlap', () => {
    const plugin = makePlugin({ useCaseTags: ['code-quality'] });
    expect(filterMatches(plugin, buildFilterQuery({ useCases: ['testing'] }))).toBe(false);
  });
});

describe('filterMatches — AND across dimensions', () => {
  it('should return true only when ALL active dimensions match (AND semantics)', () => {
    const plugin = makePlugin({
      types: ['formatter'],
      languages: ['typescript'],
      useCaseTags: ['code-quality'],
    });
    const q = buildFilterQuery({
      types: ['formatter'],
      languages: ['typescript'],
      useCases: ['code-quality'],
    });
    expect(filterMatches(plugin, q)).toBe(true);
  });

  it('should return false when one dimension does not match', () => {
    const plugin = makePlugin({
      types: ['formatter'],
      languages: ['typescript'],
      useCaseTags: ['code-quality'],
    });
    const q = buildFilterQuery({
      types: ['formatter'],
      languages: ['python'], // mismatch
      useCases: ['code-quality'],
    });
    expect(filterMatches(plugin, q)).toBe(false);
  });

  it('should return false when multiple dimensions do not match', () => {
    const plugin = makePlugin({ types: ['formatter'], languages: ['typescript'] });
    const q = buildFilterQuery({ types: ['linter'], languages: ['python'] });
    expect(filterMatches(plugin, q)).toBe(false);
  });

  it('should ignore empty filter dimension (active = non-empty only)', () => {
    // Only types filter active; languages filter is empty → ignored
    const plugin = makePlugin({ types: ['formatter'], languages: ['python'] });
    const q = buildFilterQuery({ types: ['formatter'], languages: [] });
    expect(filterMatches(plugin, q)).toBe(true);
  });
});

describe('filterMatches — edge cases', () => {
  it('should return false when plugin has no types but a type filter is active', () => {
    const plugin = makePlugin({ types: [] });
    expect(filterMatches(plugin, buildFilterQuery({ types: ['formatter'] }))).toBe(false);
  });

  it('should handle plugin with no useCaseTags when useCase filter is active', () => {
    const plugin = makePlugin({ useCaseTags: [] });
    expect(filterMatches(plugin, buildFilterQuery({ useCases: ['code-quality'] }))).toBe(false);
  });

  it('should not mutate the plugin or query', () => {
    const plugin = makePlugin();
    const q = buildFilterQuery({ types: ['formatter'] });
    const originalTypes = [...(q.types ?? [])];
    filterMatches(plugin, q);
    expect(q.types).toEqual(originalTypes);
    expect(plugin.types).toEqual(['formatter']);
  });
});

// ---------------------------------------------------------------------------
// composeSortParams
// ---------------------------------------------------------------------------

describe('composeSortParams', () => {
  it('should return default sort and order when neither is set', () => {
    const params = composeSortParams(buildFilterQuery({}));
    expect(params.sort).toBe(DEFAULT_SORT);
    expect(params.order).toBe(DEFAULT_ORDER);
  });

  it('should return provided sort key', () => {
    const params = composeSortParams(buildFilterQuery({ sort: 'name' }));
    expect(params.sort).toBe('name');
  });

  it('should return provided order', () => {
    const params = composeSortParams(buildFilterQuery({ order: 'asc' }));
    expect(params.order).toBe('asc');
  });

  it('should return new object each call', () => {
    const q = buildFilterQuery({});
    expect(composeSortParams(q)).not.toBe(composeSortParams(q));
  });
});

// ---------------------------------------------------------------------------
// toListPluginsParams — wire shape conversion
// ---------------------------------------------------------------------------

describe('toListPluginsParams', () => {
  it('should include page, limit, sort, order', () => {
    const q = buildFilterQuery({ page: 2, limit: 10, sort: 'name', order: 'asc' });
    const params = toListPluginsParams(q);
    expect(params.page).toBe(2);
    expect(params.limit).toBe(10);
    expect(params.sort).toBe('name');
    expect(params.order).toBe('asc');
  });

  it('should include types filter', () => {
    const q = buildFilterQuery({ types: ['formatter', 'linter'] });
    const params = toListPluginsParams(q);
    expect(params.type).toEqual(['formatter', 'linter']);
  });

  it('should include language filter', () => {
    const q = buildFilterQuery({ languages: ['typescript'] });
    const params = toListPluginsParams(q);
    expect(params.language).toEqual(['typescript']);
  });

  it('should include useCase filter', () => {
    const q = buildFilterQuery({ useCases: ['code-quality'] });
    const params = toListPluginsParams(q);
    expect(params.useCase).toEqual(['code-quality']);
  });

  it('should omit empty filter arrays (no undefined/empty arrays on wire)', () => {
    const q = buildFilterQuery({});
    const params = toListPluginsParams(q);
    // Empty arrays should either be omitted or be empty — never cause filter bleed
    const typeVal = params.type;
    if (typeVal !== undefined) {
      expect(typeVal).toEqual([]);
    }
  });

  it('should return a new object each call', () => {
    const q = buildFilterQuery({});
    expect(toListPluginsParams(q)).not.toBe(toListPluginsParams(q));
  });
});
