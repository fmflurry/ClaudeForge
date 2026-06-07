/**
 * OrgInvitationsComponent — render + action wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { OrgInvitationsComponent } from './org-invitations.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';
import type { OrgInvitation, OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Transloco test langs for organizations scope
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
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
// Fixtures
// ---------------------------------------------------------------------------

const PENDING_INV: OrgInvitation = {
  invitationId: 'inv-1',
  orgId: 'org-1',
  email: 'bob@example.com',
  role: 'member',
  status: 'pending',
  createdAt: new Date('2024-03-01T00:00:00.000Z'),
  expiresAt: new Date('2024-03-08T00:00:00.000Z'),
};

const ACCEPTED_INV: OrgInvitation = {
  invitationId: 'inv-2',
  orgId: 'org-1',
  email: 'carol@example.com',
  role: 'admin',
  status: 'accepted',
  createdAt: new Date('2024-02-01T00:00:00.000Z'),
  expiresAt: new Date('2024-02-08T00:00:00.000Z'),
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
// Fakes
// ---------------------------------------------------------------------------

function buildFakeOrgsFacade(opts: {
  invitations?: OrgInvitation[];
  inviteSpy?: (orgId: string, email: string, role: string) => void;
  acceptSpy?: (orgId: string, invId: string) => void;
  revokeSpy?: (orgId: string, invId: string) => void;
}): Partial<OrganizationsFacade> {
  const invitationsSignal = signal(opts.invitations ?? []);

  return {
    get invitations() {
      return invitationsSignal.asReadonly();
    },
    invite: opts.inviteSpy ?? (() => undefined),
    acceptInvitation: opts.acceptSpy ?? (() => undefined),
    revokeInvitation: opts.revokeSpy ?? (() => undefined),
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

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(
  opts: {
    isAuthenticated?: boolean;
    invitations?: OrgInvitation[];
    activeOrg?: OrgSummary;
    orgId?: string;
    inviteSpy?: (orgId: string, email: string, role: string) => void;
    acceptSpy?: (orgId: string, invId: string) => void;
    revokeSpy?: (orgId: string, invId: string) => void;
  } = {},
): { fixture: ComponentFixture<OrgInvitationsComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      OrgInvitationsComponent,
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
          invitations: opts.invitations ?? [],
          inviteSpy: opts.inviteSpy,
          acceptSpy: opts.acceptSpy,
          revokeSpy: opts.revokeSpy,
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
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(OrgInvitationsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(OrgInvitationsComponent);
  fixture.componentRef.setInput('orgId', opts.orgId ?? 'org-1');
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgInvitationsComponent — unauthenticated', () => {
  it('should not render invitations section when unauthenticated', () => {
    const { fixture } = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-inv')).toBeNull();
  });
});

describe('OrgInvitationsComponent — empty state', () => {
  it('should show "No pending invitations" when list is empty', () => {
    const { fixture } = setup({ invitations: [] });
    expect(fixture.nativeElement.textContent).toContain('No pending invitations');
  });

  it('should render the Invitations heading', () => {
    const { fixture } = setup({ invitations: [] });
    expect(fixture.nativeElement.textContent).toContain('Invitations');
  });
});

describe('OrgInvitationsComponent — role-gated invite form', () => {
  it('should show invite form for owner', () => {
    const { fixture } = setup({ activeOrg: OWNER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-inv__send-form')).not.toBeNull();
  });

  it('should NOT show invite form for member', () => {
    const { fixture } = setup({ activeOrg: MEMBER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-inv__send-form')).toBeNull();
  });
});

describe('OrgInvitationsComponent — invitation list rendering', () => {
  it('should render pending invitation email', () => {
    const { fixture } = setup({ invitations: [PENDING_INV] });
    expect(fixture.nativeElement.textContent).toContain('bob@example.com');
  });

  it('should render invitation role', () => {
    const { fixture } = setup({ invitations: [PENDING_INV] });
    expect(fixture.nativeElement.textContent).toContain('member');
  });

  it('should render invitation status', () => {
    const { fixture } = setup({ invitations: [PENDING_INV] });
    expect(fixture.nativeElement.textContent).toContain('pending');
  });

  it('should show Accept button for pending invitation', () => {
    const { fixture } = setup({ invitations: [PENDING_INV] });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Accept invitation from"]');
    expect(btn).not.toBeNull();
  });

  it('should show Revoke button for owner with pending invitation', () => {
    const { fixture } = setup({ invitations: [PENDING_INV], activeOrg: OWNER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Revoke invitation for"]');
    expect(btn).not.toBeNull();
  });

  it('should NOT show Revoke button for member role', () => {
    const { fixture } = setup({ invitations: [PENDING_INV], activeOrg: MEMBER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label^="Revoke invitation for"]');
    expect(btn).toBeNull();
  });

  it('should NOT show Accept/Revoke buttons for accepted invitation', () => {
    const { fixture } = setup({ invitations: [ACCEPTED_INV], activeOrg: OWNER_ORG });
    const el = fixture.nativeElement as HTMLElement;
    const acceptBtn = el.querySelector('button[aria-label^="Accept invitation from"]');
    const revokeBtn = el.querySelector('button[aria-label^="Revoke invitation for"]');
    expect(acceptBtn).toBeNull();
    expect(revokeBtn).toBeNull();
  });
});

describe('OrgInvitationsComponent — actions', () => {
  it('should call orgsFacade.acceptInvitation when Accept is clicked', () => {
    const acceptSpy = vi.fn();
    const { fixture } = setup({ invitations: [PENDING_INV], acceptSpy });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label^="Accept invitation from"]');
    btn?.click();
    expect(acceptSpy).toHaveBeenCalledWith('org-1', 'inv-1');
  });

  it('should call orgsFacade.revokeInvitation when Revoke is clicked', () => {
    const revokeSpy = vi.fn();
    const { fixture } = setup({ invitations: [PENDING_INV], activeOrg: OWNER_ORG, revokeSpy });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label^="Revoke invitation for"]');
    btn?.click();
    expect(revokeSpy).toHaveBeenCalledWith('org-1', 'inv-1');
  });
});

describe('OrgInvitationsComponent — invite form submission', () => {
  it('should call invite with email and role on submit', () => {
    const inviteSpy = vi.fn();
    const { fixture } = setup({ activeOrg: OWNER_ORG, inviteSpy });
    const comp = fixture.componentInstance;
    comp.inviteEmail.set('dave@example.com');
    comp.inviteRole.set('admin');
    comp.onSendInvite();
    expect(inviteSpy).toHaveBeenCalledWith('org-1', 'dave@example.com', 'admin');
  });

  it('should NOT call invite when email is empty', () => {
    const inviteSpy = vi.fn();
    const { fixture } = setup({ activeOrg: OWNER_ORG, inviteSpy });
    const comp = fixture.componentInstance;
    comp.inviteEmail.set('');
    comp.onSendInvite();
    expect(inviteSpy).not.toHaveBeenCalled();
  });

  it('should reset inviteEmail to empty string after successful invite', () => {
    const { fixture } = setup({ activeOrg: OWNER_ORG });
    const comp = fixture.componentInstance;
    comp.inviteEmail.set('someone@example.com');
    comp.onSendInvite();
    expect(comp.inviteEmail()).toBe('');
  });
});

describe('OrgInvitationsComponent — inputValue / selectValue helpers', () => {
  it('inputValue should extract value from event target', () => {
    const { fixture } = setup();
    const comp = fixture.componentInstance;
    const event = { target: { value: 'test@example.com' } } as unknown as Event;
    expect(comp.inputValue(event)).toBe('test@example.com');
  });

  it('selectValue should extract value from select event target', () => {
    const { fixture } = setup();
    const comp = fixture.componentInstance;
    const event = { target: { value: 'admin' } } as unknown as Event;
    expect(comp.selectValue(event)).toBe('admin');
  });
});

describe('OrgInvitationsComponent — i18n FR', () => {
  it('[FR] should show "Aucune invitation en attente" when list is empty and lang is fr', () => {
    const { fixture, translocoService } = setup({ invitations: [] });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Aucune invitation en attente');
  });

  it('[FR] should show "Envoyer l\'invitation" button when owner and lang is fr', () => {
    const { fixture, translocoService } = setup({ activeOrg: OWNER_ORG });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("Envoyer l'invitation");
  });

  it('[FR] should show "Accepter" button for pending invitation when lang is fr', () => {
    const { fixture, translocoService } = setup({ invitations: [PENDING_INV] });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Accepter');
  });
});
