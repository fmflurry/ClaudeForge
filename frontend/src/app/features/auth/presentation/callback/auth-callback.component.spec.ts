/**
 * AuthCallbackComponent — render + callback wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { AuthCallbackComponent } from './auth-callback.component';
import { AuthFacade } from '../../application/facades/auth.facade';
import type { AuthStatus } from '../../domain/models/auth.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for auth scope
// ---------------------------------------------------------------------------

const EN_AUTH_LANGS: Record<string, string> = {
  'auth.login.subtitle': 'Access your plugin marketplace account',
  'auth.login.sign-in-google': 'Sign in with Google',
  'auth.login.sign-in-microsoft': 'Sign in with Microsoft',
  'auth.login.redirecting': 'Redirecting to provider…',
  'auth.callback.completing': 'Completing sign-in…',
  'auth.callback.error-title': 'Sign-in failed',
  'auth.callback.retry': 'Try again',
};

const FR_AUTH_LANGS: Record<string, string> = {
  'auth.login.subtitle': 'Accédez à votre compte de la Marketplace de plugins',
  'auth.login.sign-in-google': 'Se connecter avec Google',
  'auth.login.sign-in-microsoft': 'Se connecter avec Microsoft',
  'auth.login.redirecting': 'Redirection vers le fournisseur…',
  'auth.callback.completing': 'Finalisation de la connexion…',
  'auth.callback.error-title': 'Échec de la connexion',
  'auth.callback.retry': 'Réessayer',
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function buildFakeAuthFacade(opts: {
  authStatus?: AuthStatus;
  authError?: string | undefined;
  isAuthenticated?: boolean;
  completeLoginSpy?: (code: string, state: string) => void;
}): Partial<AuthFacade> {
  const authStatusSignal = signal<AuthStatus>(opts.authStatus ?? 'idle');
  const authErrorSignal = signal<string | undefined>(opts.authError);
  const isAuthenticatedSignal = signal(opts.isAuthenticated ?? false);

  return {
    get authStatus() {
      return authStatusSignal.asReadonly();
    },
    get authError() {
      return authErrorSignal.asReadonly();
    },
    get isAuthenticated() {
      return isAuthenticatedSignal.asReadonly();
    },
    completeLogin: opts.completeLoginSpy ?? (() => undefined),
  };
}

function buildFakeActivatedRoute(params: { code?: string; state?: string } = {}): Partial<ActivatedRoute> {
  return {
    snapshot: {
      queryParamMap: {
        get: (key: string): string | null => {
          if (key === 'code') return params.code ?? null;
          if (key === 'state') return params.state ?? null;
          return null;
        },
      },
    } as ActivatedRoute['snapshot'],
  };
}

function setup(
  opts: {
    authStatus?: AuthStatus;
    authError?: string | undefined;
    isAuthenticated?: boolean;
    completeLoginSpy?: (code: string, state: string) => void;
    routeParams?: { code?: string; state?: string };
  } = {},
): {
  fixture: ComponentFixture<AuthCallbackComponent>;
  fakeRouter: { navigate: ReturnType<typeof vi.fn> };
  translocoService: TranslocoService;
} {
  const fakeRouter = { navigate: vi.fn().mockResolvedValue(true) };
  const fakeFacade = buildFakeAuthFacade(opts);
  const fakeRoute = buildFakeActivatedRoute(opts.routeParams ?? { code: 'test-code', state: 'test-state' });

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      AuthCallbackComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_AUTH_LANGS, fr: FR_AUTH_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: AuthFacade, useValue: fakeFacade },
      { provide: ActivatedRoute, useValue: fakeRoute },
      { provide: Router, useValue: fakeRouter },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(AuthCallbackComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(AuthCallbackComponent);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, fakeRouter, translocoService };
}

// ---------------------------------------------------------------------------
// Render tests — loading state
// ---------------------------------------------------------------------------

describe('AuthCallbackComponent — loading / in-progress render', () => {
  it('should instantiate', () => {
    const { fixture } = setup({ authStatus: 'authenticating' });
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should show loading text when status is not "error"', () => {
    const { fixture } = setup({ authStatus: 'authenticating' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Completing sign-in');
  });

  it('should show loading container with role="status" when not in error state', () => {
    const { fixture } = setup({ authStatus: 'idle' });
    fixture.detectChanges();
    const statusEl = fixture.debugElement.query(By.css('[role="status"]'));
    expect(statusEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Render tests — error state
// ---------------------------------------------------------------------------

describe('AuthCallbackComponent — error state render', () => {
  it('should show error UI when authStatus is "error"', () => {
    const { fixture } = setup({ authStatus: 'error', authError: 'Invalid code' });
    fixture.detectChanges();
    const alertEl = fixture.debugElement.query(By.css('[role="alert"]'));
    expect(alertEl).not.toBeNull();
  });

  it('should display authError message in error state', () => {
    const { fixture } = setup({ authStatus: 'error', authError: 'Something went wrong' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Something went wrong');
  });

  it('should show "Sign-in failed" heading in error state', () => {
    const { fixture } = setup({ authStatus: 'error', authError: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sign-in failed');
  });

  it('should show a retry link in error state', () => {
    const { fixture } = setup({ authStatus: 'error', authError: 'Error' });
    fixture.detectChanges();
    const link = fixture.debugElement.query(By.css('a[href="/login"]'));
    expect(link).not.toBeNull();
  });

  it('should NOT show loading UI when in error state', () => {
    const { fixture } = setup({ authStatus: 'error', authError: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Completing sign-in');
  });
});

// ---------------------------------------------------------------------------
// i18n — French language switch
// ---------------------------------------------------------------------------

describe('AuthCallbackComponent — i18n French', () => {
  it('should render French completing text after switching to fr', () => {
    const { fixture, translocoService } = setup({ authStatus: 'authenticating' });
    fixture.detectChanges();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Finalisation de la connexion');
  });

  it('should render French error title after switching to fr', () => {
    const { fixture, translocoService } = setup({ authStatus: 'error', authError: 'Error' });
    fixture.detectChanges();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Échec de la connexion');
  });
});

// ---------------------------------------------------------------------------
// ngOnInit wiring
// ---------------------------------------------------------------------------

describe('AuthCallbackComponent — ngOnInit wiring', () => {
  it('should call facade.completeLogin with code and state from query params', () => {
    const spy = vi.fn();
    const { fixture } = setup({
      completeLoginSpy: spy,
      routeParams: { code: 'auth-code-123', state: 'state-abc' },
    });
    fixture.detectChanges(); // triggers ngOnInit

    expect(spy).toHaveBeenCalledWith('auth-code-123', 'state-abc');
  });

  it('should call facade.completeLogin with empty strings when params are absent', () => {
    const spy = vi.fn();
    const { fixture } = setup({
      completeLoginSpy: spy,
      routeParams: {},
    });
    fixture.detectChanges();

    expect(spy).toHaveBeenCalledWith('', '');
  });

  it('should navigate to "/" after completeLogin when isAuthenticated is true', async () => {
    const { fixture, fakeRouter } = setup({
      isAuthenticated: true,
      authStatus: 'authenticated',
      routeParams: { code: 'c', state: 's' },
    });
    fixture.detectChanges();

    // Wait for the microtask (Promise.resolve().then)
    await Promise.resolve();

    expect(fakeRouter.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should NOT navigate when isAuthenticated is false', async () => {
    const { fixture, fakeRouter } = setup({
      isAuthenticated: false,
      authStatus: 'error',
      routeParams: { code: 'bad', state: 's' },
    });
    fixture.detectChanges();

    await Promise.resolve();

    expect(fakeRouter.navigate).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('AuthCallbackComponent — architecture boundary', () => {
  it('should compile with AuthFacade, ActivatedRoute, Router provided', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });
});
