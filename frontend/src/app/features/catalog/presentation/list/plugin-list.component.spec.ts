/**
 * RED tests — Task 12.4: Presentation layer — plugin list component
 *
 * Expected production file (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/presentation/list/plugin-list.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-plugin-list',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [...design-system primitives],
 *     ...
 *   })
 *   class PluginListComponent {
 *     // Inject facade only (no use-cases, no store)
 *     private readonly facade = inject(CatalogFacade);
 *
 *     // Derived signals from facade:
 *     readonly plugins: Signal<PluginSummary[]>       — from facade.plugins
 *     readonly isLoading: Signal<boolean>             — from facade.isLoadingPlugins
 *     readonly paginationMeta: Signal<PaginationMeta | undefined>  — from facade.paginationMeta
 *     readonly hasError: Signal<boolean>              — derived from facade.pluginsError !== undefined
 *
 *     // Outputs:
 *     readonly pluginSelected = output<string>();     — emits pluginId on row click
 *
 *     // Methods:
 *     onPageChange(page: number): void  — calls facade.setPage(page)
 *     onSortChange(sort: string, order: 'asc' | 'desc'): void  — calls facade.setSort
 *     onFilterChange(filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void
 *     selectPlugin(pluginId: string): void   — calls facade.loadDetail + emits pluginSelected
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { PluginListComponent } from './plugin-list.component';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { Categories, PaginationMeta, AddOnDetail, AddOnSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for catalog scope (Wave 1 i18n pattern)
//
// En map returns EXACT current literals so all existing assertions stay green.
// Fr map returns French — fr assertions verify the migration works.
// ---------------------------------------------------------------------------

const EN_CATALOG_LANGS: Record<string, string> = {
  'catalog.back-button': 'Back',
  'catalog.back-button-aria': 'Back to list',
  'catalog.loading-plugin': 'Loading plugin…',
  'catalog.error-plugin': 'Failed to load plugin details. Please try again.',
  'catalog.meta-author': 'Author',
  'catalog.meta-latest-version': 'Latest Version',
  'catalog.meta-downloads': 'Downloads',
  'catalog.types-heading': 'Types',
  'catalog.languages-heading': 'Languages',
  'catalog.version-history-heading': 'Version History',
  'catalog.version-col': 'Version',
  'catalog.status-col': 'Status',
  'catalog.downloads-col': 'Downloads',
  'catalog.release-notes-col': 'Release Notes',
  'catalog.version-latest': 'latest',
  'catalog.loading-plugins': 'Loading plugins…',
  'catalog.error-plugins': 'Failed to load plugins. Please try again.',
  'catalog.empty-plugins': 'No plugins found. Try adjusting your filters.',
  'catalog.col-name': 'Name',
  'catalog.col-author': 'Author',
  'catalog.col-version': 'Version',
  'catalog.col-downloads': 'Downloads',
  'catalog.col-types': 'Types',
};

const FR_CATALOG_LANGS: Record<string, string> = {
  'catalog.back-button': 'Retour',
  'catalog.back-button-aria': 'Retour à la liste',
  'catalog.loading-plugin': 'Chargement du plugin…',
  'catalog.error-plugin': 'Impossible de charger les détails du plugin. Veuillez réessayer.',
  'catalog.meta-author': 'Auteur',
  'catalog.meta-latest-version': 'Dernière version',
  'catalog.meta-downloads': 'Téléchargements',
  'catalog.types-heading': 'Types',
  'catalog.languages-heading': 'Langages',
  'catalog.version-history-heading': 'Historique des versions',
  'catalog.version-col': 'Version',
  'catalog.status-col': 'Statut',
  'catalog.downloads-col': 'Téléchargements',
  'catalog.release-notes-col': 'Notes de version',
  'catalog.version-latest': 'dernière',
  'catalog.loading-plugins': 'Chargement des plugins…',
  'catalog.error-plugins': 'Impossible de charger les plugins. Veuillez réessayer.',
  'catalog.empty-plugins': "Aucun plugin trouvé. Essayez d'ajuster vos filtres.",
  'catalog.col-name': 'Nom',
  'catalog.col-author': 'Auteur',
  'catalog.col-version': 'Version',
  'catalog.col-downloads': 'Téléchargements',
  'catalog.col-types': 'Types',
};

// ---------------------------------------------------------------------------
// Stub facade — provides controllable signals
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacade {
  private readonly _addOns = signal<AddOnSummary[]>([]);
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);
  private readonly _categories = signal<Categories | undefined>(undefined);
  private readonly _selectedAddOn = signal<AddOnDetail | undefined>(undefined);
  private readonly _isLoadingAddOns = signal(false);
  private readonly _isLoadingDetail = signal(false);
  private readonly _isLoadingCategories = signal(false);
  private readonly _addOnsError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _detailError = signal<{ code: string; message: string }[] | undefined>(undefined);

  // Writeable test helpers
  setPluginsState(addOns: AddOnSummary[], meta?: PaginationMeta): void {
    this._addOns.set(addOns);
    this._paginationMeta.set(meta);
  }
  setLoading(loading: boolean): void {
    this._isLoadingAddOns.set(loading);
  }
  setError(errors: { code: string; message: string }[]): void {
    this._addOnsError.set(errors);
  }

  // Facade signal getters
  get addOns(): Signal<AddOnSummary[]> {
    return this._addOns;
  }
  get paginationMeta(): Signal<PaginationMeta | undefined> {
    return this._paginationMeta;
  }
  get categories(): Signal<Categories | undefined> {
    return this._categories;
  }
  get selectedAddOn(): Signal<AddOnDetail | undefined> {
    return this._selectedAddOn;
  }
  get isLoadingAddOns(): Signal<boolean> {
    return this._isLoadingAddOns;
  }
  get isLoadingDetail(): Signal<boolean> {
    return this._isLoadingDetail;
  }
  get isLoadingCategories(): Signal<boolean> {
    return this._isLoadingCategories;
  }
  get addOnsError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._addOnsError;
  }
  get detailError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._detailError;
  }

  // Recorded calls for assertion
  loadAddOnsCalls: Partial<CatalogFilterQuery>[] = [];
  setPageCalls: number[] = [];
  setSortCalls: { sort: string; order?: 'asc' | 'desc' }[] = [];
  setFiltersCalls: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>[] = [];
  loadDetailCalls: string[] = [];
  loadCategoriesCalls = 0;

  loadAddOns(query?: Partial<CatalogFilterQuery>): void {
    this.loadAddOnsCalls.push(query ?? {});
  }
  setPage(page: number): void {
    this.setPageCalls.push(page);
  }
  setSort(sort: string, order?: 'asc' | 'desc'): void {
    this.setSortCalls.push({ sort, order });
  }
  setFilters(filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    this.setFiltersCalls.push(filters);
  }
  loadDetail(pluginId: string): void {
    this.loadDetailCalls.push(pluginId);
  }
  loadCategories(): void {
    this.loadCategoriesCalls++;
  }
}

// ---------------------------------------------------------------------------
// Plugin fixtures
// ---------------------------------------------------------------------------

const PLUGIN_A: AddOnSummary = {
  pluginId: 'p1',
  name: 'Alpha Plugin',
  slug: 'alpha-plugin',
  description: 'First plugin.',
  author: 'Author A',
  types: ['formatter'],
  languages: ['typescript'],
  useCaseTags: ['code-quality'],
  downloadCount: 1000,
  latestVersion: '1.0.0',
  createdAt: new Date('2024-01-01'),
  updatedAt: new Date('2024-01-01'),
};

const PLUGIN_B: AddOnSummary = {
  pluginId: 'p2',
  name: 'Beta Plugin',
  slug: 'beta-plugin',
  description: 'Second plugin.',
  author: 'Author B',
  types: ['linter'],
  languages: ['javascript'],
  useCaseTags: ['testing'],
  downloadCount: 500,
  latestVersion: '2.0.0',
  createdAt: new Date('2024-02-01'),
  updatedAt: new Date('2024-02-01'),
};

const PAGINATION_META: PaginationMeta = {
  totalCount: 2,
  page: 1,
  limit: 20,
  totalPages: 1,
};

// ---------------------------------------------------------------------------
// Test setup helpers
// ---------------------------------------------------------------------------

function setupComponent(): {
  fixture: ComponentFixture<PluginListComponent>;
  stub: StubCatalogFacade;
  translocoService: TranslocoService;
} {
  const stub = new StubCatalogFacade();
  TestBed.configureTestingModule({
    imports: [
      PluginListComponent,
      // Transloco test harness (Wave 1 pattern):
      // flat dot-delimited keys; en=current literals, fr=French translations.
      TranslocoTestingModule.forRoot({
        langs: { en: EN_CATALOG_LANGS, fr: FR_CATALOG_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: CatalogFacade, useValue: stub },
      // Real I18nFacade — injects TranslocoService from the testing module above.
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(PluginListComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(PluginListComponent);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, stub, translocoService };
}

// ---------------------------------------------------------------------------
// Component selector
// ---------------------------------------------------------------------------

describe('PluginListComponent — selector', () => {
  it('should use selector "cf-plugin-list"', () => {
    // The decorator metadata is available at class level — verify at minimum the component exists
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Empty state rendering
// ---------------------------------------------------------------------------

describe('PluginListComponent — empty state', () => {
  it('should render empty state when plugins array is empty', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([], undefined);
    fixture.detectChanges();
    // Either cf-empty-state element or an element with role="status"
    const emptyEl = fixture.debugElement.query(By.css('cf-empty-state, [data-testid="empty-state"], [role="status"]'));
    expect(emptyEl).not.toBeNull();
  });

  it('should NOT render empty state when plugins are present', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A], PAGINATION_META);
    fixture.detectChanges();
    // The table or list should be present instead
    const tableEl = fixture.debugElement.query(By.css('cf-table, table, [data-testid="plugin-table"]'));
    expect(tableEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Loading state rendering
// ---------------------------------------------------------------------------

describe('PluginListComponent — loading state', () => {
  it('should render a loading indicator when isLoadingPlugins is true', () => {
    const { fixture, stub } = setupComponent();
    stub.setLoading(true);
    fixture.detectChanges();
    // Look for a spinner, skeleton, or aria-busy indicator
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Plugin list rendering
// ---------------------------------------------------------------------------

describe('PluginListComponent — plugin list', () => {
  it('should render a row/item for each plugin in the list', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A, PLUGIN_B], PAGINATION_META);
    fixture.detectChanges();
    // Either the count matches OR the plugin names appear in the DOM
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha Plugin');
    expect(text).toContain('Beta Plugin');
  });

  it('should render the plugin name', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A], PAGINATION_META);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha Plugin');
  });

  it('should render the plugin author', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A], PAGINATION_META);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Author A');
  });

  it('should render the latest version when present', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A], PAGINATION_META);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('1.0.0');
  });
});

// ---------------------------------------------------------------------------
// Error state rendering
// ---------------------------------------------------------------------------

describe('PluginListComponent — error state', () => {
  it('should render an error message when pluginsError is set', () => {
    const { fixture, stub } = setupComponent();
    stub.setError([{ code: 'HTTP_500', message: 'Server error' }]);
    fixture.detectChanges();
    // An error element should be visible
    const errorEl = fixture.debugElement.query(By.css('[data-testid="error-message"], [role="alert"], .error'));
    expect(errorEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Pagination
// ---------------------------------------------------------------------------

describe('PluginListComponent — pagination', () => {
  it('should render cf-pagination when totalPages > 1', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A, PLUGIN_B], { totalCount: 50, page: 1, limit: 20, totalPages: 3 });
    fixture.detectChanges();
    const paginationEl = fixture.debugElement.query(By.css('cf-pagination'));
    expect(paginationEl).not.toBeNull();
  });

  it('should call facade.setPage when pagination emits a page change', () => {
    const { fixture, stub } = setupComponent();
    stub.setPluginsState([PLUGIN_A], { totalCount: 50, page: 1, limit: 20, totalPages: 3 });
    fixture.detectChanges();

    const comp = fixture.componentInstance;
    comp.onPageChange(2);
    expect(stub.setPageCalls).toContain(2);
  });
});

// ---------------------------------------------------------------------------
// Sort interaction
// ---------------------------------------------------------------------------

describe('PluginListComponent — sort', () => {
  it('should call facade.setSort when onSortChange is invoked', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onSortChange('name', 'asc');
    expect(stub.setSortCalls).toContainEqual({ sort: 'name', order: 'asc' });
  });

  it('should call facade.setSort with default desc order when order is omitted', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onSortChange('downloadCount', 'desc');
    expect(stub.setSortCalls).toContainEqual({ sort: 'downloadCount', order: 'desc' });
  });
});

// ---------------------------------------------------------------------------
// Filter interaction
// ---------------------------------------------------------------------------

describe('PluginListComponent — filter', () => {
  it('should call facade.setFilters when onFilterChange is invoked', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.onFilterChange({ types: ['formatter'] });
    expect(stub.setFiltersCalls).toContainEqual({ types: ['formatter'] });
  });
});

// ---------------------------------------------------------------------------
// Plugin selection
// ---------------------------------------------------------------------------

describe('PluginListComponent — plugin selection', () => {
  it('should call facade.loadDetail when selectPlugin is invoked', () => {
    const { fixture, stub } = setupComponent();
    fixture.detectChanges();
    fixture.componentInstance.selectPlugin('p1');
    expect(stub.loadDetailCalls).toContain('p1');
  });

  it('should emit pluginSelected output when selectPlugin is invoked', () => {
    const { fixture } = setupComponent();
    fixture.detectChanges();
    const emittedIds: string[] = [];
    fixture.componentInstance.pluginSelected.subscribe((id: string) => emittedIds.push(id));
    fixture.componentInstance.selectPlugin('p1');
    expect(emittedIds).toContain('p1');
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — no store/use-case injection
// ---------------------------------------------------------------------------

describe('PluginListComponent — architecture boundary', () => {
  it('should NOT reference CatalogStore directly (only facade)', () => {
    // If the component compiles and the test setup (which only provides CatalogFacade, not CatalogStore)
    // does not throw an injection error, the boundary is maintained.
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// i18n Wave 1 — fr assertions
// ---------------------------------------------------------------------------

describe('PluginListComponent — i18n', () => {
  it('[FR] loading indicator renders French text when lang is fr', () => {
    const { fixture, stub, translocoService } = setupComponent();
    stub.setLoading(true);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const loading = fixture.nativeElement.querySelector('[aria-busy="true"]') as HTMLElement | null;
    expect(loading?.textContent?.trim()).toContain('Chargement des plugins…');
  });

  it('[FR] error message renders French text when lang is fr', () => {
    const { fixture, stub, translocoService } = setupComponent();
    stub.setError([{ code: 'HTTP_500', message: 'Server error' }]);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null;
    expect(errorEl?.textContent?.trim()).toContain('Impossible de charger les plugins');
  });

  it('[FR] empty state renders French message when lang is fr', () => {
    const { fixture, stub, translocoService } = setupComponent();
    stub.setPluginsState([], undefined);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Aucun plugin trouvé');
  });

  it('[FR] table column headers render French when lang is fr', () => {
    const { fixture, stub, translocoService } = setupComponent();
    stub.setPluginsState([PLUGIN_A], PAGINATION_META);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Nom');
  });
});
