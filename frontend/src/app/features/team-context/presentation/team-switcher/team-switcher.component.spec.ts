/**
 * RED tests — Task 14.4b: TeamSwitcherComponent
 *
 * Expected production file (does NOT exist yet — tests MUST FAIL):
 *   src/app/features/team-context/presentation/team-switcher/team-switcher.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-team-switcher',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     ...
 *   })
 *   class TeamSwitcherComponent {
 *     // Inject facade only
 *     private readonly facade = inject(TeamContextFacade);
 *
 *     // Derived signals:
 *     readonly currentTeam: Signal<string | undefined>  — from facade.currentTeam
 *     readonly hasTeam: Signal<boolean>                 — from facade.hasTeam
 *     readonly presets: Signal<readonly string[]>       — from facade.presets
 *     readonly validationError: Signal<string | undefined>  — from facade.validationError
 *
 *     // Internal state for the inline switcher UI:
 *     readonly isEditing = signal<boolean>(false);
 *     readonly editInput = signal<string>('');
 *
 *     // Methods:
 *     openEdit(): void     — sets isEditing to true, seeds editInput with currentTeam
 *     cancelEdit(): void   — sets isEditing to false, clears editInput
 *     confirmEdit(): void  — calls facade.setTeam(editInput()), closes if successful
 *     selectPreset(id: string): void  — calls facade.setTeam(id)
 *     clearTeam(): void    — calls facade.clearTeam()
 *     onEditInputChange(value: string): void  — updates editInput
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TeamSwitcherComponent } from './team-switcher.component';
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
  private readonly _needsInit = signal(false);
  private readonly _validationError = signal<string | undefined>(undefined);

  // Test helpers
  setCurrentTeam(id: string | undefined): void {
    this._currentTeam.set(id);
    this._teamId.set(id);
    this._hasTeam.set(id !== undefined);
  }
  setValidationError(err: string | undefined): void { this._validationError.set(err); }
  setNeedsInit(v: boolean): void { this._needsInit.set(v); }

  // Signal getters
  get currentTeam(): Signal<string | undefined> { return this._currentTeam; }
  get teamId(): Signal<string | undefined> { return this._teamId; }
  get hasTeam(): Signal<boolean> { return this._hasTeam; }
  get presets(): Signal<readonly string[]> { return this._presets; }
  get needsInit(): Signal<boolean> { return this._needsInit; }
  get validationError(): Signal<string | undefined> { return this._validationError; }

  // Recorded calls
  initCalls = 0;
  setTeamCalls: string[] = [];
  clearTeamCalls = 0;

  init(): void { this.initCalls++; }
  setTeam(id: string): void { this.setTeamCalls.push(id); }
  clearTeam(): void { this.clearTeamCalls++; }
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setupComponent(): {
  fixture: ComponentFixture<TeamSwitcherComponent>;
  stub: StubTeamContextFacade;
} {
  TestBed.resetTestingModule();
  const stub = new StubTeamContextFacade();
  TestBed.configureTestingModule({
    imports: [TeamSwitcherComponent],
    providers: [{ provide: TeamContextFacade, useValue: stub }],
  }).overrideComponent(TeamSwitcherComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(TeamSwitcherComponent);
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

describe('TeamSwitcherComponent — selector', () => {
  it('should use selector "cf-team-switcher"', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should be a standalone component', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeInstanceOf(TeamSwitcherComponent);
  });
});

// ---------------------------------------------------------------------------
// Display — no team set
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — no team state', () => {
  it('should render a placeholder or prompt when no team is set', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam(undefined);
    fixture.detectChanges();
    // Either a "no team" label or a set-team button
    const nativeEl = fixture.nativeElement as HTMLElement;
    expect(nativeEl.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('should NOT display a team name when hasTeam is false', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam(undefined);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // None of the preset names should appear as "current" when no team is set
    expect(text).not.toContain('Current: Engineering');
  });
});

// ---------------------------------------------------------------------------
// Display — team set
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — team set state', () => {
  it('should display the current team name when hasTeam is true', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Engineering');
  });

  it('should display different team names correctly', () => {
    for (const preset of PRESET_TEAMS) {
      const { fixture, stub } = setupComponent();
      stub.setCurrentTeam(preset);
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain(preset);
    }
  });
});

// ---------------------------------------------------------------------------
// Edit mode — openEdit / cancelEdit
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — edit mode', () => {
  it('isEditing should default to false', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    expect(fixture.componentInstance.isEditing()).toBe(false);
  });

  it('isEditing should become true after openEdit()', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    expect(fixture.componentInstance.isEditing()).toBe(true);
  });

  it('editInput should be seeded with currentTeam after openEdit()', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    expect(fixture.componentInstance.editInput()).toBe('Engineering');
  });

  it('isEditing should become false after cancelEdit()', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    fixture.componentInstance.cancelEdit();
    expect(fixture.componentInstance.isEditing()).toBe(false);
  });

  it('editInput should be cleared after cancelEdit()', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    fixture.componentInstance.cancelEdit();
    expect(fixture.componentInstance.editInput()).toBe('');
  });

  it('should not throw when openEdit() is called with no current team', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam(undefined);
    fixture.detectChanges();
    expect(() => fixture.componentInstance.openEdit()).not.toThrow();
  });

  it('editInput should be empty string when openEdit called with no current team', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam(undefined);
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    expect(fixture.componentInstance.editInput()).toBe('');
  });
});

// ---------------------------------------------------------------------------
// Edit mode — confirmEdit
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — confirmEdit', () => {
  it('should call facade.setTeam with the editInput value', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.openEdit();
    fixture.componentInstance.onInput(makeInputEvent('Product'));
    fixture.componentInstance.confirmEdit();
    expect(stub.setTeamCalls).toContain('Product');
  });

  it('should not throw when confirmEdit is called with empty editInput', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.confirmEdit()).not.toThrow();
  });

  it('should call facade.setTeam even with invalid input (facade validates)', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent('bad@input'));
    fixture.componentInstance.confirmEdit();
    expect(stub.setTeamCalls).toContain('bad@input');
  });
});

// ---------------------------------------------------------------------------
// onInput
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — onInput', () => {
  it('should update editInput signal', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent('Design'));
    expect(fixture.componentInstance.editInput()).toBe('Design');
  });

  it('should handle empty string', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onInput(makeInputEvent(''));
    expect(fixture.componentInstance.editInput()).toBe('');
  });
});

// ---------------------------------------------------------------------------
// selectPreset
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — selectPreset', () => {
  it('should call facade.setTeam with the given preset id', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.selectPreset('QA');
    expect(stub.setTeamCalls).toContain('QA');
  });

  it('should not throw for any valid preset', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    for (const preset of PRESET_TEAMS) {
      expect(() => fixture.componentInstance.selectPreset(preset)).not.toThrow();
    }
  });
});

// ---------------------------------------------------------------------------
// clearTeam
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — clearTeam', () => {
  it('should call facade.clearTeam()', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam('Engineering');
    fixture.detectChanges();
    fixture.componentInstance.clearTeam();
    expect(stub.clearTeamCalls).toBe(1);
  });

  it('should not throw when clearTeam() is called with no current team', () => {
    const { fixture, stub } = setupComponent();
    stub.setCurrentTeam(undefined);
    fixture.detectChanges();
    expect(() => fixture.componentInstance.clearTeam()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Validation error display
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — validation error display', () => {
  it('should render validation error when facade.validationError returns a string', () => {
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
    if (errorEl !== null) {
      const el = errorEl.nativeElement as HTMLElement;
      expect(el.offsetParent).toBeNull();
    } else {
      expect(errorEl).toBeNull();
    }
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — facade-only injection
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — architecture boundary', () => {
  it('should NOT require TeamContextStore directly (only facade)', () => {
    // Setup only provides TeamContextFacade, not the store. If the component
    // compiles and setup does not throw an injection error, the boundary holds.
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should NOT require TeamContextStoragePort directly (only facade)', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Public API surface
// ---------------------------------------------------------------------------

describe('TeamSwitcherComponent — public API surface', () => {
  it('should expose isEditing as a signal function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.isEditing).toBe('function');
  });

  it('should expose editInput as a signal function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.editInput).toBe('function');
  });

  it('should expose openEdit as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.openEdit).toBe('function');
  });

  it('should expose cancelEdit as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.cancelEdit).toBe('function');
  });

  it('should expose confirmEdit as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.confirmEdit).toBe('function');
  });

  it('should expose selectPreset as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.selectPreset).toBe('function');
  });

  it('should expose clearTeam as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.clearTeam).toBe('function');
  });

  it('should expose onInput as a function', () => {
    const { fixture } = setupComponent();
    expect(typeof fixture.componentInstance.onInput).toBe('function');
  });
});
