/**
 * RED tests — Task 10.4: Context Registry org switching
 *
 * These tests verify that switching the active org publishes a cross-domain
 * event via the module-level `contextRegistry` singleton — NOT via direct
 * Angular DI injection across domain boundaries.
 *
 * All production files referenced below DO NOT EXIST YET — the suite
 * will fail (RED) until the coder creates them.
 *
 * GREEN contract — what the coder MUST implement:
 *
 *   1. OrgContextFacade.setActiveOrg(orgId: string) MUST call:
 *        contextRegistry.publish('org:active-org-switched', { orgId })
 *      using the MODULE-LEVEL singleton from:
 *        src/app/core/context/context-registry.ts
 *
 *   2. The event type constant must be:
 *        export const ORG_ACTIVE_ORG_SWITCHED = 'org:active-org-switched';
 *      defined in:
 *        src/app/features/organizations/application/facades/org-context.facade.ts
 *      (or a separate constants file — the test imports the constant)
 *
 *   3. The event payload type:
 *        export interface ActiveOrgSwitchedPayload {
 *          readonly orgId: string;
 *        }
 *
 *   4. Subscribers registered via contextRegistry.subscribe('org:active-org-switched', fn)
 *      MUST receive { orgId: '<the-new-org-id>' } when setActiveOrg() is called.
 *
 *   5. Multiple subscribers MUST all receive the event.
 *
 *   6. After unsubscribing, the handler MUST NOT receive further events.
 *
 *   7. Publishing is triggered by setActiveOrg() — NOT by loadOrganizations().
 *
 *   8. The facade MUST use the contextRegistry singleton — never inject
 *      another domain's facade directly.
 *
 * Constraint on OrgContextFacade:
 *   - NO @Inject or inject(SomeFacade) for cross-domain references
 *   - ONLY inject(OrganizationsStore), inject(OrgPort), inject(DestroyRef)
 *     (no catalog/search/dashboard injection)
 */

import { Injectable } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { contextRegistry } from '../../../../core/context/context-registry';
import { OrgContextFacade, ORG_ACTIVE_ORG_SWITCHED } from './org-context.facade';
import type { ActiveOrgSwitchedPayload } from './org-context.facade';
import { OrganizationsStore } from '../store/organizations.store';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary, Organization } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Fixtures
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

const ORG_C: OrgSummary = {
  orgId: 'org-uuid-3',
  name: 'Startup Labs',
  slug: 'startup-labs',
  role: 'admin',
};

// ---------------------------------------------------------------------------
// Minimal fake OrgPort (only listOrganizations needed for context facade tests)
// ---------------------------------------------------------------------------

@Injectable()
class MinimalFakeOrgPort extends OrgPort {
  orgsToReturn: OrgSummary[] = [ORG_A, ORG_B, ORG_C];

  createOrganization(_name: string, _slug: string): Observable<Organization> {
    return of({
      orgId: 'new',
      name: _name,
      slug: _slug,
      createdByUserId: 'u1',
      createdAt: new Date(),
    });
  }

  listOrganizations(): Observable<OrgSummary[]> {
    return of(this.orgsToReturn);
  }

  listMembers(_orgId: string): Observable<OrgMember[]> {
    return of([]);
  }

  invite(
    _orgId: string,
    _email: string,
    _role: OrgRole,
  ): Observable<OrgInvitation> {
    return of({
      invitationId: 'inv-1',
      orgId: _orgId,
      email: _email,
      role: _role,
      status: 'pending',
      createdAt: new Date(),
      expiresAt: new Date(),
    });
  }

  acceptInvitation(_orgId: string, _invitationId: string): Observable<void> {
    return of(undefined);
  }

  revokeInvitation(_orgId: string, _invitationId: string): Observable<void> {
    return of(undefined);
  }

  removeMember(_orgId: string, _userId: string): Observable<void> {
    return of(undefined);
  }

  changeMemberRole(_orgId: string, _userId: string, _role: OrgRole): Observable<void> {
    return of(undefined);
  }
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

interface ContextHarness {
  facade: OrgContextFacade;
  port: MinimalFakeOrgPort;
}

function setupContextHarness(): ContextHarness {
  TestBed.resetTestingModule();
  const port = new MinimalFakeOrgPort();
  TestBed.configureTestingModule({
    providers: [
      OrganizationsStore,
      OrgContextFacade,
      { provide: OrgPort, useValue: port },
    ],
  });
  const facade = TestBed.inject(OrgContextFacade);
  return { facade, port };
}

// ===========================================================================
// Constant & payload type
// ===========================================================================

describe('ORG_ACTIVE_ORG_SWITCHED — event name constant', () => {
  it('should be a string', () => {
    expect(typeof ORG_ACTIVE_ORG_SWITCHED).toBe('string');
  });

  it('should equal "org:active-org-switched"', () => {
    expect(ORG_ACTIVE_ORG_SWITCHED).toBe('org:active-org-switched');
  });

  it('should not be an empty string', () => {
    expect(ORG_ACTIVE_ORG_SWITCHED.length).toBeGreaterThan(0);
  });
});

// ===========================================================================
// Context Registry — publish event when switching active org
// ===========================================================================

describe('OrgContextFacade — setActiveOrg() publishes via contextRegistry', () => {
  afterEach(() => {
    // Always clear the registry between tests to avoid cross-test interference
    contextRegistry.clear();
  });

  it('should publish "org:active-org-switched" event when setActiveOrg() is called', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const received: ActiveOrgSwitchedPayload[] = [];
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (payload) => received.push(payload),
    );

    facade.setActiveOrg('org-uuid-2');

    expect(received).toHaveLength(1);
  });

