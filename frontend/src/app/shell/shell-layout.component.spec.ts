/**
 * Spec — shell-layout.component.ts (Wave 1 i18n RED)
 *
 * Establishes the Transloco test-harness pattern for all i18n waves.
 *
 * GREEN contract for the coder:
 *
 *   The shell layout template must be migrated to use Transloco for all
 *   user-visible strings EXCEPT the brand name "ClaudeForge" which stays
 *   literal. Keys live in the ROOT scope (no provideTranslocoScope needed).
 *
 *   Key list (en value / fr value):
 *     shell.nav-aria          → "Main navigation" / "Navigation principale"
 *     shell.nav.catalog       → "Catalog"          / "Catalogue"
 *     shell.nav.search        → "Search"            / "Rechercher"
 *     shell.nav.dashboard     → "Dashboard"         / "Tableau de bord"
 *     shell.nav.docs          → "Docs"              / "Documentation"
 *     shell.auth.sign-in      → "Sign in"           / "Se connecter"
 *     shell.auth.sign-out     → "Sign out"          / "Se déconnecter"
 *
 *   Template approach:
 *     - Use `| transloco` pipe for aria-label attributes:
 *         [attr.aria-label]="'shell.nav-aria' | transloco"
 *     - Interpolate link text via pipe or *transloco directive:
 *         {{ 'shell.nav.catalog' | transloco }}
 *     - Brand "ClaudeForge" stays as a literal string (NOT translated).
 *     - Component must NOT inject TranslocoService directly; use I18nFacade
 *       for any TS-side i18n needs (template only uses pipe/directive).
 *
 *   The ShellLayoutComponent must import TranslocoPipe (or TranslocoModule)
 *   in its `imports` array.
 *
 * TEST HARNESS PATTERN (canonical for all subsequent waves):
 *
 *   TranslocoTestingModule.forRoot({
 *     langs: {
 *       en: { 'key': 'English literal' },  // flat dot-delimited keys
 *       fr: { 'key': 'French translation' },
 *     },
 *     translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
 *     preloadLangs: true,
 *   })
 *
 *   En map returns EXACT current literals → all existing assertions stay green.
 *   Fr map returns French → fr-language assertions are RED until migration done.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable, NO_ERRORS_SCHEMA, signal, Signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { vi } from 'vitest';
import { ShellLayoutComponent } from './shell-layout.component';
import { TeamContextFacade } from '../features/team-context/application/facades/team-context.facade';
import { TeamContextStore } from '../features/team-context/application/store/team-context.store';
import { AuthFacade } from '../features/auth/application/facades/auth.facade';
import type { CurrentUser } from '../features/auth/domain/models/auth.models';
import { OrgContextFacade } from '../features/organizations/application/facades/org-context.facade';
import { CatalogFacade } from '../features/catalog/application/facades/catalog.facade';
import { I18nFacade } from '../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs — en returns EXACT current literals so existing
// assertions keep passing; fr returns French translations (RED until migrated)
// ---------------------------------------------------------------------------

const EN_SHELL_LANGS: Record<string, string> = {
  'shell.nav-aria': 'Main navigation',
  'shell.nav.catalog': 'Catalog',
  'shell.nav.search': 'Search',
  'shell.nav.dashboard': 'Dashboard',
  'shell.nav.docs': 'Docs',
  'shell.auth.sign-in': 'Sign in',
  'shell.auth.sign-out': 'Sign out',
};

const FR_SHELL_LANGS: Record<string, string> = {
  'shell.nav-aria': 'Navigation principale',
  'shell.nav.catalog': 'Catalogue',
  'shell.nav.search': 'Rechercher',
  'shell.nav.dashboard': 'Tableau de bord',
  'shell.nav.docs': 'Documentation',
  'shell.auth.sign-in': 'Se connecter',
  'shell.auth.sign-out': 'Se déconnecter',
};

// ---------------------------------------------------------------------------
// Stub facades — keep the component instantiable without real services
// ---------------------------------------------------------------------------

@Injectable()
class StubTeamContextFacade {
  private readonly _needsInit = signal(false);
  readonly needsInit: Signal<boolean> = this._needsInit.asReadonly();
  init = vi.fn();
  setTeamContext = vi.fn();
}

@Injectable()
class StubAuthFacade {
  private readonly _currentUser = signal<CurrentUser | undefined>(undefined);
  readonly currentUser: Signal<CurrentUser | undefined> = this._currentUser.asReadonly();

  setUser(user: CurrentUser | undefined): void {
    this._currentUser.set(user);
  }

  logout = vi.fn();
  silentRefresh = vi.fn();
}

@Injectable()
class StubOrgContextFacade {
  init = vi.fn();
  loadOrgs = vi.fn();
}

@Injectable()
class StubCatalogFacade {
  loadPlugins = vi.fn();
}

// ---------------------------------------------------------------------------
// Setup helper (canonical pattern for i18n wave specs)
// ---------------------------------------------------------------------------

function setup(): {
  fixture: ComponentFixture<ShellLayoutComponent>;
  component: ShellLayoutComponent;
  authStub: StubAuthFacade;
  translocoService: TranslocoService;
} {
  const authStub = new StubAuthFacade();
  const teamStub = new StubTeamContextFacade();
  const orgStub = new StubOrgContextFacade();
  const catalogStub = new StubCatalogFacade();

  TestBed.configureTestingModule({
    imports: [
      ShellLayoutComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_SHELL_LANGS, fr: FR_SHELL_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    // NO_ERRORS_SCHEMA suppresses unknown-element errors so we don't need
    // to declare the child components. Combined with overrideComponent below,
    // this lets us test only the shell's own template i18n strings.
    schemas: [NO_ERRORS_SCHEMA],
    providers: [
      provideRouter([]),
      { provide: TeamContextFacade, useValue: teamStub },
      { provide: TeamContextStore, useValue: {} },
      { provide: AuthFacade, useValue: authStub },
      { provide: OrgContextFacade, useValue: orgStub },
      { provide: CatalogFacade, useValue: catalogStub },
      // Real I18nFacade — injects TranslocoService from the testing module above.
      // translocoService.setActiveLang('fr') causes i18n.t() to re-evaluate
      // because the facade reads transloco.activeLang() internally.
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  });

  // Override the component's imports and providers so Angular doesn't try to
  // instantiate the full DI trees of child components (TeamSwitcher, OrgSwitcher,
  // TelemetrySettings, LanguageSwitcher). RouterOutlet/RouterLink/RouterLinkActive
  // are kept for routing assertions. NO_ERRORS_SCHEMA suppresses unknown-element
  // errors for the stripped children's selectors.
  // Note: no TranslocoPipe needed — the facade's i18n.t() handles all translations.
  TestBed.overrideComponent(ShellLayoutComponent, {
    set: {
      imports: [RouterOutlet, RouterLink, RouterLinkActive],
      providers: [],
      schemas: [NO_ERRORS_SCHEMA],
    },
  });

  const fixture = TestBed.createComponent(ShellLayoutComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);

  return { fixture, component: fixture.componentInstance, authStub, translocoService };
}

// ---------------------------------------------------------------------------
// Tests — EN (all should pass once migration is done AND right now if not
// yet migrated since en literals match hardcoded strings)
// ---------------------------------------------------------------------------

describe('ShellLayoutComponent — EN rendering (stays green through migration)', () => {
  it('renders the brand name "ClaudeForge" as literal (never translated)', () => {
    const { fixture } = setup();
    const brand = fixture.nativeElement.querySelector('.cf-shell__logo') as HTMLElement | null;
    expect(brand?.textContent?.trim()).toBe('ClaudeForge');
  });

  it('brand link navigates to /', () => {
    const { fixture } = setup();
    const brandLink = fixture.nativeElement.querySelector('.cf-shell__brand-link') as HTMLAnchorElement | null;
    expect(brandLink).toBeTruthy();
    expect(brandLink?.getAttribute('href')).toBe('/');
  });

  it('renders Catalog nav link', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Catalog');
  });

  it('renders Search nav link', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Search');
  });

  it('renders Dashboard nav link', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Dashboard');
  });

  it('renders Docs nav link', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Docs');
  });

  it('renders Sign in when user is not authenticated', () => {
    const { fixture } = setup();
    const signIn = fixture.nativeElement.querySelector('.cf-shell__sign-in') as HTMLElement | null;
    expect(signIn?.textContent?.trim()).toContain('Sign in');
  });

  it('renders Sign out button when user is authenticated', () => {
    const { fixture, authStub } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    } satisfies CurrentUser);
    fixture.detectChanges();
    const signOut = fixture.nativeElement.querySelector('.cf-shell__sign-out') as HTMLButtonElement | null;
    expect(signOut?.textContent?.trim()).toContain('Sign out');
  });

  it('nav element has aria-label "Main navigation"', () => {
    const { fixture } = setup();
    const nav = fixture.nativeElement.querySelector('nav') as HTMLElement | null;
    expect(nav?.getAttribute('aria-label')).toContain('Main navigation');
  });

  it('renders exactly 4 nav links', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    expect(links.length).toBe(4);
  });
});

// ---------------------------------------------------------------------------
// Tests — FR (RED: these will fail until the template uses Transloco keys
// and the component is properly migrated)
// ---------------------------------------------------------------------------

describe('ShellLayoutComponent — FR rendering (RED — fails until migration)', () => {
  it('renders Catalogue (FR) nav link when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Catalogue');
  });

  it('renders Rechercher (FR) nav link when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Rechercher');
  });

  it('renders Tableau de bord (FR) nav link when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Tableau de bord');
  });

  it('renders Documentation (FR) nav link when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Documentation');
  });

  it('renders Se connecter (FR) for sign-in when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const signIn = fixture.nativeElement.querySelector('.cf-shell__sign-in') as HTMLElement | null;
    expect(signIn?.textContent?.trim()).toContain('Se connecter');
  });

  it('renders Se déconnecter (FR) for sign-out when lang is fr and user is authenticated', () => {
    const { fixture, authStub, translocoService } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    } satisfies CurrentUser);
    fixture.detectChanges();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const signOut = fixture.nativeElement.querySelector('.cf-shell__sign-out') as HTMLButtonElement | null;
    expect(signOut?.textContent?.trim()).toContain('Se déconnecter');
  });

  it('nav aria-label is "Navigation principale" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav') as HTMLElement | null;
    expect(nav?.getAttribute('aria-label')).toBe('Navigation principale');
  });

  it('brand "ClaudeForge" stays literal even when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const brand = fixture.nativeElement.querySelector('.cf-shell__logo') as HTMLElement | null;
    expect(brand?.textContent?.trim()).toBe('ClaudeForge');
  });
});

// ---------------------------------------------------------------------------
// Sign-out behaviour
// ---------------------------------------------------------------------------

describe('ShellLayoutComponent — auth interaction', () => {
  it('clicking Sign out calls authFacade.logout()', () => {
    const { fixture, authStub } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    } satisfies CurrentUser);
    fixture.detectChanges();

    const signOutBtn = fixture.nativeElement.querySelector('.cf-shell__sign-out') as HTMLButtonElement | null;
    signOutBtn?.click();
    fixture.detectChanges();

    expect(authStub.logout).toHaveBeenCalledOnce();
  });
});
