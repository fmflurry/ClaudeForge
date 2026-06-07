import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import type { InstalledPlugin } from '../../domain/models/dashboard.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-plugin-details-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  providers: [provideTranslocoScope('dashboard')],
  template: `
    <div class="modal-overlay">
      <div class="modal-content" role="dialog" aria-modal="true">
        <div class="modal-header">
          <h2 data-testid="modal-title" class="modal-title">{{ pluginName() }}</h2>
          <button
            data-testid="modal-close-btn"
            class="modal-close"
            [attr.aria-label]="i18n.t('dashboard.modal-close-aria')"
            (click)="onClose()"
          >
            &times;
          </button>
        </div>

        <div class="modal-body">
          @if (plugin()) {
            <div>
              <span data-testid="plugin-version">{{
                i18n.t('dashboard.modal-version', { version: plugin()!.version })
              }}</span>
            </div>
            <div>{{ i18n.t('dashboard.modal-installed', { date: plugin()!.installedAt }) }}</div>

            @if (plugin()!.status === 'update-available') {
              <div data-testid="update-section" class="update-section">
                <strong>{{ i18n.t('dashboard.modal-update-available', { version: plugin()!.latestVersion }) }}</strong>
                <button data-testid="modal-update-btn" (click)="onConfirmUpdate()">
                  {{ i18n.t('dashboard.modal-update-now') }}
                </button>
              </div>
            }

            @if (plugin()!.latestVersion) {
              <div data-testid="release-notes" class="release-notes">
                <h3>{{ i18n.t('dashboard.modal-release-notes-heading') }}</h3>
                <p>{{ i18n.t('dashboard.modal-latest-version', { version: plugin()!.latestVersion }) }}</p>
              </div>
            }
          } @else {
            <div>
              <span data-testid="plugin-version">{{ i18n.t('dashboard.modal-version-unknown') }}</span>
            </div>
          }

          <div class="docs-section">
            <a data-testid="docs-placeholder" href="#docs" class="docs-link">
              {{ i18n.t('dashboard.modal-docs-link') }}
            </a>
          </div>
        </div>

        <div class="modal-footer">
          <button
            data-testid="modal-remove-btn"
            class="remove-btn"
            [attr.aria-label]="i18n.t('dashboard.modal-remove-btn-aria')"
            (click)="onConfirmRemove()"
          >
            {{ i18n.t('dashboard.modal-remove-btn') }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class PluginDetailsModalComponent {
  protected readonly i18n = inject(I18nFacade);

  readonly pluginName = input.required<string>();
  readonly plugin = input<InstalledPlugin | undefined>(undefined);

  readonly closed = output<void>();
  readonly confirmRemove = output<string>();
  readonly confirmUpdate = output<string>();

  onClose(): void {
    this.closed.emit();
  }

  onConfirmRemove(): void {
    this.confirmRemove.emit(this.pluginName());
  }

  onConfirmUpdate(): void {
    this.confirmUpdate.emit(this.pluginName());
  }
}
