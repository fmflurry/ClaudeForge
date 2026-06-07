/**
 * RED tests — Task 10.1 (part D): OrganizationsStore, OrganizationsFacade,
 * and OrgContextFacade state transitions.
 *
 * All production files referenced below DO NOT EXIST YET — the suite
 * will fail (RED) until the coder creates them.
 *
 * GREEN contract — exact types/classes the coder MUST implement:
 *
 * ── organizations.store.ts ──────────────────────────────────────────────────
 *   export const OrganizationsStoreEnum = {
 *     ORGANIZATIONS: 'ORGANIZATIONS',
 *     MEMBERS: 'MEMBERS',
 *     INVITATIONS: 'INVITATIONS',
 *   } as const;
 *   export type OrganizationsStoreEnumType = typeof OrganizationsStoreEnum;
 *
 *   export interface OrganizationsStoreData {
 *     organizations: OrgSummary[];     // orgs the current user belongs to
 *   }
 *   export interface MembersStoreData {
 *     orgId: string;
 *     members: OrgMember[];
 *   }
 *   export interface InvitationsStoreData {
 *     orgId: string;
 *     invitations: OrgInvitation[];
 *   }
 *   export interface OrganizationsState {
 *     [OrganizationsStoreEnum.ORGANIZATIONS]: ResourceState<OrganizationsStoreData>;
 *     [OrganizationsStoreEnum.MEMBERS]: ResourceState<MembersStoreData>;
 *     [OrganizationsStoreEnum.INVITATIONS]: ResourceState<InvitationsStoreData>;
 *   }
 *   @Injectable({ providedIn: 'root' })
 *   export class OrganizationsStore extends BaseStore<
 *     OrganizationsStoreEnumType, OrganizationsState
 *   > {}
 *
 * ── org-context.facade.ts ───────────────────────────────────────────────────
 *   @Injectable()
 *   export class OrgContextFacade {
 *     // Signal getters:
 *     get organizations(): Signal<OrgSummary[]>
 *     get activeOrg(): Signal<OrgSummary | undefined>
 *     get activeOrgId(): Signal<string | undefined>
 *
 *     // Methods:
 *     setActiveOrg(orgId: string): void
 *       — updates the active org; publishes 'org:active-org-switched' event via
 *         contextRegistry with payload { orgId: string } (NOT direct injection)
 *     loadOrganizations(): void
 *       — calls OrgPort.listOrganizations(), stores result, sets default active
 *         to first org if none is currently active
 *   }
 *
 * ── organizations.facade.ts ─────────────────────────────────────────────────
 *   @Injectable()
 *   export class OrganizationsFacade {
 *     // Derived signal getters:
 *     get organizations(): Signal<OrgSummary[]>
 *     get members(): Signal<OrgMember[]>
 *     get invitations(): Signal<OrgInvitation[]>
 *     get isLoadingOrgs(): Signal<boolean>
 *     get isLoadingMembers(): Signal<boolean>
 *     get orgsError(): Signal<{ code: string; message: string }[] | undefined>
 *
 *     // Methods (components call ONLY these):
 *     createOrg(name: string, slug: string): void
 *     listMembers(orgId: string): void
 *     invite(orgId: string, email: string, role: OrgRole): void
 *     acceptInvitation(orgId: string, invitationId: string): void
 *     revokeInvitation(orgId: string, invitationId: string): void
 *     removeMember(orgId: string, userId: string): void
 *     changeMemberRole(orgId: string, userId: string, role: OrgRole): void
 *   }
 *
 * ── org.port.ts ─────────────────────────────────────────────────────────────
 *   export abstract class OrgPort {
 *     abstract createOrganization(name: string, slug: string): Observable<Organization>
 *     abstract listOrganizations(): Observable<OrgSummary[]>
 *     abstract listMembers(orgId: string): Observable<OrgMember[]>
 *     abstract invite(orgId: string, email: string, role: OrgRole): Observable<OrgInvitation>
 *     abstract acceptInvitation(orgId: string, invitationId: string): Observable<void>
 *     abstract revokeInvitation(orgId: string, invitationId: string): Observable<void>
 *     abstract removeMember(orgId: string, userId: string): Observable<void>
 *     abstract changeMemberRole(orgId: string, userId: string, role: OrgRole): Observable<void>
 *   }
 */

import { Injectable } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { OrganizationsStore, OrganizationsStoreEnum } from './organizations.store';
import type { OrganizationsState, OrganizationsStoreData } from './organizations.store';
import { OrganizationsFacade } from '../facades/organizations.facade';
import { OrgContextFacade } from '../facades/org-context.facade';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary, Organization } from '../../domain/models/organizations.models';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const ORG_A: OrgSummary = {
  orgId: 'org-uuid-1',
  name: 'Acme Corp',
  slug: 'acme-corp',
  role: 'owner',
};

