import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';

export type SearchFilterOutput = Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>;

@Component({
  selector: 'cf-search-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-search-bar">
      <input
        type="search"
        class="cf-search-bar__input"
        [value]="initialKeyword()"
        [attr.aria-busy]="isLoading() || null"
        placeholder="Search plugins…"
        #searchInput
      />
      <button
        type="submit"
        class="cf-search-bar__button"
        data-testid="search-button"
        (click)="onSearch(searchInput.value)"
      >
        Search
      </button>
    </div>
  `,
})
export class SearchBarComponent {
  readonly initialKeyword = input<string>('');
  readonly isLoading = input<boolean>(false);

  readonly searchSubmitted = output<string>();
  readonly filtersChanged = output<SearchFilterOutput>();

  onSearch(keyword: string): void {
    this.searchSubmitted.emit(keyword.trim());
  }
}
