/**
 * Signal-based store for the Dashboard domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { InstalledAddOn } from '../../domain/models/dashboard.models';

export enum DashboardStoreEnum {
  INSTALLED_PLUGINS = 'INSTALLED_PLUGINS',
  UPDATE_CHECKS = 'UPDATE_CHECKS',
}

export interface DashboardState {
  [DashboardStoreEnum.INSTALLED_PLUGINS]: ResourceState<InstalledAddOn[]>;
  [DashboardStoreEnum.UPDATE_CHECKS]: ResourceState<Record<string, string | null>>;
}

@Injectable({ providedIn: 'root' })
export class DashboardStore extends BaseStore<typeof DashboardStoreEnum, DashboardState> {
  constructor() {
    super(DashboardStoreEnum);
  }
}
