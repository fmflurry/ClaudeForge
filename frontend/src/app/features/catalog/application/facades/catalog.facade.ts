/**
 * Facade for the Catalog domain.
 * Components interact with this facade only — no direct store or use-case access.
 * The facade injects CatalogPort directly to keep provider requirements minimal;
 * the use-case classes exist for reuse in other application flows.
 */

import { computed, inject, Injectable, Signal, signal } from '@angular/core';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';
import { buildFilterQuery } from '../../domain/rules/catalog-filter.rules';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { CatalogStore, CatalogStoreEnum } from '../store/catalog.store';

@Injectable()
export class CatalogFacade {
  private readonly store = inject(CatalogStore);
  private readonly port = inject(CatalogPort);

  /** Holds the current active filter query (mutable via setPage/setSort/setFilters). */
  private readonly _currentQuery = signal<CatalogFilterQuery>(buildFilterQuery({}));

  /** Pagination meta stored separately — not part of the ResourceState<PluginSummary[]> data. */
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  get plugins(): Signal<PluginSummary[]> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGINS)().data ?? []);
  }

  get paginationMeta(): Signal<PaginationMeta | undefined> {
    return this._paginationMeta.asReadonly();
  }

  get categories(): Signal<Categories | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.CATEGORIES)().data);
  }

  get selectedPlugin(): Signal<PluginDetail | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGIN_DETAIL)().data);
  }

  get isLoadingPlugins(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGINS)().isLoading ?? false);
  }

  get isLoadingDetail(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGIN_DETAIL)().isLoading ?? false);
  }

  get isLoadingCategories(): Signal<boolean> {
    return computed(() => this.store.get(CatalogStoreEnum.CATEGORIES)().isLoading ?? false);
  }

  get pluginsError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGINS)().errors);
  }

  get detailError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(CatalogStoreEnum.PLUGIN_DETAIL)().errors);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  loadPlugins(query?: Partial<CatalogFilterQuery>): void {
    const fullQuery = buildFilterQuery({ ...this._currentQuery(), ...query });
    this._currentQuery.set(fullQuery);

    this.store.startLoading(CatalogStoreEnum.PLUGINS);

    this.port.loadPlugins(fullQuery).subscribe({
      next: ({ plugins, meta }) => {
        this._paginationMeta.set(meta);
        this.store.update(CatalogStoreEnum.PLUGINS, {
          data: plugins,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(CatalogStoreEnum.PLUGINS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'LOAD_ERROR', message }],
        });
      },
    });
  }

  setPage(page: number): void {
    this.loadPlugins({ page });
  }

  setSort(sort: string, order?: 'asc' | 'desc'): void {
    this.loadPlugins({ sort, order });
  }

  setFilters(
    filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>,
  ): void {
    this.loadPlugins({ ...filters, page: 1 });
  }

  loadDetail(pluginId: string): void {
    this.store.startLoading(CatalogStoreEnum.PLUGIN_DETAIL);

    this.port.getPlugin(pluginId).subscribe({
      next: (plugin) => {
        this.store.update(CatalogStoreEnum.PLUGIN_DETAIL, {
          data: plugin,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(CatalogStoreEnum.PLUGIN_DETAIL, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'LOAD_ERROR', message }],
        });
      },
    });
  }

  loadCategories(): void {
    this.store.startLoading(CatalogStoreEnum.CATEGORIES);

    this.port.getCategories().subscribe({
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
