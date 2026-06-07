import { ChangeDetectionStrategy, Component, inject, Signal } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { TelemetryFacade } from '../../application/facades/telemetry.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

/**
 * Settings component for telemetry opt-in/opt-out.
 * Delegates all state and actions exclusively to TelemetryFacade.
 */
@Component({
  selector: 'cf-telemetry-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideTranslocoScope('telemetry')],
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
        {{ i18n.t('telemetry.toggle-label') }}
      </label>
      <p class="cf-telemetry-settings__privacy" data-testid="privacy-text">
        {{ i18n.t('telemetry.privacy-text') }}
      </p>
    </div>
  `,
})
export class TelemetrySettingsComponent {
  private readonly facade = inject(TelemetryFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly isEnabled: Signal<boolean> = this.facade.isEnabled;
  readonly isDisabled: Signal<boolean> = this.facade.isDisabled;

  onToggleEnable(): void {
    void this.facade.enable();
  }

  onToggleDisable(): void {
    this.facade.disable();
  }
}