const ORG_B: OrgSummary = {
  orgId: 'org-uuid-2',
  name: 'Widgets Inc',
  slug: 'widgets-inc',
  role: 'member',
};

const MEMBER_A: OrgMember = {
  orgId: 'org-uuid-1',
  userId: 'user-uuid-2',
  email: 'alice@example.com',
  displayName: 'Alice',
  role: 'admin',
  joinedAt: new Date('2024-02-01T09:00:00.000Z'),
};

const INVITATION_A: OrgInvitation = {
  invitationId: 'inv-uuid-1',
  orgId: 'org-uuid-1',
  email: 'bob@example.com',
  role: 'member',
  status: 'pending',
  createdAt: new Date('2024-03-01T08:00:00.000Z'),
  expiresAt: new Date('2024-03-08T08:00:00.000Z'),
};

const CREATED_ORG: Organization = {
  orgId: 'org-uuid-new',
  name: 'New Org',
  slug: 'new-org',
  createdByUserId: 'user-uuid-1',
  createdAt: new Date('2024-04-01T10:00:00.000Z'),
};

// ---------------------------------------------------------------------------
// Fake OrgPort — all happy-path by default
// ---------------------------------------------------------------------------

@Injectable()
class FakeOrgPort extends OrgPort {
  orgsToReturn: OrgSummary[] = [ORG_A, ORG_B];
  membersToReturn: OrgMember[] = [MEMBER_A];
  invitationToReturn: OrgInvitation = INVITATION_A;
  orgToReturn: Organization = CREATED_ORG;

  shouldErrorOnCreate = false;
  shouldErrorOnList = false;
  shouldErrorOnListMembers = false;
  shouldErrorOnInvite = false;
  shouldErrorOnAccept = false;
  shouldErrorOnRevoke = false;
  shouldErrorOnRemove = false;
  shouldErrorOnChangeRole = false;

  createOrganization(_name: string, _slug: string): Observable<Organization> {
    if (this.shouldErrorOnCreate) {
      return throwError(() => new Error('Org name taken'));
    }
    return of(this.orgToReturn);
  }

  listOrganizations(): Observable<OrgSummary[]> {
    if (this.shouldErrorOnList) {
      return throwError(() => new Error('Failed to load orgs'));
    }
    return of(this.orgsToReturn);
  }

  listMembers(_orgId: string): Observable<OrgMember[]> {
    if (this.shouldErrorOnListMembers) {
      return throwError(() => new Error('Not a member'));
    }
    return of(this.membersToReturn);
  }

  invite(
    _orgId: string,
    _email: string,
    _role: OrgRole,
  ): Observable<OrgInvitation> {
    if (this.shouldErrorOnInvite) {
      return throwError(() => new Error('Already a member'));
    }
    return of(this.invitationToReturn);
  }

  acceptInvitation(_orgId: string, _invitationId: string): Observable<void> {
    if (this.shouldErrorOnAccept) {
      return throwError(() => new Error('Invitation expired'));
    }
    return of(undefined);
  }

  revokeInvitation(_orgId: string, _invitationId: string): Observable<void> {
    if (this.shouldErrorOnRevoke) {
      return throwError(() => new Error('Forbidden'));
    }
    return of(undefined);
  }

  removeMember(_orgId: string, _userId: string): Observable<void> {
    if (this.shouldErrorOnRemove) {
      return throwError(() => new Error('Sole owner cannot be removed'));
    }
    return of(undefined);
  }

  changeMemberRole(_orgId: string, _userId: string, _role: OrgRole): Observable<void> {
    if (this.shouldErrorOnChangeRole) {
      return throwError(() => new Error('Forbidden'));
    }
    return of(undefined);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

interface StoreHarness {
  store: OrganizationsStore;
}

interface FacadeHarness {
  store: OrganizationsStore;
  orgsFacade: OrganizationsFacade;
  contextFacade: OrgContextFacade;
  port: FakeOrgPort;
}

function setupStoreOnly(): StoreHarness {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [OrganizationsStore] });
  return { store: TestBed.inject(OrganizationsStore) };
}

function setupFacadeHarness(): FacadeHarness {
  TestBed.resetTestingModule();
  const port = new FakeOrgPort();
  TestBed.configureTestingModule({
    providers: [
      OrganizationsStore,
      OrganizationsFacade,
      OrgContextFacade,
      { provide: OrgPort, useValue: port },
    ],
  });
  return {
    store: TestBed.inject(OrganizationsStore),
    orgsFacade: TestBed.inject(OrganizationsFacade),
    contextFacade: TestBed.inject(OrgContextFacade),
    port,
  };
}

