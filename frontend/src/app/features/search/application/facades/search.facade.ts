/**
 * Facade for the Search & Discovery domain.
 * Components interact with this facade only — no direct store or port access.
 */

import { computed, inject, Injectable, Signal, signal } from '@angular/core';
import { SearchPort } from '../../domain/ports/search.port';
import type {
  DiscoveryCriteria,
  DiscoveryResult,
  SearchResult,
} from '../../domain/models/search.models';
import {
  buildSearchQueryParams,
  combineSearchFilters,
} from '../../domain/rules/search-filter.rules';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';
import { SearchStore, SearchStoreEnum } from '../store/search.store';

export interface SearchPaginationMeta {
  readonly totalCount: number;
  readonly page: number;
  readonly limit: number;
  readonly totalPages: number;
}

@Injectable()
export class SearchFacade {
  private readonly store = inject(SearchStore);
  private readonly port = inject(SearchPort);

  /** Holds the current active search filter query. */
  private readonly _currentQuery = signal<SearchFilterQuery>({});

  /** Pagination meta stored separately. */
  private readonly _paginationMeta = signal<SearchPaginationMeta | undefined>(undefined);

  /** Category suggestions from last search response. */
  private readonly _categorySuggestions = signal<readonly string[]>([]);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  get results(): Signal<SearchResult[]> {
    return computed(() => this.store.get(SearchStoreEnum.SEARCH_RESULTS)().data?.items as SearchResult[] ?? []);
  }

  get paginationMeta(): Signal<SearchPaginationMeta | undefined> {
    return this._paginationMeta.asReadonly();
  }

  get categorySuggestions(): Signal<readonly string[]> {
    return this._categorySuggestions.asReadonly();
  }

  get discoveryResults(): Signal<DiscoveryResult[]> {
    return computed(() => this.store.get(SearchStoreEnum.DISCOVERY)().data?.items as DiscoveryResult[] ?? []);
  }

  get criteriaEchoed(): Signal<DiscoveryCriteria | undefined> {
    return computed(() => this.store.get(SearchStoreEnum.DISCOVERY)().data?.criteriaEchoed);
  }

  get isLoadingSearch(): Signal<boolean> {
    return computed(() => this.store.get(SearchStoreEnum.SEARCH_RESULTS)().isLoading ?? false);
  }

  get isLoadingDiscovery(): Signal<boolean> {
    return computed(() => this.store.get(SearchStoreEnum.DISCOVERY)().isLoading ?? false);
  }

  get searchError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(SearchStoreEnum.SEARCH_RESULTS)().errors);
  }

  get discoveryError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(SearchStoreEnum.DISCOVERY)().errors);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  search(keyword: string, filters?: Partial<SearchFilterQuery>): void {
    const merged = combineSearchFilters(
      { ...this._currentQuery(), keyword },
      filters ?? {},
    );
    this._currentQuery.set(merged);

    this.store.startLoading(SearchStoreEnum.SEARCH_RESULTS);

    this.port.search(buildSearchQueryParams(merged)).subscribe({
      next: (page) => {
        this._paginationMeta.set({
          totalCount: page.totalCount,
          page: page.page,
          limit: page.limit,
          totalPages: page.totalPages,
        });
        this._categorySuggestions.set(page.categorySuggestions);
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          data: page,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'SEARCH_ERROR', message }],
        });
      },
    });
  }

  setPage(page: number): void {
    const updated = combineSearchFilters(this._currentQuery(), { page });
    this._currentQuery.set(updated);
    this.port.search(buildSearchQueryParams(updated)).subscribe({
      next: (resultPage) => {
        this._paginationMeta.set({
          totalCount: resultPage.totalCount,
          page: resultPage.page,
          limit: resultPage.limit,
          totalPages: resultPage.totalPages,
        });
        this._categorySuggestions.set(resultPage.categorySuggestions);
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          data: resultPage,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'SEARCH_ERROR', message }],
        });
      },
    });
  }

  setFilters(
    filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>,
  ): void {
    const updated = combineSearchFilters(this._currentQuery(), { ...filters, page: 1 });
    this._currentQuery.set(updated);
    this.port.search(buildSearchQueryParams(updated)).subscribe({
      next: (resultPage) => {
        this._paginationMeta.set({
          totalCount: resultPage.totalCount,
          page: resultPage.page,
          limit: resultPage.limit,
          totalPages: resultPage.totalPages,
        });
        this._categorySuggestions.set(resultPage.categorySuggestions);
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          data: resultPage,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(SearchStoreEnum.SEARCH_RESULTS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'SEARCH_ERROR', message }],
        });
      },
    });
  }

  discover(criteria: DiscoveryCriteria): void {
    this.store.startLoading(SearchStoreEnum.DISCOVERY);

    this.port.discover(criteria).subscribe({
      next: (results) => {
        this.store.update(SearchStoreEnum.DISCOVERY, {
          data: results,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(SearchStoreEnum.DISCOVERY, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'DISCOVERY_ERROR', message }],
        });
      },
    });
  }
}
