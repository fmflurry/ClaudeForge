/**
 * Unit tests for MarketplaceFilterPanelComponent
 *
 * Tests the composite filter panel: domain radio single-select,
 * structural multi-select, keyword search, clear all, URL state sync.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute, Router } from '@angular/router';
import { MarketplaceFilterPanelComponent } from './marketplace-filter-panel.component';
import type { PluginManifest } from '../../../domain/rules/marketplace-categorization-filter.rules';
import { signal } from '@angular/core';

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
  makePlugin({ name: 'alpha', category: 'code-intelligence', keywords: ['skill', 'typescript'] }),
  makePlugin({ name: 'beta', category: 'testing-qa', keywords: ['subagent', 'testing'] }),
  makePlugin({ name: 'gamma', category: 'code-intelligence', keywords: ['mcp-server'] }),
];

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setupComponent(initialQueryParams: Record<string, string> = {}): {
  fixture: ComponentFixture<MarketplaceFilterPanelComponent>;
  comp: MarketplaceFilterPanelComponent;
  router: Router;
} {
  TestBed.configureTestingModule({
    imports: [MarketplaceFilterPanelComponent],
    providers: [
      provideRouter([
        {
          path: 'marketplace',
          component: MarketplaceFilterPanelComponent,
        },
      ]),
    ],
  });

  const router = TestBed.inject(Router);
  const route = TestBed.inject(ActivatedRoute);

  // Pre-populate query params snapshot
  Object.defineProperty(route.snapshot, 'queryParamMap', {
    value: {
      get: (key: string) => initialQueryParams[key] ?? null,
      has: (key: string) => key in initialQueryParams,
      getAll: (key: string) => (initialQueryParams[key] ? [initialQueryParams[key]] : []),
      keys: Object.keys(initialQueryParams),
    },
  });

  const fixture = TestBed.createComponent(MarketplaceFilterPanelComponent);
  fixture.componentRef.setInput('plugins', FIXTURES);
  const comp = fixture.componentInstance;

  return { fixture, comp, router };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('MarketplaceFilterPanelComponent', () => {
  describe('domain category single-select', () => {
    it('toggles category on selection', () => {
      const { comp } = setupComponent();
      expect(comp.selectedCategory()).toBeUndefined();

      comp.onCategorySelect('code-intelligence');
      expect(comp.selectedCategory()).toBe('code-intelligence');
    });

    it('deselects category when same category clicked again', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      expect(comp.selectedCategory()).toBe('code-intelligence');

      comp.onCategorySelect('code-intelligence');
      expect(comp.selectedCategory()).toBeUndefined();
    });

    it('switches category when different category clicked', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      comp.onCategorySelect('testing-qa');
      expect(comp.selectedCategory()).toBe('testing-qa');
    });
  });

  describe('structural type multi-select', () => {
    it('updates selected structural types', () => {
      const { comp } = setupComponent();
      expect(comp.selectedStructuralTypes()).toEqual([]);

      comp.onStructuralTypesChange(['skill', 'hook']);
      expect(comp.selectedStructuralTypes()).toEqual(['skill', 'hook']);
    });

    it('clears structural types', () => {
      const { comp } = setupComponent();
      comp.onStructuralTypesChange(['skill']);
      comp.onStructuralTypesChange([]);
      expect(comp.selectedStructuralTypes()).toEqual([]);
    });
  });

  describe('keyword search', () => {
    it('updates keyword signal', () => {
      const { comp } = setupComponent();
      expect(comp.keyword()).toBe('');

      comp.onKeywordChange('typescript');
      expect(comp.keyword()).toBe('typescript');
    });

    it('clears keyword', () => {
      const { comp } = setupComponent();
      comp.onKeywordChange('typescript');
      comp.onKeywordChange('');
      expect(comp.keyword()).toBe('');
    });
  });

  describe('clear all', () => {
    it('resets all filter signals', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      comp.onStructuralTypesChange(['skill']);
      comp.onKeywordChange('test');

      comp.clearAll();

      expect(comp.selectedCategory()).toBeUndefined();
      expect(comp.selectedStructuralTypes()).toEqual([]);
      expect(comp.keyword()).toBe('');
    });
  });

  describe('currentFilters computed', () => {
    it('returns empty filters when nothing selected', () => {
      const { comp } = setupComponent();
      expect(comp.currentFilters()).toEqual({
        category: undefined,
        structural: undefined,
        keywords: undefined,
      });
    });

    it('includes category when selected', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      expect(comp.currentFilters().category).toBe('code-intelligence');
    });

    it('includes structural types when selected', () => {
      const { comp } = setupComponent();
      comp.onStructuralTypesChange(['skill', 'hook']);
      expect(comp.currentFilters().structural).toEqual(['skill', 'hook']);
    });

    it('includes keywords when set', () => {
      const { comp } = setupComponent();
      comp.onKeywordChange('typescript');
      expect(comp.currentFilters().keywords).toBe('typescript');
    });

    it('combines all filter dimensions', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      comp.onStructuralTypesChange(['skill']);
      comp.onKeywordChange('test');

      const filters = comp.currentFilters();
      expect(filters.category).toBe('code-intelligence');
      expect(filters.structural).toEqual(['skill']);
      expect(filters.keywords).toBe('test');
    });
  });

  describe('hasActiveFilters computed', () => {
    it('returns false when no filters active', () => {
      const { comp } = setupComponent();
      expect(comp.hasActiveFilters()).toBe(false);
    });

    it('returns true when category selected', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('code-intelligence');
      expect(comp.hasActiveFilters()).toBe(true);
    });

    it('returns true when structural types selected', () => {
      const { comp } = setupComponent();
      comp.onStructuralTypesChange(['skill']);
      expect(comp.hasActiveFilters()).toBe(true);
    });

    it('returns true when keyword set', () => {
      const { comp } = setupComponent();
      comp.onKeywordChange('test');
      expect(comp.hasActiveFilters()).toBe(true);
    });
  });

  describe('domainCategories computed', () => {
    it('counts plugins per category', () => {
      const { comp } = setupComponent();
      const categories = comp.domainCategories();
      const codeIntel = categories.find((c) => c.id === 'code-intelligence');
      expect(codeIntel?.count).toBe(2); // alpha + gamma
    });

    it('returns zero count for categories with no plugins', () => {
      const { comp } = setupComponent();
      const categories = comp.domainCategories();
      const security = categories.find((c) => c.id === 'security');
      expect(security?.count).toBe(0);
    });
  });

  describe('structural type filtering by category', () => {
    it('filters structural types to only those in selected category', () => {
      const { comp } = setupComponent();
      comp.onCategorySelect('testing-qa');
      const types = comp.filteredStructuralTypes();
      // beta has 'subagent' and 'testing' keywords
      const typeIds = types.map((t) => t.id);
      expect(typeIds).toContain('subagent');
    });

    it('returns all structural types when no category selected', () => {
      const { comp } = setupComponent();
      const types = comp.filteredStructuralTypes();
      expect(types.length).toBeGreaterThan(0);
    });
  });
});
