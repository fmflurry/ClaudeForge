import {
  ChangeDetectionStrategy,
  Component,
  Signal,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TeamContextFacade } from '../../application/facades/team-context.facade';

/**
 * First-visit welcome overlay that prompts the user to select or enter
 * a team name. Visible when facade.needsInit() is true.
 *
 * Injects TeamContextFacade only — no store or port injection.
 */
@Component({
  selector: 'cf-team-welcome',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div data-testid="team-welcome-overlay" class="team-welcome-overlay">
      <h2 class="team-welcome-overlay__title">Welcome to ClaudeForge</h2>
      <p class="team-welcome-overlay__subtitle">
        Select your team or enter a custom name to get started.
      </p>

      <!-- Preset buttons -->
      <div class="team-welcome-overlay__presets">
        @for (preset of presets(); track preset) {
          <button
            type="button"
            [attr.data-testid]="'preset-' + preset"
            [attr.data-preset]="preset"
            class="preset-option"
            (click)="selectPreset(preset)"
          >
            {{ preset }}
          </button>
        }
      </div>

      <!-- Custom team input -->
      <div class="team-welcome-overlay__custom">
        <input
          type="text"
          data-testid="custom-team-input"
          placeholder="Enter custom team name"
          [value]="customInput()"
          (input)="onCustomInputChange($any($event.target).value)"
        />
        <button
          type="button"
          data-testid="submit-button"
          class="team-welcome-overlay__submit"
          (click)="submitCustom()"
        >
          Use Custom Team
        </button>
      </div>

      <!-- Validation error -->
      @if (validationError()) {
        <p
          data-testid="validation-error"
          role="alert"
          class="validation-error"
        >
          {{ validationError() }}
        </p>
      }

      <!-- Skip button -->
      <button
        type="button"
        data-testid="skip-button"
        class="team-welcome-overlay__skip skip-btn"
        (click)="skip()"
      >
        Skip for now
      </button>
    </div>
  `,
  styles: [
    `
      .team-welcome-overlay {
        padding: 2rem;
        background: #fff;
        border-radius: 0.5rem;
        max-width: 480px;
        margin: 0 auto;
      }

      .team-welcome-overlay__presets {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-bottom: 1rem;
      }

      .preset-option {
        padding: 0.4rem 0.8rem;
        border: 1px solid #ccc;
        border-radius: 0.25rem;
        cursor: pointer;
        background: #f5f5f5;
      }

      .team-welcome-overlay__custom {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
      }

      .validation-error {
        color: #d32f2f;
        margin: 0.5rem 0;
      }

      .team-welcome-overlay__skip {
        background: transparent;
        border: none;
        cursor: pointer;
        color: #666;
        text-decoration: underline;
      }
    `,
  ],
})
export class TeamWelcomeOverlayComponent {
  private readonly facade = inject(TeamContextFacade);

  readonly customInput = signal<string>('');

  get presets(): Signal<readonly string[]> {
    return this.facade.presets;
  }

  get validationError(): Signal<string | undefined> {
    return this.facade.validationError;
  }

  selectPreset(presetId: string): void {
    this.facade.setTeam(presetId);
  }

  submitCustom(): void {
    this.facade.setTeam(this.customInput());
  }

  skip(): void {
    this.facade.clearTeam();
  }

  onCustomInputChange(value: string): void {
    this.customInput.set(value);
  }
}
