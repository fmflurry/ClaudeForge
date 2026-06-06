import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';

export enum TeamContextStoreEnum {
  CURRENT_TEAM = 'CURRENT_TEAM',
}

export interface TeamContextState {
  [TeamContextStoreEnum.CURRENT_TEAM]: ResourceState<string>;
}

@Injectable()
export class TeamContextStore extends BaseStore<
  typeof TeamContextStoreEnum,
  TeamContextState
> {
  constructor() {
    super(TeamContextStoreEnum);
  }
}
