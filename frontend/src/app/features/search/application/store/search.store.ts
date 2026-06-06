/**
 * Signal-based store for the Search & Discovery domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { DiscoveryResults, SearchResultsPage } from '../../domain/models/search.models';

export enum SearchStoreEnum {
  SEARCH_RESULTS = 'SEARCH_RESULTS',
  DISCOVERY = 'DISCOVERY',
}

export interface SearchState {
  [SearchStoreEnum.SEARCH_RESULTS]: ResourceState<SearchResultsPage>;
  [SearchStoreEnum.DISCOVERY]: ResourceState<DiscoveryResults>;
}

@Injectable({ providedIn: 'root' })
export class SearchStore extends BaseStore<typeof SearchStoreEnum, SearchState> {
  constructor() {
    super(SearchStoreEnum);
  }
}
