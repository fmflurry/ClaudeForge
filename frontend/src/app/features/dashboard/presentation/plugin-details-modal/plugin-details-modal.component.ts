import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { InstalledPlugin } from '../../domain/models/dashboard.models';

@Component({
  selector: 'cf-plugin-details-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  template: `
    <div class="modal-overlay">
      <div class="modal-content" role="dialog" aria-modal="true">
        <div class="modal-header">
          <h2 data-testid="modal-title" class="modal-title">{{ pluginName() }}</h2>
          <button data-testid="modal-close-btn" class="modal-close" aria-label="Close modal" (click)="onClose()">
            &times;
          </button>
        </div>

        <div class="modal-body">
          @if (plugin()) {
            <div>
              <span data-testid="plugin-version">Version: {{ plugin()!.version }}</span>
            </div>
            <div>Installed: {{ plugin()!.installedAt }}</div>

            @if (plugin()!.status === 'update-available') {
              <div data-testid="update-section" class="update-section">
                <strong>Update available: {{ plugin()!.latestVersion }}</strong>
                <button data-testid="modal-update-btn" (click)="onConfirmUpdate()">Update now</button>
              </div>
            }

            @if (plugin()!.latestVersion) {
              <div data-testid="release-notes" class="release-notes">
                <h3>Release Notes</h3>
                <p>Latest version: {{ plugin()!.latestVersion }}</p>
              </div>
            }
          } @else {
            <div>
              <span data-testid="plugin-version">Version: —</span>
            </div>
          }

          <div class="docs-section">
            <a data-testid="docs-placeholder" href="#docs" class="docs-link"> View Documentation </a>
          </div>
        </div>

        <div class="modal-footer">
          <button
            data-testid="modal-remove-btn"
            class="remove-btn"
            aria-label="Remove plugin"
            (click)="onConfirmRemove()"
          >
            Remove Plugin
          </button>
        </div>
      </div>
    </div>
  `,
})
export class PluginDetailsModalComponent {
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
