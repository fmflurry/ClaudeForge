/**
 * Signal-based store for the Catalog domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { Categories, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';

export enum CatalogStoreEnum {
  PLUGINS = 'PLUGINS',
  PLUGIN_DETAIL = 'PLUGIN_DETAIL',
  CATEGORIES = 'CATEGORIES',
}

export interface CatalogState {
  [CatalogStoreEnum.PLUGINS]: ResourceState<PluginSummary[]>;
  [CatalogStoreEnum.PLUGIN_DETAIL]: ResourceState<PluginDetail>;
  [CatalogStoreEnum.CATEGORIES]: ResourceState<Categories>;
}

@Injectable({ providedIn: 'root' })
export class CatalogStore extends BaseStore<typeof CatalogStoreEnum, CatalogState> {
  constructor() {
    super(CatalogStoreEnum);
  }
}
