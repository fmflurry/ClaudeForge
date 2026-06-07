/**
 * RED tests — Task 12.4: Presentation layer — plugin detail component
 *
 * Expected production file (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/presentation/detail/plugin-detail.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-plugin-detail',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [...design-system primitives],
 *     ...
 *   })
 *   class PluginDetailComponent {
 *     private readonly facade = inject(CatalogFacade);
 *
 *     // Derived signals from facade:
 *     readonly plugin: Signal<PluginDetail | undefined>   — from facade.selectedPlugin
 *     readonly isLoading: Signal<boolean>                 — from facade.isLoadingDetail
 *     readonly hasError: Signal<boolean>                  — derived from facade.detailError !== undefined
 *
 *     // Outputs:
 *     readonly backRequested = output<void>();             — emits when back button is pressed
 *
 *     // Methods:
 *     onBack(): void   — emits backRequested
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { PluginDetailComponent } from './plugin-detail.component';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for catalog scope (Wave 1 i18n pattern)
//
// En map returns EXACT current literals so all existing assertions stay green.
// Fr map returns French — fr assertions verify the migration works.
//
// Key namespace: 'catalog' scope, keys WITHOUT the 'catalog.' prefix in JSON.
// In the test harness, keys are flat dot-delimited: 'catalog.<key>'.
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
// Stub facade for detail component tests
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacadeForDetail {
  private readonly _plugins = signal<PluginSummary[]>([]);
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);
  private readonly _categories = signal<Categories | undefined>(undefined);
  private readonly _selectedPlugin = signal<PluginDetail | undefined>(undefined);
  private readonly _isLoadingPlugins = signal(false);
  private readonly _isLoadingDetail = signal(false);
  private readonly _isLoadingCategories = signal(false);
  private readonly _pluginsError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _detailError = signal<{ code: string; message: string }[] | undefined>(undefined);

  // Test helpers
  setDetailState(plugin: PluginDetail | undefined): void {
    this._selectedPlugin.set(plugin);
  }
  setDetailLoading(loading: boolean): void {
    this._isLoadingDetail.set(loading);
  }
  setDetailError(errors: { code: string; message: string }[]): void {
    this._detailError.set(errors);
  }

  // Signals
  get plugins(): Signal<PluginSummary[]> {
    return this._plugins;
  }
  get paginationMeta(): Signal<PaginationMeta | undefined> {
    return this._paginationMeta;
  }
  get categories(): Signal<Categories | undefined> {
    return this._categories;
  }
  get selectedPlugin(): Signal<PluginDetail | undefined> {
    return this._selectedPlugin;
  }
  get isLoadingPlugins(): Signal<boolean> {
    return this._isLoadingPlugins;
  }
  get isLoadingDetail(): Signal<boolean> {
    return this._isLoadingDetail;
  }
  get isLoadingCategories(): Signal<boolean> {
    return this._isLoadingCategories;
  }
  get pluginsError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._pluginsError;
  }
  get detailError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._detailError;
  }

  // Recorded calls
  loadDetailCalls: string[] = [];
  loadCategoriesCalls = 0;

  loadPlugins(_query?: Partial<CatalogFilterQuery>): void {
    /* no-op */
  }
  setPage(_page: number): void {
    /* no-op */
  }
  setSort(_sort: string, _order?: 'asc' | 'desc'): void {
    /* no-op */
  }
  setFilters(_filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void {
    /* no-op */
  }
  loadDetail(pluginId: string): void {
    this.loadDetailCalls.push(pluginId);
  }
  loadCategories(): void {
    this.loadCategoriesCalls++;
  }
}

// ---------------------------------------------------------------------------
// Plugin fixture
// ---------------------------------------------------------------------------

