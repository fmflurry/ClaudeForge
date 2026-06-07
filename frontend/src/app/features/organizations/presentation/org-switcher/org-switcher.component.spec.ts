/**
 * OrgSwitcherComponent — tests for empty/loaded and auth-gated states.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OrgSwitcherComponent } from './org-switcher.component';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import type { OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const ORG_A: OrgSummary = { orgId: 'org-1', name: 'Acme Corp', slug: 'acme', role: 'owner' };
const ORG_B: OrgSummary = { orgId: 'org-2', name: 'Widgets', slug: 'widgets', role: 'member' };

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function buildFakeContextFacade(opts: {
  organizations?: OrgSummary[];
  activeOrg?: OrgSummary | undefined;
}): Partial<OrgContextFacade> {
  const orgsSignal = signal(opts.organizations ?? []);
  const activeOrgSignal = signal(opts.activeOrg);

  return {
    get organizations() {
      return orgsSignal.asReadonly();
    },
    get activeOrg() {
      return activeOrgSignal.asReadonly();
    },
    setActiveOrg: () => undefined,
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

function setup(opts: {
  organizations?: OrgSummary[];
  activeOrg?: OrgSummary | undefined;
  isAuthenticated?: boolean;
}): ComponentFixture<OrgSwitcherComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [OrgSwitcherComponent],
    providers: [
      {
        provide: OrgContextFacade,
        useValue: buildFakeContextFacade({
          organizations: opts.organizations ?? [],
          activeOrg: opts.activeOrg,
        }),
      },
      {
        provide: AuthFacade,
        useValue: buildFakeAuthFacade(opts.isAuthenticated ?? true),
      },
    ],
  });
  const fixture = TestBed.createComponent(OrgSwitcherComponent);
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('OrgSwitcherComponent — unauthenticated', () => {
  it('should not render switcher when unauthenticated', () => {
    const fixture = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-org-switcher')).toBeNull();
  });
});

describe('OrgSwitcherComponent — no active org', () => {
  it('should show "No organisation" when activeOrg is undefined', () => {
    const fixture = setup({ isAuthenticated: true, organizations: [], activeOrg: undefined });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No organisation');
  });
});

describe('OrgSwitcherComponent — single org', () => {
  it('should display the active org name', () => {
    const fixture = setup({ isAuthenticated: true, organizations: [ORG_A], activeOrg: ORG_A });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Acme Corp');
  });

  it('should NOT render a select when only one org exists', () => {
    const fixture = setup({ isAuthenticated: true, organizations: [ORG_A], activeOrg: ORG_A });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('select')).toBeNull();
  });
});

describe('OrgSwitcherComponent — multiple orgs', () => {
  it('should render a select when multiple orgs exist', () => {
    const fixture = setup({
      isAuthenticated: true,
      organizations: [ORG_A, ORG_B],
      activeOrg: ORG_A,
    });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('select')).not.toBeNull();
  });

  it('should list all org options in the select', () => {
    const fixture = setup({
      isAuthenticated: true,
      organizations: [ORG_A, ORG_B],
      activeOrg: ORG_A,
    });
    const el = fixture.nativeElement as HTMLElement;
    const options = el.querySelectorAll('option');
    expect(options.length).toBe(2);
  });
});
