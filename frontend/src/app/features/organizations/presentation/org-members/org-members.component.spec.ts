/**
 * OrgMembersComponent — tests for empty/loaded/error + role-gated states.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OrgMembersComponent } from './org-members.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import type { OrgMember, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const MEMBER_A: OrgMember = {
  orgId: 'org-1',
  userId: 'user-2',
  email: 'alice@example.com',
  displayName: 'Alice',
  role: 'admin',
  joinedAt: new Date('2024-02-01T09:00:00.000Z'),
};

const OWNER_ORG: OrgSummary = {
  orgId: 'org-1',
  name: 'Acme',
  slug: 'acme',
  role: 'owner',
};

const MEMBER_ORG: OrgSummary = {
  orgId: 'org-1',
  name: 'Acme',
  slug: 'acme',
  role: 'member',
};

// ---------------------------------------------------------------------------
// Helpers to build fake facades
// ---------------------------------------------------------------------------

function buildFakeOrgsFacade(overrides: {
  members?: OrgMember[];
  isLoadingMembers?: boolean;
  orgsError?: { code: string; message: string }[] | undefined;
}): Partial<OrganizationsFacade> {
  const membersSignal = signal(overrides.members ?? []);
  const isLoadingMembersSignal = signal(overrides.isLoadingMembers ?? false);
  const orgsErrorSignal = signal(overrides.orgsError);

  return {
    get members() {
      return membersSignal.asReadonly();
    },
    get isLoadingMembers() {
      return isLoadingMembersSignal.asReadonly();
    },
    get orgsError() {
      return orgsErrorSignal.asReadonly();
    },
    changeMemberRole: () => undefined,
    removeMember: () => undefined,
  };
}

function buildFakeContextFacade(activeOrg?: OrgSummary): Partial<OrgContextFacade> {
  const activeOrgSignal = signal(activeOrg);
  return {
    get activeOrg() {
      return activeOrgSignal.asReadonly();
    },
  };
}

function buildFakeAuthFacade(isAuthenticated: boolean): Partial<AuthFacade> {
  const isAuthSignal = signal(isAuthenticated);
  return {
    get isAuthenticated() {
      return isAuthSignal.asReadonly();
    },
  };
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setup(opts: {
  members?: OrgMember[];
  isLoadingMembers?: boolean;
  orgsError?: { code: string; message: string }[] | undefined;
  activeOrg?: OrgSummary;
  isAuthenticated?: boolean;
}): ComponentFixture<OrgMembersComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [OrgMembersComponent],
    providers: [
      {
        provide: OrganizationsFacade,
        useValue: buildFakeOrgsFacade({
          members: opts.members ?? [],
          isLoadingMembers: opts.isLoadingMembers ?? false,
          orgsError: opts.orgsError,
        }),
      },
      {
        provide: OrgContextFacade,
        useValue: buildFakeContextFacade(opts.activeOrg),
      },
      {
        provide: AuthFacade,
        useValue: buildFakeAuthFacade(opts.isAuthenticated ?? true),
      },
    ],
  });

  const fixture = TestBed.createComponent(OrgMembersComponent);
  fixture.componentRef.setInput('orgId', 'org-1');
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgMembersComponent — empty state', () => {
  it('should show "No members found" when members list is empty', () => {
    const fixture = setup({ members: [] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No members found');
  });

  it('should not render table when members list is empty', () => {
    const fixture = setup({ members: [] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('table')).toBeNull();
  });
});

describe('OrgMembersComponent — loading state', () => {
  it('should show loading message when isLoadingMembers is true', () => {
    const fixture = setup({ isLoadingMembers: true });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Loading members');
  });
});

describe('OrgMembersComponent — loaded state', () => {
  it('should render members table when members are present', () => {
    const fixture = setup({ members: [MEMBER_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('table')).not.toBeNull();
  });

  it('should display member display name', () => {
    const fixture = setup({ members: [MEMBER_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Alice');
  });

  it('should display member email', () => {
    const fixture = setup({ members: [MEMBER_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('alice@example.com');
  });
});

describe('OrgMembersComponent — role-gated states', () => {
  it('should show Change Role button when caller is owner', () => {
    const fixture = setup({ members: [MEMBER_A], activeOrg: OWNER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Change role for"]');
    expect(btn).not.toBeNull();
  });

  it('should NOT show Change Role button when caller is member', () => {
    const fixture = setup({ members: [MEMBER_A], activeOrg: MEMBER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Change role for"]');
    expect(btn).toBeNull();
  });

  it('should show Remove button when caller is owner', () => {
    const fixture = setup({ members: [MEMBER_A], activeOrg: OWNER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Remove"]');
    expect(btn).not.toBeNull();
  });

  it('should NOT show Remove button when caller is member', () => {
    const fixture = setup({ members: [MEMBER_A], activeOrg: MEMBER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Remove"]');
    expect(btn).toBeNull();
  });
});

describe('OrgMembersComponent — unauthenticated', () => {
  it('should not render content when unauthenticated', () => {
    const fixture = setup({ isAuthenticated: false, members: [MEMBER_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-members')).toBeNull();
  });
});
