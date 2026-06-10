import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface DomainCategory {
  readonly id: string;
  readonly label: string;
  readonly count: number;
}

@Component({
  selector: 'cf-domain-category-sidebar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-domain-sidebar">
      <h3 class="cf-domain-sidebar__title">Categories</h3>
      <div class="cf-domain-sidebar__list">
        @for (category of categories(); track category.id) {
          <label
            class="cf-domain-sidebar__item"
            [class.cf-domain-sidebar__item--selected]="category.id === selectedCategory()"
          >
            <input
              type="radio"
              name="domain-category"
              [value]="category.id"
              [checked]="category.id === selectedCategory()"
              (change)="onCategorySelect(category.id)"
              class="cf-domain-sidebar__radio"
            />
            <span class="cf-domain-sidebar__label">{{ category.label }}</span>
            <span class="cf-domain-sidebar__count">({{ category.count }})</span>
          </label>
        }
      </div>
    </div>
  `,
  styles: [`
    .cf-domain-sidebar {
      padding: 1rem;
    }

    .cf-domain-sidebar__title {
      font-size: 1rem;
      font-weight: 600;
      margin: 0 0 1rem 0;
      color: #1a1a1a;
    }

    .cf-domain-sidebar__list {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .cf-domain-sidebar__item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      cursor: pointer;
      padding: 0.5rem;
      border-radius: 4px;
      transition: background-color 0.15s ease;
    }

    .cf-domain-sidebar__item:hover {
      background-color: #f5f5f5;
    }

    .cf-domain-sidebar__item--selected {
      background-color: #e3f2fd;
    }

    .cf-domain-sidebar__radio {
      margin: 0;
      cursor: pointer;
    }

    .cf-domain-sidebar__label {
      flex: 1;
      font-size: 0.875rem;
    }

    .cf-domain-sidebar__count {
      color: #666;
      font-size: 0.8125rem;
    }
  `],
})
export class DomainCategorySidebarComponent {
  readonly categories = input.required<readonly DomainCategory[]>();
  readonly selectedCategory = input<string | undefined>(undefined);

  readonly categorySelected = output<string>();

  onCategorySelect(categoryId: string): void {
    this.categorySelected.emit(categoryId);
  }
}
