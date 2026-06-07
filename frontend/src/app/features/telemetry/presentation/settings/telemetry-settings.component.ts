import { ChangeDetectionStrategy, Component, inject, Signal } from '@angular/core';
import { TelemetryFacade } from '../../application/facades/telemetry.facade';

/**
 * Settings component for telemetry opt-in/opt-out.
 * Delegates all state and actions exclusively to TelemetryFacade.
 */
@Component({
  selector: 'cf-telemetry-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-telemetry-settings">
      <label class="cf-telemetry-settings__label">
        <input
          type="checkbox"
          data-testid="telemetry-toggle"
          [checked]="isEnabled()"
          [attr.aria-checked]="isEnabled()"
          [attr.data-enabled]="isEnabled()"
          (change)="isEnabled() ? onToggleDisable() : onToggleEnable()"
        />
        Enable anonymous telemetry
      </label>
      <p class="cf-telemetry-settings__privacy" data-testid="privacy-text">
        We collect anonymous usage data to improve ClaudeForge. No personally identifiable information is ever sent —
        only an anonymous identifier and the event type (e.g. plugin install). You can opt out at any time. Your privacy
        matters.
      </p>
    </div>
  `,
})
export class TelemetrySettingsComponent {
  private readonly facade = inject(TelemetryFacade);

  readonly isEnabled: Signal<boolean> = this.facade.isEnabled;
  readonly isDisabled: Signal<boolean> = this.facade.isDisabled;

  onToggleEnable(): void {
    void this.facade.enable();
  }

  onToggleDisable(): void {
    this.facade.disable();
  }
}
