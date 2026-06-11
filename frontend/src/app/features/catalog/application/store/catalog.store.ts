/**
 * Signal-based store for the Catalog domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { AddOnDetail, AddOnSummary, Categories } from '../../domain/models/catalog.models';

export enum CatalogStoreEnum {
  ADDONS = 'ADDONS',
  ADDON_DETAIL = 'ADDON_DETAIL',
  CATEGORIES = 'CATEGORIES',
}

export interface CatalogState {
  [CatalogStoreEnum.ADDONS]: ResourceState<AddOnSummary[]>;
  [CatalogStoreEnum.ADDON_DETAIL]: ResourceState<AddOnDetail>;
  [CatalogStoreEnum.CATEGORIES]: ResourceState<Categories>;
}

@Injectable({ providedIn: 'root' })
export class CatalogStore extends BaseStore<typeof CatalogStoreEnum, CatalogState> {
  constructor() {
    super(CatalogStoreEnum);
  }
}
