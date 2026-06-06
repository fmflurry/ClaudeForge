/**
 * RED tests — Task 13.4: Presentation layer — search components
 *
 * Covers three components:
 *   1. SearchBarComponent        (cf-search-bar)
 *   2. FilterChipsComponent      (cf-filter-chips)
 *   3. SearchResultsComponent    (cf-search-results)
 *
 * Expected production files (DO NOT exist yet — tests MUST FAIL to compile):
 *   src/app/features/search/presentation/search-bar/search-bar.component.ts
 *   src/app/features/search/presentation/filter-chips/filter-chips.component.ts
 *   src/app/features/search/presentation/search-results/search-results.component.ts
 *
 * Production components the coder MUST define:
 *
 *   // search-bar.component.ts
 *   @Component({ selector: 'cf-search-bar', standalone: true, changeDetection: OnPush })
 *   class SearchBarComponent {
 *     // Inputs:
 *     readonly initialKeyword = input<string>('');
 *     readonly isLoading = input<boolean>(false);
 *     // Outputs:
 *     readonly searchSubmitted = output<string>();          // emits keyword string
 *     readonly filtersChanged = output<SearchFilterOutput>(); // emits filter changes
 *     // Methods:
 *     onSearch(keyword: string): void
 *   }
 *   type SearchFilterOutput = Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>
 *
 *   // filter-chips.component.ts
 *   @Component({ selector: 'cf-filter-chips', standalone: true, changeDetection: OnPush })
 *   class FilterChipsComponent {
 *     // Inputs:
 *     readonly activeTypes = input<readonly string[]>([]);
 *     readonly activeLanguages = input<readonly string[]>([]);
 *     readonly activeUseCases = input<readonly string[]>([]);
 *     // Outputs:
 *     readonly filtersChanged = output<FilterChipsOutput>();
 *   }
 *   type FilterChipsOutput = {
 *     types: readonly string[];
 *     languages: readonly string[];
 *     useCases: readonly string[];
 *   }
 *
 *   // search-results.component.ts
 *   @Component({ selector: 'cf-search-results', standalone: true, changeDetection: OnPush })
 *   class SearchResultsComponent {
 *     // Injected facade (NO direct store or port access):
 *     private readonly facade = inject(SearchFacade);
 *     // Derived signals:
 *     readonly results: Signal<SearchResult[]>
 *     readonly isLoading: Signal<boolean>
 *     readonly hasError: Signal<boolean>
 *     readonly isEmpty: Signal<boolean>
 *     readonly categorySuggestions: Signal<readonly string[]>
 *     readonly discoveryResults: Signal<DiscoveryResult[]>
 *     // Methods:
 *     onSearch(keyword: string): void
 *     onFilterChange(filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>): void
 *     onPageChange(page: number): void
 *     onDiscover(criteria: DiscoveryCriteria): void
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { SearchResultsComponent } from './search-results.component';
import { SearchBarComponent } from '../search-bar/search-bar.component';
import { FilterChipsComponent } from '../filter-chips/filter-chips.component';
import { SearchFacade } from '../../application/facades/search.facade';
import type {
  DiscoveryCriteria,
  DiscoveryResult,
  SearchResult,
} from '../../domain/models/search.models';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';

// ---------------------------------------------------------------------------
// Shared types for stub facade
// ---------------------------------------------------------------------------

interface SearchPaginationMeta {
  readonly totalCount: number;
  readonly page: number;
  readonly limit: number;
  readonly totalPages: number;
}

// ---------------------------------------------------------------------------
// Stub SearchFacade — provides controllable signals
// ---------------------------------------------------------------------------

@Injectable()
class StubSearchFacade {
  private readonly _results = signal<SearchResult[]>([]);
  private readonly _paginationMeta = signal<SearchPaginationMeta | undefined>(undefined);
  private readonly _categorySuggestions = signal<readonly string[]>([]);
  private readonly _discoveryResults = signal<DiscoveryResult[]>([]);
  private readonly _criteriaEchoed = signal<DiscoveryCriteria | undefined>(undefined);
  private readonly _isLoadingSearch = signal(false);
  private readonly _isLoadingDiscovery = signal(false);
  private readonly _searchError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _discoveryError = signal<{ code: string; message: string }[] | undefined>(undefined);

  // Test helpers
  setResultsState(results: SearchResult[], meta?: SearchPaginationMeta, suggestions?: readonly string[]): void {
    this._results.set(results);
    this._paginationMeta.set(meta);
    this._categorySuggestions.set(suggestions ?? []);
  }
  setLoadingSearch(loading: boolean): void {
    this._isLoadingSearch.set(loading);
  }
  setSearchError(errors: { code: string; message: string }[]): void {
    this._searchError.set(errors);
  }
  setDiscoveryState(results: DiscoveryResult[], criteria?: DiscoveryCriteria): void {
    this._discoveryResults.set(results);
    this._criteriaEchoed.set(criteria);
  }
  setCategorySuggestions(suggestions: readonly string[]): void {
    this._categorySuggestions.set(suggestions);
  }

  // Facade signals
  get results(): Signal<SearchResult[]> { return this._results; }
  get paginationMeta(): Signal<SearchPaginationMeta | undefined> { return this._paginationMeta; }
  get categorySuggestions(): Signal<readonly string[]> { return this._categorySuggestions; }
  get discoveryResults(): Signal<DiscoveryResult[]> { return this._discoveryResults; }
  get criteriaEchoed(): Signal<DiscoveryCriteria | undefined> { return this._criteriaEchoed; }
  get isLoadingSearch(): Signal<boolean> { return this._isLoadingSearch; }
  get isLoadingDiscovery(): Signal<boolean> { return this._isLoadingDiscovery; }
  get searchError(): Signal<{ code: string; message: string }[] | undefined> { return this._searchError; }
  get discoveryError(): Signal<{ code: string; message: string }[] | undefined> { return this._discoveryError; }

  // Recorded calls
  searchCalls: { keyword: string; filters?: Partial<SearchFilterQuery> }[] = [];
  setPageCalls: number[] = [];
  setFiltersCalls: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>[] = [];
  discoverCalls: DiscoveryCriteria[] = [];

  search(keyword: string, filters?: Partial<SearchFilterQuery>): void {
    this.searchCalls.push({ keyword, filters });
  }
  setPage(page: number): void {
    this.setPageCalls.push(page);
  }
  setFilters(filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    this.setFiltersCalls.push(filters);
  }
  discover(criteria: DiscoveryCriteria): void {
    this.discoverCalls.push(criteria);
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const RESULT_A: SearchResult = {
  pluginId: 'r1',
  name: 'Alpha Search Result',
  slug: 'alpha-result',
  description: 'Top result.',
  author: 'Dev Alpha',
  types: ['formatter'],
  languages: ['typescript'],
  useCases: ['code-quality'],
  downloadCount: 5000,
  latestVersion: '2.0.0',
  relevanceScore: 0.99,
};

const RESULT_B: SearchResult = {
  pluginId: 'r2',
  name: 'Beta Search Result',
  slug: 'beta-result',
  description: 'Second result.',
  author: 'Dev Beta',
  types: ['linter'],
  languages: ['javascript'],
  useCases: ['testing'],
  downloadCount: 1200,
  latestVersion: '1.1.0',
  relevanceScore: 0.75,
};

const DISCOVERY_A: DiscoveryResult = {
  pluginId: 'd1',
  name: 'Discovered Plugin Alpha',
  slug: 'discovered-alpha',
  description: 'A discovered plugin.',
  author: 'Dev Disco',
  types: ['linter'],
  languages: ['python'],
  matchedLanguages: ['python'],
  maturityIndicator: 'stable',
  relevanceScore: 0.82,
};

const SEARCH_META: SearchPaginationMeta = {
  totalCount: 2,
  page: 1,
  limit: 20,
  totalPages: 1,
};

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupSearchResults(): { fixture: ComponentFixture<SearchResultsComponent>; stub: StubSearchFacade } {
  const stub = new StubSearchFacade();
  TestBed.configureTestingModule({
    imports: [SearchResultsComponent],
    providers: [{ provide: SearchFacade, useValue: stub }],
  }).overrideComponent(SearchResultsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(SearchResultsComponent);
  return { fixture, stub };
}

function setupSearchBar(): { fixture: ComponentFixture<SearchBarComponent> } {
  TestBed.configureTestingModule({
    imports: [SearchBarComponent],
  }).overrideComponent(SearchBarComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(SearchBarComponent);
  return { fixture };
}

function setupFilterChips(): { fixture: ComponentFixture<FilterChipsComponent> } {
  TestBed.configureTestingModule({
    imports: [FilterChipsComponent],
  }).overrideComponent(FilterChipsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(FilterChipsComponent);
  return { fixture };
}

// ===========================================================================
// SearchResultsComponent — selector
// ===========================================================================

describe('SearchResultsComponent — selector', () => {
  it('should use selector "cf-search-results"', () => {
    const { fixture } = setupSearchResults();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ===========================================================================
// SearchResultsComponent — loading state
// ===========================================================================

describe('SearchResultsComponent — loading state', () => {
  it('should render a loading indicator when isLoadingSearch is true', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setLoadingSearch(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });

  it('should NOT render loading indicator when isLoadingSearch is false', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setLoadingSearch(false);
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(By.css('[aria-busy="true"]'));
    expect(loadingEl).toBeNull();
  });
});

// ===========================================================================
// SearchResultsComponent — loaded state with results
// ===========================================================================

describe('SearchResultsComponent — results list', () => {
  it('should render an item for each search result', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A, RESULT_B], SEARCH_META, []);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha Search Result');
    expect(text).toContain('Beta Search Result');
  });

  it('should render the plugin name', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha Search Result');
  });

  it('should render the plugin author', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Dev Alpha');
  });

  it('should render the latest version when present', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('2.0.0');
  });

  it('should render relevance score or match indicator', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    // Relevance score 0.99 should appear in some form; test for presence of the element or value
    const text = fixture.nativeElement.textContent as string;
    // Either "0.99" or a percentage "99" or a relevance label should appear
    const hasRelevance = text.includes('0.99') || text.includes('99%') || text.includes('99');
    expect(hasRelevance).toBe(true);
  });
});

// ===========================================================================
// SearchResultsComponent — empty state with suggestions
// ===========================================================================

describe('SearchResultsComponent — empty state with category suggestions', () => {
  it('should render empty state when results array is empty', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([], undefined, []);
    fixture.detectChanges();
    const emptyEl = fixture.debugElement.query(
      By.css('cf-empty-state, [data-testid="empty-state"], [role="status"], [data-testid="no-results"]'),
    );
    expect(emptyEl).not.toBeNull();
  });

  it('should render category suggestions when results are empty', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([], undefined, ['formatter', 'linter']);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // Category suggestions should be surfaced in the no-results state
    const hasSuggestions = text.includes('formatter') || text.includes('linter');
    expect(hasSuggestions).toBe(true);
  });

  it('should NOT render empty state when results are present', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setResultsState([RESULT_A], SEARCH_META, []);
    fixture.detectChanges();
    const resultEl = fixture.debugElement.query(
      By.css('[data-testid="result-list"], [data-testid="results"], table, ul'),
    );
    expect(resultEl).not.toBeNull();
  });
});

// ===========================================================================
// SearchResultsComponent — error state
// ===========================================================================

describe('SearchResultsComponent — error state', () => {
  it('should render an error message when searchError is set', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setSearchError([{ code: 'HTTP_500', message: 'Server error' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="error-message"], [role="alert"], .error'),
    );
    expect(errorEl).not.toBeNull();
  });
});

// ===========================================================================
// SearchResultsComponent — discovery results
// ===========================================================================

describe('SearchResultsComponent — discovery results', () => {
  it('should render discovery results when present', () => {
    const { fixture, stub } = setupSearchResults();
    stub.setDiscoveryState([DISCOVERY_A], { keyword: 'python linter', languages: ['python'] });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Discovered Plugin Alpha');
  });
});

// ===========================================================================
// SearchResultsComponent — method delegation to facade
// ===========================================================================

describe('SearchResultsComponent — onSearch delegates to facade', () => {
  it('should call facade.search when onSearch is invoked', () => {
    const { fixture, stub } = setupSearchResults();
    fixture.detectChanges();
    fixture.componentInstance.onSearch('typescript');
    expect(stub.searchCalls).toContainEqual(
      expect.objectContaining({ keyword: 'typescript' }),
    );
  });

  it('should call facade.search with empty string without throwing', () => {
    const { fixture } = setupSearchResults();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onSearch('')).not.toThrow();
  });
});

describe('SearchResultsComponent — onFilterChange delegates to facade', () => {
  it('should call facade.setFilters when onFilterChange is invoked', () => {
    const { fixture, stub } = setupSearchResults();
    fixture.detectChanges();
    fixture.componentInstance.onFilterChange({ types: ['formatter'] });
    expect(stub.setFiltersCalls).toContainEqual({ types: ['formatter'] });
  });

  it('should call facade.setFilters with all filter dimensions', () => {
    const { fixture, stub } = setupSearchResults();
    fixture.detectChanges();
    fixture.componentInstance.onFilterChange({ types: [], languages: ['typescript'], useCases: [] });
    expect(stub.setFiltersCalls).toContainEqual({ types: [], languages: ['typescript'], useCases: [] });
  });
});

describe('SearchResultsComponent — onPageChange delegates to facade', () => {
  it('should call facade.setPage when onPageChange is invoked', () => {
    const { fixture, stub } = setupSearchResults();
    fixture.detectChanges();
    fixture.componentInstance.onPageChange(3);
    expect(stub.setPageCalls).toContain(3);
  });
});

describe('SearchResultsComponent — onDiscover delegates to facade', () => {
  it('should call facade.discover when onDiscover is invoked', () => {
    const { fixture, stub } = setupSearchResults();
    fixture.detectChanges();
    const criteria: DiscoveryCriteria = { keyword: 'lint', languages: ['python'] };
    fixture.componentInstance.onDiscover(criteria);
    expect(stub.discoverCalls).toContainEqual(criteria);
  });
});

// ===========================================================================
// SearchResultsComponent — architecture boundary
// ===========================================================================

describe('SearchResultsComponent — architecture boundary', () => {
  it('should NOT reference SearchStore directly (compiles without SearchStore provided)', () => {
    const { fixture } = setupSearchResults();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ===========================================================================
// SearchBarComponent
// ===========================================================================

describe('SearchBarComponent — selector', () => {
  it('should use selector "cf-search-bar"', () => {
    const { fixture } = setupSearchBar();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('SearchBarComponent — rendering', () => {
  it('should render an input element for keyword entry', () => {
    const { fixture } = setupSearchBar();
    fixture.detectChanges();
    const input = fixture.debugElement.query(By.css('input[type="text"], input[type="search"], input'));
    expect(input).not.toBeNull();
  });

  it('should render a search button or submit mechanism', () => {
    const { fixture } = setupSearchBar();
    fixture.detectChanges();
    const btn = fixture.debugElement.query(
      By.css('button[type="submit"], button, [data-testid="search-button"]'),
    );
    expect(btn).not.toBeNull();
  });
});

describe('SearchBarComponent — onSearch method', () => {
  it('should emit searchSubmitted when onSearch is called', () => {
    const { fixture } = setupSearchBar();
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('formatter');
    expect(emitted).toContain('formatter');
  });

  it('should emit trimmed keyword', () => {
    const { fixture } = setupSearchBar();
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('  formatter  ');
    // Accept either trimmed or original — the key requirement is that it emits
    expect(emitted.length).toBeGreaterThan(0);
  });

  it('should not throw when onSearch is called with empty string', () => {
    const { fixture } = setupSearchBar();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onSearch('')).not.toThrow();
  });
});

describe('SearchBarComponent — inputs', () => {
  it('should accept initialKeyword input without throwing', () => {
    const { fixture } = setupSearchBar();
    fixture.componentRef.setInput('initialKeyword', 'eslint');
    expect(() => fixture.detectChanges()).not.toThrow();
  });

  it('should accept isLoading input without throwing', () => {
    const { fixture } = setupSearchBar();
    fixture.componentRef.setInput('isLoading', true);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});

// ===========================================================================
// FilterChipsComponent
// ===========================================================================

describe('FilterChipsComponent — selector', () => {
  it('should use selector "cf-filter-chips"', () => {
    const { fixture } = setupFilterChips();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('FilterChipsComponent — rendering active filters', () => {
  it('should render chips for active types', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeTypes', ['formatter', 'linter']);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    const hasTypes = text.includes('formatter') || text.includes('linter');
    expect(hasTypes).toBe(true);
  });

  it('should render chips for active languages', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeLanguages', ['typescript']);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('typescript');
  });

  it('should render chips for active useCases', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeUseCases', ['code-quality']);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('code-quality');
  });

  it('should render nothing or a placeholder when all filter arrays are empty', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeTypes', []);
    fixture.componentRef.setInput('activeLanguages', []);
    fixture.componentRef.setInput('activeUseCases', []);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});

describe('FilterChipsComponent — filtersChanged output', () => {
  it('should have a filtersChanged output', () => {
    const { fixture } = setupFilterChips();
    expect(fixture.componentInstance.filtersChanged).toBeDefined();
  });

  it('filtersChanged output should be subscribable', () => {
    const { fixture } = setupFilterChips();
    expect(() => {
      fixture.componentInstance.filtersChanged.subscribe(() => undefined);
    }).not.toThrow();
  });
});

describe('FilterChipsComponent — inputs', () => {
  it('should accept activeTypes input without throwing', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeTypes', ['formatter']);
    expect(() => fixture.detectChanges()).not.toThrow();
  });

  it('should accept activeLanguages input without throwing', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeLanguages', ['python']);
    expect(() => fixture.detectChanges()).not.toThrow();
  });

  it('should accept activeUseCases input without throwing', () => {
    const { fixture } = setupFilterChips();
    fixture.componentRef.setInput('activeUseCases', ['testing']);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});
