/**
 * Facade for the Catalog domain.
 * Components interact with this facade only — no direct store or use-case access.
 * The facade injects CatalogPort directly to keep provider requirements minimal;
 * the use-case classes exist for reuse in other application flows.
 */

import { computed, DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { AddOnDetail, AddOnSummary, Categories, PaginationMeta } from '../../domain/models/catalog.models';
import { buildFilterQuery } from '../../domain/rules/catalog-filter.rules';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { CatalogStore, CatalogStoreEnum } from '../store/catalog.store';

@Injectable()
export class CatalogFacade {
  private readonly store = inject(CatalogStore);
  private readonly port = inject(CatalogPort);
  private readonly destroyRef = inject(DestroyRef);

  /** Holds the current active filter query (mutable via setPage/setSort/setFilters). */
  private readonly _currentQuery = signal<CatalogFilterQuery>(buildFilterQuery({}));

  /** Pagination meta stored separately — not part of the ResourceState<AddOnSummary[]> data. */
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  get addOns(): Signal<AddOnSummary[]> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDONS)().data ?? []);
  }

  get paginationMeta(): Signal<PaginationMeta | undefined> {
    return this._paginationMeta.asReadonly();
  }

  get categories(): Signal<Categories | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.CATEGORIES)().data);
  }

  get selectedAddOn(): Signal<AddOnDetail | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDON_DETAIL)().data);
  }

  get isLoadingAddOns(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDONS)().isLoading ?? false);
  }

  get isLoadingDetail(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDON_DETAIL)().isLoading ?? false);
  }

  get isLoadingCategories(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.CATEGORIES)().isLoading ?? false);
  }

  get addOnsError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDONS)().errors);
  }

  get detailError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.ADDON_DETAIL)().errors);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  loadAddOns(query?: Partial<CatalogFilterQuery>): void {
    const fullQuery = buildFilterQuery({ ...this._currentQuery(), ...query });
    this._currentQuery.set(fullQuery);

    this.store.startLoading(CatalogStoreEnum.ADDONS);

    this.port
      .loadAddOns(fullQuery)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ addOns, meta }) => {
          this._paginationMeta.set(meta);
          this.store.update(CatalogStoreEnum.ADDONS, {
            data: addOns,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(CatalogStoreEnum.ADDONS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  setPage(page: number): void {
    this.loadAddOns({ page });
  }

  setSort(sort: string, order?: 'asc' | 'desc'): void {
    this.loadAddOns({ sort, order });
  }

  setFilters(filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    this.loadAddOns({ ...filters, page: 1 });
  }

  loadDetail(pluginId: string): void {
    this.store.startLoading(CatalogStoreEnum.ADDON_DETAIL);

    this.port
      .getAddOn(pluginId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (addOn) => {
          this.store.update(CatalogStoreEnum.ADDON_DETAIL, {
            data: addOn,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(CatalogStoreEnum.ADDON_DETAIL, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  loadCategories(): void {
    this.store.startLoading(CatalogStoreEnum.CATEGORIES);

    this.port
      .getCategories()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.store.update(CatalogStoreEnum.CATEGORIES, {
            data: categories,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(CatalogStoreEnum.CATEGORIES, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }
}
