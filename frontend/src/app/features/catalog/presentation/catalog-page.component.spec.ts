/**
 * CatalogPageComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { CatalogPageComponent } from './catalog-page.component';
import { CatalogFacade } from '../application/facades/catalog.facade';
import type { Categories, PaginationMeta, AddOnDetail, AddOnSummary } from '../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../domain/rules/catalog-filter.rules';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Minimal catalog langs needed for child components (PluginListComponent,
// PluginDetailComponent) which inject I18nFacade.
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

// ---------------------------------------------------------------------------
// Stub facade
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

  loadAddOnsCalls = 0;
  loadCategoriesCalls = 0;

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

  loadAddOns(_query?: Partial<CatalogFilterQuery>): void {
    this.loadAddOnsCalls++;
  }
  loadCategories(): void {
    this.loadCategoriesCalls++;
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
  loadDetail(_pluginId: string): void {
    /* no-op */
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { fixture: ComponentFixture<CatalogPageComponent>; stub: StubCatalogFacade } {
  const stub = new StubCatalogFacade();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      CatalogPageComponent,
      // Transloco test harness required because child components (PluginListComponent,
      // PluginDetailComponent) inject I18nFacade which depends on TranslocoService.
      TranslocoTestingModule.forRoot({
        langs: { en: EN_CATALOG_LANGS },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: CatalogFacade, useValue: stub },
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  }).overrideComponent(CatalogPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(CatalogPageComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('CatalogPageComponent — ngOnInit wiring', () => {
  it('should call facade.loadAddOns on init', () => {
    const { fixture, stub } = setup();
    fixture.detectChanges();
    expect(stub.loadAddOnsCalls).toBeGreaterThan(0);
  });

  it('should call facade.loadCategories on init', () => {
    const { fixture, stub } = setup();
    fixture.detectChanges();
    expect(stub.loadCategoriesCalls).toBeGreaterThan(0);
  });
});

describe('CatalogPageComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render plugin list by default (showDetail = false)', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-plugin-list')).not.toBeNull();
    expect(el.querySelector('cf-plugin-detail')).toBeNull();
  });

  it('should render plugin detail when showDetail is true', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    fixture.componentInstance.showDetail.set(true);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-plugin-detail')).not.toBeNull();
    expect(el.querySelector('cf-plugin-list')).toBeNull();
  });
});

describe('CatalogPageComponent — actions', () => {
  it('onPluginSelected should set showDetail to true', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance.showDetail()).toBe(false);
    fixture.componentInstance.onPluginSelected('p-1');
    expect(fixture.componentInstance.showDetail()).toBe(true);
  });

  it('onBack should set showDetail to false', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    fixture.componentInstance.showDetail.set(true);
    fixture.componentInstance.onBack();
    expect(fixture.componentInstance.showDetail()).toBe(false);
  });
});
