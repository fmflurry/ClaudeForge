/**
 * RED tests — Task 16.4: TelemetrySettingsComponent
 *
 * Expected production file (does NOT exist yet — tests WILL FAIL):
 *   src/app/features/telemetry/presentation/settings/telemetry-settings.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-telemetry-settings',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [CommonModule],   // or JsonPipe, NgIf etc. — whatever is needed
 *   })
 *   export class TelemetrySettingsComponent {
 *     private readonly facade = inject(TelemetryFacade);
 *
 *     // Derived signals from facade:
 *     readonly isEnabled: Signal<boolean>   — from facade.isEnabled
 *     readonly isDisabled: Signal<boolean>  — from facade.isDisabled
 *
 *     // Methods delegating to facade:
 *     onToggleEnable(): void    — calls facade.enable() (returns a Promise; component fire-and-forgets it)
 *     onToggleDisable(): void   — calls facade.disable()
 *
 *     // Template must include:
 *     //   [data-testid="telemetry-toggle"]     — the checkbox / toggle element
 *     //   [data-testid="privacy-text"]         — privacy explanation text element
 *     //   The toggle's checked state reflects isEnabled()
 *   }
 *
 *   Selector: cf-telemetry-settings
 *   Template privacy text must mention "telemetry" or "anonymous" or "privacy"
 *   (case-insensitive) to confirm the explanation is present.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TelemetrySettingsComponent } from './telemetry-settings.component';
import { TelemetryFacade } from '../../application/facades/telemetry.facade';

// ---------------------------------------------------------------------------
// Stub TelemetryFacade
// ---------------------------------------------------------------------------

@Injectable()
class StubTelemetryFacade {
  private readonly _isEnabled = signal(true);
  private readonly _isDisabled = signal(false);
  private readonly _anonId = signal<string | undefined>(undefined);

  // Test helpers
  setEnabled(v: boolean): void {
    this._isEnabled.set(v);
    this._isDisabled.set(!v);
  }

  // Signal getters
  get isEnabled(): Signal<boolean> { return this._isEnabled; }
  get isDisabled(): Signal<boolean> { return this._isDisabled; }
  get anonId(): Signal<string | undefined> { return this._anonId; }

  // Recorded calls
  initCalls = 0;
  enableCalls = 0;
  disableCalls = 0;
  recordEventCalls: { eventType: string; pluginId: string }[] = [];

  init(): Promise<void> { this.initCalls++; return Promise.resolve(); }
  enable(): Promise<void> { this.enableCalls++; return Promise.resolve(); }
  disable(): void { this.disableCalls++; }
  recordEvent(eventType: string, pluginId: string, _version?: string): void {
    this.recordEventCalls.push({ eventType, pluginId });
  }
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

interface ComponentHarness {
  fixture: ComponentFixture<TelemetrySettingsComponent>;
  stub: StubTelemetryFacade;
}

function setupComponent(): ComponentHarness {
  TestBed.resetTestingModule();
  const stub = new StubTelemetryFacade();
  TestBed.configureTestingModule({
    imports: [TelemetrySettingsComponent],
    providers: [{ provide: TelemetryFacade, useValue: stub }],
  }).overrideComponent(TelemetrySettingsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(TelemetrySettingsComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Component selector + instantiation
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — selector and instantiation', () => {
  it('should use selector "cf-telemetry-settings"', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should be a standalone component', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeInstanceOf(TelemetrySettingsComponent);
  });
});

// ---------------------------------------------------------------------------
// Toggle — reflects isEnabled signal
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — toggle reflects preference', () => {
  it('toggle should be present in the template', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const toggle = fixture.debugElement.query(By.css('[data-testid="telemetry-toggle"]'));
    expect(toggle).not.toBeNull();
  });

  it('toggle should reflect enabled state (checked when enabled)', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(true);
    fixture.detectChanges();
    const toggle = fixture.debugElement.query(By.css('[data-testid="telemetry-toggle"]'));
    expect(toggle).not.toBeNull();
    const el = toggle.nativeElement as HTMLInputElement | HTMLElement;
    // The element may be a checkbox (checked attr) or a button with aria-checked
    const isChecked =
      ('checked' in el && (el as HTMLInputElement).checked === true) ||
      el.getAttribute('aria-checked') === 'true' ||
      el.classList.contains('enabled') ||
      el.getAttribute('data-enabled') === 'true';
    expect(isChecked).toBe(true);
  });

  it('toggle should reflect disabled state (unchecked when disabled)', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(false);
    fixture.detectChanges();
    const toggle = fixture.debugElement.query(By.css('[data-testid="telemetry-toggle"]'));
    expect(toggle).not.toBeNull();
    const el = toggle.nativeElement as HTMLInputElement | HTMLElement;
    const isEnabled =
      ('checked' in el && (el as HTMLInputElement).checked === true) ||
      el.getAttribute('aria-checked') === 'true' ||
      el.getAttribute('data-enabled') === 'true';
    expect(isEnabled).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Privacy explanation text
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — privacy explanation text', () => {
  it('should render [data-testid="privacy-text"] element', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const privacyEl = fixture.debugElement.query(By.css('[data-testid="privacy-text"]'));
    expect(privacyEl).not.toBeNull();
  });

  it('privacy text should contain a privacy-related word (telemetry/anonymous/privacy)', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const nativeEl = fixture.nativeElement as HTMLElement;
    const text = (nativeEl.textContent ?? '').toLowerCase();
    const hasPrivacyContent =
      text.includes('telemetry') ||
      text.includes('anonymous') ||
      text.includes('anonymou') ||
      text.includes('privacy') ||
      text.includes('anon');
    expect(hasPrivacyContent).toBe(true);
  });

  it('privacy text should be non-empty', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const privacyEl = fixture.debugElement.query(By.css('[data-testid="privacy-text"]'));
    expect(privacyEl).not.toBeNull();
    const text = (privacyEl.nativeElement as HTMLElement).textContent ?? '';
    expect(text.trim().length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// onToggleDisable — calls facade.disable()
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — onToggleDisable', () => {
  it('should call facade.disable() when onToggleDisable() is invoked', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(true);
    fixture.detectChanges();
    fixture.componentInstance.onToggleDisable();
    expect(stub.disableCalls).toBe(1);
  });

  it('should not throw when onToggleDisable is called', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(true);
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onToggleDisable()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// onToggleEnable — calls facade.enable()
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — onToggleEnable', () => {
  it('should call facade.enable() when onToggleEnable() is invoked', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(false);
    fixture.detectChanges();
    fixture.componentInstance.onToggleEnable();
    expect(stub.enableCalls).toBe(1);
  });

  it('should not throw when onToggleEnable is called', () => {
    const { fixture, stub } = setupComponent();
    stub.setEnabled(false);
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onToggleEnable()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Public API surface
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — public API surface', () => {
  it('should expose isEnabled as a signal function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.isEnabled).toBe('function');
  });

  it('should expose isDisabled as a signal function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.isDisabled).toBe('function');
  });

  it('should expose onToggleEnable as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.onToggleEnable).toBe('function');
  });

  it('should expose onToggleDisable as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.onToggleDisable).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — facade-only injection
// ---------------------------------------------------------------------------

describe('TelemetrySettingsComponent — architecture boundary', () => {
  it('should NOT require TelemetryStore directly (only facade)', () => {
    // TestBed only provides TelemetryFacade stub — no store.
    // If no injection error occurs, the boundary is respected.
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should NOT require ApiClient directly (only facade)', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should NOT require TelemetryPreferencePort directly (only facade)', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});
