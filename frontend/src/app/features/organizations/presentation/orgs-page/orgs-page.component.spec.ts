/**
 * OrgsPageComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { OrgsPageComponent } from './orgs-page.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';
import type { OrgInvitation, OrgMember, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Transloco test langs for organizations scope
// En=exact current literals so existing assertions stay green.
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'organizations.title': 'Organisations',
  'organizations.loading-orgs': 'Loading organisations…',
  'organizations.empty-orgs': 'You are not a member of any organisation yet.',
  'organizations.your-orgs-aria': 'Your organisations',
  'organizations.active-org-aria': 'Active organisation details',
  'organizations.sign-in-prompt': 'Please',
  'organizations.sign-in-link': 'sign in',
  'organizations.sign-in-suffix': 'to view organisations.',
  'organizations.create-org-title': 'Create Organisation',
  'organizations.org-name-label': 'Organisation Name',
  'organizations.org-name-placeholder': 'My Organisation',
  'organizations.slug-label': 'Slug',
  'organizations.slug-placeholder': 'my-organisation',
  'organizations.create-org-btn': 'Create Organisation',
  'organizations.creating': 'Creating…',
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
  'organizations.current-org-aria': 'Current organisation',
  'organizations.switch-org-aria': 'Switch organisation',
  'organizations.no-org': 'No organisation',
};

const FR_LANGS: Record<string, string> = {
  'organizations.title': 'Organisations',
  'organizations.loading-orgs': 'Chargement des organisations…',
  'organizations.empty-orgs': 'Vous n’êtes membre d’aucune organisation pour le moment.',
  'organizations.your-orgs-aria': 'Vos organisations',
  'organizations.active-org-aria': 'Détails de l’organisation active',
  'organizations.sign-in-prompt': 'Veuillez',
  'organizations.sign-in-link': 'vous connecter',
  'organizations.sign-in-suffix': 'pour voir les organisations.',
  'organizations.create-org-title': 'Créer une organisation',
  'organizations.org-name-label': 'Nom de l’organisation',
  'organizations.org-name-placeholder': 'Mon Organisation',
  'organizations.slug-label': 'Identifiant',
  'organizations.slug-placeholder': 'mon-organisation',
  'organizations.create-org-btn': 'Créer l’organisation',
  'organizations.creating': 'Création en cours…',
  'organizations.back-to-orgs': '← Retour aux organisations',
  'organizations.members-title': 'Membres',
  'organizations.loading-members': 'Chargement des membres…',
  'organizations.no-members': 'Aucun membre trouvé.',
  'organizations.members-table-aria': 'Membres de l’organisation',
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
  'organizations.invite-role-aria': 'Rôle de l’invitation',
  'organizations.role-member': 'Membre',
  'organizations.role-admin': 'Administrateur',
  'organizations.send-invite-btn': 'Envoyer l’invitation',
  'organizations.accept-btn': 'Accepter',
  'organizations.accept-aria': 'Accepter l’invitation de',
  'organizations.revoke-btn': 'Révoquer',
  'organizations.revoke-aria': 'Révoquer l’invitation pour',
  'organizations.current-org-aria': 'Organisation actuelle',
  'organizations.switch-org-aria': 'Changer d’organisation',
  'organizations.no-org': 'Aucune organisation',
};

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
): { fixture: ComponentFixture<OrgsPageComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      OrgsPageComponent,
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
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(OrgsPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(OrgsPageComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgsPageComponent — unauthenticated', () => {
  it('should render sign-in prompt when unauthenticated', () => {
    const { fixture } = setup({ isAuthenticated: false });
    expect(fixture.nativeElement.textContent).toContain('sign in');
  });

  it('should NOT render org list when unauthenticated', () => {
    const { fixture } = setup({ isAuthenticated: false });
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
    const { fixture } = setup();
    expect(fixture.nativeElement.textContent).toContain('Organisations');
  });

  it('should render cf-create-org component', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-create-org')).not.toBeNull();
  });

  it('should show loading message when isLoadingOrgs is true', () => {
    const { fixture } = setup({ isLoadingOrgs: true });
    expect(fixture.nativeElement.textContent).toContain('Loading organisations');
  });

  it('should show empty state when no orgs and not loading', () => {
    const { fixture } = setup({ organizations: [], isLoadingOrgs: false });
    expect(fixture.nativeElement.textContent).toContain('not a member of any organisation');
  });

  it('should render org list when orgs exist', () => {
    const { fixture } = setup({ organizations: [ORG_A, ORG_B] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('ul[aria-label="Your organisations"]')).not.toBeNull();
  });

  it('should display each org name in the list', () => {
    const { fixture } = setup({ organizations: [ORG_A, ORG_B] });
    expect(fixture.nativeElement.textContent).toContain('Acme Corp');
    expect(fixture.nativeElement.textContent).toContain('Widgets Ltd');
  });

  it('should display org role badge for each org', () => {
    const { fixture } = setup({ organizations: [ORG_A] });
    expect(fixture.nativeElement.textContent).toContain('owner');
  });
});

describe('OrgsPageComponent — active org section', () => {
  it('should render active org section when activeOrgId is set', () => {
    const { fixture } = setup({ activeOrgId: 'org-1', activeOrg: ORG_A, organizations: [ORG_A] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('section[aria-label="Active organisation details"]')).not.toBeNull();
  });

  it('should NOT render active org section when activeOrgId is undefined', () => {
    const { fixture } = setup({ activeOrgId: undefined, organizations: [] });
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

describe('OrgsPageComponent — i18n FR', () => {
  it('[FR] should render "Chargement des organisations" when lang is fr', () => {
    const { fixture, translocoService } = setup({ isLoadingOrgs: true });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Chargement des organisations');
  });

  it('[FR] should render "Aucune invitation en attente" in invitations when lang is fr', () => {
    const { fixture, translocoService } = setup({ activeOrgId: 'org-1', activeOrg: ORG_A, organizations: [ORG_A] });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Aucune invitation en attente');
  });
});