// ===========================================================================
// OrganizationsStore — enum key sanity
// ===========================================================================

describe('OrganizationsStore — enum keys', () => {
  it('ORGANIZATIONS key should equal "ORGANIZATIONS"', () => {
    expect(OrganizationsStoreEnum.ORGANIZATIONS).toBe('ORGANIZATIONS');
  });

  it('MEMBERS key should equal "MEMBERS"', () => {
    expect(OrganizationsStoreEnum.MEMBERS).toBe('MEMBERS');
  });

  it('INVITATIONS key should equal "INVITATIONS"', () => {
    expect(OrganizationsStoreEnum.INVITATIONS).toBe('INVITATIONS');
  });
});

describe('OrganizationsStore — initial state', () => {
  it('should initialise ORGANIZATIONS with an empty non-loading state', () => {
    const { store } = setupStoreOnly();
    const state: ResourceState<OrganizationsStoreData> =
      store.get(OrganizationsStoreEnum.ORGANIZATIONS)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('should initialise MEMBERS with an empty non-loading state', () => {
    const { store } = setupStoreOnly();
    expect(store.get(OrganizationsStoreEnum.MEMBERS)().data).toBeUndefined();
  });

  it('should initialise INVITATIONS with an empty non-loading state', () => {
    const { store } = setupStoreOnly();
    expect(store.get(OrganizationsStoreEnum.INVITATIONS)().data).toBeUndefined();
  });

  it('should be an instance of OrganizationsStore', () => {
    const { store } = setupStoreOnly();
    expect(store).toBeInstanceOf(OrganizationsStore);
  });

  it('OrganizationsState type should accept ResourceState<OrganizationsStoreData>', () => {
    const { store } = setupStoreOnly();
    const partial: Partial<OrganizationsState[typeof OrganizationsStoreEnum.ORGANIZATIONS]> = {
      data: { organizations: [ORG_A] },
      status: 'Success',
    };
    store.update(OrganizationsStoreEnum.ORGANIZATIONS, partial);
    expect(store.get(OrganizationsStoreEnum.ORGANIZATIONS)().status).toBe('Success');
  });
});

// ===========================================================================
// OrgContextFacade — initial signals
// ===========================================================================

describe('OrgContextFacade — initial signal values', () => {
  it('organizations should be an empty array before loading', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(contextFacade.organizations()).toEqual([]);
  });

  it('activeOrg should be undefined before loading', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(contextFacade.activeOrg()).toBeUndefined();
  });

  it('activeOrgId should be undefined before loading', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(contextFacade.activeOrgId()).toBeUndefined();
  });

  it('organizations should be a signal function', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(typeof contextFacade.organizations).toBe('function');
  });

  it('activeOrg should be a signal function', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(typeof contextFacade.activeOrg).toBe('function');
  });

  it('activeOrgId should be a signal function', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(typeof contextFacade.activeOrgId).toBe('function');
  });
});

// ===========================================================================
// OrgContextFacade — loadOrganizations()
// ===========================================================================

describe('OrgContextFacade — loadOrganizations()', () => {
  it('should populate organizations signal after loadOrganizations()', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    expect(contextFacade.organizations()).toEqual([ORG_A, ORG_B]);
  });

  it('should set activeOrg to the first org if none was previously set', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    // First org by default
    expect(contextFacade.activeOrg()).toEqual(ORG_A);
  });

  it('should set activeOrgId to the first org id if none was previously set', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    expect(contextFacade.activeOrgId()).toBe('org-uuid-1');
  });

  it('should keep existing activeOrg when orgs reload and active org still present', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    contextFacade.setActiveOrg('org-uuid-2');
    // Reload — ORG_B still in list, should remain active
    contextFacade.loadOrganizations();
    expect(contextFacade.activeOrgId()).toBe('org-uuid-2');
  });

  it('should handle an empty organizations list (no crash, activeOrg undefined)', () => {
    const { contextFacade, port } = setupFacadeHarness();
    port.orgsToReturn = [];
    expect(() => contextFacade.loadOrganizations()).not.toThrow();
    expect(contextFacade.organizations()).toEqual([]);
    expect(contextFacade.activeOrg()).toBeUndefined();
  });

  it('should not throw when loadOrganizations() errors', () => {
    const { contextFacade, port } = setupFacadeHarness();
    port.shouldErrorOnList = true;
    expect(() => contextFacade.loadOrganizations()).not.toThrow();
  });
});

