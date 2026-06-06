/**
 * Facade for the Docs domain.
 * Components interact with this facade only — no direct store or adapter access.
 */

import { computed, inject, Injectable, Signal } from '@angular/core';
import { DocsPort } from '../../domain/ports/docs.port';
import type { DocCategoryNode, DocPage, DocSearchResult } from '../../domain/models/docs.models';
import { buildCategoryTree } from '../../domain/rules/docs-category-tree.rules';
import { DocsStore, DocsStoreEnum } from '../store/docs.store';

@Injectable()
export class DocsFacade {
  private readonly store = inject(DocsStore);
  private readonly port = inject(DocsPort);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  get searchResults(): Signal<DocSearchResult[]> {
    return computed(() => this.store.get(DocsStoreEnum.SEARCH_RESULTS)().data ?? []);
  }

  get categoryTree(): Signal<readonly DocCategoryNode[]> {
    return computed(() => this.store.get(DocsStoreEnum.CATEGORY_TREE)().data ?? []);
  }

  get currentDoc(): Signal<DocPage | undefined> {
    return computed(() => this.store.get(DocsStoreEnum.CURRENT_DOC)().data);
  }

  get isLoadingSearch(): Signal<boolean> {
    return computed(() => this.store.get(DocsStoreEnum.SEARCH_RESULTS)().isLoading ?? false);
  }

  get isLoadingDoc(): Signal<boolean> {
    return computed(() => this.store.get(DocsStoreEnum.CURRENT_DOC)().isLoading ?? false);
  }

  get searchError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(DocsStoreEnum.SEARCH_RESULTS)().errors);
  }

  get docError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(DocsStoreEnum.CURRENT_DOC)().errors);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  search(query: string): void {
    this.store.startLoading(DocsStoreEnum.SEARCH_RESULTS);

    this.port.search(query).subscribe({
      next: ({ items }) => {
        const tree = buildCategoryTree(items);
        this.store.update(DocsStoreEnum.SEARCH_RESULTS, {
          data: items,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
        this.store.update(DocsStoreEnum.CATEGORY_TREE, {
          data: [...tree],
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(DocsStoreEnum.SEARCH_RESULTS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'SEARCH_ERROR', message }],
        });
      },
    });
  }

  openDoc(slug: string): void {
    this.store.startLoading(DocsStoreEnum.CURRENT_DOC);

    this.port.getPage(slug).subscribe({
      next: (doc) => {
        this.store.update(DocsStoreEnum.CURRENT_DOC, {
          data: doc,
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(DocsStoreEnum.CURRENT_DOC, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'DOC_ERROR', message }],
        });
      },
    });
  }

  openPluginDoc(pluginSlug: string): void {
    this.openDoc(`plugin:${pluginSlug}`);
  }
}
