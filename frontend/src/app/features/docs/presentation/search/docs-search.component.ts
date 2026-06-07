/**
 * Docs search component.
 * Search input + ranked results list with snippet display.
 */

import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { DocsFacade } from '../../application/facades/docs.facade';
import type { DocSearchResult } from '../../domain/models/docs.models';

@Component({
  selector: 'cf-docs-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div>
      <input
        type="search"
        data-testid="search-input"
        placeholder="Search documentation…"
        (input)="onSearchInput($event)"
        aria-label="Search documentation"
      />

      @if (facade.isLoadingSearch()) {
        <div aria-busy="true" data-testid="loading" class="loading">Searching…</div>
      }

      @if (facade.searchError(); as errors) {
        <div role="alert" data-testid="error-message" class="error">
          {{ searchErrorMessage(errors) }}
        </div>
      }

      <ul>
        @for (result of facade.searchResults(); track result.slug) {
          <li>
            <button type="button" (click)="selectResult(result.slug)">
              <span class="result-title">{{ result.title }}</span>
              <span class="result-category">{{ result.category }}</span>
              <span data-testid="relevance-score" class="relevance">{{ result.relevanceScore }}</span>
              <p class="result-snippet">{{ result.snippet }}</p>
            </button>
          </li>
        }
      </ul>
    </div>
  `,
})
export class DocsSearchComponent {
  protected readonly facade = inject(DocsFacade);

  readonly docSelected = output<string>();

  onSearch(query: string): void {
    this.facade.search(query);
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.onSearch(input.value);
  }

  selectResult(slug: string): void {
    this.facade.openDoc(slug);
    this.docSelected.emit(slug);
  }

  protected searchErrorMessage(errors: { code: string; message: string }[]): string {
    return errors[0]?.message ?? 'Search failed.';
  }

  protected trackBySlug(_index: number, result: DocSearchResult): string {
    return result.slug;
  }
}
