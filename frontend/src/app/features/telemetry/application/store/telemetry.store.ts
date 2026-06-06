import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';

export enum TelemetryStoreEnum {
  PREFERENCE = 'PREFERENCE',
  ANON_ID = 'ANON_ID',
}

export interface TelemetryState {
  [TelemetryStoreEnum.PREFERENCE]: ResourceState<boolean>;
  [TelemetryStoreEnum.ANON_ID]: ResourceState<string>;
}

@Injectable({ providedIn: 'root' })
export class TelemetryStore extends BaseStore<typeof TelemetryStoreEnum, TelemetryState> {
  constructor() {
    super(TelemetryStoreEnum);
  }
}
