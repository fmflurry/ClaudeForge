/**
 * RED tests — Task 13.2 / 13.3: SearchPort + SearchHttpAdapter + SearchStore + SearchFacade
 *
 * Expected production files (DO NOT exist yet — tests MUST FAIL to compile):
 *   src/app/features/search/domain/ports/search.port.ts
 *   src/app/features/search/infrastructure/adapter/search-http.adapter.ts
 *   src/app/features/search/application/store/search.store.ts
 *   src/app/features/search/application/facades/search.facade.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // search.port.ts
 *   abstract class SearchPort {
 *     abstract search(query: SearchFilterQuery): Observable<SearchResultsPage>;
 *     abstract discover(criteria: DiscoveryCriteria): Observable<DiscoveryResults>;
 *   }
 *
 *   // search-http.adapter.ts
 *   @Injectable()
 *   class SearchHttpAdapter extends SearchPort {
 *     // uses ApiClient.searchPlugins() or searchPluginsAlias()
 *     // uses ApiClient.discoverPlugins()
 *     search(query: SearchFilterQuery): Observable<SearchResultsPage>
 *     discover(criteria: DiscoveryCriteria): Observable<DiscoveryResults>
 *   }
 *
 *   // search.store.ts
 *   enum SearchStoreEnum {
 *     SEARCH_RESULTS = 'SEARCH_RESULTS',
 *     DISCOVERY = 'DISCOVERY',
 *   }
 *   interface SearchState {
 *     [SearchStoreEnum.SEARCH_RESULTS]: ResourceState<SearchResultsPage>;
 *     [SearchStoreEnum.DISCOVERY]: ResourceState<DiscoveryResults>;
 *   }
 *   @Injectable({ providedIn: 'root' })
 *   class SearchStore extends BaseStore<typeof SearchStoreEnum, SearchState>
 *
 *   // search.facade.ts
 *   @Injectable()
 *   class SearchFacade {
 *     // Signals:
 *     get results(): Signal<SearchResult[]>
 *     get paginationMeta(): Signal<SearchPaginationMeta | undefined>
 *     get categorySuggestions(): Signal<readonly string[]>
 *     get discoveryResults(): Signal<DiscoveryResult[]>
 *     get criteriaEchoed(): Signal<DiscoveryCriteria | undefined>
 *     get isLoadingSearch(): Signal<boolean>
 *     get isLoadingDiscovery(): Signal<boolean>
 *     get searchError(): Signal<{ code: string; message: string }[] | undefined>
 *     get discoveryError(): Signal<{ code: string; message: string }[] | undefined>
 *
 *     // Methods:
 *     search(keyword: string, filters?: Partial<SearchFilterQuery>): void
 *     setPage(page: number): void
 *     setFilters(filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>): void
 *     discover(criteria: DiscoveryCriteria): void
 *   }
 *
 *   // SearchPaginationMeta type (may be re-used from SearchResultsPage or defined separately):
 *   type SearchPaginationMeta = {
 *     readonly totalCount: number;
 *     readonly page: number;
 *     readonly limit: number;
 *     readonly totalPages: number;
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';
import { SearchStore, SearchStoreEnum } from './search.store';
import type { SearchState } from './search.store';
import { SearchFacade } from '../facades/search.facade';
import { SearchPort } from '../../domain/ports/search.port';
import type {
  DiscoveryCriteria,
  DiscoveryResult,
  DiscoveryResults,
  SearchResult,
  SearchResultsPage,
} from '../../domain/models/search.models';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';

// ---------------------------------------------------------------------------
// Fake data fixtures
// ---------------------------------------------------------------------------

const FAKE_SEARCH_RESULT: SearchResult = {
  pluginId: 'sr-1',
  name: 'Result One',
  slug: 'result-one',
  description: 'A search result.',
  author: 'Auth A',
  types: ['formatter'],
  languages: ['typescript'],
  useCases: ['code-quality'],
  downloadCount: 1200,
  latestVersion: '1.0.0',
  relevanceScore: 0.95,
};

const FAKE_SEARCH_PAGE: SearchResultsPage = {
  items: [FAKE_SEARCH_RESULT],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
  categorySuggestions: ['formatter', 'linter'],
};

const EMPTY_SEARCH_PAGE: SearchResultsPage = {
  items: [],
  totalCount: 0,
  page: 1,
  limit: 20,
  totalPages: 0,
  categorySuggestions: ['formatter', 'linter'],
};

const FAKE_DISCOVERY_RESULT: DiscoveryResult = {
  pluginId: 'dr-1',
  name: 'Discovery One',
  slug: 'discovery-one',
  description: 'A discovery result.',
  author: 'Auth B',
  types: ['linter'],
  languages: ['python'],
  matchedLanguages: ['python'],
  maturityIndicator: 'stable',
  relevanceScore: 0.88,
};

const FAKE_CRITERIA: DiscoveryCriteria = {
  keyword: 'python linter',
  languages: ['python'],
};

const FAKE_DISCOVERY_RESULTS: DiscoveryResults = {
  items: [FAKE_DISCOVERY_RESULT],
  criteriaEchoed: FAKE_CRITERIA,
};

const EMPTY_DISCOVERY_RESULTS: DiscoveryResults = {
  items: [],
  criteriaEchoed: {},
};

// ---------------------------------------------------------------------------
// Fake SearchPort implementations
// ---------------------------------------------------------------------------

@Injectable()
class FakeSearchPort extends SearchPort {
  search(_query: SearchFilterQuery): Observable<SearchResultsPage> {
    return of(FAKE_SEARCH_PAGE);
  }

  discover(_criteria: DiscoveryCriteria): Observable<DiscoveryResults> {
    return of(FAKE_DISCOVERY_RESULTS);
  }
}

@Injectable()
class EmptySearchPort extends SearchPort {
  search(_query: SearchFilterQuery): Observable<SearchResultsPage> {
    return of(EMPTY_SEARCH_PAGE);
  }

  discover(_criteria: DiscoveryCriteria): Observable<DiscoveryResults> {
    return of(EMPTY_DISCOVERY_RESULTS);
  }
}

@Injectable()
class ErrorSearchPort extends SearchPort {
  search(_query: SearchFilterQuery): Observable<SearchResultsPage> {
    return throwError(() => new Error('Search network failure'));
  }

  discover(_criteria: DiscoveryCriteria): Observable<DiscoveryResults> {
    return throwError(() => new Error('Discovery network failure'));
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithFakePort(): { store: SearchStore; facade: SearchFacade } {
  TestBed.configureTestingModule({
    providers: [SearchStore, SearchFacade, { provide: SearchPort, useClass: FakeSearchPort }],
  });
  return {
    store: TestBed.inject(SearchStore),
    facade: TestBed.inject(SearchFacade),
  };
}

function setupWithEmptyPort(): { store: SearchStore; facade: SearchFacade } {
  TestBed.configureTestingModule({
    providers: [SearchStore, SearchFacade, { provide: SearchPort, useClass: EmptySearchPort }],
  });
  return {
    store: TestBed.inject(SearchStore),
    facade: TestBed.inject(SearchFacade),
  };
}

function setupWithErrorPort(): { store: SearchStore; facade: SearchFacade } {
  TestBed.configureTestingModule({
    providers: [SearchStore, SearchFacade, { provide: SearchPort, useClass: ErrorSearchPort }],
  });
  return {
    store: TestBed.inject(SearchStore),
    facade: TestBed.inject(SearchFacade),
  };
}

// ---------------------------------------------------------------------------
// SearchStore — enum key existence
// ---------------------------------------------------------------------------

describe('SearchStore — enum keys', () => {
  it('should have SEARCH_RESULTS key', () => {
    expect(SearchStoreEnum.SEARCH_RESULTS).toBe('SEARCH_RESULTS');
  });

  it('should have DISCOVERY key', () => {
    expect(SearchStoreEnum.DISCOVERY).toBe('DISCOVERY');
  });
});

// ---------------------------------------------------------------------------
// SearchStore — initial state
// ---------------------------------------------------------------------------

describe('SearchStore — initial state', () => {
  it('should initialise SEARCH_RESULTS with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [SearchStore] });
    const store = TestBed.inject(SearchStore);
    const state: ResourceState<SearchResultsPage> = store.get(SearchStoreEnum.SEARCH_RESULTS)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
    expect(state.status).toBeUndefined();
  });

  it('should initialise DISCOVERY with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [SearchStore] });
    const store = TestBed.inject(SearchStore);
    const state: ResourceState<DiscoveryResults> = store.get(SearchStoreEnum.DISCOVERY)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// SearchStore — type conformance
// ---------------------------------------------------------------------------

describe('SearchStore — type conformance', () => {
  it('should be an instance of SearchStore', () => {
    TestBed.configureTestingModule({ providers: [SearchStore] });
    const store = TestBed.inject(SearchStore);
    expect(store).toBeInstanceOf(SearchStore);
  });

  it('SEARCH_RESULTS state type should accept ResourceState<SearchResultsPage>', () => {
    TestBed.configureTestingModule({ providers: [SearchStore] });
    const store = TestBed.inject(SearchStore);
    const partial: Partial<SearchState[typeof SearchStoreEnum.SEARCH_RESULTS]> = {
      data: FAKE_SEARCH_PAGE,
      status: 'Success',
    };
    store.update(SearchStoreEnum.SEARCH_RESULTS, partial);
    expect(store.get(SearchStoreEnum.SEARCH_RESULTS)().status).toBe('Success');
  });

  it('DISCOVERY state type should accept ResourceState<DiscoveryResults>', () => {
    TestBed.configureTestingModule({ providers: [SearchStore] });
    const store = TestBed.inject(SearchStore);
    const partial: Partial<SearchState[typeof SearchStoreEnum.DISCOVERY]> = {
      data: FAKE_DISCOVERY_RESULTS,
      status: 'Success',
    };
    store.update(SearchStoreEnum.DISCOVERY, partial);
    expect(store.get(SearchStoreEnum.DISCOVERY)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — initial signal values
// ---------------------------------------------------------------------------

describe('SearchFacade — initial signal values', () => {
  it('results signal should return empty array before any search', () => {
    const { facade } = setupWithFakePort();
    expect(facade.results()).toEqual([]);
  });

  it('paginationMeta signal should return undefined before any search', () => {
    const { facade } = setupWithFakePort();
    expect(facade.paginationMeta()).toBeUndefined();
  });

  it('categorySuggestions signal should return empty array before any search', () => {
    const { facade } = setupWithFakePort();
    expect(facade.categorySuggestions()).toEqual([]);
  });

  it('discoveryResults signal should return empty array before any discovery', () => {
    const { facade } = setupWithFakePort();
    expect(facade.discoveryResults()).toEqual([]);
  });

  it('criteriaEchoed signal should return undefined before any discovery', () => {
    const { facade } = setupWithFakePort();
    expect(facade.criteriaEchoed()).toBeUndefined();
  });

  it('isLoadingSearch should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('isLoadingDiscovery should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingDiscovery()).toBe(false);
  });

  it('searchError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.searchError()).toBeUndefined();
  });

  it('discoveryError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.discoveryError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — search success path
// ---------------------------------------------------------------------------

describe('SearchFacade — search success', () => {
  it('should populate results signal after search()', () => {
    const { facade } = setupWithFakePort();
    facade.search('formatter');
    expect(facade.results()).toEqual([FAKE_SEARCH_RESULT]);
  });

  it('should populate paginationMeta after search()', () => {
    const { facade } = setupWithFakePort();
    facade.search('formatter');
    const meta = facade.paginationMeta();
    expect(meta).toBeDefined();
    expect(meta?.totalCount).toBe(1);
    expect(meta?.page).toBe(1);
  });

  it('should populate categorySuggestions after search()', () => {
    const { facade } = setupWithFakePort();
    facade.search('formatter');
    expect(facade.categorySuggestions()).toEqual(['formatter', 'linter']);
  });

  it('should set isLoadingSearch to false after successful search', () => {
    const { facade } = setupWithFakePort();
    facade.search('formatter');
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('should clear searchError after successful search', () => {
    const { facade } = setupWithFakePort();
    facade.search('formatter');
    expect(facade.searchError()).toBeUndefined();
  });

  it('should accept optional filters alongside keyword', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.search('formatter', { types: ['formatter'], page: 2 })).not.toThrow();
    expect(facade.results()).toEqual([FAKE_SEARCH_RESULT]);
  });

  it('should accept empty string keyword without throwing', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.search('')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — empty results → categorySuggestions surfaced
// ---------------------------------------------------------------------------

describe('SearchFacade — empty search results', () => {
  it('should return empty results array when port returns zero items', () => {
    const { facade } = setupWithEmptyPort();
    facade.search('nonexistent');
    expect(facade.results()).toEqual([]);
  });

  it('should still surface categorySuggestions when results are empty', () => {
    const { facade } = setupWithEmptyPort();
    facade.search('nonexistent');
    expect(facade.categorySuggestions()).toEqual(['formatter', 'linter']);
  });

  it('should set isLoadingSearch to false after empty result load', () => {
    const { facade } = setupWithEmptyPort();
    facade.search('nonexistent');
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('should not set searchError on empty results (empty is not an error)', () => {
    const { facade } = setupWithEmptyPort();
    facade.search('nonexistent');
    expect(facade.searchError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — search error path
// ---------------------------------------------------------------------------

describe('SearchFacade — search error', () => {
  it('should set searchError when port throws', () => {
    const { facade } = setupWithErrorPort();
    facade.search('boom');
    expect(facade.searchError()).toBeDefined();
    expect(Array.isArray(facade.searchError())).toBe(true);
  });

  it('should not crash when port throws on search', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.search('boom')).not.toThrow();
  });

  it('should set isLoadingSearch to false after search error', () => {
    const { facade } = setupWithErrorPort();
    facade.search('boom');
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('should leave results as empty array after error', () => {
    const { facade } = setupWithErrorPort();
    facade.search('boom');
    expect(facade.results()).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — discover success path
// ---------------------------------------------------------------------------

describe('SearchFacade — discover success', () => {
  it('should populate discoveryResults after discover()', () => {
    const { facade } = setupWithFakePort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.discoveryResults()).toEqual([FAKE_DISCOVERY_RESULT]);
  });

  it('should populate criteriaEchoed after discover()', () => {
    const { facade } = setupWithFakePort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.criteriaEchoed()).toEqual(FAKE_CRITERIA);
  });

  it('should set isLoadingDiscovery to false after successful discover', () => {
    const { facade } = setupWithFakePort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.isLoadingDiscovery()).toBe(false);
  });

  it('should clear discoveryError after successful discover', () => {
    const { facade } = setupWithFakePort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.discoveryError()).toBeUndefined();
  });

  it('should accept criteria with no keyword (discovery without keyword)', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.discover({ languages: ['python'] })).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — blank discovery keyword guard / surfaced error
// ---------------------------------------------------------------------------

describe('SearchFacade — blank discovery keyword', () => {
  it('should not throw when discover() is called with empty keyword string', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.discover({ keyword: '' })).not.toThrow();
  });

  it('should not throw when discover() is called with whitespace-only keyword', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.discover({ keyword: '   ' })).not.toThrow();
  });

  it('should handle discover() called with completely empty criteria object', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.discover({})).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — discover error path
// ---------------------------------------------------------------------------

describe('SearchFacade — discover error', () => {
  it('should set discoveryError when port throws', () => {
    const { facade } = setupWithErrorPort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.discoveryError()).toBeDefined();
  });

  it('should not crash when port throws on discover', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.discover(FAKE_CRITERIA)).not.toThrow();
  });

  it('should set isLoadingDiscovery to false after discovery error', () => {
    const { facade } = setupWithErrorPort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.isLoadingDiscovery()).toBe(false);
  });

  it('should leave discoveryResults as empty array after error', () => {
    const { facade } = setupWithErrorPort();
    facade.discover(FAKE_CRITERIA);
    expect(facade.discoveryResults()).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — setPage
// ---------------------------------------------------------------------------

describe('SearchFacade — setPage', () => {
  it('should not throw when setPage is called after an initial search', () => {
    const { facade } = setupWithFakePort();
    facade.search('test');
    expect(() => facade.setPage(2)).not.toThrow();
  });

  it('should trigger a reload and keep results populated', () => {
    const { facade } = setupWithFakePort();
    facade.search('test');
    facade.setPage(3);
    expect(facade.results()).toEqual([FAKE_SEARCH_RESULT]);
  });
});

// ---------------------------------------------------------------------------
// SearchFacade — setFilters
// ---------------------------------------------------------------------------

describe('SearchFacade — setFilters', () => {
  it('should not throw when setting type filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ types: ['formatter'] })).not.toThrow();
  });

  it('should not throw when setting language filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ languages: ['typescript'] })).not.toThrow();
  });

  it('should not throw when clearing all filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ types: [], languages: [], useCases: [] })).not.toThrow();
  });

  it('should keep results populated after setFilters triggers a reload', () => {
    const { facade } = setupWithFakePort();
    facade.search('test');
    facade.setFilters({ types: ['linter'] });
    expect(facade.results()).toEqual([FAKE_SEARCH_RESULT]);
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('SearchFacade — architecture boundary', () => {
  it('facade public API should expose signals and named methods', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.results).toBe('function');
    expect(typeof facade.paginationMeta).toBe('function');
    expect(typeof facade.categorySuggestions).toBe('function');
    expect(typeof facade.discoveryResults).toBe('function');
    expect(typeof facade.criteriaEchoed).toBe('function');
    expect(typeof facade.isLoadingSearch).toBe('function');
    expect(typeof facade.isLoadingDiscovery).toBe('function');
    expect(typeof facade.searchError).toBe('function');
    expect(typeof facade.discoveryError).toBe('function');
    expect(typeof facade.search).toBe('function');
    expect(typeof facade.setPage).toBe('function');
    expect(typeof facade.setFilters).toBe('function');
    expect(typeof facade.discover).toBe('function');
  });
});
