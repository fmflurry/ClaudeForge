/**
 * Unit tests for marketplace-categorization-filter.rules.ts
 */

import { describe, it, expect } from 'vitest';
import type {
  PluginManifest,
  MarketplaceFilters,
  DeprecatedFilters,
} from './marketplace-categorization-filter.rules';
import {
  filterByCategory,
  filterByStructural,
  filterByKeywords,
  applyFilters,
  mapDeprecatedFilters,
  buildDeprecationHeader,
} from './marketplace-categorization-filter.rules';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makePlugin(overrides: Partial<PluginManifest> = {}): PluginManifest {
  return {
    name: 'test-plugin',
    version: '1.0.0',
    description: 'A test plugin',
    author: 'tester',
    category: 'code-intelligence',
    languages: ['typescript'],
    entrypoints: ['index.js'],
    keywords: ['skill', 'typescript'],
    ...overrides,
  };
}

const FIXTURES: PluginManifest[] = [
  makePlugin({
    name: 'alpha',
    category: 'code-intelligence',
    keywords: ['skill', 'typescript'],
  }),
  makePlugin({
    name: 'beta',
    category: 'testing-qa',
    keywords: ['subagent', 'testing'],
  }),
  makePlugin({
    name: 'gamma',
    category: 'code-intelligence',
    keywords: ['mcp-server'],
  }),
  makePlugin({
    name: 'delta',
    category: 'workflow-orchestration',
    keywords: ['hook'],
  }),
  makePlugin({
    name: 'epsilon',
    category: 'code-intelligence',
    keywords: ['typescript', 'testing'],
  }),
];

// ---------------------------------------------------------------------------
// filterByCategory
// ---------------------------------------------------------------------------

