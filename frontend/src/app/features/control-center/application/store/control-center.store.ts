import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type {
  AnalysisConfig,
  Appeal,
  AppealDetail,
  AuditLogEntry,
  ConfigChangeLog,
  ControlCenterMetrics,
  Notification,
  Organization,
  OrgMember,
} from '../../domain/models/control-center.models';

export enum ControlCenterStoreEnum {
  METRICS = 'METRICS',
  APPEALS = 'APPEALS',
  APPEAL_DETAIL = 'APPEAL_DETAIL',
  CONFIG = 'CONFIG',
  CONFIG_HISTORY = 'CONFIG_HISTORY',
  AUDIT_LOGS = 'AUDIT_LOGS',
  NOTIFICATIONS = 'NOTIFICATIONS',
  ORGANIZATIONS = 'ORGANIZATIONS',
  ORG_DETAIL = 'ORG_DETAIL',
  ORG_MEMBERS = 'ORG_MEMBERS',
}

export interface ControlCenterState {
  [ControlCenterStoreEnum.METRICS]: ResourceState<ControlCenterMetrics>;
  [ControlCenterStoreEnum.APPEALS]: ResourceState<Appeal[]>;
  [ControlCenterStoreEnum.APPEAL_DETAIL]: ResourceState<AppealDetail>;
  [ControlCenterStoreEnum.CONFIG]: ResourceState<AnalysisConfig>;
  [ControlCenterStoreEnum.CONFIG_HISTORY]: ResourceState<ConfigChangeLog[]>;
  [ControlCenterStoreEnum.AUDIT_LOGS]: ResourceState<AuditLogEntry[]>;
  [ControlCenterStoreEnum.NOTIFICATIONS]: ResourceState<Notification[]>;
  [ControlCenterStoreEnum.ORGANIZATIONS]: ResourceState<Organization[]>;
  [ControlCenterStoreEnum.ORG_DETAIL]: ResourceState<Organization>;
  [ControlCenterStoreEnum.ORG_MEMBERS]: ResourceState<OrgMember[]>;
}

@Injectable({ providedIn: 'root' })
export class ControlCenterStore extends BaseStore<typeof ControlCenterStoreEnum, ControlCenterState> {
  constructor() {
    super(ControlCenterStoreEnum);
  }
}
