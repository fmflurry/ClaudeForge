import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface StructuralType {
  readonly id: string;
  readonly label: string;
  readonly count: number;
}

@Component({
  selector: 'cf-structural-type-checkboxes',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-structural-types">
      <h3 class="cf-structural-types__title">Plugin Types</h3>
      <div class="cf-structural-types__list">
        @for (type of types(); track type.id) {
          <label class="cf-structural-types__item">
            <input
              type="checkbox"
              [value]="type.id"
              [checked]="isSelected(type.id)"
              (change)="onTypeToggle(type.id)"
              class="cf-structural-types__checkbox"
            />
            <span class="cf-structural-types__label">{{ type.label }}</span>
            <span class="cf-structural-types__count">({{ type.count }})</span>
          </label>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .cf-structural-types {
        padding: 1rem;
        border-top: 1px solid #e0e0e0;
      }

      .cf-structural-types__title {
        font-size: 1rem;
        font-weight: 600;
        margin: 0 0 1rem 0;
        color: #1a1a1a;
      }

      .cf-structural-types__list {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .cf-structural-types__item {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        cursor: pointer;
        padding: 0.5rem;
        border-radius: 4px;
        transition: background-color 0.15s ease;
      }

      .cf-structural-types__item:hover {
        background-color: #f5f5f5;
      }

      .cf-structural-types__checkbox {
        margin: 0;
        cursor: pointer;
      }

      .cf-structural-types__label {
        flex: 1;
        font-size: 0.875rem;
      }

      .cf-structural-types__count {
        color: #666;
        font-size: 0.8125rem;
      }
    `,
  ],
})
export class StructuralTypeCheckboxesComponent {
  readonly types = input.required<readonly StructuralType[]>();
  readonly selectedTypes = input<readonly string[]>([]);

  readonly typesChanged = output<readonly string[]>();

  isSelected(typeId: string): boolean {
    return this.selectedTypes().includes(typeId);
  }

  onTypeToggle(typeId: string): void {
    const current = this.selectedTypes();
    const updated = current.includes(typeId) ? current.filter((t) => t !== typeId) : [...current, typeId];
    this.typesChanged.emit(updated);
  }
}