describe('filterByCategory', () => {
  it('returns plugins matching exact category', () => {
    const result = filterByCategory(FIXTURES, 'code-intelligence');
    expect(result.map((p) => p.name)).toEqual(['alpha', 'gamma', 'epsilon']);
  });

  it('returns empty array when no match', () => {
    const result = filterByCategory(FIXTURES, 'nonexistent');
    expect(result).toEqual([]);
  });

  it('is case-sensitive', () => {
    const result = filterByCategory(FIXTURES, 'Code-Intelligence');
    expect(result).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// filterByStructural
// ---------------------------------------------------------------------------

describe('filterByStructural', () => {
  it('returns plugins with ANY matching structural keyword', () => {
    const result = filterByStructural(FIXTURES, ['skill', 'subagent']);
    expect(result.map((p) => p.name)).toEqual(['alpha', 'beta']);
  });

  it('returns all plugins when structural is empty', () => {
    const result = filterByStructural(FIXTURES, []);
    expect(result).toEqual(FIXTURES);
  });

  it('returns all plugins when structural is undefined', () => {
    const result = filterByStructural(FIXTURES, undefined as unknown as string[]);
    expect(result).toEqual(FIXTURES);
  });

  it('returns empty when no keyword matches', () => {
    const result = filterByStructural(FIXTURES, ['nonexistent']);
    expect(result).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// filterByKeywords
// ---------------------------------------------------------------------------

describe('filterByKeywords', () => {
  it('returns plugins matching keyword substring (OR logic)', () => {
    const result = filterByKeywords(FIXTURES, 'typescript');
    expect(result.map((p) => p.name)).toEqual(['alpha', 'epsilon']);
  });

  it('supports multi-term search (OR across terms)', () => {
    const result = filterByKeywords(FIXTURES, 'testing hook');
    // beta has 'testing', delta has 'hook', epsilon has 'testing'
    expect(result.map((p) => p.name)).toEqual(['beta', 'delta', 'epsilon']);
  });

  it('is case-insensitive', () => {
    const result = filterByKeywords(FIXTURES, 'TypeScript');
    expect(result.map((p) => p.name)).toEqual(['alpha', 'epsilon']);
  });

  it('returns all plugins when keywords is empty', () => {
    const result = filterByKeywords(FIXTURES, '');
    expect(result).toEqual(FIXTURES);
  });

  it('returns all plugins when keywords is whitespace', () => {
    const result = filterByKeywords(FIXTURES, '   ');
    expect(result).toEqual(FIXTURES);
  });
});

// ---------------------------------------------------------------------------
// applyFilters — domain-first hierarchy
// ---------------------------------------------------------------------------

describe('applyFilters', () => {
  it('returns all plugins when no filters provided', () => {
    const result = applyFilters(FIXTURES, {});
    expect(result.plugins).toEqual(FIXTURES);
    expect(result.total).toBe(FIXTURES.length);
  });

  it('filters by category only (AND)', () => {
    const result = applyFilters(FIXTURES, { category: 'code-intelligence' });
    expect(result.plugins.map((p) => p.name)).toEqual(['alpha', 'gamma', 'epsilon']);
    expect(result.total).toBe(3);
  });

  it('filters by structural only (OR)', () => {
    const result = applyFilters(FIXTURES, { structural: ['skill'] });
    expect(result.plugins.map((p) => p.name)).toEqual(['alpha']);
  });

  it('filters by keywords only (OR)', () => {
    const result = applyFilters(FIXTURES, { keywords: 'testing' });
    expect(result.plugins.map((p) => p.name)).toEqual(['beta', 'epsilon']);
  });

  it('ANDs category with structural OR keywords', () => {
    const result = applyFilters(FIXTURES, {
      category: 'code-intelligence',
      structural: ['mcp-server'],
    });
    expect(result.plugins.map((p) => p.name)).toEqual(['gamma']);
  });

  it('ORs structural and keywords within the group', () => {
    const result = applyFilters(FIXTURES, {
      category: 'code-intelligence',
      structural: ['mcp-server'],
      keywords: 'typescript',
    });
    // gamma matches mcp-server, alpha+epsilon match typescript
    expect(result.plugins.map((p) => p.name)).toEqual(['alpha', 'gamma', 'epsilon']);
  });

  it('returns empty when category matches but no structural/keywords match', () => {
    const result = applyFilters(FIXTURES, {
      category: 'code-intelligence',
      structural: ['nonexistent'],
    });
    expect(result.plugins).toEqual([]);
    expect(result.total).toBe(0);
  });

  it('filters correctly across all three dimensions', () => {
    const result = applyFilters(FIXTURES, {
      category: 'code-intelligence',
      structural: ['skill'],
      keywords: 'testing',
    });
    // alpha has skill, epsilon has testing
    expect(result.plugins.map((p) => p.name)).toEqual(['alpha', 'epsilon']);
  });

  it('returns FilterResult with filters echoed', () => {
    const filters: MarketplaceFilters = { category: 'testing-qa' };
    const result = applyFilters(FIXTURES, filters);
    expect(result.filters).toBe(filters);
  });
});

// ---------------------------------------------------------------------------
// mapDeprecatedFilters
// ---------------------------------------------------------------------------

describe('mapDeprecatedFilters', () => {
  it('maps types to structural keywords', () => {
    const result = mapDeprecatedFilters({ types: ['skill', 'hook'] });
    expect(result.structural).toEqual(['skill', 'hook']);
    expect(result.category).toBeUndefined();
  });

  it('filters out unknown types', () => {
    const result = mapDeprecatedFilters({ types: ['unknown', 'skill'] });
    expect(result.structural).toEqual(['skill']);
  });

  it('maps useCaseTags to category (first tag)', () => {
    const result = mapDeprecatedFilters({ useCaseTags: ['testing', 'dev-team'] });
    expect(result.category).toBe('testing-qa');
    expect(result.structural).toBeUndefined();
  });

  it('ignores unknown useCaseTags', () => {
    const result = mapDeprecatedFilters({ useCaseTags: ['unknown'] });
    expect(result.category).toBeUndefined();
  });

  it('maps both types and useCaseTags', () => {
    const result = mapDeprecatedFilters({
      types: ['agent'],
      useCaseTags: ['dev-team'],
    });
    expect(result.structural).toEqual(['subagent']);
    expect(result.category).toBe('workflow-orchestration');
  });

  it('returns empty object when no deprecated params', () => {
    const result = mapDeprecatedFilters({});
    expect(result).toEqual({});
  });
});

// ---------------------------------------------------------------------------
// buildDeprecationHeader
// ---------------------------------------------------------------------------

describe('buildDeprecationHeader', () => {
  it('builds warning for types', () => {
    const header = buildDeprecationHeader({ types: ['skill'] });
    expect(header).toContain('types=skill is deprecated');
    expect(header).toContain('Use structural instead');
  });

  it('builds warning for useCaseTags', () => {
    const header = buildDeprecationHeader({ useCaseTags: ['testing'] });
    expect(header).toContain('useCaseTags=testing is deprecated');
    expect(header).toContain('Use category instead');
  });

  it('builds combined warning for both', () => {
    const header = buildDeprecationHeader({
      types: ['skill'],
      useCaseTags: ['testing'],
    });
    expect(header).toContain('types=');
    expect(header).toContain('useCaseTags=');
    expect(header).toContain('; ');
  });

  it('returns empty string when no deprecated params', () => {
    const header = buildDeprecationHeader({});
    expect(header).toBe('');
  });
});
