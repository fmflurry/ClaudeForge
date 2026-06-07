/**
 * OrgDetailComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { OrgDetailComponent } from './org-detail.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';
import type { OrgMember, OrgInvitation, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Transloco test langs for organizations scope
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'organizations.back-to-orgs': '← Back to Organisations',
  'organizations.members-title': 'Members',
  'organizations.loading-members': 'Loading members…',
  'organizations.no-members': 'No members found.',
  'organizations.members-table-aria': 'Organisation members',
  'organizations.col-name': 'Name',
  'organizations.col-email': 'Email',
  'organizations.col-role': 'Role',
  'organizations.col-actions': 'Actions',
  'organizations.change-role-btn': 'Change Role',
  'organizations.change-role-aria': 'Change role for',
  'organizations.remove-btn': 'Remove',
  'organizations.remove-aria': 'Remove',
  'organizations.invitations-title': 'Invitations',
  'organizations.no-invitations': 'No pending invitations.',
  'organizations.invitations-list-aria': 'Pending invitations',
  'organizations.invite-email-aria': 'Invite email address',
  'organizations.invite-role-aria': 'Invite role',
  'organizations.role-member': 'Member',
  'organizations.role-admin': 'Admin',
  'organizations.send-invite-btn': 'Send Invite',
  'organizations.accept-btn': 'Accept',
  'organizations.accept-aria': 'Accept invitation from',
  'organizations.revoke-btn': 'Revoke',
  'organizations.revoke-aria': 'Revoke invitation for',
};

const FR_LANGS: Record<string, string> = {
  'organizations.back-to-orgs': '← Retour aux organisations',
  'organizations.members-title': 'Membres',
  'organizations.loading-members': 'Chargement des membres…',
  'organizations.no-members': 'Aucun membre trouvé.',
  'organizations.members-table-aria': "Membres de l'organisation",
  'organizations.col-name': 'Nom',
  'organizations.col-email': 'E-mail',
  'organizations.col-role': 'Rôle',
  'organizations.col-actions': 'Actions',
  'organizations.change-role-btn': 'Changer le rôle',
  'organizations.change-role-aria': 'Changer le rôle de',
  'organizations.remove-btn': 'Retirer',
  'organizations.remove-aria': 'Retirer',
  'organizations.invitations-title': 'Invitations',
  'organizations.no-invitations': 'Aucune invitation en attente.',
  'organizations.invitations-list-aria': 'Invitations en attente',
  'organizations.invite-email-aria': 'Adresse e-mail à inviter',
  'organizations.invite-role-aria': "Rôle de l'invitation",
  'organizations.role-member': 'Membre',
  'organizations.role-admin': 'Administrateur',
  'organizations.send-invite-btn': "Envoyer l'invitation",
  'organizations.accept-btn': 'Accepter',
  'organizations.accept-aria': "Accepter l'invitation de",
  'organizations.revoke-btn': 'Révoquer',
  'organizations.revoke-aria': "Révoquer l'invitation pour",
};

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

function setup(
  opts: {
    isAuthenticated?: boolean;
    orgId?: string;
    members?: OrgMember[];
    invitations?: OrgInvitation[];
    activeOrg?: OrgSummary;
    listMembersSpy?: (orgId: string) => void;
  } = {},
): { fixture: ComponentFixture<OrgDetailComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      OrgDetailComponent,
      RouterModule.forRoot([]),
      TranslocoTestingModule.forRoot({
        langs: { en: EN_LANGS, fr: FR_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
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
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(OrgDetailComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(OrgDetailComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgDetailComponent — unauthenticated', () => {
  it('should not render org detail when unauthenticated', () => {
    const { fixture } = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-detail')).toBeNull();
  });
});

describe('OrgDetailComponent — authenticated render', () => {
  it('should render org detail container when authenticated', () => {
    const { fixture } = setup({ isAuthenticated: true });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-detail')).not.toBeNull();
  });

  it('should render a back link to /orgs', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const link = el.querySelector('a[href="/orgs"]');
    expect(link).not.toBeNull();
  });

  it('should render cf-org-members when orgId is set', () => {
    const { fixture } = setup({ orgId: 'org-1' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-org-members')).not.toBeNull();
  });

  it('should render cf-org-invitations when orgId is set', () => {
    const { fixture } = setup({ orgId: 'org-1' });
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
      imports: [
        OrgDetailComponent,
        RouterModule.forRoot([]),
        TranslocoTestingModule.forRoot({
          langs: { en: EN_LANGS, fr: FR_LANGS },
          translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
      providers: [
        {
          provide: OrganizationsFacade,
          useValue: buildFakeOrgsFacade({ listMembersSpy: spy }),
        },
        { provide: OrgContextFacade, useValue: buildFakeContextFacade() },
        { provide: AuthFacade, useValue: buildFakeAuthFacade(true) },
        { provide: ActivatedRoute, useValue: fakeRoute },
        I18nFacade,
        { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
      ],
    }).overrideComponent(OrgDetailComponent, {
      set: { changeDetection: ChangeDetectionStrategy.Default },
    });
    const fixture = TestBed.createComponent(OrgDetailComponent);
    fixture.detectChanges();
    expect(spy).not.toHaveBeenCalled();
  });
});

describe('OrgDetailComponent — i18n FR', () => {
  it('[FR] should render "← Retour aux organisations" back link text when lang is fr', () => {
    const { fixture, translocoService } = setup({ orgId: 'org-1' });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Retour aux organisations');
  });

  it('[FR] should render "Membres" section heading when lang is fr', () => {
    const { fixture, translocoService } = setup({ orgId: 'org-1' });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Membres');
  });
});
