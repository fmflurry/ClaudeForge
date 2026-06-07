import { ChangeDetectionStrategy, Component, Signal, inject, signal } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { TeamContextFacade } from '../../application/facades/team-context.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

/**
 * First-visit welcome overlay that prompts the user to select or enter
 * a team name. Visible when facade.needsInit() is true.
 *
 * Injects TeamContextFacade only — no store or port injection.
 */
@Component({
  selector: 'cf-team-welcome',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideTranslocoScope('team-context')],
  template: `
    <div data-testid="team-welcome-overlay" class="team-welcome-overlay">
      <h2 class="team-welcome-overlay__title">{{ i18n.t('team-context.welcome-title') }}</h2>
      <p class="team-welcome-overlay__subtitle">{{ i18n.t('team-context.welcome-subtitle') }}</p>

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
          [placeholder]="i18n.t('team-context.custom-input-placeholder')"
          [value]="customInput()"
          (input)="onInput($event)"
        />
        <button type="button" data-testid="submit-button" class="team-welcome-overlay__submit" (click)="submitCustom()">
          {{ i18n.t('team-context.use-custom-team-btn') }}
        </button>
      </div>

      <!-- Validation error -->
      @if (validationError()) {
        <p data-testid="validation-error" role="alert" class="validation-error">
          {{ validationError() }}
        </p>
      }

      <!-- Skip button -->
      <button type="button" data-testid="skip-button" class="team-welcome-overlay__skip skip-btn" (click)="skip()">
        {{ i18n.t('team-context.skip-btn') }}
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
  protected readonly i18n = inject(I18nFacade);

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

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.customInput.set(value);
  }
}