// ===========================================================================
// OrgContextFacade — setActiveOrg()
// ===========================================================================

describe('OrgContextFacade — setActiveOrg()', () => {
  it('should update activeOrgId after setActiveOrg()', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    contextFacade.setActiveOrg('org-uuid-2');
    expect(contextFacade.activeOrgId()).toBe('org-uuid-2');
  });

  it('should update activeOrg to the matching OrgSummary', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    contextFacade.setActiveOrg('org-uuid-2');
    expect(contextFacade.activeOrg()).toEqual(ORG_B);
  });

  it('should set activeOrg to undefined when orgId not in known orgs', () => {
    const { contextFacade } = setupFacadeHarness();
    contextFacade.loadOrganizations();
    contextFacade.setActiveOrg('nonexistent-org-id');
    expect(contextFacade.activeOrg()).toBeUndefined();
  });

  it('should not throw when called without prior loadOrganizations()', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(() => contextFacade.setActiveOrg('org-uuid-1')).not.toThrow();
  });

  it('should expose setActiveOrg as a method', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(typeof contextFacade.setActiveOrg).toBe('function');
  });

  it('should expose loadOrganizations as a method', () => {
    const { contextFacade } = setupFacadeHarness();
    expect(typeof contextFacade.loadOrganizations).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — initial signal values
// ===========================================================================

describe('OrganizationsFacade — initial signal values', () => {
  it('organizations signal should return empty array initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.organizations()).toEqual([]);
  });

  it('members signal should return empty array initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.members()).toEqual([]);
  });

  it('invitations signal should return empty array initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.invitations()).toEqual([]);
  });

  it('isLoadingOrgs should be false initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.isLoadingOrgs()).toBe(false);
  });

  it('isLoadingMembers should be false initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.isLoadingMembers()).toBe(false);
  });

  it('orgsError should be undefined initially', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(orgsFacade.orgsError()).toBeUndefined();
  });
});

// ===========================================================================
// OrganizationsFacade — createOrg()
// ===========================================================================

describe('OrganizationsFacade — createOrg()', () => {
  it('should not throw when called with valid name and slug', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.createOrg('Acme', 'acme')).not.toThrow();
  });

  it('should not crash on createOrg() error from port', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnCreate = true;
    expect(() => orgsFacade.createOrg('Taken Name', 'taken')).not.toThrow();
  });

  it('orgsError should be set when createOrg() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnCreate = true;
    orgsFacade.createOrg('Taken', 'taken');
    expect(orgsFacade.orgsError()).toBeDefined();
    expect(Array.isArray(orgsFacade.orgsError())).toBe(true);
  });
});

// ===========================================================================
// OrganizationsFacade — listMembers()
// ===========================================================================

describe('OrganizationsFacade — listMembers()', () => {
  it('should populate members signal after listMembers()', () => {
    const { orgsFacade } = setupFacadeHarness();
    orgsFacade.listMembers('org-uuid-1');
    expect(orgsFacade.members()).toEqual([MEMBER_A]);
  });

  it('should not throw when listMembers() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnListMembers = true;
    expect(() => orgsFacade.listMembers('org-uuid-1')).not.toThrow();
  });

  it('should expose listMembers as a method', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.listMembers).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — invite()
// ===========================================================================

describe('OrganizationsFacade — invite()', () => {
  it('should not throw when invite() succeeds', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.invite('org-uuid-1', 'new@example.com', 'member')).not.toThrow();
  });

  it('should not throw when invite() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnInvite = true;
    expect(() =>
      orgsFacade.invite('org-uuid-1', 'existing@example.com', 'member'),
    ).not.toThrow();
  });

  it('should set orgsError when invite() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnInvite = true;
    orgsFacade.invite('org-uuid-1', 'existing@example.com', 'member');
    expect(orgsFacade.orgsError()).toBeDefined();
  });

  it('should expose invite as a method', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.invite).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — acceptInvitation()
// ===========================================================================

describe('OrganizationsFacade — acceptInvitation()', () => {
  it('should not throw on success', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.acceptInvitation('org-uuid-1', 'inv-uuid-1')).not.toThrow();
  });

  it('should not throw on port error', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnAccept = true;
    expect(() => orgsFacade.acceptInvitation('org-uuid-1', 'inv-uuid-1')).not.toThrow();
  });

  it('should set orgsError when acceptInvitation() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnAccept = true;
    orgsFacade.acceptInvitation('org-uuid-1', 'inv-uuid-expired');
    expect(orgsFacade.orgsError()).toBeDefined();
  });
});

