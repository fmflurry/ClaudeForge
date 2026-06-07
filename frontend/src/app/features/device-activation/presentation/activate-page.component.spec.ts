/**
 * RED tests — ActivatePageComponent
 *
 * Expected production files (DO NOT exist yet — tests will FAIL):
 *   src/app/features/device-activation/presentation/activate-page.component.ts
 *   src/app/features/device-activation/application/facades/device-activation.facade.ts
 *   src/app/features/device-activation/domain/ports/device-activation.port.ts
 *
 * GREEN contract:
 *
 *   @Component({
 *     selector: 'cf-activate-page',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [ReactiveFormsModule],  // or FormsModule if using template-driven
 *     template: `...`
 *   })
 *   export class ActivatePageComponent implements OnInit {
 *     // Injections:
 *     private readonly facade = inject(DeviceActivationFacade);
 *     private readonly route   = inject(ActivatedRoute);
 *
 *     // Expose facade signals to template (no any/$any):
 *     readonly status = this.facade.status;
 *     readonly errorReason = this.facade.errorReason;
 *
 *     // Form: a single text input for the user_code
 *     // (FormControl or FormGroup — implementer's choice; must be type-safe)
 *
 *     ngOnInit(): void
 *       — reads queryParamMap.get('user_code')
 *       — if present, prefills the form control with that value
 *
 *     onSubmit(): void
 *       — if the control is empty/whitespace: do nothing (or mark invalid)
 *       — otherwise: facade.approve(trimmedCode)
 *
 *     // Template MUST contain:
 *     //   - a heading / title with text containing "Approve" or "activate" (case-insensitive)
 *     //   - a text input for the user_code (input[type=text] or input[type=search] or <input>)
 *     //   - a submit button with [disabled] bound to status()==='submitting'
 *     //   - @if (status() === 'approved')  — approved success block (role="status" or .cf-activate__approved)
 *     //   - @if (status() === 'error')     — error block (role="alert")
 *     //       — shows human-readable message per errorReason():
 *     //           'invalid'         → "invalid or missing"
 *     //           'not-found'       → "not found" or "unrecognized"
 *     //           'already-approved'→ "already been approved" or "already approved"
 *     //           'expired'         → "expired"
 *     //           'unauthorized'    → "sign in" or "not authorized" or "unauthorized"
 *     //           'unknown'         → generic fallback message
 *     //   - @if (status() === 'submitting') — loading indicator (optional but tested if present)
 *     //
 *     // Route: registered at path 'activate' behind FunctionalAuthGuard in app.routes.ts.
 *     // No any/$any in template or class.
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ActivatePageComponent } from './activate-page.component';
import { DeviceActivationFacade } from '../application/facades/device-activation.facade';
import type {
  DeviceActivationStatus,
  DeviceActivationErrorReason,
} from '../domain/ports/device-activation.port';

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
} {
  const fakeFacade = buildFakeFacade(opts);
  const fakeRoute = buildFakeRoute(opts.userCodeParam ?? null);

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [ActivatePageComponent],
    providers: [
      { provide: DeviceActivationFacade, useValue: fakeFacade },
      { provide: ActivatedRoute, useValue: fakeRoute },
    ],
  }).overrideComponent(ActivatePageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });

  const fixture = TestBed.createComponent(ActivatePageComponent);
  fixture.detectChanges();
  return { fixture, fakeFacade };
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
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="submit"]',
    );
    expect(btn?.disabled).toBe(false);
  });

  it('should disable submit button when status is "submitting"', () => {
    const { fixture } = setup({ status: 'submitting' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="submit"]',
    );
    expect(btn?.disabled).toBe(true);
  });

  it('should NOT disable submit button when status is "error"', () => {
    const { fixture } = setup({ status: 'error', errorReason: 'invalid' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="submit"]',
    );
    expect(btn?.disabled).toBe(false);
  });

  it('should NOT disable submit button when status is "approved"', () => {
    const { fixture } = setup({ status: 'approved' });
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="submit"]',
    );
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
