import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'cf-keyword-search',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-keyword-search">
      <input
        type="text"
        class="cf-keyword-search__input"
        [ngModel]="searchQuery()"
        (ngModelChange)="onSearchChange($event)"
        placeholder="Search keywords..."
      />
    </div>
  `,
  styles: [`
    .cf-keyword-search {
      padding: 1rem;
      border-bottom: 1px solid #e0e0e0;
    }

    .cf-keyword-search__input {
      width: 100%;
      padding: 0.625rem 0.75rem;
      font-size: 0.875rem;
      border: 1px solid #d0d0d0;
      border-radius: 4px;
      outline: none;
      transition: border-color 0.15s ease;
    }

    .cf-keyword-search__input:focus {
      border-color: #1976d2;
    }

    .cf-keyword-search__input::placeholder {
      color: #999;
    }
  `],
})
export class KeywordSearchComponent {
  readonly searchQuery = input<string>('');

  readonly searchChanged = output<string>();

  onSearchChange(query: string): void {
    this.searchChanged.emit(query);
  }
}
