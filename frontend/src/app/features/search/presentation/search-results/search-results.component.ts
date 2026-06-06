import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  Signal,
} from '@angular/core';
import { SearchFacade } from '../../application/facades/search.facade';
import type { DiscoveryCriteria, DiscoveryResult, SearchResult } from '../../domain/models/search.models';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';
import { EmptyStateComponent } from '../../../../shared/design-system/empty-state.component';

@Component({
  selector: 'cf-search-results',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent],
  template: `
    @if (isLoading()) {
      <div aria-busy="true" data-testid="loading" class="loading">Loading results…</div>
    }

    @if (!isLoading() && hasError()) {
      <div role="alert" class="error" data-testid="error-message">
        Failed to load search results. Please try again.
      </div>
    }

    @if (!isLoading() && !hasError() && isEmpty() && categorySuggestions().length > 0) {
      <div data-testid="no-results" role="status">
        <cf-empty-state message="No results found. Try one of these categories:" />
        <ul class="cf-search-results__suggestions" data-testid="category-suggestions">
          @for (suggestion of categorySuggestions(); track suggestion) {
            <li>{{ suggestion }}</li>
          }
        </ul>
      </div>
    }

    @if (!isLoading() && !hasError() && isEmpty() && categorySuggestions().length === 0) {
      <div data-testid="no-results" role="status">
        <cf-empty-state message="No results found. Try a different search term." />
      </div>
    }

    @if (!isLoading() && !hasError() && !isEmpty()) {
      <ul data-testid="result-list" class="cf-search-results__list">
        @for (result of results(); track result.pluginId) {
          <li class="cf-search-results__item">
            <span class="cf-search-results__name">{{ result.name }}</span>
            <span class="cf-search-results__author">{{ result.author }}</span>
            @if (result.latestVersion) {
              <span class="cf-search-results__version">{{ result.latestVersion }}</span>
            }
            <span class="cf-search-results__score" data-testid="relevance-score">
              {{ result.relevanceScore }}
            </span>
          </li>
        }
      </ul>
    }

    @if (discoveryResults().length > 0) {
      <ul data-testid="discovery-list" class="cf-search-results__discovery-list">
        @for (result of discoveryResults(); track result.pluginId) {
          <li class="cf-search-results__discovery-item">
            <span class="cf-search-results__name">{{ result.name }}</span>
            <span class="cf-search-results__author">{{ result.author }}</span>
            <span class="cf-search-results__maturity">{{ result.maturityIndicator }}</span>
          </li>
        }
      </ul>
    }
  `,
})
export class SearchResultsComponent {
  private readonly facade = inject(SearchFacade);

  readonly results: Signal<SearchResult[]> = this.facade.results;
  readonly isLoading: Signal<boolean> = this.facade.isLoadingSearch;
  readonly hasError: Signal<boolean> = computed(() => this.facade.searchError() !== undefined);
  readonly isEmpty: Signal<boolean> = computed(() => this.facade.results().length === 0);
  readonly categorySuggestions: Signal<readonly string[]> = this.facade.categorySuggestions;
  readonly discoveryResults: Signal<DiscoveryResult[]> = this.facade.discoveryResults;

  onSearch(keyword: string): void {
    this.facade.search(keyword);
  }

  onFilterChange(
    filters: Partial<Pick<SearchFilterQuery, 'types' | 'languages' | 'useCases'>>,
  ): void {
    this.facade.setFilters(filters);
  }

  onPageChange(page: number): void {
    this.facade.setPage(page);
  }

  onDiscover(criteria: DiscoveryCriteria): void {
    this.facade.discover(criteria);
  }
}
