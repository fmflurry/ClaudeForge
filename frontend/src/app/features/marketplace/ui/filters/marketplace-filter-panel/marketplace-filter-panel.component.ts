import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  DomainCategorySidebarComponent,
  DomainCategory,
} from '../domain-category-sidebar/domain-category-sidebar.component';
import {
  StructuralTypeCheckboxesComponent,
  StructuralType,
} from '../structural-type-checkboxes/structural-type-checkboxes.component';
import { KeywordSearchComponent } from '../keyword-search/keyword-search.component';
import type { PluginManifest } from '../../../domain/rules/marketplace-categorization-filter.rules';
import { MarketplaceFilters } from '../../../domain/rules/marketplace-categorization-filter.rules';

@Component({
  selector: 'cf-marketplace-filter-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DomainCategorySidebarComponent,
    StructuralTypeCheckboxesComponent,
    KeywordSearchComponent,
  ],
  template: `
    <div class="cf-filter-panel">
      <div class="cf-filter-panel__header">
        <h2 class="cf-filter-panel__title">Filters</h2>
        @if (hasActiveFilters()) {
          <button
            type="button"
            class="cf-filter-panel__clear"
            (click)="clearAll()"
          >
            Clear all
          </button>
        }
      </div>

      <cf-keyword-search
        [searchQuery]="keyword()"
        (searchChanged)="onKeywordChange($event)"
      />

      <div class="cf-filter-panel__body">
        <cf-domain-category-sidebar
          [categories]="domainCategories()"
          [selectedCategory]="selectedCategory()"
          (categorySelected)="onCategorySelect($event)"
        />

        <cf-structural-type-checkboxes
          [types]="filteredStructuralTypes()"
          [selectedTypes]="selectedStructuralTypes()"
          (typesChanged)="onStructuralTypesChange($event)"
        />
      </div>
    </div>
  `,
  styles: [`
    .cf-filter-panel {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: #fff;
      border-right: 1px solid #e0e0e0;
    }

    .cf-filter-panel__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1rem;
      border-bottom: 1px solid #e0e0e0;
    }

    .cf-filter-panel__title {
      font-size: 1.125rem;
      font-weight: 600;
      margin: 0;
      color: #1a1a1a;
    }

    .cf-filter-panel__clear {
      font-size: 0.8125rem;
      color: #1976d2;
      background: none;
      border: none;
      cursor: pointer;
      padding: 0;
    }

    .cf-filter-panel__clear:hover {
      text-decoration: underline;
    }

    .cf-filter-panel__body {
      flex: 1;
      overflow-y: auto;
    }
  `],
})
export class MarketplaceFilterPanelComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly plugins = input.required<readonly PluginManifest[]>();

  readonly filtersChanged = output<MarketplaceFilters>();

  readonly selectedCategory = signal<string | undefined>(undefined);
  readonly selectedStructuralTypes = signal<readonly string[]>([]);
  readonly keyword = signal<string>('');

  readonly domainCategories = computed<readonly DomainCategory[]>(() => {
    const plugins = this.plugins();
    const counts = new Map<string, number>();

    for (const plugin of plugins) {
      counts.set(plugin.category, (counts.get(plugin.category) ?? 0) + 1);
    }

    const categories: DomainCategory[] = [
      { id: 'code-intelligence', label: 'Code Intelligence', count: 0 },
      { id: 'testing-qa', label: 'Testing & QA', count: 0 },
      { id: 'devops-infrastructure', label: 'DevOps & Infrastructure', count: 0 },
      { id: 'documentation', label: 'Documentation', count: 0 },
      { id: 'security', label: 'Security', count: 0 },
      { id: 'data-analytics', label: 'Data & Analytics', count: 0 },
      { id: 'workflow-orchestration', label: 'Workflow Orchestration', count: 0 },
      { id: 'productivity-utilities', label: 'Productivity Utilities', count: 0 },
      { id: 'external-service', label: 'External Service', count: 0 },
      { id: 'language-framework', label: 'Language & Framework', count: 0 },
      { id: 'domain-vertical', label: 'Domain Vertical', count: 0 },
    ];

    return categories.map((cat) => ({
      ...cat,
      count: counts.get(cat.id) ?? 0,
    }));
  });

  readonly allStructuralTypes = computed<readonly StructuralType[]>(() => {
    const plugins = this.plugins();
    const counts = new Map<string, number>();

    for (const plugin of plugins) {
      const keywords = plugin.keywords ?? [];
      for (const keyword of keywords) {
        if (['skill', 'hook', 'subagent', 'command', 'mcp-server'].includes(keyword)) {
          counts.set(keyword, (counts.get(keyword) ?? 0) + 1);
        }
      }
    }

    return [
      { id: 'skill', label: 'Skill', count: counts.get('skill') ?? 0 },
      { id: 'hook', label: 'Hook', count: counts.get('hook') ?? 0 },
      { id: 'subagent', label: 'Subagent', count: counts.get('subagent') ?? 0 },
      { id: 'command', label: 'Command', count: counts.get('command') ?? 0 },
      { id: 'mcp-server', label: 'MCP Server', count: counts.get('mcp-server') ?? 0 },
    ];
  });

  readonly filteredStructuralTypes = computed<readonly StructuralType[]>(() => {
    const category = this.selectedCategory();
    if (!category) {
      return this.allStructuralTypes();
    }

    const plugins = this.plugins().filter((p) => p.category === category);
    const counts = new Map<string, number>();

    for (const plugin of plugins) {
      const keywords = plugin.keywords ?? [];
      for (const keyword of keywords) {
        if (['skill', 'hook', 'subagent', 'command', 'mcp-server'].includes(keyword)) {
          counts.set(keyword, (counts.get(keyword) ?? 0) + 1);
        }
      }
    }

    return this.allStructuralTypes()
      .map((type) => ({ ...type, count: counts.get(type.id) ?? 0 }))
      .filter((type) => type.count > 0);
  });

  readonly currentFilters = computed<MarketplaceFilters>(() => ({
    category: this.selectedCategory(),
    structural:
      this.selectedStructuralTypes().length > 0
        ? this.selectedStructuralTypes()
        : undefined,
    keywords: this.keyword() || undefined,
  }));

  readonly hasActiveFilters = computed<boolean>(() => {
    const f = this.currentFilters();
    return !!(f.category || (f.structural && f.structural.length > 0) || f.keywords);
  });

  constructor() {
    effect(() => {
      const filters = this.currentFilters();
      this.filtersChanged.emit(filters);
    });

    this.restoreFromQueryParams();
  }

  onCategorySelect(categoryId: string): void {
    const current = this.selectedCategory();
    this.selectedCategory.set(current === categoryId ? undefined : categoryId);

    // Clear structural types that are no longer valid for new category
    const validIds = this.filteredStructuralTypes().map((t) => t.id);
    const currentTypes = this.selectedStructuralTypes();
    const validTypes = currentTypes.filter((t) => validIds.includes(t));
    if (validTypes.length !== currentTypes.length) {
      this.selectedStructuralTypes.set(validTypes);
    }

    this.updateQueryParams();
  }

  onStructuralTypesChange(types: readonly string[]): void {
    this.selectedStructuralTypes.set(types);
    this.updateQueryParams();
  }

  onKeywordChange(query: string): void {
    this.keyword.set(query);
    this.updateQueryParams();
  }

  clearAll(): void {
    this.selectedCategory.set(undefined);
    this.selectedStructuralTypes.set([]);
    this.keyword.set('');
    this.updateQueryParams();
  }

  private restoreFromQueryParams(): void {
    const queryParams = this.route.snapshot.queryParamMap;

    const category = queryParams.get('category');
    if (category) {
      this.selectedCategory.set(category);
    }

    const structural = queryParams.get('structural');
    if (structural) {
      this.selectedStructuralTypes.set(structural.split(','));
    }

    const keywords = queryParams.get('keywords');
    if (keywords) {
      this.keyword.set(keywords);
    }
  }

  private updateQueryParams(): void {
    const filters = this.currentFilters();
    const queryParams: Record<string, string | null> = {
      category: filters.category ?? null,
      structural: filters.structural?.join(',') ?? null,
      keywords: filters.keywords ?? null,
    };

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
    });
  }
}
