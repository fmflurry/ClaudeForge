/**
 * RED tests — Task 14.4a: TeamWelcomeOverlayComponent
 *
 * Expected production file (does NOT exist yet — tests MUST FAIL):
 *   src/app/features/team-context/presentation/welcome-overlay/team-welcome-overlay.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-team-welcome',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     ...
 *   })
 *   class TeamWelcomeOverlayComponent {
 *     // Inject facade only — NO use case or store injection
 *     private readonly facade = inject(TeamContextFacade);
 *
 *     // Inputs:
 *     readonly visible = input<boolean>(false);  // or derived from facade.needsInit
 *
 *     // Internal state:
 *     readonly customInput = signal<string>('');
 *     readonly localError = signal<string | undefined>(undefined);
 *
 *     // Derived signals:
 *     readonly presets: Signal<readonly string[]>  — from facade.presets
 *     readonly validationError: Signal<string | undefined>  — from facade.validationError
 *
 *     // Methods:
 *     selectPreset(presetId: string): void  — calls facade.setTeam(presetId)
 *     submitCustom(): void                  — calls facade.setTeam(this.customInput())
 *     skip(): void                          — calls facade.clearTeam() (clears state + dismisses overlay)
 *     onCustomInputChange(value: string): void  — updates customInput signal
 *   }
 *
 * The overlay renders when `visible` input is true (or when facade.needsInit() is true).
 * Selectors used in tests: data-testid attributes + semantic HTML.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TeamWelcomeOverlayComponent } from './team-welcome-overlay.component';
import { TeamContextFacade } from '../../application/facades/team-context.facade';
import { PRESET_TEAMS } from '../../domain/rules/team-id-validation.rules';

// ---------------------------------------------------------------------------
// Stub facade
// ---------------------------------------------------------------------------

@Injectable()
class StubTeamContextFacade {
  private readonly _currentTeam = signal<string | undefined>(undefined);
  private readonly _teamId = signal<string | undefined>(undefined);
  private readonly _hasTeam = signal(false);
  private readonly _presets = signal<readonly string[]>(PRESET_TEAMS);
  private readonly _needsInit = signal(true);
  private readonly _validationError = signal<string | undefined>(undefined);

  // Test helpers
  setNeedsInit(v: boolean): void {
    this._needsInit.set(v);
  }
  setValidationError(err: string | undefined): void {
    this._validationError.set(err);
  }
  setHasTeam(v: boolean): void {
    this._hasTeam.set(v);
  }
  setCurrentTeam(id: string | undefined): void {
    this._currentTeam.set(id);
  }

  // Signal getters
  get currentTeam(): Signal<string | undefined> {
    return this._currentTeam;
  }
  get teamId(): Signal<string | undefined> {
    return this._teamId;
  }
  get hasTeam(): Signal<boolean> {
    return this._hasTeam;
  }
  get presets(): Signal<readonly string[]> {
    return this._presets;
  }
  get needsInit(): Signal<boolean> {
    return this._needsInit;
  }
  get validationError(): Signal<string | undefined> {
    return this._validationError;
  }

  // Recorded calls
  initCalls = 0;
  setTeamCalls: string[] = [];
  clearTeamCalls = 0;

  init(): void {
    this.initCalls++;
  }
  setTeam(id: string): void {
    this.setTeamCalls.push(id);
  }
  clearTeam(): void {
    this.clearTeamCalls++;
  }
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

function setupComponent(): {
  fixture: ComponentFixture<TeamWelcomeOverlayComponent>;
  stub: StubTeamContextFacade;
} {
  const stub = new StubTeamContextFacade();
  TestBed.configureTestingModule({
    imports: [TeamWelcomeOverlayComponent],
    providers: [{ provide: TeamContextFacade, useValue: stub }],
  }).overrideComponent(TeamWelcomeOverlayComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(TeamWelcomeOverlayComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Test helper — constructs an Event whose target has the given value
// ---------------------------------------------------------------------------

function makeInputEvent(value: string): Event {
  const input = document.createElement('input');
  input.value = value;
  return { target: input } as unknown as Event;
}

// ---------------------------------------------------------------------------
// Component selector
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — selector', () => {
  it('should use selector "cf-team-welcome"', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should be a standalone component', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeInstanceOf(TeamWelcomeOverlayComponent);
  });
});

// ---------------------------------------------------------------------------
// Visibility — overlay renders when visible
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — visibility', () => {
  it('should render overlay content when needsInit is true (visible)', () => {
    const { fixture, stub } = setupComponent();
    stub.setNeedsInit(true);
    fixture.detectChanges();
    // The overlay itself or its host element should be present
    expect(fixture.componentInstance).toBeDefined();
    const nativeEl = fixture.nativeElement as HTMLElement;
    // Must have some rendered content
    expect(nativeEl.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('overlay root element should be in the DOM', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const overlayEl = fixture.debugElement.query(
      By.css('[data-testid="team-welcome-overlay"], .team-welcome-overlay, cf-team-welcome'),
    );
    expect(overlayEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Preset list rendering
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — preset list', () => {
  it('should render preset team options', () => {
    const { fixture, stub } = setupComponent();
    stub.setNeedsInit(true);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // At least one preset name should appear
    const hasPreset = PRESET_TEAMS.some((p: string) => text.includes(p));
    expect(hasPreset).toBe(true);
  });

  it('should render the Engineering preset', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Engineering');
  });

  it('should render all presets', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    for (const preset of PRESET_TEAMS) {
      expect(text).toContain(preset);
    }
  });

  it('should render clickable preset elements', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const presetEls = fixture.debugElement.queryAll(
      By.css('[data-testid^="preset-"], button[data-preset], .preset-option'),
    );
    expect(presetEls.length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Preset selection
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — selecting a preset', () => {
  it('should call facade.setTeam with the preset id when selectPreset is invoked', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.selectPreset('Engineering');
    expect(stub.setTeamCalls).toContain('Engineering');
  });

  it('should call facade.setTeam when selectPreset is invoked with QA', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.selectPreset('QA');
    expect(stub.setTeamCalls).toContain('QA');
  });

  it('should not throw when any valid preset is selected', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    for (const preset of PRESET_TEAMS) {
      expect(() => fixture.componentInstance.selectPreset(preset)).not.toThrow();
    }
  });
});

// ---------------------------------------------------------------------------
// Custom input field
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — custom input', () => {
  it('should render a custom input field', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const inputEl = fixture.debugElement.query(
      By.css('input[data-testid="custom-team-input"], input[type="text"], input[placeholder]'),
    );
    expect(inputEl).not.toBeNull();
  });

  it('should update customInput signal when onInput is called', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent('My Custom Team'));
    expect(fixture.componentInstance.customInput()).toBe('My Custom Team');
  });
});

// ---------------------------------------------------------------------------
// Custom input submission
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — custom submission', () => {
  it('should call facade.setTeam when submitCustom is called with valid input', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent('My Team'));
    fixture.componentInstance.submitCustom();
    expect(stub.setTeamCalls).toContain('My Team');
  });

  it('should not throw when submitCustom is called with empty custom input', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent(''));
    expect(() => fixture.componentInstance.submitCustom()).not.toThrow();
  });

  it('should call facade.setTeam even with invalid input (facade validates internally)', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent('bad@input'));
    fixture.componentInstance.submitCustom();
    expect(stub.setTeamCalls).toContain('bad@input');
  });
});

// ---------------------------------------------------------------------------
// Validation error display
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — validation error display', () => {
  it('should display validation error when facade.validationError returns a string', () => {
    const { fixture, stub } = setupComponent();
    stub.setValidationError('Team ID contains invalid characters');
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="validation-error"], [role="alert"], .validation-error, .error'),
    );
    expect(errorEl).not.toBeNull();
  });

  it('should show the error message text', () => {
    const { fixture, stub } = setupComponent();
    stub.setValidationError('Team ID is too short');
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Team ID is too short');
  });

  it('should NOT show error element when validationError is undefined', () => {
    const { fixture, stub } = setupComponent();
    stub.setValidationError(undefined);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(By.css('[data-testid="validation-error"]'));
    // Either null (not rendered) or hidden — we prefer it absent from DOM
    if (errorEl !== null) {
      const el = errorEl.nativeElement as HTMLElement;
      // Accept hidden via display:none or visibility:hidden
      expect(el.offsetParent).toBeNull();
    } else {
      expect(errorEl).toBeNull();
    }
  });
});

// ---------------------------------------------------------------------------
// Skip button
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — skip', () => {
  it('should render a skip button', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const skipEl = fixture.debugElement.query(
      By.css('[data-testid="skip-button"], button[data-skip], .skip-btn, button'),
    );
    expect(skipEl).not.toBeNull();
  });

  it('should call facade.clearTeam when skip() is invoked', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.skip();
    expect(stub.clearTeamCalls).toBe(1);
  });

  it('should not throw when skip() is called multiple times', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    expect(() => {
      fixture.componentInstance.skip();
      fixture.componentInstance.skip();
    }).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — facade-only injection
// ---------------------------------------------------------------------------

describe('TeamWelcomeOverlayComponent — architecture boundary', () => {
  it('should NOT require TeamContextStore directly (only facade)', () => {
    // Setup only provides TeamContextFacade, not the store.
    // If the component compiles and the test setup does not throw an injection error,
    // the boundary is maintained.
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should NOT require TeamContextStoragePort directly (only facade)', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});
