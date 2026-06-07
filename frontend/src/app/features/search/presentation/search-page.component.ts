import { ChangeDetectionStrategy, Component, inject, Signal, signal } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { SearchFacade } from '../application/facades/search.facade';
import { SearchBarComponent } from './search-bar/search-bar.component';
import { FilterChipsComponent, FilterChipsOutput } from './filter-chips/filter-chips.component';
import { SearchResultsComponent } from './search-results/search-results.component';
import type { SearchFilterQuery } from '../domain/rules/search-filter.rules';
import { I18nFacade } from '../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-search-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SearchBarComponent, FilterChipsComponent, SearchResultsComponent],
  providers: [provideTranslocoScope('search')],
  template: `
    <div class="cf-search-page" data-testid="search-page">
      <cf-search-bar [isLoading]="isLoading()" (searchSubmitted)="onSearch($event)" />

      <cf-filter-chips
        [activeTypes]="activeTypes()"
        [activeLanguages]="activeLanguages()"
        [activeUseCases]="activeUseCases()"
        (filtersChanged)="onFiltersChanged($event)"
      />

      <cf-search-results />
    </div>
  `,
})
export class SearchPageComponent {
  private readonly facade = inject(SearchFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly isLoading: Signal<boolean> = this.facade.isLoadingSearch;

  /**
   * Active filter chips bound to FilterChipsComponent.
   * types mirrors category suggestions from the last search response.
   * languages and useCases are not yet surfaced by the search port.
   */
  readonly activeTypes: Signal<readonly string[]> = this.facade.categorySuggestions;
  readonly activeLanguages: Signal<readonly string[]> = signal<readonly string[]>([]).asReadonly();
  readonly activeUseCases: Signal<readonly string[]> = signal<readonly string[]>([]).asReadonly();

  onSearch(keyword: string): void {
    this.facade.search(keyword);
  }

  onFiltersChanged(filters: FilterChipsOutput): void {
    const partial: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>> = {
      types: [...filters.types],
      languages: [...filters.languages],
      useCases: [...filters.useCases],
    };
    this.facade.setFilters(partial);
  }
}