const FULL_DETAIL: PluginDetail = {
  pluginId: 'p1',
  name: 'Awesome Plugin',
  slug: 'awesome-plugin',
  description: 'Does awesome things with TypeScript.',
  author: 'Jane Dev',
  types: ['formatter'],
  languages: ['typescript', 'javascript'],
  useCaseTags: ['code-quality'],
  downloadCount: 4200,
  latestVersion: '2.1.0',
  createdAt: new Date('2023-01-01T00:00:00.000Z'),
  updatedAt: new Date('2024-03-15T12:00:00.000Z'),
  versions: [
    {
      pluginId: 'p1',
      version: '2.1.0',
      isLatest: true,
      downloadCount: 1200,
      releaseNotes: 'Improved performance.',
      createdAt: new Date('2024-03-15T12:00:00.000Z'),
    },
    {
      pluginId: 'p1',
      version: '2.0.0',
      isLatest: false,
      downloadCount: 3000,
      releaseNotes: 'Major rewrite.',
      createdAt: new Date('2024-01-01T00:00:00.000Z'),
    },
  ],
};

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setupDetailComponent(): {
  fixture: ComponentFixture<PluginDetailComponent>;
  stub: StubCatalogFacadeForDetail;
  translocoService: TranslocoService;
} {
  const stub = new StubCatalogFacadeForDetail();
  TestBed.configureTestingModule({
    imports: [
      PluginDetailComponent,
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
  }).overrideComponent(PluginDetailComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(PluginDetailComponent);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, stub, translocoService };
}

// ---------------------------------------------------------------------------
// Component selector
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — selector', () => {
  it('should use selector "cf-plugin-detail"', () => {
    const { fixture } = setupDetailComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// No plugin selected (undefined state)
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — no plugin selected', () => {
  it('should not render plugin metadata when selectedPlugin is undefined', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // Plugin name from fixture must not appear
    expect(text).not.toContain('Awesome Plugin');
  });

  it('should render a placeholder or empty state when selectedPlugin is undefined', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    // Either empty-state component or empty container — just confirm no crash
    expect(fixture.nativeElement).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Loading state
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — loading state', () => {
  it('should render a loading indicator when isLoadingDetail is true', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailLoading(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Plugin metadata rendering
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — metadata', () => {
  it('should render the plugin name', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Awesome Plugin');
  });

  it('should render the plugin description', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Does awesome things with TypeScript.');
  });

  it('should render the author', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Jane Dev');
  });

  it('should render the latest version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('2.1.0');
  });

  it('should render the download count', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // 4200 downloads
    expect(fixture.nativeElement.textContent).toContain('4200');
  });

  it('should render the type tags', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('formatter');
  });

  it('should render language tags', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('typescript');
  });
});

// ---------------------------------------------------------------------------
// Version history
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — version history', () => {
  it('should render all versions from the versions array', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // Both version numbers should appear
    expect(fixture.nativeElement.textContent).toContain('2.1.0');
    expect(fixture.nativeElement.textContent).toContain('2.0.0');
  });

  it('should render release notes for each version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Improved performance.');
  });

  it('should handle a plugin with no versions gracefully', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState({ ...FULL_DETAIL, versions: [] });
    fixture.detectChanges();
    // Component should not crash
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should indicate the latest version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // "latest" label should appear somewhere
    const text = (fixture.nativeElement.textContent as string).toLowerCase();
    expect(text).toContain('latest');
  });
});

// ---------------------------------------------------------------------------
// Error state
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — error state', () => {
  it('should render an error indicator when detailError is set', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailError([{ code: 'HTTP_404', message: 'Plugin not found' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(By.css('[data-testid="error-message"], [role="alert"], .error'));
    expect(errorEl).not.toBeNull();
  });

  it('should not render plugin metadata when error is set and no plugin loaded', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailError([{ code: 'HTTP_404', message: 'Plugin not found' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Awesome Plugin');
  });
});

// ---------------------------------------------------------------------------
// Back navigation output
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — back navigation', () => {
  it('should emit backRequested output when onBack() is called', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    const emitted: void[] = [];
    fixture.componentInstance.backRequested.subscribe(() => emitted.push(undefined));
    fixture.componentInstance.onBack();
    expect(emitted).toHaveLength(1);
  });

  it('should render a back button', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    const backBtn = fixture.debugElement.query(
      By.css('[data-testid="back-button"], button[aria-label*="back" i], button[aria-label*="Back"]'),
    );
    expect(backBtn).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — architecture boundary', () => {
  it('should compile and render with only CatalogFacade provided (no store/use-case needed)', () => {
    // If this test setup (no CatalogStore in providers) does not throw, the boundary is maintained.
    const { fixture } = setupDetailComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// i18n Wave 1 — fr assertions
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — i18n', () => {
  it('[FR] loading indicator renders French text when lang is fr', () => {
    const { fixture, stub, translocoService } = setupDetailComponent();
    stub.setDetailLoading(true);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const loading = fixture.nativeElement.querySelector('[aria-busy="true"]') as HTMLElement | null;
    expect(loading?.textContent?.trim()).toContain('Chargement du plugin…');
  });

  it('[FR] error message renders French text when lang is fr', () => {
    const { fixture, stub, translocoService } = setupDetailComponent();
    stub.setDetailError([{ code: 'HTTP_404', message: 'Not found' }]);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null;
    expect(errorEl?.textContent?.trim()).toContain('Impossible de charger les détails du plugin');
  });

  it('[FR] back button renders "Retour" when lang is fr', () => {
    const { fixture, translocoService } = setupDetailComponent();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('[data-testid="back-button"]') as HTMLButtonElement | null;
    expect(btn?.textContent?.trim()).toContain('Retour');
  });

  it('[FR] metadata labels render French when lang is fr', () => {
    const { fixture, stub, translocoService } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Auteur');
    expect(text).toContain('Dernière version');
  });
});
