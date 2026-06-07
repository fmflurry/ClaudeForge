/**
 * OrgDetailComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { OrgDetailComponent } from './org-detail.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import type { OrgMember, OrgInvitation, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

function buildFakeOrgsFacade(opts: {
  members?: OrgMember[];
  invitations?: OrgInvitation[];
  isLoadingMembers?: boolean;
  orgsError?: { code: string; message: string }[] | undefined;
  listMembersSpy?: (orgId: string) => void;
}): Partial<OrganizationsFacade> {
  const membersSignal = signal(opts.members ?? []);
  const invitationsSignal = signal(opts.invitations ?? []);
  const isLoadingMembersSignal = signal(opts.isLoadingMembers ?? false);
  const orgsErrorSignal = signal(opts.orgsError);

  return {
    get members() {
      return membersSignal.asReadonly();
    },
    get invitations() {
      return invitationsSignal.asReadonly();
    },
    get isLoadingMembers() {
      return isLoadingMembersSignal.asReadonly();
    },
    get orgsError() {
      return orgsErrorSignal.asReadonly();
    },
    listMembers: opts.listMembersSpy ?? (() => undefined),
    invite: () => undefined,
    acceptInvitation: () => undefined,
    revokeInvitation: () => undefined,
    removeMember: () => undefined,
    changeMemberRole: () => undefined,
  };
}

function buildFakeContextFacade(activeOrg?: OrgSummary): Partial<OrgContextFacade> {
  const sig = signal(activeOrg);
  return {
    get activeOrg() {
      return sig.asReadonly();
    },
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

function buildFakeRoute(orgId?: string): Partial<ActivatedRoute> {
  return {
    snapshot: {
      paramMap: {
        get: (key: string) => (key === 'orgId' ? (orgId ?? null) : null),
      },
    } as ActivatedRoute['snapshot'],
  };
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(opts: {
  isAuthenticated?: boolean;
  orgId?: string;
  members?: OrgMember[];
  invitations?: OrgInvitation[];
  activeOrg?: OrgSummary;
  listMembersSpy?: (orgId: string) => void;
} = {}): ComponentFixture<OrgDetailComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [OrgDetailComponent, RouterModule.forRoot([])],
    providers: [
      {
        provide: OrganizationsFacade,
        useValue: buildFakeOrgsFacade({
          members: opts.members ?? [],
          invitations: opts.invitations ?? [],
          listMembersSpy: opts.listMembersSpy,
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
      {
        provide: ActivatedRoute,
        useValue: buildFakeRoute(opts.orgId ?? 'org-1'),
      },
    ],
  }).overrideComponent(OrgDetailComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(OrgDetailComponent);
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgDetailComponent — unauthenticated', () => {
  it('should not render org detail when unauthenticated', () => {
    const fixture = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-detail')).toBeNull();
  });
});

describe('OrgDetailComponent — authenticated render', () => {
  it('should render org detail container when authenticated', () => {
    const fixture = setup({ isAuthenticated: true });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-detail')).not.toBeNull();
  });

  it('should render a back link to /orgs', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;
    const link = el.querySelector('a[href="/orgs"]');
    expect(link).not.toBeNull();
  });

  it('should render cf-org-members when orgId is set', () => {
    const fixture = setup({ orgId: 'org-1' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-org-members')).not.toBeNull();
  });

  it('should render cf-org-invitations when orgId is set', () => {
    const fixture = setup({ orgId: 'org-1' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-org-invitations')).not.toBeNull();
  });
});

describe('OrgDetailComponent — ngOnInit wiring', () => {
  it('should call orgsFacade.listMembers with orgId from route on init', () => {
    const spy = vi.fn();
    setup({ orgId: 'org-abc', listMembersSpy: spy });
    expect(spy).toHaveBeenCalledWith('org-abc');
  });

  it('should NOT call listMembers when orgId is absent from route', () => {
    const spy = vi.fn();
    const fakeRoute: Partial<ActivatedRoute> = {
      snapshot: {
        paramMap: { get: (_key: string): null => null },
      } as unknown as ActivatedRoute['snapshot'],
    };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [OrgDetailComponent, RouterModule.forRoot([])],
      providers: [
        {
          provide: OrganizationsFacade,
          useValue: buildFakeOrgsFacade({ listMembersSpy: spy }),
        },
        { provide: OrgContextFacade, useValue: buildFakeContextFacade() },
        { provide: AuthFacade, useValue: buildFakeAuthFacade(true) },
        { provide: ActivatedRoute, useValue: fakeRoute },
      ],
    }).overrideComponent(OrgDetailComponent, {
      set: { changeDetection: ChangeDetectionStrategy.Default },
    });
    const fixture = TestBed.createComponent(OrgDetailComponent);
    fixture.detectChanges();
    expect(spy).not.toHaveBeenCalled();
  });
});
