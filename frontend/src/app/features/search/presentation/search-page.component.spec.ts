/**
 * SearchPageComponent — render + action wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { SearchPageComponent } from './search-page.component';
import { SearchFacade } from '../application/facades/search.facade';
import type { DiscoveryResult, SearchResult } from '../domain/models/search.models';
import type { SearchPaginationMeta } from '../application/facades/search.facade';
import type { SearchFilterQuery } from '../domain/rules/search-filter.rules';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for search scope (Wave 1 i18n)
// En map returns EXACT current literals so all existing assertions stay green.
// Fr map returns French — fr assertions confirm reactive translation.
// ---------------------------------------------------------------------------

const EN_SEARCH_LANGS: Record<string, string> = {
  'search.search-input-placeholder': 'Search plugins…',
  'search.search-button': 'Search',
  'search.loading-results': 'Loading results…',
  'search.error-message': 'Failed to load search results. Please try again.',
  'search.no-results-with-suggestions': 'No results found. Try one of these categories:',
  'search.no-results': 'No results found. Try a different search term.',
};

const FR_SEARCH_LANGS: Record<string, string> = {
  'search.search-input-placeholder': 'Rechercher des plugins…',
  'search.search-button': 'Rechercher',
  'search.loading-results': 'Chargement des résultats…',
  'search.error-message': 'Impossible de charger les résultats. Veuillez réessayer.',
  'search.no-results-with-suggestions': "Aucun résultat. Essayez l'une de ces catégories :",
  'search.no-results': 'Aucun résultat. Essayez un autre terme de recherche.',
};

// ---------------------------------------------------------------------------
// Stub facade
// ---------------------------------------------------------------------------

@Injectable()
class StubSearchFacade {
  private readonly _results = signal<SearchResult[]>([]);
  private readonly _discoveryResults = signal<DiscoveryResult[]>([]);
  private readonly _isLoadingSearch = signal(false);
  private readonly _isLoadingDiscovery = signal(false);
  private readonly _searchError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _paginationMeta = signal<SearchPaginationMeta | undefined>(undefined);
  private readonly _categorySuggestions = signal<readonly string[]>([]);

  searchCalls: string[] = [];
  setFiltersCalls: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>[] = [];

  get results(): Signal<SearchResult[]> {
    return this._results;
  }
  get discoveryResults(): Signal<DiscoveryResult[]> {
    return this._discoveryResults;
  }
  get isLoadingSearch(): Signal<boolean> {
    return this._isLoadingSearch;
  }
  get isLoadingDiscovery(): Signal<boolean> {
    return this._isLoadingDiscovery;
  }
  get searchError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._searchError;
  }
  get paginationMeta(): Signal<SearchPaginationMeta | undefined> {
    return this._paginationMeta;
  }
  get categorySuggestions(): Signal<readonly string[]> {
    return this._categorySuggestions;
  }

  search(keyword: string): void {
    this.searchCalls.push(keyword);
  }
  setFilters(filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    this.setFiltersCalls.push(filters);
  }
  discover(_criteria: unknown): void {
    /* no-op */
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): {
  fixture: ComponentFixture<SearchPageComponent>;
  stub: StubSearchFacade;
  translocoService: TranslocoService;
} {
  const stub = new StubSearchFacade();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      SearchPageComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_SEARCH_LANGS, fr: FR_SEARCH_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: SearchFacade, useValue: stub },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(SearchPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(SearchPageComponent);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, stub, translocoService };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('SearchPageComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render the search page container', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="search-page"]')).not.toBeNull();
  });

  it('should render cf-search-bar component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-search-bar')).not.toBeNull();
  });

  it('should render cf-filter-chips component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-filter-chips')).not.toBeNull();
  });

  it('should render cf-search-results component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-search-results')).not.toBeNull();
  });
});

describe('SearchPageComponent — onSearch', () => {
  it('should call facade.search with the keyword', () => {
    const { fixture, stub } = setup();
    fixture.detectChanges();
    fixture.componentInstance.onSearch('typescript linter');
    expect(stub.searchCalls).toContain('typescript linter');
  });

  it('should not throw when onSearch is called with empty string', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onSearch('')).not.toThrow();
  });
});

describe('SearchPageComponent — onFiltersChanged', () => {
  it('should call facade.setFilters with the converted filters', () => {
    const { fixture, stub } = setup();
    fixture.detectChanges();
    fixture.componentInstance.onFiltersChanged({
      types: ['formatter'],
      languages: ['typescript'],
      useCases: ['code-quality'],
    });
    expect(stub.setFiltersCalls).toHaveLength(1);
    expect(stub.setFiltersCalls[0].types).toEqual(['formatter']);
    expect(stub.setFiltersCalls[0].languages).toEqual(['typescript']);
    expect(stub.setFiltersCalls[0].useCases).toEqual(['code-quality']);
  });

  it('should not throw when onFiltersChanged is called with empty arrays', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onFiltersChanged({ types: [], languages: [], useCases: [] })).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// i18n — FR language rendering
// ---------------------------------------------------------------------------

describe('SearchPageComponent — i18n FR language rendering', () => {
  it('[FR] search button inside cf-search-bar renders "Rechercher" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    fixture.detectChanges();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[data-testid="search-button"]') as HTMLButtonElement | null;
    expect(btn?.textContent?.trim()).toBe('Rechercher');
  });
});
