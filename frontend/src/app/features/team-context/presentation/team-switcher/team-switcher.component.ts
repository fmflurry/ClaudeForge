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
 * Header-mounted team switcher. Shows the current team and provides inline
 * editing with preset selection, free-text entry, clear, and cancel.
 *
 * Injects TeamContextFacade only — no store or port injection.
 */
@Component({
  selector: 'cf-team-switcher',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="team-switcher" data-testid="team-switcher">
      @if (!isEditing()) {
        <!-- Display mode -->
        <div class="team-switcher__display">
          @if (hasTeam()) {
            <span class="team-switcher__current-team" data-testid="current-team">
              {{ currentTeam() }}
            </span>
            <button
              type="button"
              data-testid="edit-button"
              class="team-switcher__edit-btn"
              (click)="openEdit()"
            >
              Change
            </button>
            <button
              type="button"
              data-testid="clear-button"
              class="team-switcher__clear-btn"
              (click)="clearTeam()"
            >
              Clear
            </button>
          } @else {
            <span class="team-switcher__no-team" data-testid="no-team-label">
              No team selected
            </span>
            <button
              type="button"
              data-testid="set-team-button"
              class="team-switcher__set-btn"
              (click)="openEdit()"
            >
              Set Team
            </button>
          }
        </div>
      } @else {
        <!-- Edit mode -->
        <div class="team-switcher__edit">
          <input
            type="text"
            data-testid="edit-input"
            [value]="editInput()"
            placeholder="Enter team name"
            (input)="onEditInputChange($any($event.target).value)"
          />
          <button
            type="button"
            data-testid="confirm-button"
            class="team-switcher__confirm-btn"
            (click)="confirmEdit()"
          >
            Confirm
          </button>
          <button
            type="button"
            data-testid="cancel-button"
            class="team-switcher__cancel-btn"
            (click)="cancelEdit()"
          >
            Cancel
          </button>

          <!-- Preset shortcuts in edit mode -->
          <div class="team-switcher__presets">
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
        </div>
      }

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
    </div>
  `,
  styles: [
    `
      .team-switcher {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .team-switcher__display,
      .team-switcher__edit {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
      }

      .team-switcher__presets {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem;
        width: 100%;
        margin-top: 0.25rem;
      }

      .preset-option {
        padding: 0.25rem 0.5rem;
        border: 1px solid #ccc;
        border-radius: 0.25rem;
        cursor: pointer;
        background: #f5f5f5;
        font-size: 0.75rem;
      }

      .validation-error {
        color: #d32f2f;
        font-size: 0.75rem;
        margin: 0;
      }
    `,
  ],
})
export class TeamSwitcherComponent {
  private readonly facade = inject(TeamContextFacade);

  readonly isEditing = signal<boolean>(false);
  readonly editInput = signal<string>('');

  get currentTeam(): Signal<string | undefined> {
    return this.facade.currentTeam;
  }

  get hasTeam(): Signal<boolean> {
    return this.facade.hasTeam;
  }

  get presets(): Signal<readonly string[]> {
    return this.facade.presets;
  }

  get validationError(): Signal<string | undefined> {
    return this.facade.validationError;
  }

  openEdit(): void {
    this.editInput.set(this.facade.currentTeam() ?? '');
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
    this.editInput.set('');
  }

  confirmEdit(): void {
    this.facade.setTeam(this.editInput());
  }

  selectPreset(id: string): void {
    this.facade.setTeam(id);
  }

  clearTeam(): void {
    this.facade.clearTeam();
  }

  onEditInputChange(value: string): void {
    this.editInput.set(value);
  }
}
