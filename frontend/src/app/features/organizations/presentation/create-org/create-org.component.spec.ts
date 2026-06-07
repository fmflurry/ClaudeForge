/**
 * CreateOrgComponent — render + form wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { CreateOrgComponent } from './create-org.component';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for organizations scope
// ---------------------------------------------------------------------------

const EN_LANGS: Record<string, string> = {
  'organizations.create-org-title': 'Create Organisation',
  'organizations.org-name-label': 'Organisation Name',
  'organizations.org-name-placeholder': 'My Organisation',
  'organizations.slug-label': 'Slug',
  'organizations.slug-placeholder': 'my-organisation',
  'organizations.create-org-btn': 'Create Organisation',
  'organizations.creating': 'Creating…',
};

const FR_LANGS: Record<string, string> = {
  'organizations.create-org-title': 'Créer une organisation',
  'organizations.org-name-label': "Nom de l'organisation",
  'organizations.org-name-placeholder': 'Mon Organisation',
  'organizations.slug-label': 'Identifiant',
  'organizations.slug-placeholder': 'mon-organisation',
  'organizations.create-org-btn': "Créer l'organisation",
  'organizations.creating': 'Création en cours…',
};

// ---------------------------------------------------------------------------
// Fakes
// ---------------------------------------------------------------------------

function buildFakeOrgsFacade(opts: {
  isLoadingOrgs?: boolean;
  orgsError?: { code: string; message: string }[] | undefined;
  createOrgSpy?: (name: string, slug: string) => void;
}): Partial<OrganizationsFacade> {
  const isLoadingSignal = signal(opts.isLoadingOrgs ?? false);
  const orgsErrorSignal = signal(opts.orgsError);

  return {
    get isLoadingOrgs() {
      return isLoadingSignal.asReadonly();
    },
    get orgsError() {
      return orgsErrorSignal.asReadonly();
    },
    createOrg: opts.createOrgSpy ?? (() => undefined),
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
    isLoadingOrgs?: boolean;
    orgsError?: { code: string; message: string }[] | undefined;
    createOrgSpy?: (name: string, slug: string) => void;
  } = {},
): { fixture: ComponentFixture<CreateOrgComponent>; translocoService: TranslocoService } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      CreateOrgComponent,
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
          isLoadingOrgs: opts.isLoadingOrgs,
          orgsError: opts.orgsError,
          createOrgSpy: opts.createOrgSpy,
        }),
      },
      {
        provide: AuthFacade,
        useValue: buildFakeAuthFacade(opts.isAuthenticated ?? true),
      },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(CreateOrgComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(CreateOrgComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// Unauthenticated
// ---------------------------------------------------------------------------

describe('CreateOrgComponent — unauthenticated', () => {
  it('should not render form when unauthenticated', () => {
    const { fixture } = setup({ isAuthenticated: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-create-org')).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Authenticated — basic render
// ---------------------------------------------------------------------------

describe('CreateOrgComponent — authenticated render', () => {
  it('should render the Create Organisation heading', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.textContent).toContain('Create Organisation');
  });

  it('should render the org name input', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#org-name')).not.toBeNull();
  });

  it('should render the slug input', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#org-slug')).not.toBeNull();
  });

  it('should render the submit button', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('button[type="submit"]')).not.toBeNull();
  });

  it('should NOT show error when orgsError is undefined', () => {
    const { fixture } = setup({ orgsError: undefined });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-create-org__error')).toBeNull();
  });

  it('should show error message when orgsError is set', () => {
    const { fixture } = setup({ orgsError: [{ code: 'CREATE_ERROR', message: 'Slug already taken' }] });
    const el = fixture.nativeElement as HTMLElement;
    const errorEl = el.querySelector('[role="alert"]');
    expect(errorEl).not.toBeNull();
    expect(errorEl?.textContent).toContain('Slug already taken');
  });

  it('should show "Creating…" text when isLoadingOrgs is true', () => {
    const { fixture } = setup({ isLoadingOrgs: true });
    expect(fixture.nativeElement.textContent).toContain('Creating');
  });

  it('should disable submit button when isLoadingOrgs is true', () => {
    const { fixture } = setup({ isLoadingOrgs: true });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(btn?.disabled).toBe(true);
  });

  it('should disable submit button when name is empty', () => {
    const { fixture } = setup();
    // No input set, signals default to ''
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(btn?.disabled).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// onSubmit
// ---------------------------------------------------------------------------

describe('CreateOrgComponent — onSubmit', () => {
  it('should call facade.createOrg with trimmed name and slug', () => {
    const createOrgSpy = vi.fn();
    const { fixture } = setup({ createOrgSpy });
    const comp = fixture.componentInstance;
    comp.name.set('  My Org  ');
    comp.slug.set('  my-org  ');
    comp.onSubmit();
    expect(createOrgSpy).toHaveBeenCalledWith('My Org', 'my-org');
  });

  it('should NOT call facade.createOrg when name is empty', () => {
    const createOrgSpy = vi.fn();
    const { fixture } = setup({ createOrgSpy });
    const comp = fixture.componentInstance;
    comp.name.set('');
    comp.slug.set('my-org');
    comp.onSubmit();
    expect(createOrgSpy).not.toHaveBeenCalled();
  });

  it('should NOT call facade.createOrg when slug is empty', () => {
    const createOrgSpy = vi.fn();
    const { fixture } = setup({ createOrgSpy });
    const comp = fixture.componentInstance;
    comp.name.set('My Org');
    comp.slug.set('');
    comp.onSubmit();
    expect(createOrgSpy).not.toHaveBeenCalled();
  });

  it('should NOT call facade.createOrg when both name and slug are whitespace only', () => {
    const createOrgSpy = vi.fn();
    const { fixture } = setup({ createOrgSpy });
    const comp = fixture.componentInstance;
    comp.name.set('   ');
    comp.slug.set('  ');
    comp.onSubmit();
    expect(createOrgSpy).not.toHaveBeenCalled();
  });

  it('should not throw on submit', () => {
    const { fixture } = setup();
    const comp = fixture.componentInstance;
    comp.name.set('Test');
    comp.slug.set('test');
    expect(() => comp.onSubmit()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// inputValue helper
// ---------------------------------------------------------------------------

describe('CreateOrgComponent — inputValue helper', () => {
  it('should extract value from input event target', () => {
    const { fixture } = setup();
    const comp = fixture.componentInstance;
    const mockEvent = { target: { value: 'hello' } } as unknown as Event;
    expect(comp.inputValue(mockEvent)).toBe('hello');
  });
});

// ---------------------------------------------------------------------------
// i18n FR
// ---------------------------------------------------------------------------

describe('CreateOrgComponent — i18n FR', () => {
  it('[FR] should render "Créer une organisation" heading when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Créer une organisation');
  });

  it('[FR] should render "Créer l\'organisation" button text when lang is fr and not loading', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("Créer l'organisation");
  });

  it('[FR] should render "Création en cours…" when isLoadingOrgs is true and lang is fr', () => {
    const { fixture, translocoService } = setup({ isLoadingOrgs: true });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Création en cours');
  });
});
