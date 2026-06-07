/**
 * OrgsPageComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { OrgsPageComponent } from './orgs-page.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import type { OrgInvitation, OrgMember, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const ORG_A: OrgSummary = { orgId: 'org-1', name: 'Acme Corp', slug: 'acme', role: 'owner' };
const ORG_B: OrgSummary = { orgId: 'org-2', name: 'Widgets Ltd', slug: 'widgets', role: 'member' };

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

function buildFakeOrgsFacade(opts: {
  organizations?: OrgSummary[];
  members?: OrgMember[];
  invitations?: OrgInvitation[];
  isLoadingOrgs?: boolean;
  orgsError?: { code: string; message: string }[] | undefined;
}): Partial<OrganizationsFacade> {
  const orgsSignal = signal(opts.organizations ?? []);
  const membersSignal = signal(opts.members ?? []);
  const invitationsSignal = signal(opts.invitations ?? []);
  const isLoadingSignal = signal(opts.isLoadingOrgs ?? false);
  const orgsErrorSignal = signal(opts.orgsError);

  return {
    get organizations() {
      return orgsSignal.asReadonly();
    },
    get members() {
      return membersSignal.asReadonly();
    },
    get invitations() {
      return invitationsSignal.asReadonly();
    },
    get isLoadingOrgs() {
      return isLoadingSignal.asReadonly();
    },
    get isLoadingMembers() {
      return signal(false).asReadonly();
    },
    get orgsError() {
      return orgsErrorSignal.asReadonly();
    },
    createOrg: () => undefined,
    listMembers: () => undefined,
    invite: () => undefined,
    acceptInvitation: () => undefined,
    revokeInvitation: () => undefined,
    removeMember: () => undefined,
    changeMemberRole: () => undefined,
  };
}

function buildFakeContextFacade(opts: {
  activeOrgId?: string;
  organizations?: OrgSummary[];
  activeOrg?: OrgSummary;
  loadOrganizationsSpy?: () => void;
}): Partial<OrgContextFacade> {
  const activeOrgIdSignal = signal<string | undefined>(opts.activeOrgId);
  const activeOrgSignal = signal<OrgSummary | undefined>(opts.activeOrg);
  const orgsSignal = signal(opts.organizations ?? []);

  return {
    get activeOrgId() {
      return activeOrgIdSignal.asReadonly();
    },
    get activeOrg() {
      return activeOrgSignal.asReadonly();
    },
    get organizations() {
      return orgsSignal.asReadonly();
    },
    loadOrganizations: opts.loadOrganizationsSpy ?? (() => undefined),
    setActiveOrg: () => undefined,
  };
}

function buildFakeAuthFacade(isAuthenticated: boolean): Partial<AuthFacade> {
  const sig = signal(isAuthenticated);
  return {
    get isAuthenticated() {
      return sig.asReadonly();
    },
  };
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(
  opts: {
    isAuthenticated?: boolean;
    organizations?: OrgSummary[];
    isLoadingOrgs?: boolean;
    orgsError?: { code: string; message: string }[] | undefined;
    activeOrgId?: string;
    activeOrg?: OrgSummary;
    loadOrganizationsSpy?: () => void;
  } = {},
): ComponentFixture<OrgsPageComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [OrgsPageComponent, RouterModule.forRoot([])],
    providers: [
      {
        provide: OrganizationsFacade,
        useValue: buildFakeOrgsFacade({
          organizations: opts.organizations ?? [],
          isLoadingOrgs: opts.isLoadingOrgs,
          orgsError: opts.orgsError,
        }),
      },
      {
        provide: OrgContextFacade,
        useValue: buildFakeContextFacade({
          activeOrgId: opts.activeOrgId,
          activeOrg: opts.activeOrg,
          organizations: opts.organizations ?? [],
          loadOrganizationsSpy: opts.loadOrganizationsSpy,
        }),
      },
      {
        provide: AuthFacade,
        useValue: buildFakeAuthFacade(opts.isAuthenticated ?? true),
      },
    ],
  }).overrideComponent(OrgsPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(OrgsPageComponent);
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgsPageComponent — unauthenticated', () => {
  it('should render sign-in prompt when unauthenticated', () => {
    const fixture = setup({ isAuthenticated: false });
    expect(fixture.nativeElement.textContent).toContain('sign in');
  });

  it('should NOT render org list when unauthenticated', () => {
    const fixture = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-orgs-page')).toBeNull();
  });

  it('should NOT call loadOrganizations when unauthenticated', () => {
    const spy = vi.fn();
    setup({ isAuthenticated: false, loadOrganizationsSpy: spy });
    expect(spy).not.toHaveBeenCalled();
  });
});

describe('OrgsPageComponent — authenticated render', () => {
  it('should render Organisations heading', () => {
    const fixture = setup();
    expect(fixture.nativeElement.textContent).toContain('Organisations');
  });

  it('should render cf-create-org component', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-create-org')).not.toBeNull();
  });

  it('should show loading message when isLoadingOrgs is true', () => {
    const fixture = setup({ isLoadingOrgs: true });
    expect(fixture.nativeElement.textContent).toContain('Loading organisations');
  });

  it('should show empty state when no orgs and not loading', () => {
    const fixture = setup({ organizations: [], isLoadingOrgs: false });
    expect(fixture.nativeElement.textContent).toContain('not a member of any organisation');
  });

  it('should render org list when orgs exist', () => {
    const fixture = setup({ organizations: [ORG_A, ORG_B] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('ul[aria-label="Your organisations"]')).not.toBeNull();
  });

  it('should display each org name in the list', () => {
    const fixture = setup({ organizations: [ORG_A, ORG_B] });
    expect(fixture.nativeElement.textContent).toContain('Acme Corp');
    expect(fixture.nativeElement.textContent).toContain('Widgets Ltd');
  });

  it('should display org role badge for each org', () => {
    const fixture = setup({ organizations: [ORG_A] });
    expect(fixture.nativeElement.textContent).toContain('owner');
  });
});

describe('OrgsPageComponent — active org section', () => {
  it('should render active org section when activeOrgId is set', () => {
    const fixture = setup({ activeOrgId: 'org-1', activeOrg: ORG_A, organizations: [ORG_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('section[aria-label="Active organisation details"]')).not.toBeNull();
  });

  it('should NOT render active org section when activeOrgId is undefined', () => {
    const fixture = setup({ activeOrgId: undefined, organizations: [] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('section[aria-label="Active organisation details"]')).toBeNull();
  });
});

describe('OrgsPageComponent — ngOnInit wiring', () => {
  it('should call contextFacade.loadOrganizations on init when authenticated', () => {
    const spy = vi.fn();
    setup({ isAuthenticated: true, loadOrganizationsSpy: spy });
    expect(spy).toHaveBeenCalled();
  });
});
