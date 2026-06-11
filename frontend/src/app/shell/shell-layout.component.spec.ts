/**
 * Spec — shell-layout.component.ts (Wave 1 i18n + optimize-landing-page-layout)
 *
 * Changes from optimize-landing-page-layout:
 *   - shell.nav.catalog now resolves to "Plugins" (EN) / "Plugins" (FR)
 *   - The Search nav item has been removed from the shell
 *   - Tableau de bord (Dashboard) is auth-gated (@if currentUser())
 *
 * GREEN contract (already implemented):
 *   - Nav links: Plugins, [Dashboard — auth only], Documentation
 *   - No Search nav link
 *   - shell.nav.catalog → "Plugins" in both EN and FR
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
// Transloco test langs — reflects the optimize-landing-page-layout change:
//   shell.nav.catalog  → "Plugins"  (was "Catalog" / "Catalogue")
//   shell.nav.search   → removed (key kept in map for backward compat but no link rendered)
// ---------------------------------------------------------------------------

const EN_SHELL_LANGS: Record<string, string> = {
  'shell.nav-aria': 'Main navigation',
  'shell.nav.catalog': 'Plugins',
  'shell.nav.dashboard': 'Dashboard',
  'shell.nav.docs': 'Docs',
  'shell.auth.sign-in': 'Sign in',
  'shell.auth.sign-out': 'Sign out',
};

const FR_SHELL_LANGS: Record<string, string> = {
  'shell.nav-aria': 'Navigation principale',
  'shell.nav.catalog': 'Plugins',
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
    // to declare the child components.
    schemas: [NO_ERRORS_SCHEMA],
    providers: [
      provideRouter([]),
      { provide: TeamContextFacade, useValue: teamStub },
      { provide: TeamContextStore, useValue: {} },
      { provide: AuthFacade, useValue: authStub },
      { provide: OrgContextFacade, useValue: orgStub },
      { provide: CatalogFacade, useValue: catalogStub },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  });

  // Override the component's imports and providers so Angular doesn't try to
  // instantiate the full DI trees of child components.
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
// Tests — EN (anonymous user — no Dashboard link)
// ---------------------------------------------------------------------------

describe('ShellLayoutComponent — EN rendering (stays green through migration)', () => {
  it('renders the brand logo image', () => {
    const { fixture } = setup();
    const logoImg = fixture.nativeElement.querySelector('.cf-shell__logo-img') as HTMLImageElement | null;
    expect(logoImg).toBeTruthy();
    expect(logoImg?.getAttribute('src')).toBe('/logo-assets/claudeforge-header-logo-fit.png');
    expect(logoImg?.getAttribute('alt')).toBe('ClaudeForge');
  });

  it('brand link navigates to /', () => {
    const { fixture } = setup();
    const brandLink = fixture.nativeElement.querySelector('.cf-shell__brand-link') as HTMLAnchorElement | null;
    expect(brandLink).toBeTruthy();
    expect(brandLink?.getAttribute('href')).toBe('/');
  });

  // Task 4.1 — shell.nav.catalog now resolves to "Plugins"
  it('renders Plugins nav link (task 4.1: shell.nav.catalog → Plugins)', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Plugins');
  });

  it('does NOT render Search nav link (removed)', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).not.toContain('Search');
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

  // Anonymous: Plugins + Docs = 2 nav links
  it('renders exactly 2 nav links when user is NOT authenticated', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    expect(links.length).toBe(2);
  });

  // Task 4.2 — auth-gated Dashboard
  it('does NOT render Dashboard nav link when user is NOT authenticated', () => {
    const { fixture } = setup();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).not.toContain('Dashboard');
  });

  it('renders exactly 3 nav links when user IS authenticated (Plugins + Dashboard + Docs)', () => {
    const { fixture, authStub } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    } satisfies CurrentUser);
    fixture.detectChanges();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    expect(links.length).toBe(3);
  });

  it('renders Dashboard nav link when user IS authenticated (task 4.2)', () => {
    const { fixture, authStub } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    } satisfies CurrentUser);
    fixture.detectChanges();
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Dashboard');
  });
});

// ---------------------------------------------------------------------------
// Tests — FR (RED: fail until template is migrated to use Transloco i18n.t())
// ---------------------------------------------------------------------------

describe('ShellLayoutComponent — FR rendering (RED — fails until migration)', () => {
  // Task 4.1 — shell.nav.catalog in FR → "Plugins" (same in both languages per spec)
  it('renders Plugins (FR) nav link when lang is fr (task 4.1)', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Plugins');
  });

  it('does NOT render Rechercher (FR) nav link when lang is fr (Search nav removed)', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).not.toContain('Rechercher');
  });

  // Task 4.2 — auth-gated Tableau de bord in FR
  it('renders Tableau de bord (FR) nav link when lang is fr and user is authenticated (task 4.2)', () => {
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

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).toContain('Tableau de bord');
  });

  it('does NOT render Tableau de bord (FR) nav link when user is NOT authenticated', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.cf-shell__nav-link');
    const texts = Array.from(links).map((l) => l.textContent?.trim());
    expect(texts).not.toContain('Tableau de bord');
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