// ===========================================================================
// OrganizationsFacade — revokeInvitation()
// ===========================================================================

describe('OrganizationsFacade — revokeInvitation()', () => {
  it('should not throw on success', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.revokeInvitation('org-uuid-1', 'inv-uuid-1')).not.toThrow();
  });

  it('should not throw on port error', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnRevoke = true;
    expect(() => orgsFacade.revokeInvitation('org-uuid-1', 'inv-uuid-1')).not.toThrow();
  });

  it('should expose revokeInvitation as a method', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.revokeInvitation).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — removeMember()
// ===========================================================================

describe('OrganizationsFacade — removeMember()', () => {
  it('should not throw on success', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.removeMember('org-uuid-1', 'user-uuid-2')).not.toThrow();
  });

  it('should not throw when removeMember() errors (sole owner guard)', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnRemove = true;
    expect(() => orgsFacade.removeMember('org-uuid-1', 'user-uuid-1')).not.toThrow();
  });

  it('should set orgsError when removeMember() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnRemove = true;
    orgsFacade.removeMember('org-uuid-1', 'user-uuid-1');
    expect(orgsFacade.orgsError()).toBeDefined();
  });

  it('should expose removeMember as a method', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.removeMember).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — changeMemberRole()
// ===========================================================================

describe('OrganizationsFacade — changeMemberRole()', () => {
  it('should not throw on success', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(() => orgsFacade.changeMemberRole('org-uuid-1', 'user-uuid-2', 'admin')).not.toThrow();
  });

  it('should not throw when changeMemberRole() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnChangeRole = true;
    expect(() =>
      orgsFacade.changeMemberRole('org-uuid-1', 'user-uuid-2', 'member'),
    ).not.toThrow();
  });

  it('should set orgsError when changeMemberRole() errors', () => {
    const { orgsFacade, port } = setupFacadeHarness();
    port.shouldErrorOnChangeRole = true;
    orgsFacade.changeMemberRole('org-uuid-1', 'user-uuid-2', 'member');
    expect(orgsFacade.orgsError()).toBeDefined();
  });

  it('should expose changeMemberRole as a method', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.changeMemberRole).toBe('function');
  });
});

// ===========================================================================
// OrganizationsFacade — architecture boundary
// ===========================================================================

describe('OrganizationsFacade — architecture boundary', () => {
  it('should not expose OrgPort directly to consumers', () => {
    const { orgsFacade } = setupFacadeHarness();
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(orgsFacade)).filter(
      (k) => k !== 'constructor',
    );
    // The port must not appear as a named property on the prototype
    expect(protoKeys).not.toContain('port');
  });

  it('should expose all documented public methods on the prototype', () => {
    const { orgsFacade } = setupFacadeHarness();
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(orgsFacade)).filter(
      (k) => k !== 'constructor',
    );
    const expectedMembers = [
      'organizations',
      'members',
      'invitations',
      'isLoadingOrgs',
      'isLoadingMembers',
      'orgsError',
      'createOrg',
      'listMembers',
      'invite',
      'acceptInvitation',
      'revokeInvitation',
      'removeMember',
      'changeMemberRole',
    ];
    for (const member of expectedMembers) {
      expect(protoKeys).toContain(member);
    }
  });

  it('signal getters should return callable functions', () => {
    const { orgsFacade } = setupFacadeHarness();
    expect(typeof orgsFacade.organizations).toBe('function');
    expect(typeof orgsFacade.members).toBe('function');
    expect(typeof orgsFacade.invitations).toBe('function');
    expect(typeof orgsFacade.isLoadingOrgs).toBe('function');
    expect(typeof orgsFacade.isLoadingMembers).toBe('function');
    expect(typeof orgsFacade.orgsError).toBe('function');
  });
});

// ===========================================================================
// OrgContextFacade — architecture boundary
// ===========================================================================

describe('OrgContextFacade — architecture boundary', () => {
  it('should expose all documented public members on the prototype', () => {
    const { contextFacade } = setupFacadeHarness();
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(contextFacade)).filter(
      (k) => k !== 'constructor',
    );
    const expectedMembers = [
      'organizations',
      'activeOrg',
      'activeOrgId',
      'setActiveOrg',
      'loadOrganizations',
    ];
    for (const member of expectedMembers) {
      expect(protoKeys).toContain(member);
    }
  });

  it('should not directly expose the store on the prototype', () => {
    const { contextFacade } = setupFacadeHarness();
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(contextFacade)).filter(
      (k) => k !== 'constructor',
    );
    expect(protoKeys).not.toContain('store');
  });
});
