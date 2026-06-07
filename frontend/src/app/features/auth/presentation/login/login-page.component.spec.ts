/**
 * LoginPageComponent — render + primary action wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { LoginPageComponent } from './login-page.component';
import { AuthFacade } from '../../application/facades/auth.facade';
import type { AuthStatus } from '../../domain/models/auth.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for auth scope
//
// En map returns EXACT current literals so all existing rendered-text
// assertions keep passing unchanged after migration.
// Fr map returns French — used in fr-switch assertions.
//
// Key namespace: 'auth' scope loaded via provideTranslocoScope('auth').
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
  'auth.login.subtitle': 'Accédez à votre compte de la place de marché de plugins',
  'auth.login.sign-in-google': 'Se connecter avec Google',
  'auth.login.sign-in-microsoft': 'Se connecter avec Microsoft',
  'auth.login.redirecting': 'Redirection vers le fournisseur…',
  'auth.callback.completing': 'Finalisation de la connexion…',
  'auth.callback.error-title': 'Échec de la connexion',
  'auth.callback.retry': 'Réessayer',
};

// ---------------------------------------------------------------------------
// Fake AuthFacade
// ---------------------------------------------------------------------------

function buildFakeAuthFacade(opts: {
  isAuthenticating?: boolean;
  authStatus?: AuthStatus;
  authError?: string | undefined;
  loginSpy?: (provider: string) => void;
}): Partial<AuthFacade> {
  const isAuthenticatingSignal = signal(opts.isAuthenticating ?? false);
  const authStatusSignal = signal<AuthStatus>(opts.authStatus ?? 'idle');
  const authErrorSignal = signal<string | undefined>(opts.authError);

  return {
    get isAuthenticating() {
      return isAuthenticatingSignal.asReadonly();
    },
    get authStatus() {
      return authStatusSignal.asReadonly();
    },
    get authError() {
      return authErrorSignal.asReadonly();
    },
    login: opts.loginSpy ?? (() => undefined),
  };
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(
  opts: {
    isAuthenticating?: boolean;
    authStatus?: AuthStatus;
    authError?: string | undefined;
    loginSpy?: (provider: string) => void;
  } = {},
): {
  fixture: ComponentFixture<LoginPageComponent>;
  fakeFacade: Partial<AuthFacade>;
  translocoService: TranslocoService;
} {
  const fakeFacade = buildFakeAuthFacade(opts);
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      LoginPageComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_AUTH_LANGS, fr: FR_AUTH_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: AuthFacade, useValue: fakeFacade },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(LoginPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(LoginPageComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, fakeFacade, translocoService };
}

// ---------------------------------------------------------------------------
// Render tests
// ---------------------------------------------------------------------------

describe('LoginPageComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render "Sign in to ClaudeForge" heading', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.textContent).toContain('Sign in to ClaudeForge');
  });

  it('should render Sign in with Google button', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label="Sign in with Google"]');
    expect(btn).not.toBeNull();
  });

  it('should render Sign in with Microsoft button', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button[aria-label="Sign in with Microsoft"]');
    expect(btn).not.toBeNull();
  });

  it('should NOT show error banner when authError is undefined', () => {
    const { fixture } = setup({ authError: undefined });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-login__error')).toBeNull();
  });

  it('should show error banner when authError is set', () => {
    const { fixture } = setup({ authError: 'Sign-in failed' });
    const el = fixture.nativeElement as HTMLElement;
    const errorEl = el.querySelector('[role="alert"]');
    expect(errorEl).not.toBeNull();
    expect(errorEl?.textContent).toContain('Sign-in failed');
  });

  it('should NOT show loading message when not authenticating', () => {
    const { fixture } = setup({ isAuthenticating: false });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('Redirecting');
  });

  it('should show loading message when isAuthenticating is true', () => {
    const { fixture } = setup({ isAuthenticating: true });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Redirecting');
  });

  it('should disable Google button while authenticating', () => {
    const { fixture } = setup({ isAuthenticating: true });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Google"]');
    expect(btn?.disabled).toBe(true);
  });

  it('should disable Microsoft button while authenticating', () => {
    const { fixture } = setup({ isAuthenticating: true });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Microsoft"]');
    expect(btn?.disabled).toBe(true);
  });

  it('should enable buttons when not authenticating', () => {
    const { fixture } = setup({ isAuthenticating: false });
    const el = fixture.nativeElement as HTMLElement;
    const googleBtn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Google"]');
    const msBtn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Microsoft"]');
    expect(googleBtn?.disabled).toBe(false);
    expect(msBtn?.disabled).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// i18n — French language switch
// ---------------------------------------------------------------------------

describe('LoginPageComponent — i18n French', () => {
  it('should render French Google button label after switching to fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Se connecter avec Google"]');
    expect(btn).not.toBeNull();
  });

  it('should render French Microsoft button label after switching to fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Se connecter avec Microsoft"]');
    expect(btn).not.toBeNull();
  });

  it('should render French redirecting message after switching to fr', () => {
    const { fixture, translocoService } = setup({ isAuthenticating: true });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Redirection vers le fournisseur');
  });
});

// ---------------------------------------------------------------------------
// Action wiring
// ---------------------------------------------------------------------------

describe('LoginPageComponent — action wiring', () => {
  it('should call facade.login("google") when Google button is clicked', () => {
    const loginSpy = vi.fn();
    const { fixture } = setup({ loginSpy });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Google"]');
    btn?.click();
    expect(loginSpy).toHaveBeenCalledWith('google');
  });

  it('should call facade.login("microsoft") when Microsoft button is clicked', () => {
    const loginSpy = vi.fn();
    const { fixture } = setup({ loginSpy });
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button[aria-label="Sign in with Microsoft"]');
    btn?.click();
    expect(loginSpy).toHaveBeenCalledWith('microsoft');
  });

  it('onLogin("google") should delegate to facade.login("google")', () => {
    const loginSpy = vi.fn();
    const { fixture } = setup({ loginSpy });
    fixture.componentInstance.onLogin('google');
    expect(loginSpy).toHaveBeenCalledWith('google');
  });

  it('onLogin("microsoft") should delegate to facade.login("microsoft")', () => {
    const loginSpy = vi.fn();
    const { fixture } = setup({ loginSpy });
    fixture.componentInstance.onLogin('microsoft');
    expect(loginSpy).toHaveBeenCalledWith('microsoft');
  });

  it('should not throw when onLogin is called', () => {
    const { fixture } = setup();
    expect(() => fixture.componentInstance.onLogin('google')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('LoginPageComponent — architecture boundary', () => {
  it('should compile with only AuthFacade provided (no store/adapter injection)', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });
});
