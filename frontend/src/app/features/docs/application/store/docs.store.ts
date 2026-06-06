/**
 * Signal-based store for the Docs domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { DocCategoryNode, DocPage, DocSearchResult } from '../../domain/models/docs.models';

export enum DocsStoreEnum {
  SEARCH_RESULTS = 'SEARCH_RESULTS',
  CURRENT_DOC = 'CURRENT_DOC',
  CATEGORY_TREE = 'CATEGORY_TREE',
}

export interface DocsState {
  [DocsStoreEnum.SEARCH_RESULTS]: ResourceState<DocSearchResult[]>;
  [DocsStoreEnum.CURRENT_DOC]: ResourceState<DocPage>;
  [DocsStoreEnum.CATEGORY_TREE]: ResourceState<DocCategoryNode[]>;
}

@Injectable({ providedIn: 'root' })
export class DocsStore extends BaseStore<typeof DocsStoreEnum, DocsState> {
  constructor() {
    super(DocsStoreEnum);
  }
}
