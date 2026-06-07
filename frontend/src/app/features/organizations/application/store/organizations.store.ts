/**
 * Signal-based store for the Organizations domain.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { OrgInvitation, OrgMember, OrgSummary } from '../../domain/models/organizations.models';

export const OrganizationsStoreEnum = {
  ORGANIZATIONS: 'ORGANIZATIONS',
  MEMBERS: 'MEMBERS',
  INVITATIONS: 'INVITATIONS',
} as const;

export type OrganizationsStoreEnumType = typeof OrganizationsStoreEnum;

export interface OrganizationsStoreData {
  organizations: OrgSummary[]; // orgs the current user belongs to
}

export interface MembersStoreData {
  orgId: string;
  members: OrgMember[];
}

export interface InvitationsStoreData {
  orgId: string;
  invitations: OrgInvitation[];
}

export interface OrganizationsState {
  [OrganizationsStoreEnum.ORGANIZATIONS]: ResourceState<OrganizationsStoreData>;
  [OrganizationsStoreEnum.MEMBERS]: ResourceState<MembersStoreData>;
  [OrganizationsStoreEnum.INVITATIONS]: ResourceState<InvitationsStoreData>;
}

@Injectable({ providedIn: 'root' })
export class OrganizationsStore extends BaseStore<OrganizationsStoreEnumType, OrganizationsState> {
  constructor() {
    super(OrganizationsStoreEnum);
  }
}
