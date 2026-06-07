/**
 * ActivatePageComponent — render + wiring tests.
 *
 * Uses TranslocoTestingModule harness (same Wave-1 pattern as home / catalog).
 * en map returns EXACT current literals so existing assertions stay green.
 * fr map is used in French language switch assertions.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { ActivatePageComponent } from './activate-page.component';
import { DeviceActivationFacade } from '../application/facades/device-activation.facade';
import type { DeviceActivationStatus, DeviceActivationErrorReason } from '../domain/ports/device-activation.port';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for device-activation scope
//
// En map returns EXACT current literals so all existing rendered-text
// assertions keep passing unchanged after migration.
// Fr map returns French — used in fr-switch assertions.
// ---------------------------------------------------------------------------

const EN_DEVICE_ACTIVATION_LANGS: Record<string, string> = {
  'device-activation.title': 'Approve Device',
  'device-activation.subtitle': 'Enter the user code displayed on your device to authorize it.',
  'device-activation.approved': 'Device successfully approved. You may close this page.',
  'device-activation.label-user-code': 'User Code',
  'device-activation.placeholder': 'e.g. ABCD-1234',
  'device-activation.btn-approve': 'Approve Device',
  'device-activation.btn-approving': 'Approving…',
  'device-activation.error.invalid': 'The code you entered is invalid or missing. Please check and try again.',
  'device-activation.error.not-found': 'The code was not found or is unrecognized. Please verify the code.',
  'device-activation.error.already-approved': 'This device has already been approved.',
  'device-activation.error.expired': 'The code has expired. Please restart the device authorization flow.',
  'device-activation.error.unauthorized':
    'You are not authorized to approve this device. Please sign in and try again.',
  'device-activation.error.unknown': 'An unexpected error occurred. Please try again later.',
};

const FR_DEVICE_ACTIVATION_LANGS: Record<string, string> = {
  'device-activation.title': "Approuver l'appareil",
  'device-activation.subtitle': "Entrez le code utilisateur affiché sur votre appareil pour l'autoriser.",
  'device-activation.approved': 'Appareil approuvé avec succès. Vous pouvez fermer cette page.',
  'device-activation.label-user-code': 'Code utilisateur',
  'device-activation.placeholder': 'ex. ABCD-1234',
  'device-activation.btn-approve': "Approuver l'appareil",
  'device-activation.btn-approving': 'Approbation…',
  'device-activation.error.invalid':
    'Le code que vous avez saisi est invalide ou manquant. Veuillez vérifier et réessayer.',
  'device-activation.error.not-found': 'Le code est introuvable ou non reconnu. Veuillez vérifier le code.',
  'device-activation.error.already-approved': 'Cet appareil a déjà été approuvé.',
  'device-activation.error.expired': "Le code a expiré. Veuillez redémarrer le flux d'autorisation de l'appareil.",
  'device-activation.error.unauthorized':
    "Vous n'êtes pas autorisé à approuver cet appareil. Veuillez vous connecter et réessayer.",
  'device-activation.error.unknown': "Une erreur inattendue s'est produite. Veuillez réessayer plus tard.",
};

// ---------------------------------------------------------------------------
// Fake DeviceActivationFacade
// ---------------------------------------------------------------------------

function buildFakeFacade(opts: {
  status?: DeviceActivationStatus;
  errorReason?: DeviceActivationErrorReason | undefined;
  approveSpy?: (code: string) => void;
  resetSpy?: () => void;
}): Partial<DeviceActivationFacade> {
  const statusSignal = signal<DeviceActivationStatus>(opts.status ?? 'idle');
  const errorReasonSignal = signal<DeviceActivationErrorReason | undefined>(opts.errorReason);

  return {
    get status() {
      return statusSignal.asReadonly();
    },
    get errorReason() {
      return errorReasonSignal.asReadonly();
    },
    approve: opts.approveSpy ?? (() => undefined),
    reset: opts.resetSpy ?? (() => undefined),
  };
}

// ---------------------------------------------------------------------------
// Fake ActivatedRoute
// ---------------------------------------------------------------------------

function buildFakeRoute(userCode: string | null = null): Partial<ActivatedRoute> {
  return {
    snapshot: {
      queryParamMap: {
        get: (key: string): string | null => (key === 'user_code' ? userCode : null),
      },
    } as ActivatedRoute['snapshot'],
  };
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

interface SetupOpts {
  status?: DeviceActivationStatus;
  errorReason?: DeviceActivationErrorReason | undefined;
  approveSpy?: (code: string) => void;
  resetSpy?: () => void;
  userCodeParam?: string | null;
}

function setup(opts: SetupOpts = {}): {
  fixture: ComponentFixture<ActivatePageComponent>;
  fakeFacade: Partial<DeviceActivationFacade>;
  translocoService: TranslocoService;
} {
  const fakeFacade = buildFakeFacade(opts);
  const fakeRoute = buildFakeRoute(opts.userCodeParam ?? null);

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ActivatePageComponent,
      TranslocoTestingModule.forRoot({
        langs: { en: EN_DEVICE_ACTIVATION_LANGS, fr: FR_DEVICE_ACTIVATION_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: DeviceActivationFacade, useValue: fakeFacade },
      { provide: ActivatedRoute, useValue: fakeRoute },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(ActivatePageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });

  const fixture = TestBed.createComponent(ActivatePageComponent);
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, fakeFacade, translocoService };
}

// ---------------------------------------------------------------------------
// Render — basic structure
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — render: basic structure', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render a heading containing "Approve" or "activate" (case-insensitive)', () => {
    const { fixture } = setup();
    const text = (fixture.nativeElement as HTMLElement).textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/approv|activat/);
  });

  it('should render a text input for the user_code', () => {
    const { fixture } = setup();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input');
    expect(input).not.toBeNull();
  });

  it('should render a submit button', () => {
    const { fixture } = setup();
    const btn = (fixture.nativeElement as HTMLElement).querySelector('button[type="submit"]');
    expect(btn).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Render — query param prefill
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — query param prefill', () => {
  it('should prefill the input when ?user_code is present', () => {
    const { fixture } = setup({ userCodeParam: 'ABCD-1234' });
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    expect(input?.value).toBe('ABCD-1234');
  });

  it('should leave the input empty when ?user_code is absent', () => {
    const { fixture } = setup({ userCodeParam: null });
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    expect(input?.value ?? '').toBe('');
  });

  it('should not throw when ?user_code query param is absent', () => {
    expect(() => setup({ userCodeParam: null })).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Submit button disabled state
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — submit button disabled during submitting', () => {
  it('should NOT disable submit button when status is "idle"', () => {
    const { fixture } = setup({ status: 'idle' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(btn?.disabled).toBe(false);
  });

  it('should disable submit button when status is "submitting"', () => {
    const { fixture } = setup({ status: 'submitting' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(btn?.disabled).toBe(true);
  });

  it('should NOT disable submit button when status is "error"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'invalid' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(btn?.disabled).toBe(false);
  });

  it('should NOT disable submit button when status is "approved"', () => {
    const { fixture } = setup({ status: 'approved' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[type="submit"]');
    // After approval the button may be hidden entirely — either null or not disabled
    if (btn !== null) {
      expect(btn.disabled).toBe(false);
    } else {
      expect(btn).toBeNull(); // hidden in approved state is acceptable
    }
  });
});

// ---------------------------------------------------------------------------
// Approved state
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — approved state render', () => {
  it('should show an approved success block when status is "approved"', () => {
    const { fixture } = setup({ status: 'approved' });
    const el = fixture.nativeElement as HTMLElement;
    // Must have role="status" or a class containing "approved"
    const byRole = el.querySelector('[role="status"]');
    const byClass = el.querySelector('[class*="approved"]');
    expect(byRole ?? byClass).not.toBeNull();
  });

  it('should NOT show an error block when status is "approved"', () => {
    const { fixture } = setup({ status: 'approved' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('approved block should contain confirmation text', () => {
    const { fixture } = setup({ status: 'approved' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/approv|success|authorized/);
  });
});

// ---------------------------------------------------------------------------
// Error state — error block presence
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — error state: error block', () => {
  it('should show role="alert" element when status is "error"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'invalid' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('should NOT show error block when status is "idle"', () => {
    const { fixture } = setup({ status: 'idle' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('should NOT show error block when status is "submitting"', () => {
    const { fixture } = setup({ status: 'submitting' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Error state — per-reason messages
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — error state: invalid reason', () => {
  it('should show an "invalid or missing" message for errorReason "invalid"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'invalid' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/invalid|missing/);
  });
});

describe('ActivatePageComponent — error state: not-found reason', () => {
  it('should show "not found" or "unrecognized" message for errorReason "not-found"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'not-found' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/not found|unrecogni/);
  });
});

describe('ActivatePageComponent — error state: already-approved reason', () => {
  it('should show "already approved" message for errorReason "already-approved"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'already-approved' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/already.*approv/);
  });
});

describe('ActivatePageComponent — error state: expired reason', () => {
  it('should show "expired" message for errorReason "expired"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'expired' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toContain('expired');
  });
});

describe('ActivatePageComponent — error state: unauthorized reason', () => {
  it('should show sign-in / unauthorized message for errorReason "unauthorized"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'unauthorized' });
    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent?.toLowerCase() ?? '';
    expect(text).toMatch(/sign in|not authorized|unauthorized/);
  });
});

describe('ActivatePageComponent — error state: unknown reason', () => {
  it('should show a generic fallback message for errorReason "unknown"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'unknown' });
    const el = fixture.nativeElement as HTMLElement;
    // Any fallback text is acceptable — just must not be empty
    const alertEl = el.querySelector('[role="alert"]');
    expect(alertEl?.textContent?.trim().length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// i18n — French language switch
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — i18n French', () => {
  it('should render French title after switching to fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const h1 = (fixture.nativeElement as HTMLElement).querySelector('h1');
    expect(h1?.textContent?.trim()).toContain('Approuver');
  });

  it('should render French approved message after switching to fr', () => {
    const { fixture, translocoService } = setup({ status: 'approved' });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('approuvé');
  });

  it('should render French error message for "invalid" after switching to fr', () => {
    const { fixture, translocoService } = setup({ status: 'error', errorReason: 'invalid' });
    translocoService.setActiveLang('fr');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent?.toLowerCase()).toMatch(/invalide|manquant/);
  });
});

// ---------------------------------------------------------------------------
// Form submission — onSubmit wiring
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — form submission wiring', () => {
  it('should call facade.approve() with the typed code on submit', () => {
    const approveSpy = vi.fn();
    const { fixture } = setup({ approveSpy });

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    if (input) {
      input.value = 'ABCD-5678';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }

    fixture.componentInstance.onSubmit();

    expect(approveSpy).toHaveBeenCalledWith('ABCD-5678');
  });

  it('should call facade.approve() with trimmed code', () => {
    const approveSpy = vi.fn();
    const { fixture } = setup({ approveSpy });

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    if (input) {
      input.value = '  TRIM-ME  ';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }

    fixture.componentInstance.onSubmit();

    // Trimmed: leading/trailing whitespace removed
    expect(approveSpy).toHaveBeenCalledWith('TRIM-ME');
  });

  it('should NOT call facade.approve() when the input is empty', () => {
    const approveSpy = vi.fn();
    const { fixture } = setup({ approveSpy });

    // Leave input empty (default)
    fixture.componentInstance.onSubmit();

    expect(approveSpy).not.toHaveBeenCalled();
  });

  it('should NOT call facade.approve() when input is only whitespace', () => {
    const approveSpy = vi.fn();
    const { fixture } = setup({ approveSpy });

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    if (input) {
      input.value = '   ';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }

    fixture.componentInstance.onSubmit();

    expect(approveSpy).not.toHaveBeenCalled();
  });

  it('should not throw when onSubmit is called with a valid code', () => {
    const { fixture } = setup();
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    if (input) {
      input.value = 'CODE-1234';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }
    expect(() => fixture.componentInstance.onSubmit()).not.toThrow();
  });

  it('should call facade.approve() with the prefilled ?user_code on submit without user interaction', () => {
    const approveSpy = vi.fn();
    const { fixture } = setup({ approveSpy, userCodeParam: 'PRE-FILLED' });

    fixture.componentInstance.onSubmit();

    expect(approveSpy).toHaveBeenCalledWith('PRE-FILLED');
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('ActivatePageComponent — architecture boundary', () => {
  it('should compile with only DeviceActivationFacade and ActivatedRoute provided', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should expose onSubmit() as a method on the component instance', () => {
    const { fixture } = setup();
    expect(typeof fixture.componentInstance.onSubmit).toBe('function');
  });

  it('should expose status signal from the facade (no direct store access)', () => {
    const { fixture } = setup({ status: 'idle' });
    expect(typeof fixture.componentInstance.status).toBe('function');
    expect(fixture.componentInstance.status()).toBe('idle');
  });

  it('should expose errorReason signal from the facade', () => {
    const { fixture } = setup({ errorReason: undefined });
    expect(typeof fixture.componentInstance.errorReason).toBe('function');
  });
});