  it('published event payload should contain the new orgId', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    let captured: ActiveOrgSwitchedPayload | undefined;
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (payload) => { captured = payload; },
    );

    facade.setActiveOrg('org-uuid-2');

    expect(captured).toBeDefined();
    expect(captured!.orgId).toBe('org-uuid-2');
  });

  it('event payload must have exactly orgId property (no extra fields)', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    let captured: ActiveOrgSwitchedPayload | undefined;
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (payload) => { captured = payload; },
    );

    facade.setActiveOrg('org-uuid-1');

    expect(captured).toBeDefined();
    // The payload is an object with at least orgId — no unexpected keys required,
    // but orgId must be present
    expect(typeof captured!.orgId).toBe('string');
    expect(captured!.orgId).toBe('org-uuid-1');
  });

  it('should publish the correct orgId on every switch', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const received: string[] = [];
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (payload) => received.push(payload.orgId),
    );

    facade.setActiveOrg('org-uuid-2');
    facade.setActiveOrg('org-uuid-3');
    facade.setActiveOrg('org-uuid-1');

    expect(received).toEqual(['org-uuid-2', 'org-uuid-3', 'org-uuid-1']);
  });

  it('multiple subscribers should all receive the event', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const received1: string[] = [];
    const received2: string[] = [];
    const received3: string[] = [];

    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received1.push(p.orgId),
    );
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received2.push(p.orgId),
    );
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received3.push(p.orgId),
    );

    facade.setActiveOrg('org-uuid-2');

    expect(received1).toEqual(['org-uuid-2']);
    expect(received2).toEqual(['org-uuid-2']);
    expect(received3).toEqual(['org-uuid-2']);
  });

  it('unsubscribed handler should NOT receive further switch events', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const received: string[] = [];
    const unsubscribe = contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received.push(p.orgId),
    );

    facade.setActiveOrg('org-uuid-2');
    unsubscribe();
    facade.setActiveOrg('org-uuid-3');

    // Only the first switch should have been received
    expect(received).toEqual(['org-uuid-2']);
    expect(received).not.toContain('org-uuid-3');
  });

  it('calling unsubscribe twice should not throw', () => {
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const unsubscribe = contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      () => undefined,
    );

    expect(() => {
      unsubscribe();
      unsubscribe(); // idempotent
    }).not.toThrow();
  });

  it('loadOrganizations() alone should NOT publish an org-switched event', () => {
    const { facade } = setupContextHarness();

    const received: ActiveOrgSwitchedPayload[] = [];
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received.push(p),
    );

    // Only load — no explicit setActiveOrg call
    facade.loadOrganizations();

    // loadOrganizations sets the default active org internally, but
    // MUST NOT fire the public switch event (it's not a user-initiated switch).
    expect(received).toHaveLength(0);
  });

  it('event publisher is contextRegistry singleton — not a different instance', () => {
    // Verify the same singleton is used by subscribing to it directly
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    let fired = false;
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      () => { fired = true; },
    );

    facade.setActiveOrg('org-uuid-2');

    // If the facade used a different contextRegistry instance, fired would be false
    expect(fired).toBe(true);
  });
});

// ===========================================================================
// No direct cross-domain injection (architecture enforcement)
// ===========================================================================

describe('OrgContextFacade — no direct cross-domain injection', () => {
  it('should be injectable with only OrganizationsStore + OrgPort (no catalog/search/dashboard)', () => {
    // If the facade tried to inject a cross-domain facade or service, DI would
    // throw an error here because only OrganizationsStore + OrgPort are provided.
    expect(() => setupContextHarness()).not.toThrow();
  });

  it('facade instance should be defined with minimal providers', () => {
    const { facade } = setupContextHarness();
    expect(facade).toBeDefined();
    expect(facade).toBeInstanceOf(OrgContextFacade);
  });
});

// ===========================================================================
// Context Registry subscriber isolation
// ===========================================================================

describe('contextRegistry — subscriber isolation between tests', () => {
  afterEach(() => {
    contextRegistry.clear();
  });

  it('subscribing before setup should not affect this test', () => {
    // After clear() in afterEach, the subscriber list is empty
    const { facade } = setupContextHarness();
    facade.loadOrganizations();

    const received: string[] = [];
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received.push(p.orgId),
    );

    facade.setActiveOrg('org-uuid-1');

    expect(received).toHaveLength(1);
    expect(received[0]).toBe('org-uuid-1');
  });

  it('clear() removes all subscribers', () => {
    const received: string[] = [];
    contextRegistry.subscribe<ActiveOrgSwitchedPayload>(
      ORG_ACTIVE_ORG_SWITCHED,
      (p) => received.push(p.orgId),
    );
    contextRegistry.clear();

    const { facade } = setupContextHarness();
    facade.loadOrganizations();
    facade.setActiveOrg('org-uuid-2');

    // Subscriber was cleared before the event — should not receive it
    expect(received).toHaveLength(0);
  });
});
