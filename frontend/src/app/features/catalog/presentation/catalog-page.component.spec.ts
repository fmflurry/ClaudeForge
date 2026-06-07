/**
 * CatalogPageComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { CatalogPageComponent } from './catalog-page.component';
import { CatalogFacade } from '../application/facades/catalog.facade';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../domain/rules/catalog-filter.rules';

// ---------------------------------------------------------------------------
// Stub facade
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacade {
  private readonly _plugins = signal<PluginSummary[]>([]);
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);
  private readonly _categories = signal<Categories | undefined>(undefined);
  private readonly _selectedPlugin = signal<PluginDetail | undefined>(undefined);
  private readonly _isLoadingPlugins = signal(false);
  private readonly _isLoadingDetail = signal(false);
  private readonly _isLoadingCategories = signal(false);
  private readonly _pluginsError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _detailError = signal<{ code: string; message: string }[] | undefined>(undefined);

  loadPluginsCalls = 0;
  loadCategoriesCalls = 0;

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

  loadPlugins(_query?: Partial<CatalogFilterQuery>): void {
    this.loadPluginsCalls++;
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
    imports: [CatalogPageComponent],
    providers: [{ provide: CatalogFacade, useValue: stub }],
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
  it('should call facade.loadPlugins on init', () => {
    const { fixture, stub } = setup();
    fixture.detectChanges();
    expect(stub.loadPluginsCalls).toBeGreaterThan(0);
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
