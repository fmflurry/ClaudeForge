import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

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
        [placeholder]="i18n.t('search.search-input-placeholder')"
        #searchInput
      />
      <button
        type="submit"
        class="cf-search-bar__button"
        data-testid="search-button"
        (click)="onSearch(searchInput.value)"
      >
        {{ i18n.t('search.search-button') }}
      </button>
    </div>
  `,
})
export class SearchBarComponent {
  readonly initialKeyword = input<string>('');
  readonly isLoading = input<boolean>(false);

  readonly searchSubmitted = output<string>();
  readonly filtersChanged = output<SearchFilterOutput>();

  protected readonly i18n = inject(I18nFacade);

  onSearch(keyword: string): void {
    this.searchSubmitted.emit(keyword.trim());
  }
}
