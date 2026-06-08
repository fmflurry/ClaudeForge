import { ChangeDetectionStrategy, Component, computed, inject, output, Signal } from '@angular/core';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { PaginationMeta, PluginSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { EmptyStateComponent } from '../../../../shared/design-system/empty-state.component';
import { PaginationComponent } from '../../../../shared/design-system/pagination.component';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-plugin-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, PaginationComponent],
  template: `
    @if (isLoading()) {
      <div aria-busy="true" data-testid="loading" class="loading">{{ i18n.t('catalog.loading-plugins') }}</div>
    }

    @if (!isLoading() && hasError()) {
      <div role="alert" class="error" data-testid="error-message">{{ i18n.t('catalog.error-plugins') }}</div>
    }

    @if (!isLoading() && !hasError() && plugins().length === 0) {
      <cf-empty-state [message]="i18n.t('catalog.empty-plugins')" />
    }

    @if (!isLoading() && !hasError() && plugins().length > 0) {
      <table data-testid="plugin-table" class="cf-plugin-table">
        <thead>
          <tr>
            <th scope="col">{{ i18n.t('catalog.col-name') }}</th>
            <th scope="col">{{ i18n.t('catalog.col-author') }}</th>
            <th scope="col">{{ i18n.t('catalog.col-version') }}</th>
            <th scope="col">{{ i18n.t('catalog.col-downloads') }}</th>
            <th scope="col">{{ i18n.t('catalog.col-types') }}</th>
          </tr>
        </thead>
        <tbody>
          @for (plugin of plugins(); track plugin.pluginId) {
            <tr class="cf-plugin-table__row" (click)="selectPlugin(plugin.pluginId)" style="cursor:pointer">
              <td>{{ plugin.name }}</td>
              <td>{{ plugin.author }}</td>
              <td>{{ plugin.latestVersion }}</td>
              <td>{{ plugin.downloadCount }}</td>
              <td>{{ plugin.types.join(', ') }}</td>
            </tr>
          }
        </tbody>
      </table>
    }

    @if (showPagination()) {
      <cf-pagination [currentPage]="currentPage()" [totalPages]="totalPages()" (pageChange)="onPageChange($event)" />
    }
  `,
})
export class PluginListComponent {
  private readonly facade = inject(CatalogFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly plugins: Signal<PluginSummary[]> = this.facade.plugins;
  readonly isLoading: Signal<boolean> = this.facade.isLoadingPlugins;
  readonly paginationMeta: Signal<PaginationMeta | undefined> = this.facade.paginationMeta;
  readonly hasError: Signal<boolean> = computed(() => this.facade.pluginsError() !== undefined);

  readonly showPagination = computed(() => (this.paginationMeta()?.totalPages ?? 0) > 1);
  readonly currentPage = computed(() => this.paginationMeta()?.page ?? 1);
  readonly totalPages = computed(() => this.paginationMeta()?.totalPages ?? 1);

  readonly pluginSelected = output<string>();

  onPageChange(page: number): void {
    this.facade.setPage(page);
  }

  onSortChange(sort: string, order: 'asc' | 'desc'): void {
    this.facade.setSort(sort, order);
  }

  onFilterChange(filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    this.facade.setFilters(filters);
  }

  selectPlugin(pluginId: string): void {
    this.facade.loadDetail(pluginId);
    this.pluginSelected.emit(pluginId);
  }
}
